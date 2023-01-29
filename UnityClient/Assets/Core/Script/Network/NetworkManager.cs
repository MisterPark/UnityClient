using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ServerType
{
    TCP,
    UDP,
}
public class NetworkManager : MonoBehaviour
{
    [SerializeField] private bool isServer;

    #region ServerFields
    [DrawIf("isServer", true, DrawIfAttribute.DisablingType.DontDraw)]
    [SerializeField] private ServerType serverType = ServerType.TCP;
    #endregion

    #region ClientFields
    [DrawIf("isServer",false, DrawIfAttribute.DisablingType.DontDraw)]
    [SerializeField] private string serverIP = "127.0.0.1";

    #endregion
    [SerializeField] private int port;



    #region ServerMethods
    #endregion


    #region ClientMethods
    #endregion
}
