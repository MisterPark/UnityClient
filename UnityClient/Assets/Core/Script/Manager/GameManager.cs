using System;
using UnityEngine;

public class GameManager : MonoSingleton<GameManager>
{
    [SerializeField] private GameEvent<object> onReceiveEvent;
    [SerializeField] private GameEventString onSendChatEvent;
    [SerializeField] private GameEventString onRecvChatEvent;

    [SerializeField] private Variable<int> someThing;

    protected override void Awake()
    {
        base.Awake();
    }

    private void OnEnable()
    {
        onReceiveEvent.AddListener(OnReceive);
        onSendChatEvent.AddListener(OnSendChat);
        onRecvChatEvent.AddListener(OnRecvChat);
    }

    private void OnDisable()
    {
        onReceiveEvent.RemoveListener(OnReceive);
        onSendChatEvent.RemoveListener(OnSendChat);
        onRecvChatEvent.RemoveListener(OnRecvChat);
    }

    private void Start()
    {
        UnityEngine.Random.InitState(0);
        for (int i = 0; i < 10; i++)
        {
            Debug.Log(UnityEngine.Random.Range(0, 10));

        }
    }

    private void Update()
    {

    }

    public void OnReceive(object message)
    {
        Type type = message.GetType();
        if (type == typeof(MsgChat))
        {
            MsgChat chat = (MsgChat)message;
            onRecvChatEvent.Invoke(chat.message);
        }
    }

    public void OnSendChat(string message)
    {
        MsgChat msg = new MsgChat();
        msg.message = message;
        Client.Instance.SendUnicast(msg);
    }

    public void OnRecvChat(string message)
    {
        Logger.Log(LogLevel.Debug, message);
    }
}
