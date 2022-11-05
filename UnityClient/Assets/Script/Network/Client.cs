using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
    [SerializeField] private int receiveBufferSize = 1024;
    [SerializeField] private int packetPoolSize = 1000;
    [SerializeField] private GameEvent<object> OnReceive;

    private Socket socket;
    private MemoryPool<SocketAsyncEventArgs> readWritePool;
    private string sessionId;
    private IPEndPoint remoteEndPoint;

    private MemoryPool<Packet> packetPool;
    private NetBuffer recvBuffer;

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
        OnReceive.AddListener(OnReceiveCallback);
        recvBuffer = new NetBuffer(receiveBufferSize);
        packetPool = new MemoryPool<Packet>(0);
        for (int i = 0; i < packetPoolSize; i++)
        {
            Packet packet = new Packet(receiveBufferSize);
            packetPool.Free(packet);
        }

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
        socket.LingerState = new LingerOption(true, 0);
        socket.NoDelay = true;

        if (autoConnection)
        {
            Connect();
        }
    }

    private void OnApplicationQuit()
    {
        Disconnect();
    }

    private void Connect()
    {
        if (socket.Connected) return;
        Logger.Log(LogLevel.Debug, "Try Connect...");
        SocketAsyncEventArgs args = new SocketAsyncEventArgs();
        args.Completed += ConnectCompleted;
        args.RemoteEndPoint = remoteEndPoint;
        bool pending = socket.ConnectAsync(args);
        if (!pending)
        {
            ProcessConnect(args);
        }
    }

    private void ConnectCompleted(object sender, SocketAsyncEventArgs e)
    {
        ProcessConnect(e);
    }

    private void ProcessConnect(SocketAsyncEventArgs e)
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
        if (e.SocketError == SocketError.Success && e.BytesTransferred > 0)
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
        else if (e.BytesTransferred == 0)
        {
            Logger.Log(LogLevel.System, $"정상 종료.");
            readWritePool.Free(e);
            Disconnect();
        }
        else
        {
            Logger.Log(LogLevel.Error, $"Receicve Failed! SocketError.{e.SocketError}");
            readWritePool.Free(e);
            Disconnect();
        }
    }

    private void ProcessPacket()
    {
        NetHeader header;
        header.Code = 0;
        header.Length = 0;
        int headerSize = Marshal.SizeOf(header);
        int size;

        while (true)
        {
            size = recvBuffer.Length;
            if (size < headerSize) break;
            recvBuffer.Peek<NetHeader>(ref header);
            if (header.Code != Packet.CODE)
            {
                Logger.Log(LogLevel.Warning, $"패킷의 코드가 일치하지 않습니다. Code : {header.Code}");
                break;
            }
            if (size < headerSize + header.Length) break;

            Packet packet = packetPool.Allocate();
            packet.Initialize();
            if (packet.WritableLength < header.Length)
            {
                packet.Resize(header.Length);
            }

            recvBuffer.MoveFront(headerSize);
            recvBuffer.Read(ref packet.Buffer, packet.Rear, header.Length);
            packet.MoveRear(header.Length);

            string typeName = string.Empty;
            string json = string.Empty;
            packet.Read(ref typeName);
            packet.Read(ref json);

            packetPool.Free(packet);

            object msg = JsonConvert.DeserializeObject(json, Type.GetType(typeName));

            OnReceive.Invoke(msg);
        }
    }

    private void ProcessSend(SocketAsyncEventArgs e)
    {
        if (e.SocketError == SocketError.Success)
        {
            Packet packet = (Packet)e.UserToken;
            packetPool.Free(packet);
        }
        else if(e.SocketError == SocketError.IOPending)
        {

        }
        else
        {
            Logger.Log(LogLevel.Error, $"Send Failed. SocketError : {e.SocketError}");
            Packet packet = (Packet)e.UserToken;
            packetPool.Free(packet);
            readWritePool.Free(e);
            Disconnect();
        }
    }

    public void SendUnicast(object data)
    {
        Packet packet = packetPool.Allocate();
        packet.Initialize();

        string typeName = data.GetType().Name;
        string json = JsonConvert.SerializeObject(data);
        packet.Write(typeName);
        packet.Write(json);

        packet.SetHeader();
        SocketAsyncEventArgs args = readWritePool.Allocate();
        args.UserToken = packet;
        args.SetBuffer(packet.Buffer, packet.Front, packet.Length);
        bool pending = socket.SendAsync(args);
        if (!pending)
        {
            ProcessSend(args);
        }
    }

    public void Disconnect()
    {
        if (socket != null)
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Disconnect(false);
            socket.Close(5);
            socket.Dispose();
            socket = null;
            Logger.Log(LogLevel.System, "Disconnected.");
        }
    }

    public string GetPublicIPAddress()
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

    public string GetLocalIPAddress()
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
        if (msg.GetType() == typeof(MsgNetStat))
        {
            MsgNetStat obj = (MsgNetStat)msg;
            obj.ipAddress = GetPublicIPAddress();
            sessionId = obj.id;
            SendUnicast(obj);
        }
    }
}
