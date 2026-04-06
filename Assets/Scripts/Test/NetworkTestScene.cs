using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// PUN2 RaiseEvent + IOnEventCallback 단독 테스트.
/// Bootstrap 없이 직접 연결 → 이벤트 송수신 확인.
///
/// 사용법:
///   Yachu/씬 빌더/15 네트워크 테스트씬 생성 → 두 클라이언트 실행 →
///   방 입장 후 [SEND] 버튼 → 양쪽에 수신 로그 확인.
/// </summary>
public class NetworkTestScene : MonoBehaviourPunCallbacks, IOnEventCallback
{
    // 게임 이벤트(1,2)와 겹치지 않는 테스트 전용 코드
    const byte EV_TEST = 99;

    [SerializeField] TextMeshProUGUI txtLog;
    [SerializeField] Button          btnSend;
    [SerializeField] TextMeshProUGUI txtStatus;

    readonly List<string> _lines = new List<string>();

    // ── 초기화 ────────────────────────────────────────────────────────
    void Start()
    {
        // 이 스크립트도 IOnEventCallback 로 등록
        PhotonNetwork.AddCallbackTarget(this);

        btnSend?.gameObject.SetActive(false);
        btnSend?.onClick.AddListener(OnSendClicked);

        if (string.IsNullOrEmpty(PhotonNetwork.NickName))
            PhotonNetwork.NickName = "Tester" + Random.Range(1000, 9999);

        Log($"=== PUN2 테스트 시작 ===");
        Log($"닉네임: {PhotonNetwork.NickName}");
        Log("서버 연결 중...");

        PhotonNetwork.GameVersion = "test_1";
        PhotonNetwork.ConnectUsingSettings();

        SetStatus("연결 중...");
    }

    void OnDestroy()
    {
        PhotonNetwork.RemoveCallbackTarget(this);
    }

    // ── SEND 버튼 ─────────────────────────────────────────────────────
    void OnSendClicked()
    {
        if (!PhotonNetwork.InRoom) { Log("❌ 방 안에 없음"); return; }

        object[] data = new object[] { 1, 2, 3, 4, 5, Time.realtimeSinceStartup };

        // ReceiverGroup.All = 자신 포함 전송 → 자신도 수신되는지 확인
        var opts = new RaiseEventOptions { Receivers = ReceiverGroup.All };
        bool ok = PhotonNetwork.RaiseEvent(EV_TEST, data, opts, SendOptions.SendReliable);

        Log($"▶ SEND EV_TEST={EV_TEST}  ok={ok}  players={PhotonNetwork.CurrentRoom.PlayerCount}");
    }

    // ── IOnEventCallback ──────────────────────────────────────────────
    public void OnEvent(EventData ev)
    {
        if (ev.Code != EV_TEST) return;

        var d = (object[])ev.CustomData;
        Log($"◀ RECV EV_TEST  from={ev.Sender}  data=[{string.Join(", ", d)}]");
    }

    // ── PUN2 콜백 ─────────────────────────────────────────────────────
    public override void OnConnectedToMaster()
    {
        Log("✅ 서버 연결 성공 → 로비 입장 중...");
        SetStatus("로비 입장 중...");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        Log("✅ 로비 입장 완료 → 랜덤 방 참가 시도...");
        SetStatus("방 찾는 중...");
        PhotonNetwork.JoinRandomRoom();
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        Log($"ℹ 랜덤 참가 실패({message}) → 새 방 생성...");
        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = 2 });
    }

    public override void OnCreatedRoom()
    {
        Log($"✅ 방 생성: [{PhotonNetwork.CurrentRoom.Name}]");
        SetStatus($"방 대기: {PhotonNetwork.CurrentRoom.Name}");
    }

    public override void OnJoinedRoom()
    {
        Log($"✅ 방 입장: [{PhotonNetwork.CurrentRoom.Name}] IsMaster={PhotonNetwork.IsMasterClient} ({PhotonNetwork.CurrentRoom.PlayerCount}/2)");
        SetStatus($"방: {PhotonNetwork.CurrentRoom.Name} | {(PhotonNetwork.IsMasterClient ? "방장" : "참가자")}");
        btnSend?.gameObject.SetActive(true);
    }

    public override void OnPlayerEnteredRoom(Photon.Realtime.Player newPlayer)
    {
        Log($"✅ 상대방 입장: {newPlayer.NickName} → 2명 완료!  [SEND] 버튼을 눌러 테스트하세요.");
        SetStatus("2명 준비 완료 — SEND 버튼으로 테스트");
    }

    public override void OnPlayerLeftRoom(Photon.Realtime.Player otherPlayer)
    {
        Log($"⚠ 상대방 퇴장: {otherPlayer.NickName}");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        Log($"❌ 연결 끊김: {cause}");
        SetStatus($"연결 끊김: {cause}");
    }

    // ── 유틸 ─────────────────────────────────────────────────────────
    void Log(string msg)
    {
        string line = $"[{System.DateTime.Now:HH:mm:ss}] {msg}";
        _lines.Add(line);
        if (_lines.Count > 30) _lines.RemoveAt(0);
        if (txtLog != null) txtLog.text = string.Join("\n", _lines);
        Debug.Log($"[NetworkTest] {msg}");
    }

    void SetStatus(string msg)
    {
        if (txtStatus != null) txtStatus.text = msg;
    }
}
