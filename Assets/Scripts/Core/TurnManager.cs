using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class TurnManager : MonoBehaviour
{
    // ─── State Enum ──────────────────────────────────────────────────
    public enum State
    {
        TurnStart,       // Initialize + show banner. All input blocked
        WaitingToRoll,   // Cup active (up to 3 rolls). Only cup is interactable
        Rolling,         // From cup press ~ dice lineup complete. All input blocked
        WaitingForChoice,// After roll. Can roll again or register (rollCount 1~2)
        MustRegister,    // rollCount=3 reached. Must register a score
        Registering,     // Waiting for score category click
        TurnEnd,         // Score confirmed, transitioning
        GameOver         // All categories filled
    }
    public State CurrentState { get; private set; } = State.TurnStart;

    // ─── Inspector 연결 ──────────────────────────────────────────────
    [SerializeField] private DiceController       diceCtrl;
    [SerializeField] private DiceCup              diceCup;
    [SerializeField] private UICupShaker          uiCupShaker;
    [SerializeField] private ScoreBoardUI         scoreBoardUI;
    [SerializeField] private TimerUI              timerUI;
    [SerializeField] private TurnToken            turnToken;
    [SerializeField] private RegisterButton       registerBtn;
    [SerializeField] private TurnBannerUI         turnBanner;
    [SerializeField] private CelebrationBannerUI  celebrationBanner;
    [SerializeField] private GameSettings         settings;

    // ─── 런타임 ──────────────────────────────────────────────────────
    private List<PlayerData> _players;
    private int              _currentPlayerIndex;
    private int              _currentRound;

    public PlayerData CurrentPlayer => _players[_currentPlayerIndex];

    // ─── Awake ───────────────────────────────────────────────────────
    void Awake()
    {
        if (diceCtrl == null) diceCtrl = FindObjectOfType<DiceController>();
        if (diceCtrl != null)
        {
            diceCtrl.OnRollStart    += OnDiceRollStart;
            diceCtrl.OnRollComplete += OnRollComplete;
        }
    }

    void OnDestroy()
    {
        if (diceCtrl != null)
        {
            diceCtrl.OnRollStart    -= OnDiceRollStart;
            diceCtrl.OnRollComplete -= OnRollComplete;
        }
        if (scoreBoardUI != null) scoreBoardUI.OnCategorySelected -= OnCategorySelected;
        if (registerBtn  != null) registerBtn.OnRegisterClicked   -= OnRegisterClicked;
        if (timerUI      != null) timerUI.OnTimeUp                -= OnTimeUp;
    }

    // ─── 초기화 ──────────────────────────────────────────────────────
    void Start()
    {
        _currentRound       = 1;
        _currentPlayerIndex = 0;
        StartCoroutine(InitCoroutine());
    }

    IEnumerator InitCoroutine()
    {
        yield return null;  // 모든 Start() 완료 대기

        if (diceCtrl     == null) diceCtrl     = FindObjectOfType<DiceController>();
        if (diceCup      == null) diceCup      = FindObjectOfType<DiceCup>();
        if (uiCupShaker  == null) uiCupShaker  = FindObjectOfType<UICupShaker>();
        if (scoreBoardUI == null) scoreBoardUI = FindObjectOfType<ScoreBoardUI>(true);
        if (timerUI      == null) timerUI      = FindObjectOfType<TimerUI>();
        if (registerBtn  == null) registerBtn  = FindObjectOfType<RegisterButton>();
        if (turnToken    == null) turnToken    = FindObjectOfType<TurnToken>();
        if (turnBanner        == null) turnBanner        = FindObjectOfType<TurnBannerUI>(true);
        if (celebrationBanner == null) celebrationBanner = FindObjectOfType<CelebrationBannerUI>(true);

        if (diceCtrl != null && !IsSubscribed())
        {
            diceCtrl.OnRollStart    += OnDiceRollStart;
            diceCtrl.OnRollComplete += OnRollComplete;
        }

        if (scoreBoardUI != null) scoreBoardUI.OnCategorySelected += OnCategorySelected;
        if (registerBtn  != null) registerBtn.OnRegisterClicked   += OnRegisterClicked;
        if (timerUI      != null) timerUI.OnTimeUp                += OnTimeUp;

        // 플레이어 데이터
        if (GameManager.Instance != null)
        {
            _players = GameManager.Instance.Players;
            settings = GameManager.Instance.Settings;
            if (_players != null)
                foreach (var p in _players)
                    if (p.scores == null) p.Init(8);
        }

        if (_players == null || _players.Count == 0)
        {
            var p0 = new PlayerData { name = "플레이어1" };
            var p1 = new PlayerData { name = "플레이어2" };
            if (PhotonNetwork.InRoom)
                foreach (var pp in PhotonNetwork.PlayerList)
                {
                    if (pp.IsMasterClient) p0.name = pp.NickName;
                    else                   p1.name = pp.NickName;
                }
            p0.Init(8); p1.Init(8);
            _players = new List<PlayerData> { p0, p1 };
        }

        if (settings == null)
        {
            Debug.LogWarning("[TurnManager] GameSettings 없음 — 기본값 사용");
            settings = ScriptableObject.CreateInstance<GameSettings>();
        }

        if (scoreBoardUI == null) { Debug.LogError("[TurnManager] scoreBoardUI 없음!"); yield break; }

        scoreBoardUI.Build(_players);
        scoreBoardUI.UpdateRound(_currentRound, settings.totalRounds);
        AudioManager.Instance?.PlayGameBGM();

        BeginTurn();
    }

    // Check if already subscribed (prevent double subscription)
    bool IsSubscribed() => diceCtrl != null && diceCtrl.OnRollComplete != null;

    // ─── 핵심: 상태 전환 — 모든 UI 제어가 여기서만 ───────────────────
    void ChangeState(State next)
    {
        CurrentState = next;
        bool my        = IsMyTurn();
        bool rollsLeft = diceCtrl != null && diceCtrl.RollCount < (settings?.maxRollCount ?? 3);

        Debug.Log($"[TurnManager] 상태 → {next} | player={_currentPlayerIndex} myTurn={my} rollCount={diceCtrl?.RollCount}");

        switch (next)
        {
            case State.TurnStart:
                timerUI?.StopTimer();
                SetCupActive(false);
                registerBtn?.SetInteractable(false);
                scoreBoardUI?.SetScoringMode(_currentPlayerIndex, false);
                break;

            case State.WaitingToRoll:
                if (my) timerUI?.StartTimer(settings.turnTimeLimit);
                SetCupActive(my);
                if (my) diceCup?.SetFirstRollHint(diceCtrl.RollCount == 0);
                registerBtn?.SetInteractable(false);
                scoreBoardUI?.SetScoringMode(_currentPlayerIndex, false);
                break;

            case State.Rolling:
                timerUI?.StopTimer();
                SetCupActive(false);
                uiCupShaker?.HideCup();
                registerBtn?.SetInteractable(false);
                scoreBoardUI?.SetScoringMode(_currentPlayerIndex, false);
                diceCtrl?.SetDiceInteractable(false);
                break;

            case State.WaitingForChoice:
                // Cup is activated separately after celebration banner check (in OnRollComplete)
                if (my) timerUI?.StartTimer(settings.turnTimeLimit);
                SetCupActive(false);
                registerBtn?.SetInteractable(my);
                if (my) diceCtrl?.SetDiceInteractable(true);
                break;

            case State.MustRegister:
                // No cup, scoreboard only (activated after celebration banner check)
                if (my) timerUI?.StartTimer(settings.turnTimeLimit);
                SetCupActive(false);
                registerBtn?.SetInteractable(false);
                diceCtrl?.SetDiceInteractable(false);
                break;

            case State.Registering:
                SetCupActive(false);
                registerBtn?.SetInteractable(false);
                diceCtrl?.SetDiceInteractable(false);
                scoreBoardUI?.SetScoringMode(_currentPlayerIndex, my);
                break;

            case State.TurnEnd:
            case State.GameOver:
                timerUI?.StopTimer();
                SetCupActive(false);
                registerBtn?.SetInteractable(false);
                scoreBoardUI?.SetScoringMode(_currentPlayerIndex, false);
                break;
        }
    }

    void SetCupActive(bool on)
    {
        diceCup?.SetInteractable(on);
        uiCupShaker?.SetInteractable(on);
        if (on) uiCupShaker?.ShowCup();
        diceCup?.UpdateRollCountBadge();
        uiCupShaker?.UpdateRollCountBadge();
    }

    bool IsMyTurn()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsOnline) return true;
        return _currentPlayerIndex == GameManager.Instance.LocalPlayerIndex;
    }

    // ─── 턴 시작 ─────────────────────────────────────────────────────
    void BeginTurn()
    {
        Debug.Log($"[TurnManager] BeginTurn — 라운드={_currentRound} 플레이어[{_currentPlayerIndex}]={CurrentPlayer?.name}");

        ChangeState(State.TurnStart);
        diceCtrl?.ResetForNewTurn();

        scoreBoardUI.SetActivePlayer(_currentPlayerIndex);
        scoreBoardUI.ClearAllPreviews(_currentPlayerIndex);
        registerBtn?.SetLabel("족보 등록");
        turnToken?.Refresh(CurrentPlayer.avatarSprite);

        if (turnBanner != null)
            turnBanner.Show(CurrentPlayer.name, _currentRound, settings.totalRounds, OnBannerComplete);
        else
            OnBannerComplete();
    }

    void OnBannerComplete()
    {
        if (CurrentPlayer.isAI)
        {
            StartCoroutine(AITurnCoroutine());
            return;
        }
        ChangeState(State.WaitingToRoll);
    }

    // ─── Roll Start (DiceController.OnRollStart) ─────────────────────
    void OnDiceRollStart()
    {
        // ForceRollThenScore calls ChangeState(Rolling) first, skip duplicate
        if (CurrentState != State.Rolling)
            ChangeState(State.Rolling);
    }

    // ─── 굴리기 완료 (DiceController.OnRollComplete) ─────────────────
    void OnRollComplete()
    {
        bool rollsLeft = diceCtrl.RollCount < (settings?.maxRollCount ?? 3);

        if (rollsLeft)
        {
            ChangeState(State.WaitingForChoice);
            if (IsMyTurn())
            {
                bool bannerShown = ShowScorePreviews(() => SetCupActive(true));
                if (!bannerShown) SetCupActive(true);
                diceCup?.UpdateRollCountBadge();
                uiCupShaker?.UpdateRollCountBadge();
            }
        }
        else
        {
            ChangeState(State.MustRegister);
            if (IsMyTurn())
            {
                bool bannerShown = ShowScorePreviews(() => scoreBoardUI?.SetScoringMode(_currentPlayerIndex, true));
                if (!bannerShown) scoreBoardUI?.SetScoringMode(_currentPlayerIndex, true);
                diceCup?.UpdateRollCountBadge();
                uiCupShaker?.UpdateRollCountBadge();
            }
        }
    }

    // ─── 점수 미리보기 + 축하 배너 ───────────────────────────────────
    // 배너가 표시되면 true 반환, onComplete는 배너 종료 후 호출
    bool ShowScorePreviews(System.Action onComplete = null)
    {
        if (scoreBoardUI == null) return false;
        scoreBoardUI.ShowPreviews(_currentPlayerIndex, diceCtrl.CurrentValues);

        if (celebrationBanner == null) return false;
        bool[] recorded = scoreBoardUI.GetRecordedFlags(_currentPlayerIndex);
        for (int cat = 7; cat >= 5; cat--)
        {
            if (cat >= recorded.Length || recorded[cat]) continue;
            int score = ScoreCalculator.Calculate(cat, diceCtrl.CurrentValues);
            if (score > 0)
            {
                celebrationBanner.Show(cat, score, onComplete);
                return true;
            }
        }
        return false;
    }

    // ─── 등록 버튼 ───────────────────────────────────────────────────
    void OnRegisterClicked()
    {
        if (CurrentState != State.WaitingForChoice) return;
        ChangeState(State.Registering);
    }

    // ─── Category Selected ───────────────────────────────────────────
    void OnCategorySelected(int categoryIndex, int playerIndex)
    {
        if (CurrentState != State.WaitingForChoice &&
            CurrentState != State.Registering &&
            CurrentState != State.MustRegister) return;
        if (playerIndex != _currentPlayerIndex) return;

        int score = ScoreCalculator.Calculate(categoryIndex, diceCtrl.CurrentValues);
        _players[_currentPlayerIndex].scores[categoryIndex] = score;
        scoreBoardUI.ConfirmScore(categoryIndex, _currentPlayerIndex, score);
        scoreBoardUI.ClearAllPreviews(_currentPlayerIndex);
        scoreBoardUI.RefreshTotalScores(_players);

        AudioManager.Play("score_register");
        FindObjectOfType<NetworkDiceRPC>()?.SendCategorySelected(categoryIndex, _currentPlayerIndex, score);

        EndTurn();
    }

    // ─── 타임아웃 ────────────────────────────────────────────────────
    void OnTimeUp()
    {
        if (!IsMyTurn()) return;

        bool rollsLeft = diceCtrl != null && diceCtrl.RollCount < (settings?.maxRollCount ?? 3);

        Debug.Log($"[TurnManager] OnTimeUp — state={CurrentState} rollCount={diceCtrl?.RollCount} rollsLeft={rollsLeft} CanRoll={diceCtrl?.CanRoll}");

        // 굴림 대기 중 or 선택 대기 중 + 굴림 횟수 남음 → 자동 굴림
        if ((CurrentState == State.WaitingToRoll || CurrentState == State.WaitingForChoice) && rollsLeft)
        {
            if (diceCtrl != null && diceCtrl.CanRoll)
            {
                uiCupShaker?.HideCup();
                diceCtrl.Roll();
            }
            else
            {
                // 굴리기가 불가능한 상태 (IsRolling stuck 등) → 점수 등록으로 강제 진행
                Debug.LogWarning("[TurnManager] OnTimeUp: CanRoll=false, 자동 점수 등록으로 전환");
                AutoScore();
            }
            return;
        }

        // 굴림 소진 or MustRegister → 자동 점수 등록
        AutoScore();
    }

    void AutoScore()
    {
        Debug.Log($"[TurnManager] AutoScore — state={CurrentState} player={_currentPlayerIndex}");
        if (scoreBoardUI == null) { Debug.LogError("[TurnManager] AutoScore: scoreBoardUI null!"); return; }
        bool[] recorded = scoreBoardUI.GetRecordedFlags(_currentPlayerIndex);
        int    bestCat  = ScoreCalculator.BestCategory(diceCtrl.CurrentValues, recorded);
        Debug.Log($"[TurnManager] AutoScore → bestCat={bestCat} values=[{string.Join(",", diceCtrl.CurrentValues)}]");
        if (bestCat >= 0)
            OnCategorySelected(bestCat, _currentPlayerIndex);
        else
            EndTurn();
    }

    // ─── 턴 종료 ─────────────────────────────────────────────────────
    void EndTurn()
    {
        ChangeState(State.TurnEnd);

        _currentPlayerIndex++;
        if (_currentPlayerIndex >= _players.Count)
        {
            _currentPlayerIndex = 0;
            _currentRound++;
            GameManager.Instance?.AdvanceRound();
            scoreBoardUI.UpdateRound(_currentRound, settings.totalRounds);
        }

        bool allFilled = true;
        foreach (var p in _players)
            if (!p.IsAllFilled) { allFilled = false; break; }

        if (allFilled)
        {
            ChangeState(State.GameOver);
            StartCoroutine(GoToResultDelayed());
            return;
        }

        BeginTurn();
    }

    IEnumerator GoToResultDelayed()
    {
        yield return new WaitForSeconds(1.5f);
        GameManager.Instance.GoToResult();
    }

    // ─── 원격 점수 수신 (NetworkDiceRPC → 호출) ──────────────────────
    public void ReceiveRemoteCategorySelected(int categoryIndex, int playerIndex, int score)
    {
        Debug.Log($"[TurnManager] ReceiveRemote — cat={categoryIndex} player={playerIndex} score={score}");
        if (playerIndex < 0 || playerIndex >= _players.Count) return;

        _players[playerIndex].scores[categoryIndex] = score;
        scoreBoardUI?.ConfirmScore(categoryIndex, playerIndex, score);
        scoreBoardUI?.ClearAllPreviews(playerIndex);
        scoreBoardUI?.RefreshTotalScores(_players);
        AudioManager.Play("score_register");
        EndTurn();
    }

    // ─── AI 턴 ───────────────────────────────────────────────────────
    IEnumerator AITurnCoroutine()
    {
        var ai = GetComponent<AIPlayer>();
        if (ai == null) { BeginTurn(); yield break; }

        ChangeState(State.WaitingToRoll);
        yield return new WaitForSeconds(settings.aiThinkDelay);

        while (diceCtrl.RollCount < settings.maxRollCount)
        {
            yield return new WaitForSeconds(settings.aiRollDelay);
            diceCtrl.Roll();
            yield return new WaitUntil(() => !diceCtrl.IsRolling);

            bool shouldContinue = ai.ShouldReroll(_players[_currentPlayerIndex],
                                                   diceCtrl.CurrentValues,
                                                   diceCtrl.RollCount,
                                                   settings.maxRollCount);
            if (!shouldContinue) break;

            bool[] keepFlags = ai.DecideKeep(_players[_currentPlayerIndex], diceCtrl.CurrentValues);
            for (int i = 0; i < keepFlags.Length; i++)
                diceCtrl.AIKeepDice(i, keepFlags[i]);
        }

        yield return new WaitForSeconds(settings.aiScoreDelay);
        bool[] recorded = scoreBoardUI.GetRecordedFlags(_currentPlayerIndex);
        int    bestCat  = ai.ChooseCategory(_players[_currentPlayerIndex],
                                            diceCtrl.CurrentValues, recorded);
        ChangeState(State.Registering);
        OnCategorySelected(bestCat, _currentPlayerIndex);
    }
}
