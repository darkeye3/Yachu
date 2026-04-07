using System.Collections;
using UnityEngine;

/// <summary>
/// 주사위 쉐이커(컵) + 주사위 물리 관리.
/// 방/보드 생성은 DiceBoardCreator가 담당.
///
/// [역할]
///   _cupDice[5]  : 컵 안 시각용 주사위 — Rigidbody 없음, 컵 자식 오브젝트.
///   _dice[5]     : 보드 위 물리 주사위 — Rigidbody 있음.
///
/// [흐름]
///   ReturnToCup       → 비보관 보드 주사위 숨김, 비보관 컵 주사위 표시
///   ShakeCupCoroutine → 컵 흔들기
///   TipCupCoroutine   → 컵 기울여 보드 주사위 발사
///   LineupDice        → 보드 주사위 가로 일자 정렬
/// </summary>
public class DiceBox3D : MonoBehaviour
{
    [Header("주사위 프리팹")]
    [SerializeField] private GameObject normalDicePrefab;
    [SerializeField] private GameObject wildDicePrefab;

    [Header("컵 에셋")]
    [SerializeField] private GameObject diceShakerPrefab;
    private Vector3 shakerScale = new Vector3(4.5f, 3f, 4.5f);

    [Header("컵 설정")]
    private float cupTipAngle    = 120f;
    private float cupTipDuration = 0.35f;
    private float cupTipRelease  = 90f;

    // ── 런타임 ───────────────────────────────────────────────────────
    private Die3D[]    _dice    = new Die3D[5];   // 보드 위 물리 주사위
    private Die3D[]    _cupDice = new Die3D[5];   // 컵 안 시각용 주사위 (Rigidbody 없음)
    private Vector3[]  _cupDiceBaseLocalPos = new Vector3[5]; // 쉐이크 지터 기저 위치
    private float[]    _dieEdgeLengths = new float[5];
    private Transform  _cupRoot;
    private Vector3    _cupBasePosition;
    private Collider[] _cupColliders;
    private Coroutine  _diceJitterRoutine;
    private Transform  _boardDiceParent;   // DiceBoardCreator.BoardDiceParent

    [Header("UI 연동")]
    [SerializeField] private float uiToWorldScale = 0.01f;  // UI 픽셀 → 3D 월드 단위 변환 배율

    [Header("주사위 오버레이 UI")]
    [SerializeField] private Sprite[]       diceFaceSprites;    // 인덱스 0=1눈 ~ 5=6눈
    [SerializeField] private RectTransform  overlayParent;      // RawImage 위 오버레이 부모
    [SerializeField] private float          overlayDiceSize = 60f; // 오버레이 이미지 크기(px)

    private readonly System.Collections.Generic.Dictionary<int, UnityEngine.UI.Image> _overlays
        = new System.Collections.Generic.Dictionary<int, UnityEngine.UI.Image>();

    // ── 컵 이벤트 (DiceCup UI가 구독) ──────────────────────────────
    public System.Action OnCupReturnStart;   // 컵 주사위 표시 직전
    public System.Action OnCupTipStart;      // 컵이 기울어져 주사위를 쏟을 때
    public System.Action OnCupTipEnd;        // 컵이 원위치로 돌아왔을 때

    public static readonly Vector3 RoomCenter = new Vector3(4f, 0f, 0f);
    private static readonly Vector3 CupOffset = new Vector3(7f, 4f, 0f);

    // 컵 안 주사위 월드 위치 오프셋 (RoomCenter 기준)
    private readonly Vector3[] _cupDiceWorldOffset =
    {
        new Vector3( 8.00f, 1f,  0.80f),
        new Vector3( 5.65f, 1f,  0.00f),
        new Vector3( 6.83f, 1f, -1.18f),
        new Vector3( 8.33f, 1f, -0.45f),
        new Vector3( 6.70f, 1f,  1.50f),
    };

    // 정렬 후 가로 일자 위치 (방 중심 기준) — Start()에서 보드 크기 기준으로 계산
    private Vector3[] _lineupPos = new Vector3[5];

    // ── 초기화 ──────────────────────────────────────────────────────
    // Awake 대신 Start를 사용해 Bootstrap이 Awake 중에 SerializeField를 reflection으로
    // 설정할 시간을 확보한다 (AddComponent는 즉시 Awake를 실행하므로).
    void Start()
    {
        if (normalDicePrefab == null)
        {
            Debug.LogError("[DiceBox3D] normalDicePrefab 미연결!");
            return;
        }
        // 보드 주사위 부모 탐색 (DiceBoardCreator가 먼저 Start 실행되어야 함)
        var boardCreator = FindObjectOfType<DiceBoardCreator>();
        _boardDiceParent = boardCreator != null ? boardCreator.BoardDiceParent : transform;

        float visW = boardCreator != null ? boardCreator.VisibleWidth  : 10f;
        float visH = boardCreator != null ? boardCreator.VisibleHeight : 8f;

        EnsureReferences();


        // 정렬 위치: 보드 가시 너비의 80% 안에 5개 균등 배치
        float usableW = visW * 0.8f;
        float spacing = usableW / 4f;
        for (int i = 0; i < 5; i++)
            _lineupPos[i] = new Vector3((i - 2) * spacing, 0.3f, 0f);


        CreateCup();
        SpawnDice();      // 보드 주사위 (물리 있음, 초기 숨김)
        SpawnCupDice();   // 컵 주사위  (물리 없음, 초기 표시)
    }

    // ── 컵 생성 ──────────────────────────────────────────────────────
    void CreateCup()
    {
        if (diceShakerPrefab == null)
        {
            Debug.LogError("[DiceBox3D] diceShakerPrefab 미연결!");
            return;
        }

        var shaker = Instantiate(diceShakerPrefab, transform);
        shaker.name = "DiceShaker";
        shaker.transform.position      = RoomCenter + CupOffset;
        shaker.transform.localRotation = Quaternion.Euler(0f, 0f, 180f);
        shaker.transform.localScale    = shakerScale;
        _cupRoot         = shaker.transform;
        _cupBasePosition = shaker.transform.position;

        var mc = shaker.GetComponent<MeshCollider>();
        if (mc != null) { mc.convex = false; mc.enabled = true; }

        var innerFloor = new GameObject("CupInnerFloor");
        innerFloor.transform.SetParent(_cupRoot);
        innerFloor.transform.localPosition = new Vector3(0f, 0.3f, 0f);
        innerFloor.transform.localRotation = Quaternion.identity;
        innerFloor.transform.localScale    = Vector3.one;
        var floorCol = innerFloor.AddComponent<BoxCollider>();
        floorCol.size   = new Vector3(0.6f, 0.05f, 0.6f);
        floorCol.center = Vector3.zero;

        _cupColliders = shaker.GetComponentsInChildren<Collider>();
    }


    // ── 보드 주사위 생성 (Rigidbody 있음, 초기 숨김) ─────────────────
    void SpawnDice()
    {
        var slippyMat = new PhysicMaterial("DiceSlippy")
        {
            dynamicFriction = 0.02f,
            staticFriction  = 0.02f,
            bounciness      = 0.5f,
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounceCombine   = PhysicMaterialCombine.Maximum,
        };

        for (int i = 0; i < 5; i++)
        {
            bool isWild = (i == 4);
            var  prefab = isWild && wildDicePrefab != null ? wildDicePrefab : normalDicePrefab;

            var go = Instantiate(prefab, RoomCenter + _cupDiceWorldOffset[i],
                                 Quaternion.identity, _boardDiceParent);
            go.name = $"Die3D_{i}";
            go.transform.localScale *= 0.75f;

            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) { rb = go.AddComponent<Rigidbody>(); rb.mass = 1f; }
            rb.drag                   = 0.005f;
            rb.angularDrag            = 0.01f;
            rb.isKinematic            = true;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var die = go.GetComponent<Die3D>() ?? go.AddComponent<Die3D>();
            die.IsWild = isWild;
            _dice[i]   = die;

            var col = die.GetComponentInChildren<Collider>() ?? die.GetComponent<Collider>();
            if (col != null) col.material = slippyMat;

            go.SetActive(false); // ReleaseDiceStaggered에서 활성화
        }

    }

    // ── 컵 주사위 생성 (Rigidbody 없음, 컵 자식, 초기 표시) ──────────
    void SpawnCupDice()
    {
        if (_cupRoot == null) return;

        // DiceShaker 스케일이 비균등(4.5, 3, 4.5)이라 주사위를 바로 자식으로 붙이면
        // localScale이 비균등(0.167, 0.25, 0.167)해져 회전 시 전단 변형이 발생.
        // 역스케일 컨테이너를 중간에 두면 컨테이너의 lossy scale이 (1,1,1)이 되어
        // 그 아래 주사위는 균등한 월드 스케일을 유지.
        var container = new GameObject("CupDiceContainer");
        container.transform.SetParent(_cupRoot, false);
        container.transform.localPosition = Vector3.zero;
        container.transform.localRotation = Quaternion.identity;
        var ps = _cupRoot.localScale;
        container.transform.localScale = new Vector3(1f / ps.x, 1f / ps.y, 1f / ps.z);

        for (int i = 0; i < 5; i++)
        {
            bool isWild = (i == 4);
            var  prefab = isWild && wildDicePrefab != null ? wildDicePrefab : normalDicePrefab;
            if (prefab == null) continue;

            // 월드 위치를 먼저 설정한 뒤 컨테이너 자식으로 편입
            // container의 lossy scale = (1,1,1)이므로 localScale 보정 없이 균등 유지
            var go = Instantiate(prefab, RoomCenter + _cupDiceWorldOffset[i], Quaternion.identity);
            go.name = $"CupDie_{i}";
            go.transform.localScale *= 0.75f;
            go.transform.SetParent(container.transform, true);

            // Rigidbody 물리 비활성화 (Die3D가 RequireComponent라 삭제 불가)
            var rb = go.GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.useGravity = false; }

            // 컵 주사위는 물리 충돌 없음 — 즉시 비활성화 (Destroy는 프레임 지연)
            foreach (var col in go.GetComponentsInChildren<Collider>())
                col.enabled = false;

            var die = go.GetComponent<Die3D>() ?? go.AddComponent<Die3D>();
            die.IsWild = isWild;
            _cupDice[i] = die;

            die.SnapToFace(Random.Range(1, 7));
            _cupDiceBaseLocalPos[i] = go.transform.localPosition;
            go.SetActive(true);
        }
    }

    // ── 공개 API ─────────────────────────────────────────────────────
    public void StartRollDiceCoroutine(bool[] keepFlags, System.Action<int[]> onComplete)
        => StartCoroutine(RollDice(keepFlags, onComplete));

    IEnumerator RollDice(bool[] keepFlags, System.Action<int[]> onComplete, float maxWait = 7f)
    {
        int diceCount = Mathf.Min(_dice.Length, keepFlags.Length);

        yield return StartCoroutine(ReturnToCup(keepFlags, diceCount));
        // ShakeCupCoroutine 스킵 — UI(DiceCup)가 주도하는 쉐이크를 따라가므로 불필요
        yield return StartCoroutine(TipCupCoroutine(keepFlags, diceCount));

        // 0.5초 후 주사위 비활성화 + 값 읽기
        yield return new WaitForSeconds(0.5f);

        var values = new int[diceCount];
        for (int i = 0; i < diceCount; i++)
        {
            if (keepFlags[i]) { values[i] = _dice[i].Value; continue; }
            var rb = _dice[i].GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.velocity = rb.angularVelocity = Vector3.zero; }
            values[i]      = _dice[i].GetTopFace();
            _dice[i].Value = values[i];
            _dice[i].SnapToFace(values[i]);
            _dice[i].gameObject.SetActive(false);
        }

        onComplete?.Invoke(values);
    }

    // ── 컵으로 복귀 ──────────────────────────────────────────────────
    // 비보관 보드 주사위 숨기고, 비보관 컵 주사위 표시
    IEnumerator ReturnToCup(bool[] keepFlags, int diceCount)
    {
        OnCupReturnStart?.Invoke();
        for (int i = 0; i < diceCount; i++)
        {
            if (keepFlags[i])
            {
                // 보관 주사위: 보드에 그대로, 컵 주사위 숨김
                if (_cupDice[i] != null) _cupDice[i].gameObject.SetActive(false);
            }
            else
            {
                // 비보관 주사위: 보드 주사위 숨기고 컵 주사위 표시
                if (_dice[i] != null)
                {
                    var rb = _dice[i].GetComponent<Rigidbody>();
                    if (rb != null) { rb.isKinematic = true; rb.velocity = rb.angularVelocity = Vector3.zero; }
                    _dice[i].gameObject.SetActive(false);
                }
                if (_cupDice[i] != null)
                {
                    _cupDice[i].gameObject.SetActive(true);
                    _cupDice[i].transform.localPosition = _cupDiceBaseLocalPos[i];
                    _cupDice[i].SnapToFace(Random.Range(1, 7));
                }
            }
        }
        yield return null;
    }

    // ── 쉐이크 ───────────────────────────────────────────────────────
    // 컵 주사위는 컵의 자식이므로 Rigidbody 없이 transform만으로 따라감
    // → 물리/transform 충돌 완전 해소
    IEnumerator ShakeCupCoroutine(bool[] keepFlags, int diceCount)
    {
        var faceCoroutine = StartCoroutine(RandomizeCupFaces(keepFlags, diceCount));

        int   shakeCount    = 3;
        float shakeAngle    = 25f;
        float shakeDuration = 0.12f;

        for (int s = 0; s < shakeCount; s++)
        {
            float target     = (s % 2 == 0) ? shakeAngle : -shakeAngle;
            float elapsed    = 0f;
            float startAngle = _cupRoot.localEulerAngles.z;
            if (startAngle > 180f) startAngle -= 360f;

            while (elapsed < shakeDuration)
            {
                elapsed += Time.deltaTime;
                _cupRoot.localRotation = Quaternion.Euler(0f, 0f,
                    Mathf.Lerp(startAngle, target, elapsed / shakeDuration));
                yield return null;
            }
        }

        // 원위치
        float ret = 0f, retDur = 0.1f;
        float curAngle = _cupRoot.localEulerAngles.z;
        if (curAngle > 180f) curAngle -= 360f;
        while (ret < retDur)
        {
            ret += Time.deltaTime;
            _cupRoot.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Lerp(curAngle, 0f, ret / retDur));
            yield return null;
        }
        _cupRoot.localRotation = Quaternion.identity;

        StopCoroutine(faceCoroutine);
    }

    // ── 쉐이크 중 컵 주사위 뒹굴림 (매 프레임 회전+위치 랜덤) ──────
    IEnumerator RandomizeCupFaces(bool[] keepFlags, int diceCount)
    {
        while (true)
        {
            for (int i = 0; i < diceCount; i++)
            {
                if (keepFlags[i]) continue;
                if (_cupDice[i] != null && _cupDice[i].gameObject.activeSelf)
                {
                    _cupDice[i].transform.localRotation = Random.rotation;
                    _cupDice[i].transform.localPosition =
                        _cupDiceBaseLocalPos[i] + Random.insideUnitSphere * 0.5f;
                }
            }
            yield return null;
        }
    }

    // ── 컵 기울이기 ──────────────────────────────────────────────────
    // 기울어지면 컵 주사위 숨기고, 컵 입구에서 보드 주사위 발사
    IEnumerator TipCupCoroutine(bool[] keepFlags, int diceCount)
    {
        float angle   = 0f, elapsed = 0f;
        bool  released = false;

        while (elapsed < cupTipDuration)
        {
            elapsed += Time.deltaTime;
            angle = Mathf.Lerp(0f, cupTipAngle,
                EaseInOut(Mathf.Clamp01(elapsed / cupTipDuration)));
            _cupRoot.localRotation = Quaternion.Euler(0f, 0f, angle);

            if (!released && angle >= cupTipRelease)
            {
                released = true;
                OnCupTipStart?.Invoke();

                // 컵 주사위 숨기기
                for (int i = 0; i < diceCount; i++)
                    if (_cupDice[i] != null) _cupDice[i].gameObject.SetActive(false);

                // 보드 주사위를 컵 입구에서 발사
                StartCoroutine(ReleaseDiceStaggered(keepFlags, diceCount));
            }
            yield return null;
        }

        yield return new WaitForSeconds(5f);

        // 컵 복귀
        elapsed = 0f;
        float returnDur = 0.4f;
        while (elapsed < returnDur)
        {
            elapsed += Time.deltaTime;
            _cupRoot.localRotation = Quaternion.Euler(0f, 0f,
                Mathf.Lerp(cupTipAngle, 0f, Mathf.Clamp01(elapsed / returnDur)));
            yield return null;
        }
        _cupRoot.localRotation = Quaternion.identity;
        OnCupTipEnd?.Invoke();
    }

    // ── 보드 주사위 발사 (컵 입구 위치에서 시작) ─────────────────────
    IEnumerator ReleaseDiceStaggered(bool[] keepFlags, int diceCount)
    {
        float[] zSpreads = { -1.2f, -0.6f, 0f, 0.6f, 1.2f };

        for (int i = 0; i < diceCount; i++)
        {
            if (keepFlags[i]) continue;

            // 컵의 local up 방향(기울어진 상태) + z 방향으로 스태거
            Vector3 mouthPos = _cupRoot.position
                               + _cupRoot.up   * 0.5f
                               + _cupRoot.right * (zSpreads[i] * 0.4f);

            _dice[i].gameObject.SetActive(true);
            _dice[i].transform.position = mouthPos;
            _dice[i].transform.rotation = Random.rotation;

            var rb = _dice[i].GetComponent<Rigidbody>();
            if (rb == null) { yield return null; continue; }

            rb.isKinematic = false;
            rb.velocity    = Vector3.zero;

            Vector3 baseDir   = (_cupRoot.up + Vector3.down * 0.15f).normalized;
            Vector3 spread    = new Vector3(0f,
                                            Random.Range(-0.2f, 0.2f),
                                            zSpreads[i] + Random.Range(-0.2f, 0.2f));
            Vector3 launchDir = (baseDir + spread).normalized;
            float   speed     = Random.Range(6f, 10f);

            rb.velocity = launchDir * speed;
            rb.AddTorque(Random.insideUnitSphere * Random.Range(5f, 10f), ForceMode.Impulse);

            yield return new WaitForSeconds(0.04f);
        }
    }

    // ── 가로 일자 정렬 ────────────────────────────────────────────────
    IEnumerator LineupDice(bool[] keepFlags)
    {
        return LineupDice(keepFlags, null);
    }

    IEnumerator LineupDice(bool[] keepFlags, int[] authoritativeValues)
    {
        // 활성화된 비보관 주사위 수집 + 눈 값 기준 오름차순 정렬
        var collected = new System.Collections.Generic.List<(int idx, Die3D die, int face)>();
        for (int i = 0; i < _dice.Length; i++)
        {
            if (i < keepFlags.Length && keepFlags[i]) continue;
            if (_dice[i] == null || !_dice[i].gameObject.activeSelf) continue;

            var rb = _dice[i].GetComponent<Rigidbody>();
            if (rb != null) { rb.isKinematic = true; rb.velocity = rb.angularVelocity = Vector3.zero; }

            bool hasAuthoritativeFace = authoritativeValues != null && i < authoritativeValues.Length;
            int face = hasAuthoritativeFace ? authoritativeValues[i] : _dice[i].GetTopFace();
            _dice[i].Value = face;
            if (_diceController != null) _diceController.Dice[i].Value = face;  // 공유 상태 저장

            collected.Add((i, _dice[i], face));
        }
        collected.Sort((a, b) => a.face.CompareTo(b.face));  // 낮은 눈 → 왼쪽

        var toLineup   = new System.Collections.Generic.List<Die3D>();
        var startPos   = new System.Collections.Generic.List<Vector3>();
        var startRot   = new System.Collections.Generic.List<Quaternion>();
        var targetRot  = new System.Collections.Generic.List<Quaternion>();

        // N개 기준으로 가운데 정렬 위치 계산
        int   n       = collected.Count;
        float spacing = n > 1 ? (_lineupPos[1].x - _lineupPos[0].x) : 0f;
        float startX  = -(n - 1) * spacing * 0.5f;
        var   targets = new Vector3[n];
        for (int i = 0; i < n; i++)
            targets[i] = new Vector3(startX + i * spacing, _lineupPos[0].y, _lineupPos[0].z);

        foreach (var (idx, die, face) in collected)
        {
            toLineup.Add(die);
            startPos.Add(die.transform.position);
            startRot.Add(die.transform.rotation);
            targetRot.Add(die.FaceUpRotation(face));
        }

        float duration = 1f, elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / duration));
            for (int i = 0; i < toLineup.Count; i++)
            {
                toLineup[i].transform.position = Vector3.Lerp(startPos[i], RoomCenter + targets[i], t);
                toLineup[i].transform.rotation = Quaternion.Slerp(startRot[i], targetRot[i], t);
            }
            yield return null;
        }

        for (int i = 0; i < toLineup.Count; i++)
        {
            toLineup[i].transform.position = RoomCenter + targets[i];
            toLineup[i].transform.rotation = targetRot[i];
        }

        // 정렬 완료 — 보드 주사위 숨김, 상태 OnBoard로 전환 (오버레이는 ShowDiceOverlays에서 생성)
        foreach (var (idx, die, face) in collected)
            SetState(idx, DiceState.OnBoard);
    }

    // ── 상태 적용 ─────────────────────────────────────────────────────
    // DiceInfo.State 에 따라 컵 주사위 / 보드 주사위 / 머티리얼 visibility 를 일괄 적용
    // 오버레이는 Show/Clear 메서드에서 별도 관리 (GameObject 생성/삭제가 필요하므로)
    void ApplyState(int i)
    {
        if (_diceController == null) return;
        var info  = _diceController.Dice[i];
        bool cupOn   = info.State == DiceState.InCup;
        bool boardOn = info.State == DiceState.Throwing;

        if (i < _cupDice.Length && _cupDice[i] != null)
            _cupDice[i].gameObject.SetActive(cupOn);

        if (_dice[i] != null)
        {
            if (!boardOn)
            {
                var rb = _dice[i].GetComponent<Rigidbody>();
                if (rb != null) { rb.isKinematic = true; rb.velocity = rb.angularVelocity = Vector3.zero; }
            }
            _dice[i].gameObject.SetActive(boardOn);
            _dice[i].SetKept(info.State == DiceState.Kept);
        }
    }

    void SetState(int i, DiceState state)
    {
        if (_diceController == null) return;
        _diceController.Dice[i].State = state;
        ApplyState(i);
    }

    bool CanLocalInteractWithOverlay()
    {
        EnsureReferences();
        return _turnManager == null || _turnManager.CanLocalManipulateDice();
    }

    void RestoreOverlayToBoardRow(RectTransform overlayRect)
    {
        if (overlayParent == null || overlayRect == null) return;

        overlayRect.SetParent(overlayParent, false);
        overlayRect.localScale = Vector3.one;
        overlayRect.localRotation = Quaternion.identity;
        overlayRect.anchorMin = new Vector2(0.5f, 0.5f);
        overlayRect.anchorMax = new Vector2(0.5f, 0.5f);
        overlayRect.pivot = new Vector2(0.5f, 0.5f);
        overlayRect.anchoredPosition = Vector2.zero;
        overlayRect.SetAsLastSibling();

        UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(overlayParent);
    }

    void HideCupDiceForOverlayStates()
    {
        if (_diceController == null) return;

        for (int i = 0; i < _cupDice.Length; i++)
        {
            if (_cupDice[i] == null || i >= _diceController.Dice.Length) continue;

            var state = _diceController.Dice[i].State;
            if (state == DiceState.OnBoard || state == DiceState.Kept)
                _cupDice[i].gameObject.SetActive(false);
        }
    }

    // ── 공개 API ─────────────────────────────────────────────────────
    // 킵 상태만 표시 — 3D 이동 없이 머티리얼만 변경, 컵 주사위 동기화
    public void SetDiceKept(int index, bool kept)
    {
        if (index < 0 || index >= _dice.Length) return;
        if (!CanLocalInteractWithOverlay()) return;
        SetState(index, kept ? DiceState.Kept : DiceState.OnBoard);
        HideCupDiceForOverlayStates();
    }

    public void ResetAllDice()
    {
        EnsureReferences();
        if (_cupRoot == null)
        {
            Debug.LogError("[DiceBox3D] _cupRoot null — diceShakerPrefab을 Inspector에 연결하세요.");
            return;
        }
        _cupRoot.localRotation = Quaternion.identity;

        for (int i = 0; i < _dice.Length; i++)
        {
            if (_dice[i] != null) _dice[i].ResetForNewTurn(RoomCenter + _cupDiceWorldOffset[i]);
            if (_diceController != null && _diceController.Dice[i].State != DiceState.Kept)
            {
                SetState(i, DiceState.InCup);
            }
        }
    }

    // ── 컵 주사위 지터 ───────────────────────────────────────────────
    public void StartDiceJitter()
    {
        if (_diceJitterRoutine != null) StopCoroutine(_diceJitterRoutine);
        _diceJitterRoutine = StartCoroutine(DiceJitterCoroutine());
    }

    public void StopDiceJitter()
    {
        if (_diceJitterRoutine != null)
        {
            StopCoroutine(_diceJitterRoutine);
            _diceJitterRoutine = null;
        }
        // 원래 위치로 복귀 (첫 굴림 후에는 윗면 정렬)
        bool snapFace = _diceController != null && _diceController.RollCount > 0;
        for (int i = 0; i < _cupDice.Length; i++)
        {
            if (_cupDice[i] == null || !_cupDice[i].gameObject.activeSelf) continue;
            _cupDice[i].transform.localPosition = _cupDiceBaseLocalPos[i];
            if (snapFace)
            {
                int face = i < _diceController.Dice.Length ? Mathf.Max(1, _diceController.Dice[i].Value) : 1;
                _cupDice[i].SnapToFace(face);
            }
        }
    }

    IEnumerator DiceJitterCoroutine()
    {
        float moveDuration = 0.2f;  // 이동 한 스텝 시간

        while (true)
        {
            // 매 스텝마다 활성 주사위 인덱스 수집 (켜지거나 꺼진 주사위 반영)
            var indices = new System.Collections.Generic.List<int>();
            for (int i = 0; i < _cupDice.Length; i++)
                if (_cupDice[i] != null && _cupDice[i].gameObject.activeSelf)
                    indices.Add(i);

            if (indices.Count == 0) { yield return null; continue; }

            // 포지션 셔플 (Fisher-Yates)
            for (int i = indices.Count - 1; i > 0; i--)
            {
                int j   = Random.Range(0, i + 1);
                int tmp = indices[i]; indices[i] = indices[j]; indices[j] = tmp;
            }

            // 각 주사위의 시작 위치·회전 저장 + 목표 설정
            var startPos  = new Vector3[indices.Count];
            var startRot  = new Quaternion[indices.Count];
            var targetRot = new Quaternion[indices.Count];
            for (int i = 0; i < indices.Count; i++)
            {
                var die      = _cupDice[indices[i]];
                startPos[i]  = die.transform.localPosition;
                startRot[i]  = die.transform.localRotation;
                targetRot[i] = Random.rotation;
            }

            // 이동 + 회전 보간
            float elapsed = 0f;
            while (elapsed < moveDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / moveDuration));
                for (int i = 0; i < indices.Count; i++)
                {
                    var die = _cupDice[indices[i]];
                    if (die == null || !die.gameObject.activeSelf) continue;
                    die.transform.localPosition = Vector3.Lerp(startPos[i], _cupDiceBaseLocalPos[i], t);
                    die.transform.localRotation = Quaternion.Slerp(startRot[i], targetRot[i], t);
                }
                yield return null;
            }
        }
    }

    // ── [TEST] 컵 주사위 깜빡임 ─────────────────────────────────────
    // 테스트용 — 지울 때 이 블록 + UICupShaker의 TEST_BlinkCupDice 호출 제거
    public void TEST_BlinkCupDice()
    {
        StartCoroutine(TEST_BlinkCoroutine());
    }

    IEnumerator TEST_BlinkCoroutine()
    {
        for (int i = 0; i < _cupDice.Length; i++)
            if (_cupDice[i] != null) _cupDice[i].gameObject.SetActive(false);

        yield return new WaitForSeconds(1f);

        for (int i = 0; i < _dice.Length; i++)
            if (_diceController != null && _diceController.Dice[i].State != DiceState.Kept)
                SetState(i, DiceState.InCup);

        // [TEST] RollCount 강제 증가 (DiceController.OnDice3DClicked 조건 통과용)
        var dc = FindObjectOfType<DiceController>();
        if (dc != null) dc.TEST_ForceRollCount();

        ThrowDiceOntoBoard(); // [TEST] 지울 때 이 줄 제거
    }
    // ── [TEST END] ───────────────────────────────────────────────────

    // 정렬 완료 시 호출 — DiceController가 구독해서 클릭 활성화 등 처리
    public System.Action OnDiceLineupComplete;

    private Camera          _boardCamera;
    private DiceController  _diceController;
    private TurnManager     _turnManager;
    private bool            _diceClickable = false;

    void EnsureReferences()
    {
        if (_diceController == null) _diceController = FindObjectOfType<DiceController>();
        if (_turnManager == null) _turnManager = FindObjectOfType<TurnManager>();
        if (_boardCamera == null)
        {
            var cameraObject = GameObject.Find("BoardCamera");
            if (cameraObject != null) _boardCamera = cameraObject.GetComponent<Camera>();
        }
    }

    void GetBoardHalfSize(out float halfW, out float halfH)
    {
        halfW = 3f;
        halfH = 2f;

        var boardCreator = FindObjectOfType<DiceBoardCreator>();
        if (boardCreator != null)
        {
            halfW = boardCreator.VisibleWidth * 0.5f;
            halfH = boardCreator.VisibleHeight * 0.5f;
        }
    }

    [Header("오버레이 좌표 기준 RawImage")]
    [SerializeField] private UnityEngine.UI.RawImage boardRawImage;

    public void EnableDiceClicking()  => _diceClickable = true;
    public void DisableDiceClicking() => _diceClickable = false;

    public System.Action<int> OnBoardDiceClicked;

    // ── 보드에 주사위 던지기 ──────────────────────────────────────────
    // 보드 위 랜덤 위치에서 생성 후 물리로 낙하
    public void ThrowDiceOntoBoard(bool[] keepFlags = null)
    {
        EnsureReferences();
        if (keepFlags == null)
        {
            keepFlags = _diceController != null ? _diceController.GetKeptFlags() : new bool[5];
        }
        StartCoroutine(ThrowDiceCo(keepFlags));
    }

    IEnumerator ThrowDiceCo(bool[] keepFlags)
    {
        EnsureReferences();
        // 킵되지 않은 오버레이 제거 (StartShake에서 못 지웠을 경우 대비)
        ClearNonKeptOverlays();

        // 물리 재질 + angularDrag 초기화 (이전 던지기에서 마찰력 부스트된 경우 대비)
        var slippyMat = new PhysicMaterial("DiceSlippy")
        {
            dynamicFriction = 0.02f,
            staticFriction  = 0.02f,
            bounciness      = 0.5f,
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounceCombine   = PhysicMaterialCombine.Maximum,
        };
        for (int i = 0; i < _dice.Length; i++)
        {
            if (_dice[i] == null) continue;
            var col = _dice[i].GetComponent<Collider>();
            if (col != null) col.material = slippyMat;
            var rb = _dice[i].GetComponent<Rigidbody>();
            if (rb != null) rb.angularDrag = 0.05f;
        }

        // 컵 주사위 전부 숨김 (상태는 아직 유지, Throwing으로 전환은 각 주사위 스폰 시)
        for (int i = 0; i < _cupDice.Length; i++)
            if (_cupDice[i] != null) _cupDice[i].gameObject.SetActive(false);


        // 보드 크기 가져오기
        GetBoardHalfSize(out float halfW, out float halfH);

        for (int i = 0; i < _dice.Length; i++)
        {
            if (i < keepFlags.Length && keepFlags[i]) continue;
            if (_dice[i] == null) continue;

            // 오른쪽(+X) 영역에서 스폰 — 3,6,9 열 근처
            Vector3 spawnPos = RoomCenter + new Vector3(
                halfW * 0.6f,                          // 오른쪽 60% 지점
                Random.Range(2f, 2.5f),                // y 높이
                Random.Range(-halfH * 0.7f, halfH * 0.7f)  // Z 랜덤 분산
            );

            SetState(i, DiceState.Throwing);
            _dice[i].transform.position = spawnPos;
            _dice[i].transform.rotation = Random.rotation;

            var rb = _dice[i].GetComponent<Rigidbody>();
            if (rb == null) { yield return null; continue; }

            rb.isKinematic = false;
            // -X 방향(좌측 1,4,7)으로 던짐 + 약간의 하강 + 작은 Z 분산
            rb.velocity = new Vector3(
                Random.Range(-13.5f, -9f),           // 왼쪽으로 (기존 대비 1.5배)
                Random.Range(-1f,  0f),              // 약간 하강
                Random.Range(-1f,  1f)               // Z 분산
            );
            rb.angularVelocity = Random.insideUnitSphere * Random.Range(8f, 15f);

            yield return new WaitForSeconds(0.08f);  // 스태거
        }

        // 모든 주사위 스폰 완료 → 정지 감지 시작
        StartCoroutine(WaitForSettledThenLineup(keepFlags));
    }

    IEnumerator WaitForSettledThenLineup(bool[] keepFlags)
    {
        float timeout        = 10f;
        float elapsed        = 0f;
        float settledTime    = 0f;
        float confirmSec     = 0.4f;
        float frictionStart  = 1f;    // 마찰력 증가 시작
        float frictionEnd    = 1.75f; // 마찰력 최대 도달

        // 주사위별 PhysicMaterial 캐시 (매 프레임 GetComponent 방지)
        var mats = new PhysicMaterial[_dice.Length];
        var rbs  = new Rigidbody[_dice.Length];
        for (int i = 0; i < _dice.Length; i++)
        {
            if (_dice[i] == null) continue;
            var col = _dice[i].GetComponent<Collider>();
            if (col != null)
            {
                mats[i] = new PhysicMaterial("DiceFriction")
                {
                    dynamicFriction = 0.02f,
                    staticFriction  = 0.02f,
                    bounciness      = 0.5f,
                    frictionCombine = PhysicMaterialCombine.Minimum,
                    bounceCombine   = PhysicMaterialCombine.Maximum,
                };
                col.material = mats[i];
            }
            rbs[i] = _dice[i].GetComponent<Rigidbody>();
        }

        while (elapsed < timeout)
        {
            elapsed += Time.deltaTime;

            // 1.5초~3초 구간: 마찰력 0.02 → 10 선형 증가
            if (elapsed >= frictionStart)
            {
                float t = Mathf.Clamp01((elapsed - frictionStart) / (frictionEnd - frictionStart));
                float friction    = Mathf.Lerp(0.02f, 10f, t);
                float bounce      = Mathf.Lerp(0.5f,  0f,  t);
                float angularDrag = Mathf.Lerp(0.05f, 20f, t);
                for (int i = 0; i < _dice.Length; i++)
                {
                    if (i < keepFlags.Length && keepFlags[i]) continue;
                    if (mats[i] != null)
                    {
                        mats[i].dynamicFriction = friction;
                        mats[i].staticFriction  = friction;
                        mats[i].bounciness      = bounce;
                    }
                    if (rbs[i] != null) rbs[i].angularDrag = angularDrag;
                }
            }

            bool allSettled = true;
            for (int i = 0; i < _dice.Length; i++)
            {
                if (i < keepFlags.Length && keepFlags[i]) continue;
                if (_dice[i] == null || !_dice[i].gameObject.activeSelf) continue;
                if (!_dice[i].IsSettled()) { allSettled = false; break; }
            }

            settledTime = allSettled ? settledTime + Time.deltaTime : 0f;
            if (settledTime >= confirmSec) break;

            yield return null;
        }

        yield return StartCoroutine(LineupDice(keepFlags));
        ShowDiceOverlays(keepFlags);
        EnableDiceClicking();
        OnDiceLineupComplete?.Invoke();
    }

    // ── 정렬 후 주사위 오버레이 UI 생성 ──────────────────────────────
    void ShowDiceOverlays(bool[] keepFlags)
    {
        if (overlayParent == null) return;

        // 오버레이 부모에 Canvas 추가 → 컵 UI 위에 렌더링
        var overlayCanvas = overlayParent.GetComponent<Canvas>();
        if (overlayCanvas == null) overlayCanvas = overlayParent.gameObject.AddComponent<Canvas>();
        overlayCanvas.overrideSorting = true;
        overlayCanvas.sortingOrder = 100;
        if (overlayParent.GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            overlayParent.gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // 킵된 주사위 오버레이(DiceSlot에 있는 것)는 유지, 나머지만 제거
        var toRemove = new System.Collections.Generic.List<int>();
        foreach (var kv in _overlays)
        {
            if (_diceController == null || _diceController.Dice[kv.Key].State != DiceState.Kept)
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var key in toRemove) _overlays.Remove(key);

        // 3D 정렬 위치 기준으로 셀 크기 + 간격 계산
        float px    = overlayDiceSize;
        float gapPx = 8f;
        if (_boardCamera != null)
        {
            float camH = _boardCamera.orthographicSize * 2f;
            float camW = camH * _boardCamera.aspect;
            float ppuX = overlayParent.rect.width  / camW;
            float ppuY = overlayParent.rect.height / camH;

            // 셀 크기: 첫 번째 OnBoard 주사위 메시 기준 (비활성 상태이므로 activeSelf 대신 State 확인)
            for (int j = 0; j < _dice.Length; j++)
            {
                if (_dice[j] == null) continue;
                if (_diceController == null || _diceController.Dice[j].State != DiceState.OnBoard) continue;
                // localBounds(로컬 크기) × lossyScale = 회전 무관한 월드 크기
                var rend = _dice[j].GetComponentInChildren<Renderer>(true);
                float wSize = rend != null
                    ? rend.localBounds.size.x * rend.transform.lossyScale.x
                    : _dice[j].transform.lossyScale.x;
                px = wSize * (ppuX + ppuY) * 0.5f;
                break;
            }

            // 간격: 3D 정렬 위치의 center-to-center 거리 → 픽셀, 셀 크기 뺌
            if (_lineupPos.Length >= 2)
            {
                float worldGap   = _lineupPos[1].x - _lineupPos[0].x;
                float centerPx   = worldGap * ppuX;
                gapPx = Mathf.Max(0f, centerPx - px);
            }
        }

        // GridLayoutGroup 설정 — 없으면 추가
        var grid = overlayParent.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        if (grid == null) grid = overlayParent.gameObject.AddComponent<UnityEngine.UI.GridLayoutGroup>();
        grid.cellSize        = new Vector2(px, px);
        grid.spacing         = new Vector2(gapPx, 0f);
        grid.childAlignment  = TextAnchor.MiddleCenter;
        grid.constraint      = UnityEngine.UI.GridLayoutGroup.Constraint.FixedRowCount;
        grid.constraintCount = 1;

        // LineupDice와 동일한 순서(눈 값 오름차순)로 수집 — 시각 위치와 오버레이 인덱스를 일치시킴
        var sortedIndices = new System.Collections.Generic.List<int>();
        for (int i = 0; i < _dice.Length; i++)
        {
            if (_diceController == null) break;
            if (_diceController.Dice[i].State != DiceState.OnBoard) continue;
            sortedIndices.Add(i);
        }
        sortedIndices.Sort((a, b) =>
        {
            int fA = _diceController != null ? _diceController.Dice[a].Value : _dice[a].Value;
            int fB = _diceController != null ? _diceController.Dice[b].Value : _dice[b].Value;
            return fA.CompareTo(fB);
        });

        foreach (int i in sortedIndices)
        {
            var go  = new GameObject($"DiceOverlay_{i}");
            go.transform.SetParent(overlayParent, false);

            var img = go.AddComponent<UnityEngine.UI.Image>();
            int face = _diceController != null ? _diceController.Dice[i].Value : (_dice[i].Value > 0 ? _dice[i].Value : 1);
            if (diceFaceSprites != null && face - 1 < diceFaceSprites.Length)
                img.sprite = diceFaceSprites[face - 1];

            // 클릭 버튼
            var btn = go.AddComponent<UnityEngine.UI.Button>();
            int capturedIndex = i;
            btn.onClick.AddListener(() => OnOverlayClicked(capturedIndex, img));

            _overlays[i] = img;
        }

        HideCupDiceForOverlayStates();
    }

    void OnOverlayClicked(int diceIndex, UnityEngine.UI.Image overlay)
    {
        Debug.Log($"[DiceBox3D] OnOverlayClicked({diceIndex}) — _diceClickable={_diceClickable}");
        if (!_diceClickable || !CanLocalInteractWithOverlay()) return;

        // 빈 DiceSlot 탐색 (비활성 포함, X 위치 오름차순 = 왼쪽부터)
        var slots = FindObjectsOfType<DiceSlot>(true);
        System.Array.Sort(slots, (a, b) =>
            a.transform.position.x.CompareTo(b.transform.position.x));
        Debug.Log($"[DiceBox3D] DiceSlot 탐색 — 총 {slots.Length}개");
        DiceSlot targetSlot = null;
        foreach (var s in slots)
        {
            Debug.Log($"[DiceBox3D]   {s.name} IsOccupied={s.IsOccupied}");
            if (!s.IsOccupied) { targetSlot = s; break; }
        }

        if (targetSlot == null)
        {
            Debug.LogWarning("[DiceBox3D] 빈 DiceSlot 없음");
            return;
        }
        Debug.Log($"[DiceBox3D] 목표 슬롯: {targetSlot.name}, 위치={targetSlot.transform.position}");

        targetSlot.OccupyWithOverlay();
        SetState(diceIndex, DiceState.Kept);

        var btn = overlay.GetComponent<UnityEngine.UI.Button>();
        btn.interactable = false;

        StartCoroutine(FlyOverlayToSlot(
            overlay.rectTransform,
            targetSlot.GetComponent<RectTransform>(),
            targetSlot,
            () =>
            {
                // 도착 후 버튼을 "복귀" 동작으로 교체
                btn.interactable = true;
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OnOverlayReturnClicked(diceIndex, overlay, targetSlot));
            }
        ));
    }

    IEnumerator FlyOverlayToSlot(RectTransform overlay, RectTransform slot, DiceSlot diceSlot, System.Action onComplete = null)
    {
        var canvas = overlayParent.GetComponentInParent<Canvas>();
        if (canvas != null) overlay.SetParent(canvas.transform, true);

        Vector3 from = overlay.position;
        Vector3 to   = slot.position;

        float duration = 0.4f, elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / duration));
            overlay.position = Vector3.Lerp(from, to, t);
            yield return null;
        }

        overlay.SetParent(slot, false);
        overlay.anchorMin = Vector2.zero;
        overlay.anchorMax = Vector2.one;
        overlay.offsetMin = Vector2.zero;
        overlay.offsetMax = Vector2.zero;

        diceSlot.OnOverlayEnter();
        onComplete?.Invoke();
    }

    void OnOverlayReturnClicked(int diceIndex, UnityEngine.UI.Image overlay, DiceSlot slot)
    {
        if (!CanLocalInteractWithOverlay()) return;

        slot.OnOverlayExit();
        slot.Clear();
        SetState(diceIndex, DiceState.OnBoard);
        RestoreOverlayToBoardRow(overlay.rectTransform);
        HideCupDiceForOverlayStates();

        var btn = overlay.GetComponent<UnityEngine.UI.Button>();
        btn.interactable = true;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() => OnOverlayClicked(diceIndex, overlay));
    }

    // 킵되지 않은 주사위를 InCup 상태로 전환 (흔들기 시작 시 호출)
    public void ShowNonKeptCupDice()
    {
        for (int i = 0; i < _dice.Length; i++)
        {
            if (_diceController == null) break;
            if (_diceController.Dice[i].State != DiceState.Kept)
                SetState(i, DiceState.InCup);
        }
    }

    // OnBoard 상태인 오버레이 제거 (컵 흔들기 시작 시 호출)
    public void ClearNonKeptOverlays()
    {
        var toRemove = new System.Collections.Generic.List<int>();
        foreach (var kv in _overlays)
        {
            if (_diceController == null || _diceController.Dice[kv.Key].State != DiceState.Kept)
            {
                if (kv.Value != null) Destroy(kv.Value.gameObject);
                toRemove.Add(kv.Key);
            }
        }
        foreach (var key in toRemove) _overlays.Remove(key);
    }

    // 오버레이 전체 제거 (턴 리셋 시 호출)
    public void ClearDiceOverlays()
    {
        foreach (var img in _overlays.Values)
            if (img != null) Destroy(img.gameObject);
        _overlays.Clear();
    }

    // ── UI 연동 API ──────────────────────────────────────────────────
    // UI 오프셋(픽셀)을 받아 3D 컵 포지션에 동일하게 적용
    public void SetCupPosition(Vector2 uiOffset)
    {
        if (_cupRoot == null) return;
        _cupRoot.position = _cupBasePosition + new Vector3(uiOffset.x, uiOffset.y, 0f) * uiToWorldScale;
    }

    public void ResetCupPosition()
    {
        if (_cupRoot == null) return;
        _cupRoot.position = _cupBasePosition;
    }

    /// 새 턴 시작 시 보드 주사위를 모두 숨기고 컵 주사위를 원위치로 복원.
    /// UICupShaker.ShowCup()에서 호출 → 이전 턴 주사위가 보드에 남아 보이는 문제 해결.
    public void ReturnAllToCup()
    {
        // 보드 주사위 전부 숨김
        for (int i = 0; i < _dice.Length; i++)
            if (_dice[i] != null) _dice[i].gameObject.SetActive(false);

        // 컵 주사위 원위치 표시 (첫 굴림 후에는 윗면 정렬)
        if (_cupRoot != null)
        {
            bool snapFace = _diceController != null && _diceController.RollCount > 0;
            for (int i = 0; i < _cupDice.Length; i++)
            {
                if (_cupDice[i] == null) continue;
                _cupDice[i].transform.localPosition = _cupDiceBaseLocalPos[i];
                if (snapFace)
                {
                    int face = i < _diceController.Dice.Length ? Mathf.Max(1, _diceController.Dice[i].Value) : 1;
                    _cupDice[i].SnapToFace(face);
                }
                _cupDice[i].gameObject.SetActive(true);
            }
        }

        ResetCupPosition();
    }

    // 기존 호환용
    public void SetCupAngle(float angle)
    {
        if (_cupRoot == null) return;
        _cupRoot.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    public void ResetCupAngle()
    {
        if (_cupRoot == null) return;
        _cupRoot.localRotation = Quaternion.identity;
    }

    // ── 애니메이션 버전 던지기 (네트워크 상대방용) ───────────────────
    /// <summary>
    /// 물리 없이 애니메이션으로 주사위를 던지고 정렬합니다.
    /// finalValues: 미리 결정된 주사위 값 5개 (1~6)
    /// keepFlags:   킵 상태 배열
    /// </summary>
    /// <summary>
    /// seed: 네트워크에서 호스트가 생성해 클라이언트에 전달. -1이면 로컬 랜덤 생성.
    /// 동일한 (finalValues, keepFlags, gaugeValue, seed) → 동일한 애니메이션 보장.
    /// </summary>
    public void ThrowDiceAnimated(int[] finalValues, bool[] keepFlags = null, float gaugeValue = 1f, int seed = -1)
    {
        if (keepFlags == null) keepFlags = new bool[5];
        if (seed < 0) seed = Random.Range(0, int.MaxValue);
        StartCoroutine(ThrowDiceAnimatedCo(finalValues, keepFlags, gaugeValue, seed));
    }

    IEnumerator ThrowDiceAnimatedCo(int[] finalValues, bool[] keepFlags, float gaugeValue, int seed)
    {
        EnsureReferences();
        ClearNonKeptOverlays();
        for (int i = 0; i < _cupDice.Length; i++)
            if (_cupDice[i] != null) _cupDice[i].gameObject.SetActive(false);

        // ── 보드 경계 계산 ────────────────────────────────────────────
        GetBoardHalfSize(out float halfW, out float halfH);

        float boardY  = _lineupPos[0].y;
        float wallInset = 0.5f;  // 벽 waypoint를 안쪽으로 당김 (정확히 벽에 닿지 않게)
        float leftX   = RoomCenter.x - halfW + wallInset;
        float rightX  = RoomCenter.x + halfW - wallInset;
        float cupX    = RoomCenter.x + halfW * 0.5f;
        float topZ    = RoomCenter.z + halfH - wallInset;
        float bottomZ = RoomCenter.z - halfH + wallInset;

        // ── seed 기반 격리 랜덤 시작 ─────────────────────────────────
        // 게임 전체 Random 상태를 오염시키지 않도록 저장 후 복원
        var savedState = Random.state;
        Random.InitState(seed);

        // ── 정렬 인덱스 ───────────────────────────────────────────────
        var sortedIndices = new System.Collections.Generic.List<int>();
        for (int i = 0; i < _dice.Length; i++)
            if (!(i < keepFlags.Length && keepFlags[i]) && _dice[i] != null)
                sortedIndices.Add(i);
        sortedIndices.Sort((a, b) => finalValues[a].CompareTo(finalValues[b]));

        int n = sortedIndices.Count;

        // ── 경로 + 코루틴 파라미터 사전 계산 (모두 seed에서 파생) ─────
        const int wallDivisions  = 7;
        float     leftWallSecZ   = (topZ - bottomZ) / wallDivisions;
        float     topWallSecX    = (rightX - leftX) / wallDivisions;

        // 기초 아크 높이: 주사위는 낮게 구르므로 작은 값, gaugeValue로 소폭 조정
        float baseArc = Mathf.Lerp(0.15f, 0.35f, gaugeValue);

        // 착지 위치: 마지막 벽 바운스에서 이 거리 이내로 제한
        const float maxLandDist = 3.6f;
        float       pad         = 0.5f;
        float       minDist     = 1.3f;

        int doneCount = 0;

        var landPos = new Vector3[5];
        var landRot = new Quaternion[5];

        // 경로·파라미터를 struct 배열로 미리 계산 → 코루틴에 값으로 전달
        var perDie = new (System.Collections.Generic.List<Vector3> path,
                          Quaternion startRot,
                          Quaternion landRot,
                          Vector3 settlePos,
                          int plannerSegments,
                          float segDur,
                          float initArc,
                          float arcDecay,
                          Vector3 spinAxis,
                          float spinDeg,
                          float spinDecay)[5];

        for (int slot = 0; slot < n; slot++)
        {
            int i = sortedIndices[slot];

            // 바운스 횟수 (seed 기반)
            int baseBounce = gaugeValue < 0.34f ? 2 : gaugeValue < 0.67f ? 3 : 4;
            int dieBounce  = Mathf.Clamp(baseBounce + Random.Range(-1, 2), 2, 5);

            // ── 반사 기반 경로 (당구공처럼 벽 법선으로 반사) ─────────────
            var path = new System.Collections.Generic.List<Vector3>();
            float startZ = bottomZ + (Random.Range(0, wallDivisions) + Random.value) * leftWallSecZ;
            Vector3 curPt  = new Vector3(cupX, boardY, startZ);
            path.Add(curPt);

            // 초기 방향: 왼쪽 벽 + 약간의 Z 각도 (완전 수평 방지)
            float   initDz = (Random.value - 0.5f) * (topZ - bottomZ) * 0.7f;
            Vector3 curDir = new Vector3(leftX - cupX, 0f, initDz).normalized;

            for (int b = 0; b < dieBounce; b++)
            {
                // XZ 평면 레이 vs 4벽 교차 → 최소 양수 t 선택
                float   tMin      = float.MaxValue;
                Vector3 hitNormal = Vector3.right;

                if (curDir.x < -0.001f) { float t = (leftX   - curPt.x) / curDir.x; if (t > 0.001f && t < tMin) { tMin = t; hitNormal =  Vector3.right;   } }
                if (curDir.x >  0.001f) { float t = (rightX  - curPt.x) / curDir.x; if (t > 0.001f && t < tMin) { tMin = t; hitNormal =  Vector3.left;    } }
                if (curDir.z < -0.001f) { float t = (bottomZ - curPt.z) / curDir.z; if (t > 0.001f && t < tMin) { tMin = t; hitNormal =  Vector3.forward;  } }
                if (curDir.z >  0.001f) { float t = (topZ    - curPt.z) / curDir.z; if (t > 0.001f && t < tMin) { tMin = t; hitNormal =  Vector3.back;     } }

                Vector3 hitPt = curPt + curDir * tMin;
                // 벽 충돌점에 소폭 랜덤 편차 (코너 고착 방지 + 자연스러움)
                hitPt += new Vector3(
                    Random.Range(-0.12f, 0.12f) * Mathf.Abs(hitNormal.z),
                    0f,
                    Random.Range(-0.12f, 0.12f) * Mathf.Abs(hitNormal.x)
                );
                hitPt.x = Mathf.Clamp(hitPt.x, leftX, rightX);
                hitPt.z = Mathf.Clamp(hitPt.z, bottomZ, topZ);
                hitPt.y = boardY;
                path.Add(hitPt);

                // 반사 후 다음 구간 준비
                curDir = Vector3.Reflect(curDir, hitNormal);
                curDir.y = 0f;
                curDir.Normalize();
                curPt = hitPt;
            }

            // 착지 위치: 마지막 바운스 위치에서 maxLandDist 이내 + 보드 안 + 겹침 방지
            Vector3 lastBounce = path[path.Count - 1];
            Vector3 landPosI   = lastBounce;  // fallback
            for (int attempt = 0; attempt < 50; attempt++)
            {
                float angle = Random.value * Mathf.PI * 2f;
                float dist  = Random.Range(1.35f, maxLandDist);
                Vector3 candidate = new Vector3(
                    lastBounce.x + Mathf.Cos(angle) * dist,
                    boardY,
                    lastBounce.z + Mathf.Sin(angle) * dist
                );
                // 보드 경계 클램프
                candidate.x = Mathf.Clamp(candidate.x, leftX + pad, rightX - pad);
                candidate.z = Mathf.Clamp(candidate.z, bottomZ + pad, topZ - pad);

                bool overlap = false;
                for (int prev = 0; prev < slot; prev++)
                    if (Vector3.Distance(candidate, landPos[sortedIndices[prev]]) < minDist)
                    { overlap = true; break; }

                if (!overlap) { landPosI = candidate; break; }
            }
            landPos[i] = landPosI;
            // 윗면은 고정, Y축(수직)으로 랜덤 회전 추가 → 반듯하게 멈추지 않음
            path.Add(landPosI);
            path = CompactShortSegments(path, 0.7f, leftX + pad, rightX - pad, bottomZ + pad, topZ - pad, boardY);
            int plannerSegments = Mathf.Max(1, path.Count - 1);
            float maxSegmentDistance = Mathf.Max(2.25f, GetDieEdgeLength(i) * 3.0f);
            path = SubdivideLongSegments(path, maxSegmentDistance, boardY);
            landPosI = path[path.Count - 1];
            landPos[i] = landPosI;

            Quaternion startRotI = SelectStartRotationForTarget(i, path, finalValues[i], out Quaternion finalLandRotation);
            landRot[i] = finalLandRotation;

            // 속도: 바운스 많을수록 빠름
            float segDur = Mathf.Lerp(0.76f, 0.40f, (dieBounce - 2f) / 3f);

            // 아크 감쇠: 바운스마다 0.6배 (에너지 손실)
            float arcDecay = 0.6f;

            // 회전: 구간당 최대 120°, 밑면이 위로 향하지 않도록 제한
            float avgSeg   = segDur * 0.65f;
            float spinDeg  = 120f / avgSeg;
            float spinDecay = 0.7f;

            Vector3 settlePos = landPosI;
            if (path.Count >= 2 && Random.value < 0.48f)
            {
                Vector3 slideDir = (path[path.Count - 1] - path[path.Count - 2]);
                slideDir.y = 0f;
                if (slideDir.sqrMagnitude > 0.0001f)
                {
                    slideDir.Normalize();
                    Vector3 side = Vector3.Cross(Vector3.up, slideDir).normalized;
                    float sideMix = Random.Range(-0.22f, 0.22f);
                    slideDir = (slideDir + side * sideMix).normalized;

                    float edgeLength = GetDieEdgeLength(i);
                    float slideDistance = Random.Range(edgeLength * 0.18f, edgeLength * 0.5f) * Mathf.Lerp(0.95f, 1.18f, gaugeValue);
                    settlePos += slideDir * slideDistance;
                    settlePos.x = Mathf.Clamp(settlePos.x, leftX + pad, rightX - pad);
                    settlePos.z = Mathf.Clamp(settlePos.z, bottomZ + pad, topZ - pad);
                    settlePos.y = boardY;
                }
            }

            perDie[i] = (path, startRotI, landRot[i], settlePos, plannerSegments, segDur, baseArc, arcDecay, Vector3.zero, spinDeg, spinDecay);
        }

        // ── 게임 Random 상태 복원 ─────────────────────────────────────
        Random.state = savedState;

        // ── 주사위 초기화 + 코루틴 실행 ──────────────────────────────
        for (int slot = 0; slot < n; slot++)
        {
            int i = sortedIndices[slot];
            var p = perDie[i];

            SetState(i, DiceState.Throwing);
            _dice[i].transform.position = p.path[0];
            _dice[i].transform.rotation = p.startRot;
            var rb = _dice[i].GetComponent<Rigidbody>();
            if (rb != null) rb.isKinematic = true;
            _dice[i].gameObject.SetActive(true);

            int captured = i;
            StartCoroutine(AnimateSingleDieEdgeRollCo(
                captured, p.path, p.landRot, p.settlePos, boardY,
                p.plannerSegments, p.segDur, p.initArc, p.arcDecay,
                p.spinAxis, p.spinDeg, p.spinDecay,
                () => doneCount++));
        }

        yield return new WaitUntil(() => doneCount >= n);
        yield return new WaitForSeconds(0.28f);

        // 값 확정 + OnBoard 상태로 전환 (주사위는 아직 보임 — LineupDice가 이동 후 숨김)
        foreach (int i in sortedIndices)
        {
            _dice[i].Value = finalValues[i];
            if (_diceController is not null) _diceController.Dice[i].Value = finalValues[i];
            SetState(i, DiceState.OnBoard);
            _dice[i].gameObject.SetActive(true);  // LineupDice 이동용으로 활성 유지
        }

        yield return StartCoroutine(LineupDice(keepFlags, finalValues));
        ShowDiceOverlays(keepFlags);
        EnableDiceClicking();
        OnDiceLineupComplete?.Invoke();
    }

    IEnumerator AnimateSingleDieEdgeRollCo(
        int dieIndex,
        System.Collections.Generic.List<Vector3> path,
        Quaternion landRot,
        Vector3 settlePos,
        float boardY,
        int plannerSegments,
        float segDuration,
        float initArc,
        float arcDecay,
        Vector3 _unused,
        float spinDegPerSec,
        float spinDecay,
        System.Action onDone)
    {
        float edgeLength = GetDieEdgeLength(dieIndex);
        float curArc = initArc * 0.35f;
        Vector3 lastRollDirection = Vector3.left;
        float totalPathDistance = TotalPathDistance(path);
        float desiredTotalDuration = EstimateDesiredEdgeRollDuration(segDuration, plannerSegments);
        float weightedDistance = 0f;

        for (int seg = 0; seg < path.Count - 1; seg++)
        {
            Vector3 from = path[seg];
            Vector3 to = path[seg + 1];
            from.y = boardY;
            to.y = boardY;

            float segmentDistance = Vector3.Distance(from, to);
            if (segmentDistance <= 0.001f)
                continue;

            float segmentMidProgress = totalPathDistance > 0.001f
                ? (DistanceAlongPath(path, seg) + segmentDistance * 0.5f) / totalPathDistance
                : 0f;
            float speedFactor = EvaluateRollSpeedFactor(segmentMidProgress);
            weightedDistance += segmentDistance / Mathf.Max(speedFactor, 0.01f);
        }

        float targetUnitsPerSecond = desiredTotalDuration > 0.001f
            ? weightedDistance / desiredTotalDuration
            : 1f;
        float travelledDistance = 0f;

        int totalSegs = path.Count - 1;
        for (int seg = 0; seg < totalSegs; seg++)
        {
            bool isLastSeg = seg == totalSegs - 1;

            Vector3 fromPos = _dice[dieIndex].transform.position;
            Vector3 toPos = path[seg + 1];
            fromPos.y = boardY;
            toPos.y = boardY;

            Vector3 flatDelta = toPos - fromPos;
            flatDelta.y = 0f;
            float segmentDistance = flatDelta.magnitude;
            if (segmentDistance <= 0.001f)
                continue;

            Vector3 rollDirection = flatDelta.normalized;
            Vector3 rollAxis = Vector3.Cross(Vector3.up, rollDirection);
            if (rollAxis.sqrMagnitude <= 0.0001f)
                rollAxis = Vector3.right;
            else
                rollAxis.Normalize();

            float segmentMidProgress = totalPathDistance > 0.001f
                ? (travelledDistance + segmentDistance * 0.5f) / totalPathDistance
                : 0f;
            float speedFactor = EvaluateRollSpeedFactor(segmentMidProgress);
            float duration = (segmentDistance / Mathf.Max(speedFactor, 0.01f)) / Mathf.Max(targetUnitsPerSecond, 0.01f);
            duration = Mathf.Max(0.075f, duration);
            float totalRollAngle = (segmentDistance / Mathf.Max(edgeLength, 0.001f)) * 90f;

            float elapsed = 0f;
            float lastMoveProgress = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / duration);
                float moveProgress = isLastSeg
                    ? Mathf.Lerp(raw, EaseOut(raw), 0.28f)
                    : raw;

                Vector3 flatPos = Vector3.Lerp(fromPos, toPos, moveProgress);
                float arc = Mathf.Sin(Mathf.PI * raw) * curArc;
                _dice[dieIndex].transform.position = new Vector3(flatPos.x, boardY + arc, flatPos.z);

                float deltaAngle = totalRollAngle * (moveProgress - lastMoveProgress);
                _dice[dieIndex].transform.rotation =
                    Quaternion.AngleAxis(deltaAngle, rollAxis) *
                    _dice[dieIndex].transform.rotation;

                lastMoveProgress = moveProgress;
                yield return null;
            }

            _dice[dieIndex].transform.position = new Vector3(toPos.x, boardY, toPos.z);
            lastRollDirection = rollDirection;
            curArc *= arcDecay;
            travelledDistance += segmentDistance;
        }

        settlePos.y = boardY;
        yield return StartCoroutine(SettleDieToRestCo(dieIndex, settlePos, landRot, lastRollDirection));
        onDone?.Invoke();
    }

    IEnumerator AnimateSingleDieCo(
        int dieIndex,
        System.Collections.Generic.List<Vector3> path,
        Quaternion landRot,
        float boardY,
        float segDuration,
        float initArc,
        float arcDecay,
        Vector3 _unused,       // 더 이상 쓰지 않음 (경로 기반 축으로 교체)
        float spinDegPerSec,
        float spinDecay,
        System.Action onDone)
    {
        float curArc  = initArc;
        float curSpin = spinDegPerSec;
        float avgSegmentDistance = AverageSegmentDistance(path);

        int totalSegs = path.Count - 1;
        for (int seg = 0; seg < totalSegs; seg++)
        {
            bool  isLastSeg = seg == totalSegs - 1;
            float durationBase  = isLastSeg
                ? segDuration * 1.3f
                : segDuration * Mathf.Pow(1.25f, seg) * 0.65f;

            Vector3    fromPos = _dice[dieIndex].transform.position;
            Vector3    toPos   = path[seg + 1];
            Quaternion fromRot = _dice[dieIndex].transform.rotation;
            float segmentDistance = Vector3.Distance(fromPos, toPos);
            float durationScale = avgSegmentDistance > 0.001f
                ? Mathf.Clamp(segmentDistance / avgSegmentDistance, 0.55f, 1.65f)
                : 1f;
            float duration = durationBase * durationScale;

            // ── 이동 방향 기반 롤 축 계산 ────────────────────────────
            Vector3 movDir = toPos - fromPos;
            movDir.y = 0f;
            Vector3 rollAxis;
            if (movDir.sqrMagnitude > 0.001f)
            {
                Vector3 snap = Mathf.Abs(movDir.x) >= Mathf.Abs(movDir.z)
                    ? new Vector3(Mathf.Sign(movDir.x), 0f, 0f)
                    : new Vector3(0f, 0f, Mathf.Sign(movDir.z));
                rollAxis = Vector3.Cross(Vector3.up, snap);
            }
            else
            {
                rollAxis = Vector3.right;
            }

            // ── 마지막 구간 전용 계산 ────────────────────────────────────
            // landRot의 Y축 90° 회전 변형 4가지 중, rollAxis 기준 전진 방향(signed 0°~180°)인
            // 후보만 골라 가장 가까운 것 선택 → Slerp이 굴러오던 방향과 같은 방향으로 회전
            Quaternion targetRot = landRot;
            float targetRollAngle = 0f;
            if (isLastSeg)
            {
                // rollAxis에 수직인 참조 벡터 (fromRot 기준)
                Vector3 refVec = Vector3.Cross(rollAxis, Vector3.up).normalized;
                if (refVec.sqrMagnitude < 0.01f)
                    refVec = Vector3.Cross(rollAxis, Vector3.forward).normalized;
                Vector3 fromV = Vector3.ProjectOnPlane(fromRot * refVec, rollAxis).normalized;

                float bestAngle = float.MaxValue;
                for (int k = 0; k < 4; k++)
                {
                    Quaternion candidate = Quaternion.AngleAxis(90f * k, Vector3.up) * landRot;
                    Vector3 candV = Vector3.ProjectOnPlane(candidate * refVec, rollAxis).normalized;

                    float signed = Vector3.SignedAngle(fromV, candV, rollAxis);
                    if (signed < 0f) signed += 360f;  // [0, 360)

                    // Slerp은 최단 경로 → signed <= 180° 인 후보만 전진 방향
                    if (signed <= 180f && signed < bestAngle)
                    {
                        bestAngle  = signed;
                        targetRot  = candidate;
                        targetRollAngle = signed;
                    }
                }
            }

            float elapsed = 0f;
            float lastRollProgress = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float raw = Mathf.Clamp01(elapsed / duration);
                float t   = isLastSeg ? EaseOut(raw) : raw;

                Vector3 flat = Vector3.Lerp(fromPos, toPos, t);
                float   arc  = Mathf.Sin(Mathf.PI * raw) * curArc;
                _dice[dieIndex].transform.position = new Vector3(flat.x, boardY + arc, flat.z);

                if (isLastSeg)
                {
                    // fromRot → targetRot EaseOut Slerp
                    float rollProgress = EaseOut(raw);
                    float deltaAngle = targetRollAngle * (rollProgress - lastRollProgress);
                    _dice[dieIndex].transform.rotation =
                        Quaternion.AngleAxis(deltaAngle, rollAxis) *
                        _dice[dieIndex].transform.rotation;
                    lastRollProgress = rollProgress;
                }
                else
                {
                    _dice[dieIndex].transform.rotation =
                        Quaternion.AngleAxis(curSpin * Time.deltaTime, rollAxis) *
                        _dice[dieIndex].transform.rotation;
                }

                yield return null;
            }

            _dice[dieIndex].transform.position = new Vector3(toPos.x, boardY, toPos.z);
            if (isLastSeg)
                yield return StartCoroutine(SettleDieToRestCo(dieIndex, toPos, landRot, movDir));
            // 마지막 구간 끝: targetRot으로 확정 (bestTotalAngle 도달 지점과 근사)

            // 바운스마다 아크·회전 감쇠
            curArc  *= arcDecay;
            curSpin *= spinDecay;
        }

        onDone?.Invoke();
    }

    // ── 유틸 ─────────────────────────────────────────────────────────
    struct DieFaceState
    {
        public int top;
        public int bottom;
        public int left;
        public int right;
        public int forward;
        public int back;
    }

    Quaternion SelectStartRotationForTarget(int dieIndex, System.Collections.Generic.List<Vector3> path, int targetFace, out Quaternion finalRotation)
    {
        finalRotation = _dice[dieIndex] != null
            ? Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.up) * _dice[dieIndex].FaceUpRotation(targetFace)
            : Quaternion.identity;
        if (_dice[dieIndex] == null)
            return Quaternion.identity;

        float edgeLength = GetDieEdgeLength(dieIndex);
        Quaternion accumulatedRoll = CalculateAccumulatedRollRotation(path, edgeLength);
        return Quaternion.Inverse(accumulatedRoll) * finalRotation;
    }

    Quaternion CalculateAccumulatedRollRotation(System.Collections.Generic.List<Vector3> path, float edgeLength)
    {
        Quaternion accumulated = Quaternion.identity;
        if (path == null || path.Count < 2) return accumulated;

        float safeEdge = Mathf.Max(edgeLength, 0.001f);
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 delta = path[i] - path[i - 1];
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= 0.001f) continue;

            Vector3 rollDirection = delta / distance;
            Vector3 rollAxis = Vector3.Cross(Vector3.up, rollDirection);
            if (rollAxis.sqrMagnitude <= 0.0001f) continue;

            rollAxis.Normalize();
            float angle = (distance / safeEdge) * 90f;
            accumulated = Quaternion.AngleAxis(angle, rollAxis) * accumulated;
        }

        return accumulated;
    }

    System.Collections.Generic.List<Quaternion> EnumerateStableRotations(int dieIndex)
    {
        var rotations = new System.Collections.Generic.List<Quaternion>(24);
        if (_dice[dieIndex] == null) return rotations;

        for (int face = 1; face <= 6; face++)
        {
            Quaternion faceRotation = _dice[dieIndex].FaceUpRotation(face);
            for (int yawStep = 0; yawStep < 4; yawStep++)
                rotations.Add(Quaternion.AngleAxis(90f * yawStep, Vector3.up) * faceRotation);
        }

        return rotations;
    }

    DieFaceState CaptureFaceState(int dieIndex, Quaternion rotation)
    {
        return new DieFaceState
        {
            top = GetFaceForWorldDirection(dieIndex, rotation, Vector3.up),
            bottom = GetFaceForWorldDirection(dieIndex, rotation, Vector3.down),
            left = GetFaceForWorldDirection(dieIndex, rotation, Vector3.left),
            right = GetFaceForWorldDirection(dieIndex, rotation, Vector3.right),
            forward = GetFaceForWorldDirection(dieIndex, rotation, Vector3.forward),
            back = GetFaceForWorldDirection(dieIndex, rotation, Vector3.back),
        };
    }

    int GetFaceForWorldDirection(int dieIndex, Quaternion rotation, Vector3 worldDirection)
    {
        if (_dice[dieIndex] == null) return 1;

        var die = _dice[dieIndex];
        Vector3 dir = worldDirection.normalized;
        var axes = new (Vector3 worldDir, int face)[]
        {
            (rotation * Vector3.up, die.faceLocalUp),
            (rotation * Vector3.down, die.faceLocalDown),
            (rotation * Vector3.right, die.faceLocalRight),
            (rotation * Vector3.left, die.faceLocalLeft),
            (rotation * Vector3.forward, die.faceLocalForward),
            (rotation * Vector3.back, die.faceLocalBack),
        };

        int bestFace = 1;
        float bestDot = -2f;
        foreach (var (axisWorldDir, face) in axes)
        {
            float dot = Vector3.Dot(axisWorldDir, dir);
            if (dot > bestDot)
            {
                bestDot = dot;
                bestFace = face;
            }
        }

        return bestFace;
    }

    System.Collections.Generic.List<Vector3> BuildLogicalRollPlan(System.Collections.Generic.List<Vector3> path, float edgeLength)
    {
        var plan = new System.Collections.Generic.List<Vector3>();
        if (path == null || path.Count < 2) return plan;

        float safeEdge = Mathf.Max(edgeLength, 0.001f);
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 delta = path[i] - path[i - 1];
            delta.y = 0f;

            int stepsX = Mathf.RoundToInt(Mathf.Abs(delta.x) / safeEdge);
            int stepsZ = Mathf.RoundToInt(Mathf.Abs(delta.z) / safeEdge);

            if (stepsX == 0 && stepsZ == 0 && delta.sqrMagnitude > (safeEdge * 0.2f) * (safeEdge * 0.2f))
            {
                if (Mathf.Abs(delta.x) >= Mathf.Abs(delta.z))
                    stepsX = 1;
                else
                    stepsZ = 1;
            }

            int doneX = 0;
            int doneZ = 0;
            float signX = Mathf.Sign(Mathf.Approximately(delta.x, 0f) ? 1f : delta.x);
            float signZ = Mathf.Sign(Mathf.Approximately(delta.z, 0f) ? 1f : delta.z);

            while (doneX < stepsX || doneZ < stepsZ)
            {
                if (doneX >= stepsX)
                {
                    plan.Add(new Vector3(0f, 0f, signZ));
                    doneZ++;
                    continue;
                }

                if (doneZ >= stepsZ)
                {
                    plan.Add(new Vector3(signX, 0f, 0f));
                    doneX++;
                    continue;
                }

                float nextXProgress = (doneX + 1f) / Mathf.Max(stepsX, 1);
                float nextZProgress = (doneZ + 1f) / Mathf.Max(stepsZ, 1);

                if (nextXProgress <= nextZProgress)
                {
                    plan.Add(new Vector3(signX, 0f, 0f));
                    doneX++;
                }
                else
                {
                    plan.Add(new Vector3(0f, 0f, signZ));
                    doneZ++;
                }
            }
        }

        return plan;
    }

    void SimulateRollPlan(ref DieFaceState state, System.Collections.Generic.List<Vector3> rollPlan)
    {
        if (rollPlan == null) return;

        for (int i = 0; i < rollPlan.Count; i++)
            ApplyRollToState(ref state, rollPlan[i]);
    }

    void ApplyRollToState(ref DieFaceState state, Vector3 direction)
    {
        if (Mathf.Abs(direction.x) >= Mathf.Abs(direction.z))
        {
            if (direction.x >= 0f) RollStatePositiveX(ref state);
            else RollStateNegativeX(ref state);
        }
        else
        {
            if (direction.z >= 0f) RollStatePositiveZ(ref state);
            else RollStateNegativeZ(ref state);
        }
    }

    void RollStatePositiveX(ref DieFaceState state)
    {
        int oldTop = state.top;
        int oldBottom = state.bottom;
        int oldLeft = state.left;
        int oldRight = state.right;

        state.top = oldLeft;
        state.bottom = oldRight;
        state.left = oldBottom;
        state.right = oldTop;
    }

    void RollStateNegativeX(ref DieFaceState state)
    {
        int oldTop = state.top;
        int oldBottom = state.bottom;
        int oldLeft = state.left;
        int oldRight = state.right;

        state.top = oldRight;
        state.bottom = oldLeft;
        state.left = oldTop;
        state.right = oldBottom;
    }

    void RollStatePositiveZ(ref DieFaceState state)
    {
        int oldTop = state.top;
        int oldBottom = state.bottom;
        int oldForward = state.forward;
        int oldBack = state.back;

        state.top = oldBack;
        state.bottom = oldForward;
        state.forward = oldTop;
        state.back = oldBottom;
    }

    void RollStateNegativeZ(ref DieFaceState state)
    {
        int oldTop = state.top;
        int oldBottom = state.bottom;
        int oldForward = state.forward;
        int oldBack = state.back;

        state.top = oldForward;
        state.bottom = oldBack;
        state.forward = oldBottom;
        state.back = oldTop;
    }

    Quaternion FindRotationForState(int dieIndex, DieFaceState targetState)
    {
        foreach (Quaternion candidate in EnumerateStableRotations(dieIndex))
        {
            DieFaceState candidateState = CaptureFaceState(dieIndex, candidate);
            if (candidateState.top == targetState.top &&
                candidateState.bottom == targetState.bottom &&
                candidateState.left == targetState.left &&
                candidateState.right == targetState.right &&
                candidateState.forward == targetState.forward &&
                candidateState.back == targetState.back)
            {
                return candidate;
            }
        }

        return _dice[dieIndex] != null ? _dice[dieIndex].FaceUpRotation(targetState.top) : Quaternion.identity;
    }

    Quaternion GetRandomRollStartRotation(int dieIndex)
    {
        if (_dice[dieIndex] == null) return Quaternion.identity;

        int face = Random.Range(1, 7);
        float yaw = Random.Range(0f, 360f);
        return Quaternion.AngleAxis(yaw, Vector3.up) * _dice[dieIndex].FaceUpRotation(face);
    }

    float GetDieEdgeLength(int dieIndex)
    {
        if (dieIndex < 0 || dieIndex >= _dice.Length || _dice[dieIndex] == null)
            return 0.75f;

        if (_dieEdgeLengths[dieIndex] > 0.001f)
            return _dieEdgeLengths[dieIndex];

        float edgeLength = 0.75f;
        var meshFilter = _dice[dieIndex].GetComponentInChildren<MeshFilter>(true);
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Vector3 meshSize = meshFilter.sharedMesh.bounds.size;
            Vector3 scale = meshFilter.transform.lossyScale;
            edgeLength = Mathf.Max(
                Mathf.Abs(meshSize.x * scale.x),
                Mathf.Abs(meshSize.y * scale.y),
                Mathf.Abs(meshSize.z * scale.z));
        }
        else
        {
            var renderer = _dice[dieIndex].GetComponentInChildren<Renderer>(true);
            if (renderer != null)
            {
                Vector3 size = renderer.bounds.size;
                edgeLength = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            }
        }

        _dieEdgeLengths[dieIndex] = Mathf.Max(0.1f, edgeLength);
        return _dieEdgeLengths[dieIndex];
    }

    System.Collections.Generic.List<Vector3> BuildRollDirections(Vector3 flatDelta, float edgeLength)
    {
        var directions = new System.Collections.Generic.List<Vector3>();
        float threshold = edgeLength * 0.45f;
        float remainingX = flatDelta.x;
        float remainingZ = flatDelta.z;
        int safety = 0;

        while ((Mathf.Abs(remainingX) >= threshold || Mathf.Abs(remainingZ) >= threshold) && safety++ < 64)
        {
            bool takeX = Mathf.Abs(remainingX) >= Mathf.Abs(remainingZ);
            if (takeX && Mathf.Abs(remainingX) >= threshold)
            {
                float sign = Mathf.Sign(remainingX);
                directions.Add(new Vector3(sign, 0f, 0f));
                remainingX -= sign * edgeLength;
                continue;
            }

            if (Mathf.Abs(remainingZ) >= threshold)
            {
                float sign = Mathf.Sign(remainingZ);
                directions.Add(new Vector3(0f, 0f, sign));
                remainingZ -= sign * edgeLength;
            }
        }

        return directions;
    }

    IEnumerator RollDieStepCo(int dieIndex, Vector3 direction, float edgeLength, float boardY, float duration)
    {
        if (_dice[dieIndex] == null) yield break;

        Transform dieTransform = _dice[dieIndex].transform;
        Vector3 stepDirection = direction.normalized;
        if (stepDirection.sqrMagnitude <= 0.001f) yield break;

        Vector3 startPos = dieTransform.position;
        startPos.y = boardY;
        Quaternion startRot = dieTransform.rotation;

        float halfEdge = edgeLength * 0.5f;
        Vector3 axis = Vector3.Cross(Vector3.up, stepDirection).normalized;
        Vector3 pivot = startPos + stepDirection * halfEdge + Vector3.down * halfEdge;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = EaseInOut(Mathf.Clamp01(elapsed / duration));
            Quaternion stepRotation = Quaternion.AngleAxis(90f * t, axis);
            dieTransform.position = pivot + stepRotation * (startPos - pivot);
            dieTransform.rotation = stepRotation * startRot;
            yield return null;
        }

        Quaternion finalRotation = Quaternion.AngleAxis(90f, axis) * startRot;
        Vector3 finalPosition = startPos + stepDirection * edgeLength;
        finalPosition.y = boardY;

        dieTransform.position = finalPosition;
        dieTransform.rotation = finalRotation;
    }

    IEnumerator SettleDieToRestCo(int dieIndex, Vector3 restPos, Quaternion baseRestRotation, Vector3 lastMoveDirection)
    {
        if (_dice[dieIndex] == null) yield break;

        Transform dieTransform = _dice[dieIndex].transform;
        Quaternion startRot = dieTransform.rotation;
        Quaternion targetRot = GetNaturalRestRotation(startRot, baseRestRotation);
        float angle = Quaternion.Angle(startRot, targetRot);
        Vector3 startPos = dieTransform.position;

        Vector3 targetPos = restPos;
        targetPos.y = restPos.y;

        Vector3 finalMoveDirection = lastMoveDirection;
        finalMoveDirection.y = 0f;
        if (finalMoveDirection.sqrMagnitude <= 0.0001f)
            finalMoveDirection = targetPos - startPos;
        finalMoveDirection.y = 0f;

        bool hasFinalMoveDirection = finalMoveDirection.sqrMagnitude > 0.0001f;
        if (hasFinalMoveDirection)
            finalMoveDirection.Normalize();

        float slideDistance = Vector3.Distance(startPos, targetPos);
        bool hasVisibleSlide = slideDistance >= 0.04f;
        float edgeLength = GetDieEdgeLength(dieIndex);
        float overshootDistance = hasFinalMoveDirection
            ? (hasVisibleSlide
                ? Mathf.Min(edgeLength * 0.32f, Mathf.Max(0.05f, slideDistance * 0.48f))
                : edgeLength * 0.11f)
            : 0f;
        bool hasOvershoot = overshootDistance >= 0.01f;

        if (angle <= 0.1f && !hasVisibleSlide && !hasOvershoot)
        {
            dieTransform.position = targetPos;
            dieTransform.rotation = targetRot;
            yield break;
        }

        float angleFactor = Mathf.Clamp01(angle / 55f);
        float slideFactor = Mathf.Clamp01(slideDistance / 0.24f);

        float settleDuration = Mathf.Lerp(0.04f, 0.08f, angleFactor);
        Vector3 settlePos = hasVisibleSlide
            ? Vector3.Lerp(startPos, targetPos, 0.18f)
            : targetPos;

        float elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOut(Mathf.Clamp01(elapsed / settleDuration));
            dieTransform.position = Vector3.Lerp(startPos, settlePos, t);
            dieTransform.rotation = Quaternion.Slerp(startRot, targetRot, t);
            yield return null;
        }

        dieTransform.position = settlePos;
        dieTransform.rotation = targetRot;

        if (!hasVisibleSlide && !hasOvershoot)
            yield break;

        Vector3 approachTargetPos = hasOvershoot
            ? targetPos + finalMoveDirection * overshootDistance
            : targetPos;

        float slideDuration = hasVisibleSlide
            ? Mathf.Lerp(0.11f, 0.2f, slideFactor)
            : Mathf.Lerp(0.05f, 0.09f, Mathf.Clamp01(overshootDistance / Mathf.Max(edgeLength * 0.32f, 0.001f)));
        elapsed = 0f;
        Vector3 slideStartPos = dieTransform.position;

        while (elapsed < slideDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOut(Mathf.Clamp01(elapsed / slideDuration));
            dieTransform.position = Vector3.Lerp(slideStartPos, approachTargetPos, t);
            dieTransform.rotation = targetRot;
            yield return null;
        }

        dieTransform.position = approachTargetPos;
        dieTransform.rotation = targetRot;

        if (!hasOvershoot)
        {
            dieTransform.position = targetPos;
            dieTransform.rotation = targetRot;
            yield break;
        }

        float reboundDuration = Mathf.Lerp(0.05f, 0.1f, Mathf.Clamp01(overshootDistance / Mathf.Max(edgeLength * 0.32f, 0.001f)));
        elapsed = 0f;
        Vector3 reboundStartPos = dieTransform.position;

        while (elapsed < reboundDuration)
        {
            elapsed += Time.deltaTime;
            float t = EaseOut(Mathf.Clamp01(elapsed / reboundDuration));
            dieTransform.position = Vector3.Lerp(reboundStartPos, targetPos, t);
            dieTransform.rotation = targetRot;
            yield return null;
        }

        dieTransform.position = targetPos;
        dieTransform.rotation = targetRot;
    }

    Quaternion GetNaturalRestRotation(Quaternion currentRotation, Quaternion baseRestRotation)
    {
        Vector3 localReferenceAxis = GetRestReferenceLocalAxis(baseRestRotation);
        Vector3 baseReference = Vector3.ProjectOnPlane(baseRestRotation * localReferenceAxis, Vector3.up);
        Vector3 currentReference = Vector3.ProjectOnPlane(currentRotation * localReferenceAxis, Vector3.up);

        if (baseReference.sqrMagnitude <= 0.0001f || currentReference.sqrMagnitude <= 0.0001f)
            return baseRestRotation;

        float yawDelta = Vector3.SignedAngle(baseReference.normalized, currentReference.normalized, Vector3.up);
        return Quaternion.AngleAxis(yawDelta, Vector3.up) * baseRestRotation;
    }

    Vector3 GetRestReferenceLocalAxis(Quaternion baseRestRotation)
    {
        Vector3 bestAxis = Vector3.forward;
        float bestMagnitude = 0f;
        Vector3[] candidates = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };

        for (int i = 0; i < candidates.Length; i++)
        {
            Vector3 projected = Vector3.ProjectOnPlane(baseRestRotation * candidates[i], Vector3.up);
            float magnitude = projected.sqrMagnitude;
            if (magnitude > bestMagnitude)
            {
                bestMagnitude = magnitude;
                bestAxis = candidates[i];
            }
        }

        return bestAxis;
    }

    System.Collections.Generic.List<Vector3> CompactShortSegments(
        System.Collections.Generic.List<Vector3> path,
        float minSegmentDistance,
        float leftX,
        float rightX,
        float bottomZ,
        float topZ,
        float boardY)
    {
        if (path == null || path.Count <= 2) return path;

        var compacted = new System.Collections.Generic.List<Vector3> { path[0] };
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 current = path[i];
            Vector3 last = compacted[compacted.Count - 1];
            bool isLast = i == path.Count - 1;

            if (Vector3.Distance(last, current) >= minSegmentDistance)
            {
                compacted.Add(current);
                continue;
            }

            if (!isLast) continue;

            Vector3 pushDir = compacted.Count >= 2
                ? (last - compacted[compacted.Count - 2]).normalized
                : (current - last).normalized;

            if (pushDir.sqrMagnitude < 0.001f)
                pushDir = Vector3.left;

            Vector3 nudged = last + pushDir * minSegmentDistance;
            nudged.x = Mathf.Clamp(nudged.x, leftX, rightX);
            nudged.z = Mathf.Clamp(nudged.z, bottomZ, topZ);
            nudged.y = boardY;
            compacted.Add(nudged);
        }

        return compacted.Count >= 2 ? compacted : path;
    }

    System.Collections.Generic.List<Vector3> SubdivideLongSegments(
        System.Collections.Generic.List<Vector3> path,
        float maxSegmentDistance,
        float boardY)
    {
        if (path == null || path.Count <= 1) return path;
        if (maxSegmentDistance <= 0.001f) return path;

        var subdivided = new System.Collections.Generic.List<Vector3> { path[0] };
        for (int i = 1; i < path.Count; i++)
        {
            Vector3 from = subdivided[subdivided.Count - 1];
            Vector3 to = path[i];
            float distance = Vector3.Distance(from, to);

            if (distance <= maxSegmentDistance)
            {
                subdivided.Add(to);
                continue;
            }

            int steps = Mathf.CeilToInt(distance / maxSegmentDistance);
            for (int step = 1; step <= steps; step++)
            {
                float t = step / (float)steps;
                Vector3 point = Vector3.Lerp(from, to, t);
                point.y = boardY;
                subdivided.Add(point);
            }
        }

        return subdivided;
    }

    float TotalPathDistance(System.Collections.Generic.List<Vector3> path)
    {
        if (path == null || path.Count < 2) return 0f;

        float total = 0f;
        for (int i = 1; i < path.Count; i++)
            total += Vector3.Distance(path[i - 1], path[i]);

        return total;
    }

    float DistanceAlongPath(System.Collections.Generic.List<Vector3> path, int segmentExclusiveEnd)
    {
        if (path == null || path.Count < 2 || segmentExclusiveEnd <= 0) return 0f;

        float total = 0f;
        int maxSegment = Mathf.Min(segmentExclusiveEnd, path.Count - 1);
        for (int i = 1; i <= maxSegment; i++)
            total += Vector3.Distance(path[i - 1], path[i]);

        return total;
    }

    float EstimateDesiredEdgeRollDuration(float segDuration, int plannerSegments)
    {
        float effectiveSegments = Mathf.Clamp(plannerSegments, 2, 6);
        return segDuration * (effectiveSegments * 0.72f + 0.95f);
    }

    float EvaluateRollSpeedFactor(float progress)
    {
        float shaped = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(progress));
        return Mathf.Lerp(1.34f, 0.58f, shaped);
    }

    float ApplyRollProgressCurve(float t, float overallProgress, bool isLastSegment)
    {
        float clamped = Mathf.Clamp01(t);
        float lateBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.45f, 1f, overallProgress));
        float curved = Mathf.Lerp(clamped, EaseOut(clamped), lateBlend);
        return isLastSegment ? EaseOut(curved) : curved;
    }

    float AverageSegmentDistance(System.Collections.Generic.List<Vector3> path)
    {
        if (path == null || path.Count < 2) return 0f;

        float total = 0f;
        for (int i = 1; i < path.Count; i++)
            total += Vector3.Distance(path[i - 1], path[i]);

        return total / (path.Count - 1);
    }

    static float EaseInOut(float t) => t * t * (3f - 2f * t);
    static float EaseIn(float t)    => t * t;           // 가속 (벽 도달 시 빠름 → 자연스러운 튕김)
    static float EaseOut(float t)   => t * (2f - t);    // 감속 (착지 시 부드럽게 정착)
}
