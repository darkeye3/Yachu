using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// PUN2 연결·방 관리 싱글톤.
/// 최대 10개 방(20 CCU) 초과 시 접속 차단.
/// </summary>
public class NetworkManager : MonoBehaviourPunCallbacks
{
    public static NetworkManager Instance { get; private set; }

    public const int MaxRooms   = 10;   // 방 10개 = 20 CCU
    public const int MaxPlayers = 2;

    public bool IsConnecting   { get; private set; }
    public bool IsInRoom       => PhotonNetwork.InRoom;
    public bool IsOnlineMode   => PhotonNetwork.IsConnected;

    // 로비 UI가 구독
    public System.Action              OnConnectedToServer;
    public System.Action<string>      OnConnectionFailed;
    public System.Action              OnRoomCreated;
    public System.Action              OnRoomJoined;
    public System.Action<string>      OnRoomFailed;
    public System.Action              OnOpponentJoined;   // 상대방 입장
    public System.Action              OnOpponentLeft;     // 상대방 퇴장
    public System.Action<int>         OnRoomCountUpdated; // 현재 방 수

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        PhotonNetwork.AutomaticallySyncScene = true;
    }

    // ── 연결 ─────────────────────────────────────────────────────────
    public void Connect()
    {
        if (PhotonNetwork.IsConnected) { OnConnectedToServer?.Invoke(); return; }
        IsConnecting = true;
        PhotonNetwork.GameVersion = "1.0";
        PhotonNetwork.ConnectUsingSettings();
    }

    public void Disconnect()
    {
        if (PhotonNetwork.IsConnected) PhotonNetwork.Disconnect();
    }

    // ── 방 만들기 ─────────────────────────────────────────────────────
    public void CreateRoom(string roomName)
    {
        if (PhotonNetwork.CountOfRooms >= MaxRooms)
        {
            OnRoomFailed?.Invoke("서버가 가득 찼습니다.\n잠시 후 다시 시도해주세요.");
            return;
        }
        var options = new RoomOptions
        {
            MaxPlayers   = MaxPlayers,
            IsVisible    = true,
            IsOpen       = true,
        };
        PhotonNetwork.CreateRoom(roomName, options);
    }

    // ── 방 참가 ───────────────────────────────────────────────────────
    public void JoinRoom(string roomName)
    {
        PhotonNetwork.JoinRoom(roomName);
    }

    public void JoinRandomRoom()
    {
        PhotonNetwork.JoinRandomRoom();
    }

    // ── 방 나가기 ─────────────────────────────────────────────────────
    public void LeaveRoom()
    {
        if (PhotonNetwork.InRoom) PhotonNetwork.LeaveRoom();
    }

    // ── 씬 전환 (방장만) ──────────────────────────────────────────────
    public void LoadGameScene()
    {
        if (!PhotonNetwork.IsMasterClient) return;
        PhotonNetwork.LoadLevel(GameManager.SCENE_GAME);
    }

    // ── 콜백 ─────────────────────────────────────────────────────────
    public override void OnConnectedToMaster()
    {
        IsConnecting = false;
        NetLog.Info("서버 연결 성공 → 로비 입장 시도");
        PhotonNetwork.JoinLobby();
    }

    public override void OnJoinedLobby()
    {
        NetLog.Info("로비 입장 완료");
        OnConnectedToServer?.Invoke();
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        IsConnecting = false;
        NetLog.Warn($"연결 끊김: {cause}");
        OnConnectionFailed?.Invoke(cause.ToString());
    }

    public override void OnCreatedRoom()
    {
        NetLog.Info($"방 생성 완료: {PhotonNetwork.CurrentRoom.Name}");
        OnRoomCreated?.Invoke();
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        NetLog.Warn($"방 생성 실패: {message}");
        OnRoomFailed?.Invoke($"방 생성 실패: {message}");
    }

    public override void OnJoinedRoom()
    {
        NetLog.Info($"방 입장: {PhotonNetwork.CurrentRoom.Name} ({PhotonNetwork.CurrentRoom.PlayerCount}/{MaxPlayers}) IsMaster={PhotonNetwork.IsMasterClient}");
        OnRoomJoined?.Invoke();
        OnRoomCountUpdated?.Invoke(PhotonNetwork.CountOfRooms);
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        NetLog.Warn($"방 참가 실패: {message}");
        OnRoomFailed?.Invoke($"방 참가 실패: {message}");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        NetLog.Info("랜덤 참가 실패 → 새 방 생성");
        CreateRoom(null);
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        NetLog.Info($"상대방 입장: {newPlayer.NickName} ({PhotonNetwork.CurrentRoom.PlayerCount}/{MaxPlayers})");
        OnOpponentJoined?.Invoke();
        // OnOpponentJoined 콜백(OnlineRoomUI)이 StartOnlineGame을 호출한 뒤 씬 전환
        if (PhotonNetwork.CurrentRoom.PlayerCount == MaxPlayers && PhotonNetwork.IsMasterClient)
        {
            NetLog.Info("2명 완료 → 게임 씬 로드");
            LoadGameScene();
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        NetLog.Warn($"상대방 퇴장: {otherPlayer.NickName}");
        OnOpponentLeft?.Invoke();
    }

    public override void OnRoomListUpdate(System.Collections.Generic.List<RoomInfo> roomList)
    {
        NetLog.Info($"방 목록 업데이트: 현재 {PhotonNetwork.CountOfRooms}개");
        OnRoomCountUpdated?.Invoke(PhotonNetwork.CountOfRooms);
    }
}
