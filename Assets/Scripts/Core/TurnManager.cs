using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class TurnManager : MonoBehaviour
{
    public enum State
    {
        TurnStart,
        WaitingToRoll,
        Rolling,
        WaitingForChoice,
        MustRegister,
        Registering,
        TurnEnd,
        GameOver
    }

    public State CurrentState { get; private set; } = State.TurnStart;

    [SerializeField] private DiceController      diceCtrl;
    [SerializeField] private DiceCup             diceCup;
    [SerializeField] private UICupShaker         uiCupShaker;
    [SerializeField] private ScoreBoardUI        scoreBoardUI;
    [SerializeField] private TimerUI             timerUI;
    [SerializeField] private TurnToken           turnToken;
    [SerializeField] private RegisterButton      registerBtn;
    [SerializeField] private TurnBannerUI        turnBanner;
    [SerializeField] private CelebrationBannerUI celebrationBanner;
    [SerializeField] private GameSettings        settings;

    private List<PlayerData> _players;
    private NetworkDiceRPC _networkDiceRpc;
    private int _currentPlayerIndex;
    private int _currentRound;
    private int _turnSerial = 1;
    private bool _waitingForScoreConfirmation;

    public PlayerData CurrentPlayer => _players[_currentPlayerIndex];
    public int CurrentTurnSerial => _turnSerial;
    private int MaxRollCount => settings?.maxRollCount ?? 3;

    void Awake()
    {
        if (diceCtrl == null) diceCtrl = FindObjectOfType<DiceController>();
        SubscribeDiceEvents();
    }

    void OnDestroy()
    {
        if (diceCtrl != null)
        {
            diceCtrl.OnRollStart -= OnDiceRollStart;
            diceCtrl.OnRollComplete -= OnRollComplete;
        }

        if (scoreBoardUI != null) scoreBoardUI.OnCategorySelected -= OnCategorySelected;
        if (registerBtn != null) registerBtn.OnRegisterClicked -= OnRegisterClicked;
        if (timerUI != null) timerUI.OnTimeUp -= OnTimeUp;
        if (uiCupShaker != null) uiCupShaker.OnShakeStarted -= OnCupShakeStarted;
    }

    void Start()
    {
        _currentRound = 1;
        _currentPlayerIndex = 0;
        _turnSerial = 1;
        StartCoroutine(InitCoroutine());
    }

    IEnumerator InitCoroutine()
    {
        yield return null;

        EnsureSceneReferences();
        SubscribeDiceEvents();
        WireUiEvents();
        SetupPlayersAndSettings();

        if (scoreBoardUI == null)
        {
            Debug.LogError("[TurnManager] scoreBoardUI missing.");
            yield break;
        }

        scoreBoardUI.Build(_players);
        scoreBoardUI.UpdateRound(_currentRound, settings.totalRounds);
        AudioManager.Instance?.PlayGameBGM();

        BeginTurn();
    }

    void EnsureSceneReferences()
    {
        if (diceCtrl == null) diceCtrl = FindObjectOfType<DiceController>();
        if (diceCup == null) diceCup = FindObjectOfType<DiceCup>();
        if (uiCupShaker == null) uiCupShaker = FindObjectOfType<UICupShaker>();
        if (scoreBoardUI == null) scoreBoardUI = FindObjectOfType<ScoreBoardUI>(true);
        if (timerUI == null) timerUI = FindObjectOfType<TimerUI>();
        if (registerBtn == null) registerBtn = FindObjectOfType<RegisterButton>();
        if (turnToken == null) turnToken = FindObjectOfType<TurnToken>();
        if (turnBanner == null) turnBanner = FindObjectOfType<TurnBannerUI>(true);
        if (celebrationBanner == null) celebrationBanner = FindObjectOfType<CelebrationBannerUI>(true);
        if (_networkDiceRpc == null) _networkDiceRpc = FindObjectOfType<NetworkDiceRPC>();
    }

    void WireUiEvents()
    {
        if (scoreBoardUI != null) scoreBoardUI.OnCategorySelected += OnCategorySelected;
        if (registerBtn != null) registerBtn.OnRegisterClicked += OnRegisterClicked;
        if (timerUI != null) timerUI.OnTimeUp += OnTimeUp;
        if (uiCupShaker != null) uiCupShaker.OnShakeStarted += OnCupShakeStarted;
    }

    void SetupPlayersAndSettings()
    {
        if (GameManager.Instance != null)
        {
            _players = GameManager.Instance.Players;
            settings = GameManager.Instance.Settings;
            if (_players != null)
            {
                foreach (var p in _players)
                    if (p.scores == null) p.Init(8);
            }
        }

        if (_players == null || _players.Count == 0)
            _players = CreateFallbackPlayers();

        if (settings == null)
        {
            Debug.LogWarning("[TurnManager] GameSettings missing, using runtime default.");
            settings = ScriptableObject.CreateInstance<GameSettings>();
        }
    }

    List<PlayerData> CreateFallbackPlayers()
    {
        var firstPlayer = new PlayerData { name = "Player1" };
        var secondPlayer = new PlayerData { name = "Player2" };

        if (PhotonNetwork.InRoom)
        {
            foreach (var photonPlayer in PhotonNetwork.PlayerList)
            {
                if (photonPlayer.IsMasterClient) firstPlayer.name = photonPlayer.NickName;
                else secondPlayer.name = photonPlayer.NickName;
            }
        }

        firstPlayer.Init(8);
        secondPlayer.Init(8);
        return new List<PlayerData> { firstPlayer, secondPlayer };
    }

    void SubscribeDiceEvents()
    {
        if (diceCtrl == null) return;

        diceCtrl.OnRollStart -= OnDiceRollStart;
        diceCtrl.OnRollComplete -= OnRollComplete;
        diceCtrl.OnRollStart += OnDiceRollStart;
        diceCtrl.OnRollComplete += OnRollComplete;
    }

    void ChangeState(State next)
    {
        CurrentState = next;
        bool myTurn = IsMyTurn();

        Debug.Log($"[TurnManager] State={next} player={_currentPlayerIndex} myTurn={myTurn} rollCount={diceCtrl?.RollCount}");

        switch (next)
        {
            case State.TurnStart:
                timerUI?.StartTimer(settings.turnTimeLimit);
                timerUI?.PauseTimer(true);
                SetCupActive(false);
                registerBtn?.SetInteractable(false);
                scoreBoardUI?.SetScoringMode(_currentPlayerIndex, false);
                break;

            case State.WaitingToRoll:
                timerUI?.StartTimer(settings.turnTimeLimit);
                SetCupActive(myTurn);
                if (myTurn) diceCup?.SetFirstRollHint(diceCtrl.RollCount == 0);
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
                timerUI?.StartTimer(settings.turnTimeLimit);
                SetCupActive(false);
                registerBtn?.SetInteractable(myTurn && !_waitingForScoreConfirmation);
                if (myTurn) diceCtrl?.SetDiceInteractable(!_waitingForScoreConfirmation);
                break;

            case State.MustRegister:
                timerUI?.StartTimer(settings.turnTimeLimit);
                SetCupActive(false);
                registerBtn?.SetInteractable(false);
                diceCtrl?.SetDiceInteractable(false);
                break;

            case State.Registering:
                SetCupActive(false);
                registerBtn?.SetInteractable(false);
                diceCtrl?.SetDiceInteractable(false);
                scoreBoardUI?.SetScoringMode(_currentPlayerIndex, myTurn && !_waitingForScoreConfirmation);
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
        RefreshRollCountBadges();
    }

    bool IsMyTurn()
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsOnline) return true;
        return _currentPlayerIndex == GameManager.Instance.LocalPlayerIndex;
    }

    public bool CanLocalManipulateDice()
    {
        return IsMyTurn() && CurrentState == State.WaitingForChoice && !_waitingForScoreConfirmation;
    }

    void BeginTurn()
    {
        Debug.Log($"[TurnManager] BeginTurn round={_currentRound} playerIndex={_currentPlayerIndex} turnSerial={_turnSerial}");

        _waitingForScoreConfirmation = false;
        ChangeState(State.TurnStart);
        diceCtrl?.ResetForNewTurn();
        RefreshRollCountBadges();

        scoreBoardUI?.SetActivePlayer(_currentPlayerIndex);
        scoreBoardUI?.ClearAllPreviews(_currentPlayerIndex);
        registerBtn?.SetLabel("점수 등록");
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

    void OnDiceRollStart()
    {
        if (CurrentState != State.Rolling)
            ChangeState(State.Rolling);
    }

    void OnCupShakeStarted()
    {
        if (CurrentState != State.WaitingForChoice) return;
        if (!HasRollsLeft()) return;

        timerUI?.StartTimer(settings.turnTimeLimit);
        timerUI?.PauseTimer(true);
        scoreBoardUI?.ShowZeroPreviews(_currentPlayerIndex);
        scoreBoardUI?.SetScoringMode(_currentPlayerIndex, false);
        registerBtn?.SetInteractable(false);
        diceCtrl?.SetDiceInteractable(false);
    }

    void OnRollComplete()
    {
        bool rollsLeft = HasRollsLeft();

        if (rollsLeft)
        {
            ChangeState(State.WaitingForChoice);
            bool bannerShown = ShowScorePreviews(IsMyTurn() ? (() => SetCupActive(true)) : null);
            if (IsMyTurn() && !bannerShown) SetCupActive(true);
            RefreshRollCountBadges();
        }
        else
        {
            ChangeState(State.MustRegister);
            bool bannerShown = ShowScorePreviews(IsMyTurn() ? (() => scoreBoardUI?.SetScoringMode(_currentPlayerIndex, true)) : null);
            if (IsMyTurn() && !bannerShown) scoreBoardUI?.SetScoringMode(_currentPlayerIndex, true);
            RefreshRollCountBadges();
        }
    }

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

    void OnRegisterClicked()
    {
        if (CurrentState != State.WaitingForChoice) return;
        ChangeState(State.Registering);
    }

    void OnCategorySelected(int categoryIndex, int playerIndex)
    {
        if (!IsScoreSelectionState()) return;

        if (playerIndex != _currentPlayerIndex) return;
        if (!IsMyTurn()) return;

        bool[] recorded = scoreBoardUI.GetRecordedFlags(_currentPlayerIndex);
        if (categoryIndex < 0 || categoryIndex >= recorded.Length || recorded[categoryIndex]) return;

        if (GameManager.Instance != null && GameManager.Instance.IsOnline)
        {
            if (PhotonNetwork.IsMasterClient)
            {
                ConfirmScoreAsMaster(categoryIndex, playerIndex, _turnSerial);
            }
            else
            {
                if (_waitingForScoreConfirmation) return;

                EnterWaitingForScoreConfirmation();
                _networkDiceRpc?.SendScoreRequest(categoryIndex, playerIndex, _turnSerial);
            }

            return;
        }

        ConfirmScoreLocally(categoryIndex, playerIndex);
        ResolveNextTurnState(out int nextPlayerIndex, out int nextRound, out int nextTurnSerial, out bool gameOver);
        AdvanceAfterConfirmedScore(nextPlayerIndex, nextRound, nextTurnSerial, gameOver);
    }

    void OnTimeUp()
    {
        if (!IsMyTurn()) return;

        bool rollsLeft = HasRollsLeft();

        Debug.Log($"[TurnManager] OnTimeUp state={CurrentState} rollCount={diceCtrl?.RollCount} rollsLeft={rollsLeft} canRoll={diceCtrl?.CanRoll}");

        if ((CurrentState == State.WaitingToRoll || CurrentState == State.WaitingForChoice) && rollsLeft)
        {
            if (diceCtrl != null && diceCtrl.CanRoll)
            {
                uiCupShaker?.HideCup();
                diceCtrl.Roll();
            }
            else
            {
                Debug.LogWarning("[TurnManager] Auto-scoring because roll could not proceed.");
                AutoScore();
            }
            return;
        }

        AutoScore();
    }

    void AutoScore()
    {
        Debug.Log($"[TurnManager] AutoScore state={CurrentState} player={_currentPlayerIndex}");

        if (scoreBoardUI == null)
        {
            Debug.LogError("[TurnManager] AutoScore: scoreBoardUI missing.");
            return;
        }

        bool[] recorded = scoreBoardUI.GetRecordedFlags(_currentPlayerIndex);
        int bestCat = ScoreCalculator.BestCategory(diceCtrl.CurrentValues, recorded);
        Debug.Log($"[TurnManager] AutoScore bestCat={bestCat} values=[{string.Join(",", diceCtrl.CurrentValues)}]");

        if (bestCat >= 0)
            OnCategorySelected(bestCat, _currentPlayerIndex);
    }

    void ResolveNextTurnState(out int nextPlayerIndex, out int nextRound, out int nextTurnSerial, out bool gameOver)
    {
        nextPlayerIndex = _currentPlayerIndex + 1;
        nextRound = _currentRound;

        if (nextPlayerIndex >= _players.Count)
        {
            nextPlayerIndex = 0;
            nextRound++;
        }

        gameOver = true;
        foreach (var p in _players)
        {
            if (!p.IsAllFilled)
            {
                gameOver = false;
                break;
            }
        }

        nextTurnSerial = _turnSerial + 1;
    }

    void AdvanceAfterConfirmedScore(int nextPlayerIndex, int nextRound, int nextTurnSerial, bool gameOver)
    {
        ChangeState(gameOver ? State.GameOver : State.TurnEnd);
        _waitingForScoreConfirmation = false;
        _currentPlayerIndex = nextPlayerIndex;
        _currentRound = nextRound;
        _turnSerial = nextTurnSerial;

        if (GameManager.Instance != null)
        {
            while (GameManager.Instance.CurrentRound < _currentRound)
                GameManager.Instance.AdvanceRound();
        }

        scoreBoardUI?.UpdateRound(_currentRound, settings.totalRounds);

        if (gameOver)
        {
            StartCoroutine(GoToResultDelayed());
            return;
        }

        BeginTurn();
    }

    IEnumerator GoToResultDelayed()
    {
        yield return new WaitForSeconds(1.5f);
        GameManager.Instance?.GoToResult();
    }

    public void ReceiveRemoteScoreRequest(int categoryIndex, int playerIndex, int turnSerial)
    {
        if (GameManager.Instance == null || !GameManager.Instance.IsOnline) return;
        if (!PhotonNetwork.IsMasterClient) return;
        if (turnSerial != _turnSerial) return;
        if (playerIndex != _currentPlayerIndex) return;
        if (!IsScoreSelectionState()) return;

        ConfirmScoreAsMaster(categoryIndex, playerIndex, turnSerial);
    }

    public void ReceiveRemoteScoreConfirmed(int categoryIndex, int playerIndex, int score,
        int turnSerial, int nextPlayerIndex, int nextRound, int nextTurnSerial, bool gameOver)
    {
        Debug.Log($"[TurnManager] ReceiveRemoteScoreConfirmed cat={categoryIndex} player={playerIndex} score={score} turn={turnSerial}->{nextTurnSerial}");

        if (playerIndex < 0 || playerIndex >= _players.Count) return;
        if (turnSerial != _turnSerial) return;

        ApplyConfirmedScore(categoryIndex, playerIndex, score);
        AdvanceAfterConfirmedScore(nextPlayerIndex, nextRound, nextTurnSerial, gameOver);
    }

    void ConfirmScoreAsMaster(int categoryIndex, int playerIndex, int turnSerial)
    {
        if (turnSerial != _turnSerial) return;

        int score = ConfirmScoreLocally(categoryIndex, playerIndex);
        ResolveNextTurnState(out int nextPlayerIndex, out int nextRound, out int nextTurnSerial, out bool gameOver);

        _networkDiceRpc?.SendScoreConfirmed(
            categoryIndex,
            playerIndex,
            score,
            turnSerial,
            nextPlayerIndex,
            nextRound,
            nextTurnSerial,
            gameOver);

        AdvanceAfterConfirmedScore(nextPlayerIndex, nextRound, nextTurnSerial, gameOver);
    }

    int ConfirmScoreLocally(int categoryIndex, int playerIndex)
    {
        int score = ScoreCalculator.Calculate(categoryIndex, diceCtrl.CurrentValues);
        ApplyConfirmedScore(categoryIndex, playerIndex, score);
        return score;
    }

    void ApplyConfirmedScore(int categoryIndex, int playerIndex, int score)
    {
        if (categoryIndex < 0 || categoryIndex >= _players[playerIndex].scores.Length) return;

        _players[playerIndex].scores[categoryIndex] = score;
        scoreBoardUI?.ConfirmScore(categoryIndex, playerIndex, score);
        scoreBoardUI?.ClearAllPreviews(playerIndex);
        scoreBoardUI?.RefreshTotalScores(_players);
        AudioManager.Play("score_register");
    }

    bool HasRollsLeft()
    {
        return diceCtrl != null && diceCtrl.RollCount < MaxRollCount;
    }

    bool IsScoreSelectionState()
    {
        return CurrentState == State.WaitingForChoice ||
               CurrentState == State.Registering ||
               CurrentState == State.MustRegister;
    }

    void EnterWaitingForScoreConfirmation()
    {
        _waitingForScoreConfirmation = true;
        timerUI?.StopTimer();
        SetCupActive(false);
        registerBtn?.SetInteractable(false);
        diceCtrl?.SetDiceInteractable(false);
        scoreBoardUI?.SetScoringMode(_currentPlayerIndex, false);
    }

    void RefreshRollCountBadges()
    {
        diceCup?.UpdateRollCountBadge();
        uiCupShaker?.UpdateRollCountBadge();
    }

    IEnumerator AITurnCoroutine()
    {
        var ai = GetComponent<AIPlayer>();
        if (ai == null)
        {
            BeginTurn();
            yield break;
        }

        ChangeState(State.WaitingToRoll);
        yield return new WaitForSeconds(settings.aiThinkDelay);

        while (diceCtrl.RollCount < settings.maxRollCount)
        {
            yield return new WaitForSeconds(settings.aiRollDelay);
            diceCtrl.Roll();
            yield return new WaitUntil(() => !diceCtrl.IsRolling);

            bool shouldContinue = ai.ShouldReroll(
                _players[_currentPlayerIndex],
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
        int bestCat = ai.ChooseCategory(_players[_currentPlayerIndex], diceCtrl.CurrentValues, recorded);
        ChangeState(State.Registering);
        OnCategorySelected(bestCat, _currentPlayerIndex);
    }
}
