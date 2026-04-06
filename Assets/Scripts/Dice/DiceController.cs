using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DiceController : MonoBehaviour
{
    // ─── Inspector 연결 ──────────────────────────────────────────────
    [SerializeField] private Dice[]             dices;        // 5개 드래그 (2D 모드)
    [SerializeField] private DiceSlot[]         keepSlots;    // 4개 드래그
    [SerializeField] private Transform          diceArea;     // 굴린 주사위 배치 부모
    [SerializeField] private Vector2[]          diceOrigins;  // 5개 원래 위치
    [SerializeField] private DicePhysicsRoller  roller;       // 2D 물리 롤러 (DiceArea에 붙임)
    [SerializeField] private DiceBox3D          diceBox3D;    // 3D 주사위 방 (있으면 3D 우선)

    // ─── 공유 주사위 상태 (Single Source of Truth) ───────────────────
    public DiceInfo[] Dice { get; private set; } = new DiceInfo[5];

    // ─── 편의 프로퍼티 ───────────────────────────────────────────────
    private bool Has2D => dices != null && dices.Length > 0;
    private bool Has3D => diceBox3D != null;

    // ─── 상태 ────────────────────────────────────────────────────────
    public int  RollCount  { get; private set; }
    public bool IsRolling  { get; private set; }
    public bool CanRoll    => RollCount < MaxRollCount && !IsRolling;

    private int MaxRollCount => GameManager.Instance?.Settings?.maxRollCount ?? 3;

    // 현재 주사위 값 (5개)
    public int[] CurrentValues
    {
        get
        {
            if (Has2D && !Has3D)
            {
                var v = new int[dices.Length];
                for (int i = 0; i < dices.Length; i++) v[i] = dices[i].Value;
                return v;
            }
            var v3 = new int[Dice.Length];
            for (int i = 0; i < Dice.Length; i++) v3[i] = Dice[i].Value;
            return v3;
        }
    }

    // ─── 이벤트 ──────────────────────────────────────────────────────
    public System.Action         OnRollStart;      // Roll() 시작 시 (굴리는중 진입용)
    public System.Action         OnRollComplete;
    public System.Action<Dice>   OnDiceKeepToggled;

    // ─── 초기화 ──────────────────────────────────────────────────────
    void Awake()
    {
        for (int i = 0; i < Dice.Length; i++) Dice[i] = new DiceInfo();
    }

    void Start()
    {
        if (dices == null || dices.Length == 0)
        {
            dices = FindObjectsOfType<Dice>();
            if (dices.Length > 0)
                Debug.Log($"[DiceController] Dice {dices.Length}개를 씬에서 자동으로 찾았습니다.");
        }
        if (keepSlots == null || keepSlots.Length == 0)
        {
            keepSlots = FindObjectsOfType<DiceSlot>();
            if (keepSlots.Length > 0)
                Debug.Log($"[DiceController] DiceSlot {keepSlots.Length}개를 씬에서 자동으로 찾았습니다.");
        }

        Debug.Log($"[DiceController] Start — dices={dices?.Length}, keepSlots={keepSlots?.Length}, diceBox3D={diceBox3D != null}");

        if (!Has2D && !Has3D)
            Debug.LogError("[DiceController] Dice를 찾을 수 없습니다! 2D Dice 또는 DiceBox3D 중 하나라도 연결하세요.");

        if (Has3D)
        {
            diceBox3D.OnBoardDiceClicked  += OnDice3DClicked;
            diceBox3D.OnDiceLineupComplete += () =>
            {
                IsRolling = false;
                if (Has2D) foreach (var d in dices) d.SetInteractable(true);
                OnRollComplete?.Invoke();
            };
        }

        if (Has2D)
        {
            for (int i = 0; i < dices.Length; i++)
            {
                var origin = diceOrigins != null && i < diceOrigins.Length
                             ? diceOrigins[i] : Vector2.zero;
                dices[i].Init(i == 4, origin);
                int captured = i;
                dices[i].OnDiceClicked += _ => HandleDiceClick(captured);
                dices[i].SetInteractable(false);
            }
        }
    }

    // ─── 굴리기 ──────────────────────────────────────────────────────
    public void Roll()
    {
        if (!CanRoll)
        {
            Debug.LogWarning($"[DiceController] Roll 무시됨 — CanRoll=false (RollCount={RollCount}/{MaxRollCount}, IsRolling={IsRolling})");
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
        if (roller == null && diceArea != null) roller = diceArea.GetComponent<DicePhysicsRoller>();
        if (roller == null) roller = FindObjectOfType<DicePhysicsRoller>();

        float rollDuration = 0f;

        if (Has3D)
        {
            // ── 3D 주사위 방 사용 ─────────────────────────────────────
            // ThrowDiceOntoBoard → LineupDice → OnDiceLineupComplete → IsRolling=false + OnRollComplete
            var keepFlags = new bool[Dice.Length];
            for (int i = 0; i < Dice.Length; i++) keepFlags[i] = Dice[i].IsKept;

            // ── 애니메이션 버전: ThrowDiceAnimated / 물리 버전: ThrowDiceOntoBoard ──
            bool useAnimated = true;  // false로 바꾸면 물리 버전
            if (useAnimated)
            {
                var finalValues = new int[Dice.Length];
                for (int i = 0; i < Dice.Length; i++)
                    finalValues[i] = Dice[i].IsKept ? Dice[i].Value : Random.Range(1, 7);
                float gaugeValue = FindObjectOfType<UICupShaker>()?.LastGaugeValue ?? 1f;
                int   animSeed   = Random.Range(0, int.MaxValue);

                // 온라인 모드: 값을 상대방에게 먼저 전송 후 애니메이션
                if (GameManager.Instance != null && GameManager.Instance.IsOnline)
                    UnityEngine.Object.FindObjectOfType<NetworkDiceRPC>()
                        ?.SendDiceRoll(finalValues, keepFlags, gaugeValue, animSeed);

                diceBox3D.ThrowDiceAnimated(finalValues, keepFlags, gaugeValue, animSeed);
            }
            else
            {
                diceBox3D.ThrowDiceOntoBoard(keepFlags);
            }
            yield break;  // 이후 흐름은 OnDiceLineupComplete 이벤트에서 처리
        }
        else if (roller != null)
        {
            rollDuration = 1.8f;
            var toRoll = new List<(Dice dice, int val)>();
            foreach (var d in dices)
                if (!d.IsKept) toRoll.Add((d, Random.Range(1, 7)));

            var rollDiceArr = toRoll.ConvertAll(x => x.dice).ToArray();
            StartCoroutine(roller.RollAll(rollDiceArr, rollDuration));
            foreach (var (dice, val) in toRoll)
                StartCoroutine(dice.RollAnimationFaceOnly(val, rollDuration));
        }
        else if (Has2D)
        {
            rollDuration = 0.65f;
            foreach (var d in dices)
                if (!d.IsKept)
                    StartCoroutine(d.RollAnimation(Random.Range(1, 7)));
        }

        yield return new WaitForSeconds(rollDuration);

        IsRolling = false;

        if (Has2D)
            foreach (var d in dices) d.SetInteractable(true);

        Debug.Log($"[DiceController] 굴림 완료 — 주사위값: [{string.Join(", ", CurrentValues)}]");
        OnRollComplete?.Invoke();
    }

    // ─── 주사위 Keep 클릭 처리 ──────────────────────────────────────
    void HandleDiceClick(int index)
    {
        if (IsRolling || RollCount == 0) return;

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

    // ─── 3D 주사위 클릭 (DiceBox3D에서 직접 호출) ───────────────────
    // [TEST] RollCount 강제 1 설정 — TEST_BlinkCupDice에서 호출, 지울 때 같이 제거
    public void TEST_ForceRollCount() => RollCount = Mathf.Max(RollCount, 1);

    // 원격 굴리기 수신 시 RollCount 증가 (OnRollComplete 이벤트는 OnDiceLineupComplete에서 발동)
    public void ReceiveRemoteRoll()
    {
        RollCount = Mathf.Min(RollCount + 1, MaxRollCount);
        IsRolling = true;  // LineupComplete 시 false로 전환됨
    }

    public void OnDice3DClicked(int index)
    {
        Debug.Log($"[DiceController] OnDice3DClicked({index}) — IsRolling={IsRolling}, RollCount={RollCount}");
        if (IsRolling || RollCount == 0) return;
        HandleDiceClick(index);
    }

    // ─── 턴 시작 시 리셋 ─────────────────────────────────────────────
    public void ResetForNewTurn()
    {
        RollCount = 0;

        foreach (var d in Dice) { d.Value = 1; d.State = DiceState.InCup; }

        if (Has2D)
        {
            foreach (var d in dices)
            {
                d.ForceUnkeep();
                d.SetInteractable(false);
            }
        }

        if (keepSlots != null)
            foreach (var s in keepSlots) s.Clear();

        diceBox3D?.ClearDiceOverlays();
        diceBox3D?.ResetAllDice();
    }

    // ─── 클릭 활성/비활성 전체 제어 ─────────────────────────────────
    public void SetDiceInteractable(bool on)
    {
        if (Has2D)
            foreach (var d in dices) d.SetInteractable(on);
    }

    // ─── 현재 Keep 여부 배열 ─────────────────────────────────────────
    public bool[] GetKeptFlags()
    {
        if (Has3D)
        {
            var f = new bool[Dice.Length];
            for (int i = 0; i < Dice.Length; i++) f[i] = Dice[i].IsKept;
            return f;
        }
        if (Has2D)
        {
            var flags = new bool[dices.Length];
            for (int i = 0; i < dices.Length; i++) flags[i] = dices[i].IsKept;
            return flags;
        }
        return new bool[5];
    }

    // ─── AI용: 강제로 특정 주사위 Keep ──────────────────────────────
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
