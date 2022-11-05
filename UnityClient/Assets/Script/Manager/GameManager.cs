using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameEvent<object> onReceiveEvent;
    [SerializeField] private GameEventString onSendChatEvent;
    [SerializeField] private GameEventString onRecvChatEvent;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
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

    }

    private void Update()
    {

    }

    public void OnReceive(object message)
    {
        Type type = message.GetType();
        if(type == typeof(MsgChat))
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
