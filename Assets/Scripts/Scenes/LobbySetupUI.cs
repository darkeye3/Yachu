using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class LobbySetupUI : MonoBehaviour
{
    [Header("플레이어 슬롯 (최대 4)")]
    [SerializeField] private GameObject[]    playerSlots;       // 슬롯 루트 (활성/비활성)
    [SerializeField] private TMP_InputField[]nameInputs;
    [SerializeField] private Toggle[]        aiToggles;
    [SerializeField] private TMP_Dropdown[]  difficultyDropdowns;
    [SerializeField] private Image[]         avatarImages;      // 아바타 미리보기
    [SerializeField] private Sprite[]        defaultAvatars;    // 기본 아바타 4종

    [Header("플레이어 수 조절")]
    [SerializeField] private Button btnAddPlayer;
    [SerializeField] private Button btnRemovePlayer;
    [SerializeField] private TextMeshProUGUI playerCountText;

    [Header("시작")]
    [SerializeField] private Button btnStart;
    [SerializeField] private Button btnBack;

    private int _playerCount = 2;
    private const int MinPlayers = 2;
    private const int MaxPlayers = 4;

    void Start()
    {
        Debug.Log($"[LobbySetupUI] Start — btnStart={btnStart != null}, btnBack={btnBack != null}");
        Debug.Log($"[LobbySetupUI] playerSlots={playerSlots?.Length}, nameInputs={nameInputs?.Length}, aiToggles={aiToggles?.Length}, diffDropdowns={difficultyDropdowns?.Length}");

        if (btnStart        == null) Debug.LogWarning("[LobbySetupUI] btnStart가 Inspector에 연결되지 않았습니다!");
        if (btnBack         == null) Debug.LogWarning("[LobbySetupUI] btnBack이 Inspector에 연결되지 않았습니다!");
        if (playerSlots     == null) Debug.LogWarning("[LobbySetupUI] playerSlots가 Inspector에 연결되지 않았습니다!");

        btnAddPlayer?.onClick.AddListener(AddPlayer);
        btnRemovePlayer?.onClick.AddListener(RemovePlayer);
        btnStart?.onClick.AddListener(OnStartButtonClick);
        btnBack?.onClick.AddListener(() => GameManager.Instance.LoadScene(GameManager.SCENE_MAIN));

        SetupDefaultNames();
        RefreshSlots();
    }

    void SetupDefaultNames()
    {
        string[] defaults = { "플레이어1", "플레이어2", "플레이어3", "플레이어4" };
        if (nameInputs == null) return;
        for (int i = 0; i < nameInputs.Length && i < defaults.Length; i++)
            if (nameInputs[i]) nameInputs[i].text = defaults[i];
    }

    void AddPlayer()
    {
        if (_playerCount >= MaxPlayers) return;
        _playerCount++;
        RefreshSlots();
    }

    void RemovePlayer()
    {
        if (_playerCount <= MinPlayers) return;
        _playerCount--;
        RefreshSlots();
    }

    void RefreshSlots()
    {
        if (playerSlots == null) return;
        for (int i = 0; i < playerSlots.Length; i++)
            if (playerSlots[i]) playerSlots[i].SetActive(i < _playerCount);

        if (playerCountText) playerCountText.text = $"{_playerCount}명";
        if (btnAddPlayer)    btnAddPlayer.interactable    = _playerCount < MaxPlayers;
        if (btnRemovePlayer) btnRemovePlayer.interactable = _playerCount > MinPlayers;
    }

    public void OnStartButtonClick()
    {
        Debug.Log($"[LobbySetupUI] 시작 버튼 클릭 — _playerCount={_playerCount}");

        if (GameManager.Instance == null)
        {
            Debug.LogError("[LobbySetupUI] GameManager.Instance가 null! 씬에 GameManager가 없습니다.");
            return;
        }

        var players = new List<PlayerData>();
        for (int i = 0; i < _playerCount; i++)
        {
            string playerName = (nameInputs != null && i < nameInputs.Length && nameInputs[i])
                                ? nameInputs[i].text : $"Player{i + 1}";
            bool isAI         = (aiToggles != null && i < aiToggles.Length && aiToggles[i])
                                ? aiToggles[i].isOn : false;
            AIDifficulty diff = (difficultyDropdowns != null && i < difficultyDropdowns.Length && difficultyDropdowns[i])
                                ? (AIDifficulty)difficultyDropdowns[i].value : AIDifficulty.Normal;
            Sprite avatar     = (defaultAvatars != null && i < defaultAvatars.Length)
                                ? defaultAvatars[i] : null;

            Debug.Log($"[LobbySetupUI] 플레이어[{i}] name={playerName}, isAI={isAI}, diff={diff}");

            players.Add(new PlayerData
            {
                name         = playerName,
                isAI         = isAI,
                aiDifficulty = diff,
                avatarSprite = avatar
            });
        }

        Debug.Log($"[LobbySetupUI] GameManager.StartGame 호출 — 플레이어 {players.Count}명");
        GameManager.Instance.StartGame(players);
    }
}
