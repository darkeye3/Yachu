using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if DOTWEEN
using DG.Tweening;
#endif

public class TimerUI : MonoBehaviour
{
    [SerializeField] private Image           cloverIcon;
    [SerializeField] private TextMeshProUGUI timerText;

    private float   _remaining;
    private bool    _isRunning;
    private bool    _warningActive;
#if DOTWEEN
    private Tweener _punchTween;
#endif

    private static readonly Color NormalColor  = Color.white;
    private static readonly Color WarningColor = new Color(1f, 0.2f, 0.2f);

    public System.Action OnTimeUp;

    void Awake()
    {
        // 이름으로 정확히 탐색
        if (timerText == null)
        {
            var t = transform.Find("TimerText");
            if (t != null) timerText = t.GetComponent<TextMeshProUGUI>();
        }
        if (timerText == null) timerText = GetComponentInChildren<TextMeshProUGUI>(true);

        if (cloverIcon == null)
        {
            var t = transform.Find("CloverIcon");
            if (t != null) cloverIcon = t.GetComponent<Image>();
        }
        // Image fallback은 자기 자신 제외하고 탐색
        if (cloverIcon == null)
        {
            foreach (var img in GetComponentsInChildren<Image>(true))
            {
                if (img.gameObject != gameObject) { cloverIcon = img; break; }
            }
        }
        Debug.Log($"[TimerUI] Awake — timerText={timerText?.name ?? "null"}, cloverIcon={cloverIcon?.name ?? "null"}");
        // cloverIcon 색은 코드로 건드리지 않음 — Inspector 설정값 유지
    }

    public void StartTimer(float seconds)
    {
        _remaining     = seconds;
        _isRunning     = true;
        _warningActive = false;
#if DOTWEEN
        _punchTween?.Kill();
#endif
        SetColor(NormalColor);
        if (timerText) timerText.text = Mathf.CeilToInt(seconds).ToString(); // 즉시 표시
    }

    public void StopTimer()
    {
        _isRunning = false;
#if DOTWEEN
        _punchTween?.Kill();
#endif
        SetColor(NormalColor);
    }

    public void PauseTimer(bool pause) => _isRunning = !pause;

    void Update()
    {
        if (!_isRunning) return;

        _remaining -= Time.deltaTime;
        if (timerText) timerText.text = Mathf.CeilToInt(Mathf.Max(0f, _remaining - 0.001f)).ToString();

        if (_remaining <= 5f && !_warningActive)
        {
            _warningActive = true;
            SetColor(WarningColor);
#if DOTWEEN
            _punchTween = timerText?.transform
                .DOPunchScale(Vector3.one * 0.2f, 0.5f, 5)
                .SetLoops(-1);
#endif
            AudioManager.Play("timer_warning");
        }

        if (_remaining <= 0f)
        {
            _remaining = 0f;
            _isRunning = false;
#if DOTWEEN
            _punchTween?.Kill();
#endif
            OnTimeUp?.Invoke();
        }
    }

    void SetColor(Color c)
    {
        if (timerText) timerText.color = c;
    }
}
