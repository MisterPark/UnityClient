using UnityEngine;
using UnityEngine.UI;

public class VariableTest : MonoBehaviour
{
    [SerializeField] private Text text;
    [SerializeField] private VariableInt something;


    private void OnEnable()
    {
        something.OnValueChanged.AddListener(OnValueChanged);
    }

    private void OnDisable()
    {
        something.OnValueChanged.RemoveListener(OnValueChanged);
    }

    private void Start()
    {

    }

    private void Update()
    {

    }

    private void OnValidate()
    {
        if (text == null) GetComponent<Text>();
    }

    private void OnValueChanged(int newValue)
    {
        text.text = newValue.ToString();
    }

}
