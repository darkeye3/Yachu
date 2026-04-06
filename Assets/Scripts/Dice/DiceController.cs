using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiceController : MonoBehaviour
{
    [SerializeField] private Dice[] dices;
    [SerializeField] private DiceSlot[] keepSlots;
    [SerializeField] private Transform diceArea;
    [SerializeField] private Vector2[] diceOrigins;
    [SerializeField] private DicePhysicsRoller roller;
    [SerializeField] private DiceBox3D diceBox3D;

    public DiceInfo[] Dice { get; private set; } = new DiceInfo[5];

    private bool Has2D => dices != null && dices.Length > 0;
    private bool Has3D => diceBox3D != null;

    public int RollCount { get; private set; }
    public bool IsRolling { get; private set; }
    public bool CanRoll => RollCount < MaxRollCount && !IsRolling;

    private int MaxRollCount => GameManager.Instance?.Settings?.maxRollCount ?? 3;

    public int[] CurrentValues
    {
        get
        {
            if (Has2D && !Has3D)
            {
                var values = new int[dices.Length];
                for (int i = 0; i < dices.Length; i++) values[i] = dices[i].Value;
                return values;
            }

            var values3d = new int[Dice.Length];
            for (int i = 0; i < Dice.Length; i++) values3d[i] = Dice[i].Value;
            return values3d;
        }
    }

    public System.Action OnRollStart;
    public System.Action OnRollComplete;
    public System.Action<Dice> OnDiceKeepToggled;

    private TurnManager _turnManager;

    void Awake()
    {
        for (int i = 0; i < Dice.Length; i++) Dice[i] = new DiceInfo();
    }

    void Start()
    {
        if (dices == null || dices.Length == 0)
            dices = FindObjectsOfType<Dice>();

        if (keepSlots == null || keepSlots.Length == 0)
            keepSlots = FindObjectsOfType<DiceSlot>();

        _turnManager = FindObjectOfType<TurnManager>();

        Debug.Log($"[DiceController] Start dices={dices?.Length}, keepSlots={keepSlots?.Length}, diceBox3D={diceBox3D != null}");

        if (!Has2D && !Has3D)
            Debug.LogError("[DiceController] No dice presentation connected.");

        if (Has3D)
        {
            diceBox3D.OnBoardDiceClicked += OnDice3DClicked;
            diceBox3D.OnDiceLineupComplete += () =>
            {
                IsRolling = false;
                if (Has2D)
                {
                    foreach (var d in dices) d.SetInteractable(true);
                }
                OnRollComplete?.Invoke();
            };
        }

        if (Has2D)
        {
            for (int i = 0; i < dices.Length; i++)
            {
                var origin = diceOrigins != null && i < diceOrigins.Length ? diceOrigins[i] : Vector2.zero;
                dices[i].Init(i == 4, origin);
                int captured = i;
                dices[i].OnDiceClicked += _ => HandleDiceClick(captured);
                dices[i].SetInteractable(false);
            }
        }
    }

    public void Roll()
    {
        if (!CanRoll)
        {
            Debug.LogWarning($"[DiceController] Roll ignored. RollCount={RollCount}/{MaxRollCount}, IsRolling={IsRolling}");
            return;
        }

        StartCoroutine(RollCoroutine());
    }

    IEnumerator RollCoroutine()
    {
        IsRolling = true;
        RollCount++;
        OnRollStart?.Invoke();

        AudioManager.Play("dice_roll");

        if (diceBox3D == null) diceBox3D = FindObjectOfType<DiceBox3D>();
        if (_turnManager == null) _turnManager = FindObjectOfType<TurnManager>();
        if (roller == null && diceArea != null) roller = diceArea.GetComponent<DicePhysicsRoller>();
        if (roller == null) roller = FindObjectOfType<DicePhysicsRoller>();

        float rollDuration = 0f;

        if (Has3D)
        {
            var keepFlags = new bool[Dice.Length];
            for (int i = 0; i < Dice.Length; i++) keepFlags[i] = Dice[i].IsKept;

            bool useAnimated = true;
            if (useAnimated)
            {
                var finalValues = new int[Dice.Length];
                for (int i = 0; i < Dice.Length; i++)
                    finalValues[i] = Dice[i].IsKept ? Dice[i].Value : Random.Range(1, 7);

                float gaugeValue = FindObjectOfType<UICupShaker>()?.LastGaugeValue ?? 1f;
                int animSeed = Random.Range(0, int.MaxValue);
                int turnSerial = _turnManager != null ? _turnManager.CurrentTurnSerial : 0;

                if (GameManager.Instance != null && GameManager.Instance.IsOnline)
                {
                    FindObjectOfType<NetworkDiceRPC>()
                        ?.SendDiceRoll(finalValues, keepFlags, gaugeValue, animSeed, turnSerial, RollCount);
                }

                diceBox3D.ThrowDiceAnimated(finalValues, keepFlags, gaugeValue, animSeed);
            }
            else
            {
                diceBox3D.ThrowDiceOntoBoard(keepFlags);
            }

            yield break;
        }
        else if (roller != null)
        {
            rollDuration = 1.8f;
            var toRoll = new List<(Dice dice, int val)>();
            foreach (var d in dices)
            {
                if (!d.IsKept) toRoll.Add((d, Random.Range(1, 7)));
            }

            var rollDiceArr = toRoll.ConvertAll(x => x.dice).ToArray();
            StartCoroutine(roller.RollAll(rollDiceArr, rollDuration));
            foreach (var (dice, val) in toRoll)
                StartCoroutine(dice.RollAnimationFaceOnly(val, rollDuration));
        }
        else if (Has2D)
        {
            rollDuration = 0.65f;
            foreach (var d in dices)
            {
                if (!d.IsKept)
                    StartCoroutine(d.RollAnimation(Random.Range(1, 7)));
            }
        }

        yield return new WaitForSeconds(rollDuration);

        IsRolling = false;

        if (Has2D)
        {
            foreach (var d in dices) d.SetInteractable(true);
        }

        Debug.Log($"[DiceController] RollComplete values=[{string.Join(", ", CurrentValues)}]");
        OnRollComplete?.Invoke();
    }

    void HandleDiceClick(int index)
    {
        if (IsRolling || RollCount == 0) return;
        if (!CanInteractWithDiceLocally()) return;

        if (Has3D)
        {
            if (index >= Dice.Length) return;
            diceBox3D.SetDiceKept(index, !Dice[index].IsKept);
        }
        else if (Has2D)
        {
            if (index >= dices.Length) return;
            var dice = dices[index];

            if (!dice.IsKept)
            {
                for (int s = 0; s < keepSlots.Length; s++)
                {
                    if (!keepSlots[s].IsOccupied)
                    {
                        dice.ToggleKeep();
                        keepSlots[s].PlaceDice(dice);
                        OnDiceKeepToggled?.Invoke(dice);
                        return;
                    }
                }
            }
            else
            {
                for (int s = 0; s < keepSlots.Length; s++)
                {
                    if (keepSlots[s].OccupiedDice == dice)
                    {
                        dice.ToggleKeep();
                        keepSlots[s].RemoveDice();
                        OnDiceKeepToggled?.Invoke(dice);
                        return;
                    }
                }
            }
        }
    }

    bool CanInteractWithDiceLocally()
    {
        if (_turnManager == null) _turnManager = FindObjectOfType<TurnManager>();
        if (_turnManager == null) return true;
        return _turnManager.CanLocalManipulateDice();
    }

    public void TEST_ForceRollCount() => RollCount = Mathf.Max(RollCount, 1);

    public void ReceiveRemoteRoll(int rollIndex)
    {
        RollCount = Mathf.Clamp(rollIndex, 0, MaxRollCount);
        IsRolling = true;
    }

    public void OnDice3DClicked(int index)
    {
        Debug.Log($"[DiceController] OnDice3DClicked({index}) IsRolling={IsRolling}, RollCount={RollCount}");
        if (IsRolling || RollCount == 0) return;
        if (!CanInteractWithDiceLocally()) return;
        HandleDiceClick(index);
    }

    public void ResetForNewTurn()
    {
        RollCount = 0;
        IsRolling = false;

        foreach (var d in Dice)
        {
            d.Value = 1;
            d.State = DiceState.InCup;
            d.IsKeptForced = false;
        }

        if (Has2D)
        {
            foreach (var d in dices)
            {
                d.ForceUnkeep();
                d.SetInteractable(false);
            }
        }

        if (keepSlots != null)
        {
            foreach (var s in keepSlots) s.Clear();
        }

        diceBox3D?.ClearDiceOverlays();
        diceBox3D?.ResetAllDice();
    }

    public void SetDiceInteractable(bool on)
    {
        if (Has2D)
        {
            foreach (var d in dices) d.SetInteractable(on);
        }
    }

    public bool[] GetKeptFlags()
    {
        if (Has3D)
        {
            var flags = new bool[Dice.Length];
            for (int i = 0; i < Dice.Length; i++) flags[i] = Dice[i].IsKept;
            return flags;
        }

        if (Has2D)
        {
            var flags = new bool[dices.Length];
            for (int i = 0; i < dices.Length; i++) flags[i] = dices[i].IsKept;
            return flags;
        }

        return new bool[5];
    }

    public void AIKeepDice(int index, bool keep)
    {
        if (Has3D)
        {
            if (index < 0 || index >= Dice.Length) return;
            if (Dice[index].IsKept == keep) return;
        }
        else if (Has2D)
        {
            if (index < 0 || index >= dices.Length) return;
            if (dices[index].IsKept == keep) return;
        }

        HandleDiceClick(index);
    }
}
