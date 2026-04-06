using UnityEngine;
using Photon.Pun;

/// <summary>
/// 네트워크 통신 전용 로그 유틸.
/// Unity 콘솔에서 연두색으로 구분 가능.
/// </summary>
public static class NetLog
{
    const string COLOR = "#7FFF00";  // 연두색 (Chartreuse)

    static string Me => PhotonNetwork.IsConnected
        ? $"[{PhotonNetwork.NickName}|{(PhotonNetwork.IsMasterClient ? "방장" : "참가자")}]"
        : "[미연결]";

    public static void Send(string msg)
        => Debug.Log($"<color={COLOR}>▶ SEND {Me} {msg}</color>");

    public static void Recv(string msg)
        => Debug.Log($"<color={COLOR}>◀ RECV {Me} {msg}</color>");

    public static void Info(string msg)
        => Debug.Log($"<color={COLOR}>● NET  {Me} {msg}</color>");

    public static void Warn(string msg)
        => Debug.LogWarning($"<color={COLOR}>⚠ NET  {Me} {msg}</color>");
}
