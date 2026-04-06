using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 풀하우스 / 라지스트레이트 / 요트 달성 시 팟! 축하 배너.
/// Canvas 자식 빈 오브젝트에 붙이면 UI를 자동으로 구성합니다.
/// </summary>
public class CelebrationBannerUI : MonoBehaviour
{
    [Header("연결 (비워두면 자동 생성)")]
    [SerializeField] private TextMeshProUGUI titleText;   // "YACHT!" 등
    [SerializeField] private TextMeshProUGUI scoreText;   // "+50"
    [SerializeField] private Image           bgImage;

    [Header("타이밍 (초)")]
    [SerializeField] private float popDuration   = 0.18f;  // 튀어오르는 시간
    [SerializeField] private float holdDuration  = 0.9f;   // 보여주는 시간
    [SerializeField] private float fadeDuration  = 0.35f;  // 사라지는 시간

    [Header("스케일")]
    [SerializeField] private float peakScale     = 1.15f;  // 최대 크기
    [SerializeField] private float settleScale   = 1.0f;   // 정착 크기

    [Header("디자인")]
    [SerializeField] private float panelWidth    = 520f;
    [SerializeField] private float panelHeight   = 180f;
    [SerializeField] private float titleFontSize = 72f;
    [SerializeField] private float scoreFontSize = 36f;

    // 카테고리별 설정
    private static readonly (string label, Color color)[] CategoryInfo =
    {
        // 인덱스 5 = 풀하우스
        ("FULL HOUSE!", new Color(0.20f, 0.70f, 1.00f)),   // 하늘색
        // 인덱스 6 = 라지스트레이트
        ("LARGE STRAIGHT!", new Color(0.40f, 0.90f, 0.40f)),  // 초록
        // 인덱스 7 = 요트
        ("YACHT!",          new Color(1.00f, 0.85f, 0.10f)),  // 금색
    };

    private RectTransform _rect;
    private Canvas        _canvas;
    private CanvasGroup   _group;

    // ─── 초기화 ──────────────────────────────────────────────────────
    void Awake()
    {
        _canvas = GetComponentInParent<Canvas>(true);
        _rect   = GetComponent<RectTransform>();
        if (_rect  == null) _rect  = gameObject.AddComponent<RectTransform>();
        if (_group == null) _group = GetComponent<CanvasGroup>();
        if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();

        // 컵·오버레이보다 위에 렌더링
        var ownCanvas = GetComponent<Canvas>();
        if (ownCanvas == null) ownCanvas = gameObject.AddComponent<Canvas>();
        ownCanvas.overrideSorting = true;
        ownCanvas.sortingOrder    = 200;
        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        SetupRect();
        SetupBackground();
        SetupTexts();

        _group.alpha = 0f;
        gameObject.SetActive(false);
    }

    void SetupRect()
    {
        _rect.anchorMin        = new Vector2(0.5f, 0.5f);
        _rect.anchorMax        = new Vector2(0.5f, 0.5f);
        _rect.pivot            = new Vector2(0.5f, 0.5f);
        _rect.anchoredPosition = Vector2.zero;
        _rect.sizeDelta        = new Vector2(panelWidth, panelHeight);
        _rect.localScale       = Vector3.zero;
    }

    void SetupBackground()
    {
        if (bgImage == null) bgImage = GetComponent<Image>();
        if (bgImage == null) bgImage = gameObject.AddComponent<Image>();
        bgImage.color = new Color(0.05f, 0.05f, 0.05f, 0.92f);
    }

    void SetupTexts()
    {
        // titleText
        if (titleText == null)
        {
            var go = new GameObject("TitleText");
            go.transform.SetParent(transform, false);
            titleText = go.AddComponent<TextMeshProUGUI>();
            var tr = go.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 0.42f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.offsetMin = new Vector2(20f, 0f);
            tr.offsetMax = new Vector2(-20f, 0f);
        }
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.fontSize  = titleFontSize;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color     = Color.white;

        // scoreText
        if (scoreText == null)
        {
            var go = new GameObject("ScoreText");
            go.transform.SetParent(transform, false);
            scoreText = go.AddComponent<TextMeshProUGUI>();
            var tr = go.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 0f);
            tr.anchorMax = new Vector2(1f, 0.46f);
            tr.offsetMin = new Vector2(20f, 0f);
            tr.offsetMax = new Vector2(-20f, 0f);
        }
        scoreText.alignment = TextAlignmentOptions.Center;
        scoreText.fontSize  = scoreFontSize;
        scoreText.fontStyle = FontStyles.Normal;
        scoreText.color     = new Color(1f, 1f, 0.6f);
    }

    // ─── 공개 API ────────────────────────────────────────────────────
    /// <param name="categoryIndex">5=풀하우스 / 6=라지스트레이트 / 7=요트</param>
    /// <param name="score">획득 점수</param>
    /// <param name="categoryIndex">5=풀하우스 / 6=라지스트레이트 / 7=요트</param>
    /// <param name="score">획득 점수</param>
    /// <param name="onComplete">애니메이션 완료 후 호출할 콜백</param>
    public void Show(int categoryIndex, int score, System.Action onComplete = null)
    {
        int infoIdx = categoryIndex - 5;  // 5,6,7 → 0,1,2
        if (infoIdx < 0 || infoIdx >= CategoryInfo.Length)
        {
            onComplete?.Invoke();
            return;
        }

        var (label, color) = CategoryInfo[infoIdx];
        if (titleText) { titleText.text = label; titleText.color = color; }
        if (scoreText)   scoreText.text = $"+{score}점 획득!";
        if (bgImage)
        {
            var bg = bgImage.color;
            bg.r = color.r * 0.15f;
            bg.g = color.g * 0.15f;
            bg.b = color.b * 0.15f;
            bg.a = 0.92f;
            bgImage.color = bg;
        }

        // 텍스트 길이에 맞춰 패널 너비 자동 조정
        FitPanelToText();

        StopAllCoroutines();
        gameObject.SetActive(true);
        StartCoroutine(Animate(onComplete));
    }

    // ─── 패널 크기 자동 조정 ─────────────────────────────────────────
    void FitPanelToText()
    {
        const float padding = 100f;  // 좌우 여백 합계

        // ForceMeshUpdate: TMP가 레이아웃을 즉시 계산하게 강제
        if (titleText != null) titleText.ForceMeshUpdate();
        if (scoreText != null) scoreText.ForceMeshUpdate();

        float titleW = titleText != null
            ? titleText.GetPreferredValues(titleText.text, float.MaxValue, float.MaxValue).x
            : 0f;
        float scoreW = scoreText != null
            ? scoreText.GetPreferredValues(scoreText.text, float.MaxValue, float.MaxValue).x
            : 0f;

        float needed = Mathf.Max(titleW, scoreW) + padding;
        float width  = Mathf.Max(needed, panelWidth);   // 최소 panelWidth 보장

        _rect.sizeDelta = new Vector2(width, panelHeight);
    }

    // ─── 애니메이션 ──────────────────────────────────────────────────
    IEnumerator Animate(System.Action onComplete = null)
    {
        // Phase 1: 팡! 하고 튀어오름 (scale 0 → peakScale, alpha 0 → 1)
        yield return ScaleAlpha(0f, peakScale, 0f, 1f, popDuration, EaseOutBack);

        // Phase 2: 살짝 줄어들며 정착
        yield return ScaleAlpha(peakScale, settleScale, 1f, 1f, popDuration * 0.6f, EaseOutQuad);

        // Phase 3: 잠시 유지
        yield return new WaitForSeconds(holdDuration);

        // Phase 4: 위로 살짝 올라가며 페이드아웃
        float elapsed = 0f;
        Vector2 startPos = _rect.anchoredPosition;
        Vector2 endPos   = startPos + new Vector2(0f, 40f);
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            _group.alpha              = Mathf.Lerp(1f, 0f, t * t);
            _rect.anchoredPosition   = Vector2.Lerp(startPos, endPos, t);
            _rect.localScale         = Vector3.one * Mathf.Lerp(settleScale, settleScale * 0.9f, t);
            yield return null;
        }

        _rect.anchoredPosition = startPos;  // 위치 복원
        _group.alpha     = 0f;
        _rect.localScale = Vector3.zero;
        gameObject.SetActive(false);

        onComplete?.Invoke();
    }

    IEnumerator ScaleAlpha(float fromS, float toS, float fromA, float toA,
                           float duration, System.Func<float, float> ease)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.001f);
            float e = ease(Mathf.Clamp01(t));
            _rect.localScale = Vector3.one * Mathf.Lerp(fromS, toS, e);
            _group.alpha     = Mathf.Lerp(fromA, toA, e);
            yield return null;
        }
        _rect.localScale = Vector3.one * toS;
        _group.alpha     = toA;
    }

    // ─── 이징 함수 ────────────────────────────────────────────────────
    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
    static float EaseOutQuad(float t) => 1f - (1f - t) * (1f - t);
}
