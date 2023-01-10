using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class InputVariableTest : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private InputField inputField;
    [SerializeField] private Variable<int> something;


    private void OnEnable()
    {
        button.onClick.AddListener(OnClick);
    }

    private void OnDisable()
    {
        button.onClick.RemoveListener(OnClick);
    }
    void Start()
    {
        
    }

    void Update()
    {
        
    }

    private void OnClick()
    {
        if(int.TryParse(inputField.text, out int value))
        {
            something.Value = value;
        }
    }
}
