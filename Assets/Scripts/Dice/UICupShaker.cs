using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class UICupShaker : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("Shake")]
    [SerializeField] private float stepDuration = 0.15f;
    [SerializeField] private int maxRepeat = 5;
    [SerializeField] private Vector2 posA = new Vector2(-75f, -45f);
    [SerializeField] private Vector2 posB = new Vector2(-38f, 85f);
    [SerializeField] private float randomRange = 5f;

    [Header("Hide/Show")]
    [SerializeField] private float hideMoveDuration = 0.25f;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI rollCountText;
    [SerializeField] private RadialGauge radialGauge;

    private RectTransform _rect;
    private Vector2 _originPos;
    private RectTransform _parentRect;
    private Vector2 _parentOriginPos;
    private DiceBox3D _diceBox3D;
    private DiceController _diceController;
    private NetworkDiceRPC _networkDiceRpc;
    private Coroutine _shakeRoutine;
    private Coroutine _hideRoutine;
    private bool _autoThrown;
    private bool _shakeStarted;
    private bool _interactable = true;
    private UnityEngine.UI.Button _button;
    private bool _suppressNetworkBroadcast;

    public float LastGaugeValue { get; private set; } = 1f;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _button = GetComponent<UnityEngine.UI.Button>();
    }

    void Start()
    {
        _originPos = _rect.anchoredPosition;
        _diceBox3D = FindObjectOfType<DiceBox3D>();
        _diceController = FindObjectOfType<DiceController>();
        _networkDiceRpc = FindObjectOfType<NetworkDiceRPC>();

        _parentRect = transform.parent?.GetComponent<RectTransform>();
        if (_parentRect != null) _parentOriginPos = _parentRect.anchoredPosition;
    }

    public void UpdateRollCountBadge()
    {
        if (rollCountText == null || _diceController == null) return;

        int max = GameManager.Instance?.Settings?.maxRollCount ?? 3;
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
    }

    IEnumerator MoveParentX(Vector2 target)
    {
        Vector2 start = _parentRect.anchoredPosition;
        float elapsed = 0f;
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
        if (!_shakeStarted) return;
        StopShake();
    }

    public void StartShake()
    {
        StartShakeInternal(false);
    }

    public void StartRemoteShakeVisual()
    {
        StartShakeInternal(true);
    }

    void StartShakeInternal(bool suppressNetworkBroadcast)
    {
        if (_shakeRoutine != null) StopCoroutine(_shakeRoutine);

        _suppressNetworkBroadcast = suppressNetworkBroadcast;
        _autoThrown = false;
        _shakeStarted = true;
        _shakeRoutine = suppressNetworkBroadcast
            ? StartCoroutine(ShakeCoroutineVisualOnly())
            : StartCoroutine(ShakeCoroutine());

        _diceBox3D?.ClearNonKeptOverlays();
        _diceBox3D?.ShowNonKeptCupDice();
        _diceBox3D?.StartDiceJitter();

        if (radialGauge != null)
        {
            radialGauge.gameObject.SetActive(true);
            radialGauge.SetValue(0f);
        }

        if (!suppressNetworkBroadcast && GameManager.Instance != null && GameManager.Instance.IsOnline)
            _networkDiceRpc?.SendCupShakeStart();
    }

    public void StopShake()
    {
        StopShakeInternal(false, radialGauge != null ? radialGauge.GaugeValue : 1f);
    }

    public void StopRemoteShakeVisual(float gaugeValue)
    {
        StopShakeInternal(true, gaugeValue);
    }

    void StopShakeInternal(bool suppressNetworkBroadcast, float gaugeValue)
    {
        if (_autoThrown && !suppressNetworkBroadcast) return;

        _shakeStarted = false;
        _autoThrown = suppressNetworkBroadcast;

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        _rect.anchoredPosition = _originPos;
        _diceBox3D?.ResetCupPosition();
        _diceBox3D?.StopDiceJitter();
        _interactable = false;
        LastGaugeValue = Mathf.Clamp01(gaugeValue);

        if (radialGauge != null)
        {
            radialGauge.SetValue(0f);
            radialGauge.gameObject.SetActive(false);
        }

        HideCup();

        if (!suppressNetworkBroadcast && GameManager.Instance != null && GameManager.Instance.IsOnline)
            _networkDiceRpc?.SendCupShakeStop(LastGaugeValue);

        if (!suppressNetworkBroadcast)
            _diceController?.Roll();
    }

    IEnumerator ShakeCoroutine()
    {
        Vector2 currentOffset = _originPos;
        int totalSteps = maxRepeat * 2;
        int completedSteps = 0;

        for (int rep = 0; rep < maxRepeat; rep++)
        {
            Vector2[] targets =
            {
                posA + Random.insideUnitCircle * randomRange,
                posB + Random.insideUnitCircle * randomRange,
            };

            foreach (Vector2 target in targets)
            {
                float elapsed = 0f;
                Vector2 startOffset = currentOffset;

                while (elapsed < stepDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / stepDuration));
                    currentOffset = Vector2.Lerp(startOffset, target, t);

                    _rect.anchoredPosition = currentOffset;
                    _diceBox3D?.SetCupPosition(currentOffset - _originPos);
                    radialGauge?.SetValue((completedSteps + t) / totalSteps);
                    yield return null;
                }

                currentOffset = target;
                completedSteps++;
            }
        }

        _autoThrown = true;
        _shakeStarted = false;
        _shakeRoutine = null;
        _rect.anchoredPosition = _originPos;
        _diceBox3D?.ResetCupPosition();
        _diceBox3D?.StopDiceJitter();
        _interactable = false;
        LastGaugeValue = 1f;

        if (radialGauge != null)
        {
            radialGauge.SetValue(0f);
            radialGauge.gameObject.SetActive(false);
        }

        HideCup();

        if (GameManager.Instance != null && GameManager.Instance.IsOnline)
            _networkDiceRpc?.SendCupShakeStop(LastGaugeValue);

        _diceController?.Roll();
    }

    IEnumerator ShakeCoroutineVisualOnly()
    {
        Vector2 currentOffset = _originPos;

        while (true)
        {
            Vector2[] targets =
            {
                posA + Random.insideUnitCircle * randomRange,
                posB + Random.insideUnitCircle * randomRange,
            };

            foreach (Vector2 target in targets)
            {
                float elapsed = 0f;
                Vector2 startOffset = currentOffset;

                while (elapsed < stepDuration)
                {
                    elapsed += Time.deltaTime;
                    float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / stepDuration));
                    currentOffset = Vector2.Lerp(startOffset, target, t);

                    _rect.anchoredPosition = currentOffset;
                    _diceBox3D?.SetCupPosition(currentOffset - _originPos);
                    yield return null;
                }

                currentOffset = target;
            }
        }
    }
}
