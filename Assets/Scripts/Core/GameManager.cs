using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    private static GameManager _instance;
    public static GameManager Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("GameManager");
                _instance = go.AddComponent<GameManager>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [SerializeField] private GameSettings settings;

    public GameSettings Settings => settings;
    public List<PlayerData> Players { get; private set; } = new List<PlayerData>();
    public int CurrentRound { get; private set; } = 1;

    // 온라인 멀티플레이 여부 — PUN2 InRoom을 실시간 반영
    private bool _isOnline = false;
    public bool IsOnline
    {
        get => _isOnline || Photon.Pun.PhotonNetwork.InRoom;
        private set => _isOnline = value;
    }

    // 내 플레이어 인덱스 — PUN2 방 안에서는 IsMasterClient로 실시간 결정
    private int _localPlayerIndex = 0;
    public int LocalPlayerIndex
    {
        get => Photon.Pun.PhotonNetwork.InRoom
               ? (Photon.Pun.PhotonNetwork.IsMasterClient ? 0 : 1)
               : _localPlayerIndex;
        private set => _localPlayerIndex = value;
    }

    // ─── 씬 이름 상수 ───────────────────────────────────────────────
    public const string SCENE_BOOT    = "00_Boot";
    public const string SCENE_MAIN    = "01_MainMenu";
    public const string SCENE_LOBBY   = "02_LobbySetup";
    public const string SCENE_GAME    = "03_1_Game";
    public const string SCENE_RESULT  = "04_Result";

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.Log("[GameManager] 중복 인스턴스 — 이 컴포넌트만 파괴 (gameObject는 유지)");
            Destroy(this);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        if (settings == null)
            settings = Resources.Load<GameSettings>("GameSettings");

        Debug.Log($"[GameManager] Awake — settings={(settings != null ? settings.name : "null (Resources에 없음)")}");

        // PUN2 방 안에서 씬이 로드됐으면 자동으로 온라인 모드 설정
        // (참가자는 AutomaticallySyncScene으로 씬 전환 → StartOnlineGame 호출 전에 씬 진입 가능)
        if (!IsOnline && Photon.Pun.PhotonNetwork.InRoom)
        {
            IsOnline         = true;
            LocalPlayerIndex = Photon.Pun.PhotonNetwork.IsMasterClient ? 0 : 1;
            Debug.Log($"[GameManager] PUN2 방 내 씬 로드 감지 → IsOnline=true, LocalPlayerIndex={LocalPlayerIndex}");

            // 플레이어 데이터가 없으면 기본값 생성
            if (Players == null || Players.Count == 0)
            {
                var p0 = new PlayerData { name = "플레이어1" };
                var p1 = new PlayerData { name = "플레이어2" };
                // 실제 닉네임 반영
                foreach (var p in Photon.Pun.PhotonNetwork.PlayerList)
                {
                    int idx = p.IsMasterClient ? 0 : 1;
                    if (idx == 0) p0.name = p.NickName;
                    else          p1.name = p.NickName;
                }
                Players = new System.Collections.Generic.List<PlayerData> { p0, p1 };
                if (settings != null)
                    foreach (var pl in Players) pl.Init(settings.totalRounds);
            }
        }
    }

    // ─── 게임 시작 (로컬) ───────────────────────────────────────────
    public void StartGame(List<PlayerData> players)
    {
        IsOnline         = false;
        LocalPlayerIndex = 0;
        Debug.Log($"[GameManager] StartGame(로컬) — 플레이어 {players.Count}명");
        Players = players;
        CurrentRound = 1;
        foreach (var p in Players)
            p.Init(settings != null ? settings.totalRounds : 8);
        LoadScene(SCENE_GAME);
    }

    // ─── 게임 시작 (온라인) ─────────────────────────────────────────
    public void StartOnlineGame(List<PlayerData> players, int localPlayerIndex)
    {
        IsOnline         = true;
        LocalPlayerIndex = localPlayerIndex;
        Debug.Log($"[GameManager] StartOnlineGame — localIndex={localPlayerIndex}");
        Players = players;
        CurrentRound = 1;
        foreach (var p in Players)
            p.Init(settings != null ? settings.totalRounds : 8);
        // 씬 전환은 NetworkManager.LoadGameScene()이 담당 (방장만 호출)
    }

    // ─── 라운드 진행 ─────────────────────────────────────────────────
    public void AdvanceRound()
    {
        CurrentRound++;
    }

    public bool IsGameOver()
    {
        foreach (var p in Players)
            if (!p.IsAllFilled) return false;
        return true;
    }

    // ─── 결과 화면으로 ───────────────────────────────────────────────
    public void GoToResult()
    {
        LoadScene(SCENE_RESULT);
    }

    // ─── 씬 전환 헬퍼 ───────────────────────────────────────────────
    public void LoadScene(string sceneName)
    {
        Debug.Log($"[GameManager] LoadScene → {sceneName}");
        SceneManager.LoadScene(sceneName);
    }

    public void ReloadCurrentScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
