using UnityEngine;
using UnityEngine.Events;

public class Variable<T> : VariableBase, ISerializationCallbackReceiver
{
    [SerializeField] private T initialValue;
    [SerializeField] private T runtimeValue;

    private UnityEvent<T> onValueChanged = new UnityEvent<T>();
    public UnityEvent<T> OnValueChanged { get { return onValueChanged; } }

    public T Value
    {
        get { return runtimeValue; }
        set
        {
            if (!value.Equals(runtimeValue))
            {
                Debug.Log($"Change to {value} from {runtimeValue}");
                onValueChanged.Invoke(value);
            }

            runtimeValue = value;
        }
    }

    public Variable()
    {
        runtimeValue = initialValue;
    }

    public void OnBeforeSerialize()
    {
        
    }

    public void OnAfterDeserialize()
    {
        runtimeValue = initialValue;
    }
}

public class VariableBase : ScriptableObject
{

}
