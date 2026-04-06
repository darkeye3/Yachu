using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 붙인 GameObject(CupButton)의 anchoredPosition을 좌우로 이동시켜 흔드는 컴포넌트.
/// DiceBox3D.SetCupAngle()을 동시 호출해 3D 컵과 동기화.
/// </summary>
public class UICupShaker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("쉐이크 설정")]
    [SerializeField] private float   stepDuration = 0.15f;                  // 스텝당 이동 시간 (0.15 × 10스텝 = 1.5초)
    [SerializeField] private int     maxRepeat    = 5;                       // 최대 반복 횟수
    [SerializeField] private Vector2 posA        = new Vector2(-75f, -45f); // 1번째 위치
    [SerializeField] private Vector2 posB        = new Vector2(-38f,  85f); // 2번째 위치
    [SerializeField] private float   randomRange  = 5f;                     // 위치 랜덤 편차 (0 = 고정)

    [Header("컵 숨김 애니메이션")]
    [SerializeField] private float hideMoveDuration = 0.25f;  // 숨김/복원 이동 시간

    [Header("굴림 횟수 표시")]
    [SerializeField] private TextMeshProUGUI rollCountText;

    [Header("게이지")]
    [SerializeField] private RadialGauge radialGauge;

    private RectTransform  _rect;
    private Vector2        _originPos;
    private RectTransform  _parentRect;
    private Vector2        _parentOriginPos;
    private DiceBox3D      _diceBox3D;
    private DiceController _diceController;
    private Coroutine      _shakeRoutine;
    private Coroutine      _hideRoutine;
    private bool                              _autoThrown;    // 자동 완료로 이미 던진 경우 OnPointerUp 중복 방지
    private bool                              _shakeStarted;  // StartShake 호출 여부 — 미호출 시 StopShake 무시
    private bool                              _interactable = true;
    private UnityEngine.UI.Button             _button;

    public float LastGaugeValue { get; private set; } = 1f;

    void Awake()
    {
        _rect   = GetComponent<RectTransform>();
        _button = GetComponent<UnityEngine.UI.Button>();
    }

    void Start()
    {
        _originPos      = _rect.anchoredPosition;
        _diceBox3D      = FindObjectOfType<DiceBox3D>();
        _diceController = FindObjectOfType<DiceController>();

        _parentRect = transform.parent?.GetComponent<RectTransform>();
        if (_parentRect != null) _parentOriginPos = _parentRect.anchoredPosition;
    }

    public void UpdateRollCountBadge()
    {
        if (rollCountText == null || _diceController == null) return;
        int max  = GameManager.Instance?.Settings?.maxRollCount ?? 3;
        int left = max - _diceController.RollCount;
        rollCountText.text = left > 0 ? $"굴리기 {left}회 남음" : "";
    }

    public void SetInteractable(bool on)
    {
        _interactable = on;
        if (_button != null) _button.interactable = on;
        if (!on && _shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
            _rect.anchoredPosition = _originPos;
            _diceBox3D?.ResetCupPosition();
            _diceBox3D?.StopDiceJitter();
        }

    }

    public void HideCup()
    {
        if (_parentRect == null) return;
        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(MoveParentX(new Vector2(0f, _parentOriginPos.y)));
    }

    public void ShowCup()
    {
        if (_parentRect == null) return;
        if (_hideRoutine != null) StopCoroutine(_hideRoutine);
        _hideRoutine = StartCoroutine(MoveParentX(_parentOriginPos));
        // 이전 턴에서 보드에 남은 주사위를 모두 숨기고 컵 주사위를 초기화
        _diceBox3D?.ReturnAllToCup();
    }

    IEnumerator MoveParentX(Vector2 target)
    {
        Vector2 start   = _parentRect.anchoredPosition;
        float   elapsed = 0f;
        while (elapsed < hideMoveDuration)
        {
            elapsed += Time.deltaTime;
            _parentRect.anchoredPosition = Vector2.Lerp(start, target, Mathf.SmoothStep(0f, 1f, elapsed / hideMoveDuration));
            yield return null;
        }
        _parentRect.anchoredPosition = target;
        _hideRoutine = null;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!_interactable) return;
        StartShake();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!_shakeStarted) return;   // StartShake 없이 손 뗀 경우 무시
        StopShake();
    }

    public void StartShake()
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);
        _autoThrown   = false;
        _shakeStarted = true;
        _shakeRoutine = StartCoroutine(ShakeCoroutine());
        _diceBox3D?.ClearNonKeptOverlays();
        _diceBox3D?.ShowNonKeptCupDice();
        _diceBox3D?.StartDiceJitter();
        if (radialGauge != null) { radialGauge.gameObject.SetActive(true); radialGauge.SetValue(0f); }
    }

    public void StopShake()
    {
        if (_autoThrown) return;  // 자동 완료로 이미 처리됨
        _shakeStarted = false;
        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }
        _rect.anchoredPosition = _originPos;
        _diceBox3D?.ResetCupPosition();
        _diceBox3D?.StopDiceJitter();
        _interactable = false;  // 오버레이 표시 전까지 차단
        LastGaugeValue = radialGauge != null ? radialGauge.GaugeValue : 1f;
        if (radialGauge != null) { radialGauge.SetValue(0f); radialGauge.gameObject.SetActive(false); }
        HideCup();
        _diceController?.Roll();
    }

    IEnumerator ShakeCoroutine()
    {
        Vector2 currentOffset = _originPos;

        int totalSteps    = maxRepeat * 2;  // A→B 이동이 rep당 2스텝
        int completedSteps = 0;

        for (int rep = 0; rep < maxRepeat; rep++)
        {
            // A → B 순서로 이동
            Vector2[] targets = {
                posA + Random.insideUnitCircle * randomRange,
                posB + Random.insideUnitCircle * randomRange,
            };
            foreach (Vector2 target in targets)
            {
                float   elapsed     = 0f;
                Vector2 startOffset = currentOffset;

                while (elapsed < stepDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / stepDuration));
                    currentOffset = Vector2.Lerp(startOffset, target, t);

                    _rect.anchoredPosition = currentOffset;
                    _diceBox3D?.SetCupPosition(currentOffset - _originPos);

                    // 게이지 진행도 업데이트
                    radialGauge?.SetValue((completedSteps + t) / totalSteps);

                    yield return null;
                }
                currentOffset = target;
                completedSteps++;
            }
        }

        // 자동 완료 — 중복 방지 플래그 세운 후 처리
        _autoThrown   = true;
        _shakeStarted = false;
        _shakeRoutine = null;
        _rect.anchoredPosition = _originPos;
        _diceBox3D?.ResetCupPosition();
        _diceBox3D?.StopDiceJitter();
        _interactable = false;  // 오버레이 표시 전까지 차단
        LastGaugeValue = 1f;   // 자동완료 = 게이지 꽉 참
        if (radialGauge != null) { radialGauge.SetValue(0f); radialGauge.gameObject.SetActive(false); }
        HideCup();
        _diceController?.Roll();
    }
}
