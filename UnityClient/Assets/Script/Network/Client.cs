using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Events;


public class Client : MonoBehaviour
{
    public static Client Instance { get; private set; }

    [SerializeField] private string serverIP;
    [SerializeField] private int port;
    [SerializeField] private bool autoConnection = true;
    [SerializeField] private UnityEvent<object> OnReceive;

    private Socket socket;
    private MemoryPool<SocketAsyncEventArgs> readWritePool;
    private RingBuffer recvBuffer = new RingBuffer();
    private string sessionId;
    private IPEndPoint remoteEndPoint;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        remoteEndPoint = new IPEndPoint(IPAddress.Parse(serverIP), port);
        readWritePool = new MemoryPool<SocketAsyncEventArgs>(0);

        SocketAsyncEventArgs readWriteEventArg;
        for (int i = 0; i < ushort.MaxValue; i++)
        {
            readWriteEventArg = new SocketAsyncEventArgs();
            readWriteEventArg.Completed += new EventHandler<SocketAsyncEventArgs>(IO_Completed);

            readWritePool.Free(readWriteEventArg);
        }

        socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

        if(autoConnection)
        {
            Connect();
        }
    }

    private void Connect()
    {
        if (socket.Connected) return;
        Logger.Log(LogLevel.Debug, "Try Connect...");
        SocketAsyncEventArgs args = new SocketAsyncEventArgs();
        args.Completed += ConnectCompleted;
        args.RemoteEndPoint = remoteEndPoint;
        socket.ConnectAsync(args);
    }

    private void ConnectCompleted(object sender, SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success)
        {
            Logger.Log(LogLevel.System, $"Connection success.");

            SocketAsyncEventArgs readEventArgs = readWritePool.Allocate();
            readEventArgs.SetBuffer(recvBuffer.Buffer, recvBuffer.Rear, recvBuffer.WritableLength);
            bool pending = socket.ReceiveAsync(readEventArgs);
            if (!pending)
            {
                ProcessReceive(readEventArgs);
            }
        }
        else
        {
            Logger.Log(LogLevel.System, $"Connect Failed!!");
        }
    }

    private void IO_Completed(object sender, SocketAsyncEventArgs e)
    {
        switch (e.LastOperation)
        {
            case SocketAsyncOperation.Receive:
                ProcessReceive(e);
                break;
            case SocketAsyncOperation.Send:
                ProcessSend(e);
                break;
            default:
                Disconnect();
                Logger.Log(LogLevel.Error, $"Invalid Packet.");
                break;
        }
    }

    private void ProcessReceive(SocketAsyncEventArgs e)
    {
        if (e.BytesTransferred > 0 && e.SocketError == SocketError.Success)
        {
            recvBuffer.MoveRear(e.BytesTransferred);

            ProcessPacket();

            if (recvBuffer.WritableLength == 0)
            {
                recvBuffer.Resize(recvBuffer.BufferSize * 2);
            }

            e.SetBuffer(recvBuffer.Buffer, recvBuffer.Rear, recvBuffer.WritableLength);

            bool pending = socket.ReceiveAsync(e);
            if (!pending)
            {
                ProcessReceive(e);
            }
        }
        else
        {
            Disconnect();
        }
    }

    private void ProcessPacket()
    {
        NetworkHeader header = new NetworkHeader();
        int headerLength = Marshal.SizeOf(header);
        int packetLength;

        while (true)
        {
            if (recvBuffer.Length < headerLength)
            {
                break;
            }

            recvBuffer.Peek<NetworkHeader>(ref header);
            if (header.magicNumber != Protocol.MagicNumber)
            {
                Logger.Log(LogLevel.Error, $"Magic Code does not match.");
                Disconnect();
                break;
            }

            packetLength = headerLength + header.messageLength;

            if (recvBuffer.Length < packetLength)
            {
                break;
            }
            // 여기서 패킷처리
            recvBuffer.MoveFront(headerLength);

            int classLength = 0;
            recvBuffer.Read(ref classLength);

            string className = string.Empty;
            recvBuffer.Read(ref className, classLength);

            string json = string.Empty;
            recvBuffer.Read(ref json, header.messageLength);

            Type msgType = Type.GetType(className);
            if(msgType == null)
            {
                // 존재하지 않는 구조체 이슈 (프로토콜 버전 차이 가능성)
                Logger.Log(LogLevel.Error, $"Invalid message.");
                Disconnect();
                break;
            }

            object msg = JsonConvert.DeserializeObject(json, msgType);
            if(msg == null)
            {
                // 존재하지 않는 구조체 이슈 (프로토콜 버전 차이 가능성)
                Logger.Log(LogLevel.Error, $"Invalid message.");
                Disconnect();
                break;
            }

            OnReceiveCallback(msg);
            OnReceive.Invoke(msg);
        }
    }

    private void ProcessSend(SocketAsyncEventArgs e)
    {
        if(e.SocketError != SocketError.Success)
        {
            Logger.Log(LogLevel.Warning, $"Send Failed. SocketError : {e.SocketError}");
            Disconnect();
        }
    }

    public void SendUnicast(object data)
    {
        Packet packet = new Packet();
        
        NetworkHeader header = new NetworkHeader();
        header.magicNumber = Protocol.MagicNumber;

        string className = data.GetType().Name;
        string json = JsonConvert.SerializeObject(data);

        // 여기서 암호화
        byte[] binary = Encoding.UTF8.GetBytes(json);

        header.messageLength = binary.Length;
        packet.Write(header);
        packet.Write(className);
        packet.Write(binary);


        SocketAsyncEventArgs args = readWritePool.Allocate();
        args.SetBuffer(packet.Buffer, packet.Front, packet.Length);
        bool pending = socket.SendAsync(args);
        if (!pending)
        {
            ProcessSend(args);
        }
    }

    public void Disconnect()
    {
        try
        {
            socket.Shutdown(SocketShutdown.Both);
        }
        finally
        {
            Logger.Log($"Disconnected.");
            socket.Close();
            socket.Dispose();
            socket = null;
        }
    }

    private string GetPublicIPAddress()
    {
        var request = (HttpWebRequest)WebRequest.Create("http://ifconfig.me");

        request.UserAgent = "curl"; // this will tell the server to return the information as if the request was made by the linux "curl" command

        string publicIPAddress;

        request.Method = "GET";
        using (WebResponse response = request.GetResponse())
        {
            using (var reader = new StreamReader(response.GetResponseStream()))
            {
                publicIPAddress = reader.ReadToEnd();
            }
        }

        return publicIPAddress.Replace("\n", "");
    }

    private string GetLocalIPAddress()
    {
        string localIP;
        using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
        {
            socket.Connect("8.8.8.8", 65530);
            IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
            localIP = endPoint.Address.ToString();
        }
        return localIP;
    }

    private void OnReceiveCallback(object msg)
    {
        if (msg.GetType() == typeof(MsgNetStat_SC))
        {
            MsgNetStat_SC obj = (MsgNetStat_SC)msg;
            sessionId = obj.id;
            obj.ipAddress = GetPublicIPAddress();
            Logger.Log(LogLevel.Debug, $"Session ID: {sessionId}");
            SendUnicast(obj);
        }
    }
}
