using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [SerializeField] private GameEvent<object> onReceiveEvent;

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
    }

    private void OnDisable()
    {
        onReceiveEvent.RemoveListener(OnReceive);
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
    }

    public void OnChat(string message)
    {
        Debug.Log(message);
    }
}
