using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Photon.Pun;

/// <summary>
/// 온라인 2인 매칭 UI.
/// - 빠른 참가: 빈 방 자동 참가, 없으면 새 방 생성
/// - 방 코드: 4자리 코드로 방 만들기/참가
/// 상태: Disconnected → Connecting → Lobby → InRoom(대기) → (씬 전환)
/// </summary>
public class OnlineRoomUI : MonoBehaviour
{
    [Header("패널")]
    [SerializeField] private GameObject panelConnect;   // 연결 전
    [SerializeField] private GameObject panelLobby;     // 로비 (방 만들기/참가)
    [SerializeField] private GameObject panelWaiting;   // 방 대기 (상대방 기다리는 중)

    [Header("연결 패널")]
    [SerializeField] private Button     btnConnect;
    [SerializeField] private TMP_Text   txtStatus;

    [Header("로비 패널")]
    [SerializeField] private Button         btnQuickJoin;   // 빠른 참가
    [SerializeField] private TMP_InputField inputRoomCode;  // 방 코드 입력
    [SerializeField] private Button         btnCreateRoom;  // 방 만들기
    [SerializeField] private Button         btnJoinRoom;    // 코드로 참가
    [SerializeField] private TMP_Text       txtServerInfo;  // 현재 방 수 표시
    [SerializeField] private Button         btnBackToMenu;

    [Header("대기 패널")]
    [SerializeField] private TMP_Text   txtRoomCode;        // 방 코드 (방코드 모드에서만 표시)
    [SerializeField] private TMP_Text   txtWaitingStatus;   // 상태 메시지
    [SerializeField] private Button     btnLeaveRoom;

    private bool _isQuickJoin;

    void Start()
    {
        ShowPanel(panelConnect);

        btnConnect?.onClick.AddListener(OnConnectClicked);
        btnQuickJoin?.onClick.AddListener(OnQuickJoinClicked);
        btnCreateRoom?.onClick.AddListener(OnCreateRoomClicked);
        btnJoinRoom?.onClick.AddListener(OnJoinRoomClicked);
        btnLeaveRoom?.onClick.AddListener(OnLeaveRoomClicked);
        btnBackToMenu?.onClick.AddListener(() => GameManager.Instance.LoadScene(GameManager.SCENE_MAIN));

        if (NetworkManager.Instance == null)
        {
            var go = new GameObject("NetworkManager");
            go.AddComponent<NetworkManager>();
        }

        NetworkManager.Instance.OnConnectedToServer  += OnConnected;
        NetworkManager.Instance.OnConnectionFailed   += OnFailed;
        NetworkManager.Instance.OnRoomCreated        += OnRoomCreated;
        NetworkManager.Instance.OnRoomJoined         += OnRoomJoined;
        NetworkManager.Instance.OnRoomFailed         += OnFailed;
        NetworkManager.Instance.OnOpponentJoined     += OnOpponentJoined;
        NetworkManager.Instance.OnOpponentLeft       += OnOpponentLeft;
        NetworkManager.Instance.OnRoomCountUpdated   += OnRoomCountUpdated;

        // 이미 연결된 상태면 바로 로비 패널
        if (PhotonNetwork.IsConnected && PhotonNetwork.InLobby)
            ShowPanel(panelLobby);
    }

    void OnDestroy()
    {
        if (NetworkManager.Instance == null) return;
        NetworkManager.Instance.OnConnectedToServer  -= OnConnected;
        NetworkManager.Instance.OnConnectionFailed   -= OnFailed;
        NetworkManager.Instance.OnRoomCreated        -= OnRoomCreated;
        NetworkManager.Instance.OnRoomJoined         -= OnRoomJoined;
        NetworkManager.Instance.OnRoomFailed         -= OnFailed;
        NetworkManager.Instance.OnOpponentJoined     -= OnOpponentJoined;
        NetworkManager.Instance.OnOpponentLeft       -= OnOpponentLeft;
        NetworkManager.Instance.OnRoomCountUpdated   -= OnRoomCountUpdated;
    }

    // ── 버튼 핸들러 ──────────────────────────────────────────────────
    void OnConnectClicked()
    {
        SetStatus("서버에 연결 중...");
        btnConnect.interactable = false;

        // 닉네임 자동 생성 (연결 전에 설정해야 Photon 서버에 전달됨)
        if (string.IsNullOrEmpty(PhotonNetwork.NickName))
            PhotonNetwork.NickName = "플레이어" + UnityEngine.Random.Range(1000, 9999);

        NetworkManager.Instance.Connect();
    }

    void OnQuickJoinClicked()
    {
        _isQuickJoin = true;
        SetStatus("상대를 찾는 중...");
        NetworkManager.Instance.JoinRandomRoom();  // 없으면 OnJoinRandomFailed → CreateRoom
    }

    void OnCreateRoomClicked()
    {
        _isQuickJoin = false;
        string code = GenerateRoomCode();
        SetStatus($"방 만드는 중... [{code}]");
        NetworkManager.Instance.CreateRoom(code);
    }

    void OnJoinRoomClicked()
    {
        _isQuickJoin = false;
        string code = inputRoomCode.text.Trim().ToUpper();
        if (code.Length == 0) { SetStatus("방 코드를 입력하세요."); return; }
        SetStatus($"방 [{code}] 참가 중...");
        NetworkManager.Instance.JoinRoom(code);
    }

    void OnLeaveRoomClicked()
    {
        NetworkManager.Instance.LeaveRoom();
        ShowPanel(panelLobby);
    }

    // ── 네트워크 콜백 ────────────────────────────────────────────────
    void OnConnected()
    {
        ShowPanel(panelLobby);
        SetStatus("");
    }

    void OnFailed(string msg)
    {
        SetStatus(msg);
        btnConnect.interactable = true;
        if (PhotonNetwork.IsConnected) ShowPanel(panelLobby);
        else ShowPanel(panelConnect);
    }

    void OnRoomCreated()
    {
        // 방 생성 직후: 대기 패널만 표시, SetupOnlineGame은 OnRoomJoined에서 처리
        ShowPanel(panelWaiting);
        if (_isQuickJoin)
        {
            if (txtRoomCode)      txtRoomCode.gameObject.SetActive(false);
            if (txtWaitingStatus) txtWaitingStatus.text = "상대를 찾는 중입니다...";
        }
        else
        {
            if (txtRoomCode) { txtRoomCode.gameObject.SetActive(true); txtRoomCode.text = $"방 코드: {PhotonNetwork.CurrentRoom.Name}"; }
            if (txtWaitingStatus) txtWaitingStatus.text = "상대방을 기다리는 중...";
        }
    }

    void OnRoomJoined()
    {
        // PUN2는 방장도 OnJoinedRoom 호출됨 → IsMasterClient로 인덱스 결정
        int localIndex = PhotonNetwork.IsMasterClient ? 0 : 1;
        SetupOnlineGame(localIndex);

        ShowPanel(panelWaiting);
        if (_isQuickJoin)
        {
            if (txtRoomCode)      txtRoomCode.gameObject.SetActive(false);
            if (txtWaitingStatus) txtWaitingStatus.text = "상대를 찾는 중입니다...";
        }
        else
        {
            if (txtRoomCode) { txtRoomCode.gameObject.SetActive(true); txtRoomCode.text = $"방 코드: {PhotonNetwork.CurrentRoom.Name}"; }
            if (txtWaitingStatus) txtWaitingStatus.text = "상대방 연결 확인 중...";
        }
    }

    void SetupOnlineGame(int localIndex)
    {
        // 방장(MasterClient) = index 0, 참가자 = index 1
        string myName    = PhotonNetwork.NickName;
        string otherName = PhotonNetwork.PlayerListOthers.Length > 0
                           ? PhotonNetwork.PlayerListOthers[0].NickName
                           : (localIndex == 0 ? "플레이어2" : "플레이어1");

        if (string.IsNullOrEmpty(myName))    myName    = localIndex == 0 ? "플레이어1" : "플레이어2";
        if (string.IsNullOrEmpty(otherName)) otherName = localIndex == 0 ? "플레이어2" : "플레이어1";

        var p0 = new PlayerData { name = localIndex == 0 ? myName : otherName };
        var p1 = new PlayerData { name = localIndex == 1 ? myName : otherName };
        var players = new System.Collections.Generic.List<PlayerData> { p0, p1 };

        Debug.Log($"[OnlineRoomUI] SetupOnlineGame — localIndex={localIndex}, me={players[localIndex].name}, other={players[1-localIndex].name}");
        GameManager.Instance?.StartOnlineGame(players, localIndex);
    }

    void OnOpponentJoined()
    {
        if (txtWaitingStatus) txtWaitingStatus.text = "상대방 입장! 게임 시작 중...";
        // 상대방이 들어온 시점에 이름 확정 + IsOnline 보장 (씬 전환 직전)
        int localIndex = PhotonNetwork.IsMasterClient ? 0 : 1;
        SetupOnlineGame(localIndex);
    }

    void OnOpponentLeft()
    {
        if (txtWaitingStatus) txtWaitingStatus.text = "상대방이 나갔습니다.";
    }

    void OnRoomCountUpdated(int count)
    {
        if (txtServerInfo == null) return;
        if (count >= NetworkManager.MaxRooms)
            txtServerInfo.text = "⚠ 서버가 가득 찼습니다. 잠시 후 다시 시도해주세요.";
        else
            txtServerInfo.text = $"현재 {count} / {NetworkManager.MaxRooms} 방 사용 중";
    }

    // ── 유틸 ─────────────────────────────────────────────────────────
    void ShowPanel(GameObject target)
    {
        if (panelConnect) panelConnect.SetActive(panelConnect == target);
        if (panelLobby)   panelLobby.SetActive(panelLobby == target);
        if (panelWaiting) panelWaiting.SetActive(panelWaiting == target);
    }

    void SetStatus(string msg)
    {
        if (txtStatus) txtStatus.text = msg;
    }

    static string GenerateRoomCode()
    {
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
        var sb = new System.Text.StringBuilder(4);
        for (int i = 0; i < 4; i++)
            sb.Append(chars[UnityEngine.Random.Range(0, chars.Length)]);
        return sb.ToString();
    }
}
