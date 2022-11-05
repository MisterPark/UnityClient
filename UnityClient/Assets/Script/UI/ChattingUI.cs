using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ChattingUI : MonoBehaviour
{
    [SerializeField] private InputField inputField;
    [SerializeField] private Button button;
    [SerializeField] private GameEventString onSendChatEvent;

    private void OnValidate()
    {
        if(inputField == null) GetComponentInChildren<InputField>();
        if(button == null) GetComponentInChildren<Button>();
    }

    private void Start()
    {
        button.onClick.AddListener(OnSendChat);
    }

    
    private void Update()
    {
        if(Input.GetKeyDown(KeyCode.Return))
        {
            OnSendChat();
            inputField.ActivateInputField();
            inputField.Select();
        }
    }

    public void OnSendChat()
    {
        if (inputField.text == string.Empty) return;
        string text = inputField.text;
        inputField.text = string.Empty;
        onSendChatEvent.Invoke(text);
    }
}
