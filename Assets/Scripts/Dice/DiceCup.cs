using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
#if DOTWEEN
using DG.Tweening;
#endif
using TMPro;

public class DiceCup : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    // ─── Inspector 연결 ──────────────────────────────────────────────
    [SerializeField] private RadialGauge     gauge;
    [SerializeField] private Image           cupImage;
    [SerializeField] private Button          cupButton;
    [SerializeField] private CanvasGroup     cupCanvasGroup;
    [SerializeField] private TextMeshProUGUI rollCountText;
    [SerializeField] private TextMeshProUGUI hintText;
    [SerializeField] private DiceController  diceController;

    // ─── 상태 ────────────────────────────────────────────────────────
    private bool    _pressing;
    private bool    _interactable;
    private float   _gaugeValue;

    // 컵 기울기 연동
    private DiceBox3D _diceBox3D;
    private UnityEngine.UI.RawImage _cupDiceView;  // CupArea 안 CupDiceView RawImage
    private Coroutine _tiltRoutine;
    private Coroutine _uiShakeRoutine;
#if DOTWEEN
    private Tweener _shakeTween;
#endif

    private float ChargeSpeed => GameManager.Instance?.Settings?.gaugeChargeSpeed ?? 1.2f;
    private float DecaySpeed  => GameManager.Instance?.Settings?.gaugeDecaySpeed  ?? 0.8f;

    // ─── 이벤트 ──────────────────────────────────────────────────────
    public System.Action OnRollTriggered;

    // ─── 초기화 ──────────────────────────────────────────────────────
    void Start()
    {
        // Inspector 미연결 시 씬에서 자동 탐색
        if (diceController == null)
        {
            diceController = FindObjectOfType<DiceController>();
            if (diceController != null)
                Debug.Log("[DiceCup] DiceController를 씬에서 자동으로 찾았습니다. Inspector에 연결해주세요.");
        }

        // gauge 자동 탐색
        if (gauge == null) gauge = GetComponentInChildren<RadialGauge>(true);
        if (gauge == null) gauge = FindObjectOfType<RadialGauge>(true);

        Debug.Log($"[DiceCup] Start — cupButton={cupButton != null}, cupImage={cupImage != null}, diceController={diceController != null}, gauge={gauge != null}");
        if (cupButton      == null) Debug.LogWarning("[DiceCup] cupButton이 Inspector에 연결되지 않았습니다!");
        if (cupImage       == null) Debug.LogWarning("[DiceCup] cupImage가 Inspector에 연결되지 않았습니다!");
        if (diceController == null) Debug.LogError("[DiceCup] DiceController를 찾을 수 없습니다! 씬에 DiceController가 있는지 확인하세요.");
        if (gauge          == null) Debug.LogWarning("[DiceCup] gauge가 Inspector에 연결되지 않았습니다!");

        SetInteractable(true);
        if (hintText) hintText.gameObject.SetActive(false);
        UpdateRollCountBadge();

        // DiceBox3D를 즉시 탐색 (OnPointerDown에서 바로 StartPressShake 호출 가능하도록)
        _diceBox3D = FindObjectOfType<DiceBox3D>();
        if (_diceBox3D != null)
        {
            _diceBox3D.OnCupReturnStart += ShowCupDiceView;
            _diceBox3D.OnCupTipStart    += OnCupPourStart;
            _diceBox3D.OnCupTipEnd      += OnCupPourEnd;
        }
        StartCoroutine(HookDiceBox3D());
    }

    // ─── 늦게 생성되는 DiceBox3D 대응 + CupDiceView 탐색 ─────────────
    System.Collections.IEnumerator HookDiceBox3D()
    {
        yield return null;
        var late = FindObjectOfType<DiceBox3D>();
        if (late != null && late != _diceBox3D)
        {
            if (_diceBox3D != null)
            {
                _diceBox3D.OnCupReturnStart -= ShowCupDiceView;
                _diceBox3D.OnCupTipStart    -= OnCupPourStart;
                _diceBox3D.OnCupTipEnd      -= OnCupPourEnd;
            }
            _diceBox3D = late;
            _diceBox3D.OnCupReturnStart += ShowCupDiceView;
            _diceBox3D.OnCupTipStart    += OnCupPourStart;
            _diceBox3D.OnCupTipEnd      += OnCupPourEnd;
        }
        var setup = FindObjectOfType<DiceAreaCameraSetup>();
        _cupDiceView = setup?.CupDiceView;
    }

    void OnDestroy()
    {
        if (_diceBox3D != null)
        {
            _diceBox3D.OnCupReturnStart -= ShowCupDiceView;
            _diceBox3D.OnCupTipStart    -= OnCupPourStart;
            _diceBox3D.OnCupTipEnd      -= OnCupPourEnd;
        }
    }

    // ─── 컵 주사위 RawImage 표시 ─────────────────────────────────────
    void ShowCupDiceView()
    {
        if (_cupDiceView) _cupDiceView.gameObject.SetActive(true);
    }

    // ─── 쏟아내기 시작: RawImage 숨기고 컵 이미지 기울이기 ──────────
    void OnCupPourStart()
    {
        if (_cupDiceView) _cupDiceView.gameObject.SetActive(false);
#if DOTWEEN
        if (cupImage) DG.Tweening.DOTween.Kill(cupImage.transform);
#endif
        if (_tiltRoutine != null) StopCoroutine(_tiltRoutine);
        _tiltRoutine = StartCoroutine(TiltCupImageTo(-115f, 0.35f));
    }

    // ─── 쏟아내기 종료: 컵 이미지 원위치 ───────────────────────────
    void OnCupPourEnd()
    {
#if DOTWEEN
        if (cupImage) DG.Tweening.DOTween.Kill(cupImage.transform);
#endif
        if (_tiltRoutine != null) StopCoroutine(_tiltRoutine);
        _tiltRoutine = StartCoroutine(TiltCupImageTo(0f, 0.3f));
    }

    // ─── 컵 이미지 회전 코루틴 (DOTween 없이도 동작) ────────────────
    System.Collections.IEnumerator TiltCupImageTo(float targetAngle, float duration)
    {
        if (cupImage == null) yield break;
        float startAngle = cupImage.transform.localEulerAngles.z;
        if (startAngle > 180f) startAngle -= 360f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            cupImage.transform.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(startAngle, targetAngle, t));
            yield return null;
        }
        cupImage.transform.localEulerAngles = new Vector3(0f, 0f, targetAngle);
    }

    // ─── 매 프레임 게이지 업데이트 ───────────────────────────────────
    void Update()
    {
        if (_pressing)
        {
            _gaugeValue = Mathf.Min(1f, _gaugeValue + ChargeSpeed * Time.deltaTime);
            gauge?.SetValue(_gaugeValue);
            PlayShakeSFX(_gaugeValue);

            // 게이지 꽉 차면 자동 굴림
            if (_gaugeValue >= 1f)
            {
                _pressing = false;
                TriggerRoll();
            }
        }
        else if (_gaugeValue > 0f)
        {
            _gaugeValue = Mathf.Max(0f, _gaugeValue - DecaySpeed * Time.deltaTime);
            gauge?.SetValue(_gaugeValue);
            if (_gaugeValue == 0f) StopShake();
        }
    }

    // ─── 포인터 이벤트 ───────────────────────────────────────────────
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[DiceCup] OnPointerDown — _interactable={_interactable}");
        if (!_interactable) return;
        _pressing = true;
        StartUIShake();
        PlayShakeSFX(_gaugeValue);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log($"[DiceCup] OnPointerUp — _pressing={_pressing}, _gaugeValue={_gaugeValue:F2}");
        if (!_pressing) return;
        _pressing = false;
        // 탭 한 번으로도 굴리기 가능 (게이지 무관)
        TriggerRoll();
    }

    // ─── 굴림 트리거 ─────────────────────────────────────────────────
    void TriggerRoll()
    {
        Debug.Log($"[DiceCup] TriggerRoll 호출 — diceController={diceController != null}, CanRoll={diceController?.CanRoll}");
        StopUIShake();
        StopShake();
        _gaugeValue = 0f;
        gauge?.SetValue(0f);

        diceController?.Roll();
        OnRollTriggered?.Invoke();
        UpdateRollCountBadge();

        // 컵 기울이기 연출
#if DOTWEEN
        if (cupImage)
        {
            cupImage.transform.DORotate(new Vector3(0, 0, -45f), 0.15f)
                .OnComplete(() =>
                    cupImage.transform.DORotate(Vector3.zero, 0.2f));
        }
#endif

        AudioManager.Play("cup_pour");

        // 첫 굴리기 시 힌트 숨기기
        if (hintText && hintText.gameObject.activeSelf)
            hintText.gameObject.SetActive(false);
    }

    // ─── UI 주도 쉐이크 ──────────────────────────────────────────────
    void StartUIShake()
    {
        if (_uiShakeRoutine != null) StopCoroutine(_uiShakeRoutine);
        _uiShakeRoutine = StartCoroutine(UIShakeCoroutine());
    }

    void StopUIShake()
    {
        if (_uiShakeRoutine != null)
        {
            StopCoroutine(_uiShakeRoutine);
            _uiShakeRoutine = null;
        }
        if (cupImage) cupImage.transform.localEulerAngles = Vector3.zero;
        _diceBox3D?.ResetCupAngle();
    }

    // 누르는 동안 루프: 0.3s마다 랜덤 방향·각도(20~35°)로 SmoothStep 이동
    System.Collections.IEnumerator UIShakeCoroutine()
    {
        float stepDuration = 0.3f;
        float currentAngle = 0f;

        while (true)
        {
            float sign        = Random.value > 0.5f ? 1f : -1f;
            float targetAngle = sign * Random.Range(20f, 35f);
            float elapsed     = 0f;
            float startAngle  = currentAngle;

            while (elapsed < stepDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / stepDuration));
                currentAngle = Mathf.Lerp(startAngle, targetAngle, t);
                if (cupImage) cupImage.transform.localEulerAngles = new Vector3(0f, 0f, currentAngle);
                _diceBox3D?.SetCupAngle(currentAngle);
                yield return null;
            }
            currentAngle = targetAngle;
        }
    }

    void PlayShakeSFX(float g)
    {
        if      (g < 0.3f) AudioManager.Play("cup_shake_light", Mathf.Lerp(0.1f, 0.4f, g / 0.3f));
        else if (g < 0.7f) AudioManager.Play("cup_shake_mid",   Mathf.Lerp(0.4f, 0.7f, (g - 0.3f) / 0.4f));
        else               AudioManager.Play("cup_shake_heavy",  Mathf.Lerp(0.7f, 1.0f, (g - 0.7f) / 0.3f));
    }

    void StopShake()
    {
#if DOTWEEN
        _shakeTween?.Kill();
        if (cupImage) cupImage.transform.DORotate(Vector3.zero, 0.1f);
#else
        if (cupImage) cupImage.transform.localRotation = Quaternion.identity;
#endif
        _diceBox3D?.ResetCupAngle();
    }

    // ─── 활성/비활성 제어 ────────────────────────────────────────────
    public void SetInteractable(bool on)
    {
        _interactable = on;
        if (!on) { _pressing = false; StopUIShake(); }
        if (cupButton)      cupButton.interactable = on;
        if (cupCanvasGroup) cupCanvasGroup.alpha    = 1f;
    }

    public void SetFirstRollHint(bool show)
    {
        if (hintText) hintText.gameObject.SetActive(show);
    }

    // ─── 굴리기 횟수 뱃지 ────────────────────────────────────────────
    public void UpdateRollCountBadge()
    {
        if (rollCountText == null || diceController == null) return;
        int max  = GameManager.Instance?.Settings?.maxRollCount ?? 3;
        int left = max - diceController.RollCount;
        rollCountText.text = $"굴리기 {left}회 남음";
    }
}
