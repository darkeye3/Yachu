using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 턴 시작 배너 연출:
///   오른쪽에서 샥 날아옴 → 중앙을 아주 천천히 가로질러 → 왼쪽으로 사라짐.
/// Canvas 자식 빈 오브젝트에 이 컴포넌트를 붙이면 자동으로 UI를 구성합니다.
/// </summary>
public class TurnBannerUI : MonoBehaviour
{
    [Header("연결 (비워두면 자동 생성)")]
    [SerializeField] private TextMeshProUGUI subText;   // "Round 1 / 8"
    [SerializeField] private TextMeshProUGUI nameText;  // "Player1의 턴"
    [SerializeField] private Image           bgImage;

    [Header("타이밍 (초)")]
    [SerializeField] private float flyInDuration  = 0.35f;
    [SerializeField] private float holdDuration   = 1.6f;
    [SerializeField] private float flyOutDuration = 0.28f;

    [Header("디자인")]
    [SerializeField] private float bannerHeight = 130f;
    [SerializeField] private Color bgColor      = new Color(0.08f, 0.08f, 0.08f, 0.88f);
    [SerializeField] private float fontSize     = 60f;

    private RectTransform _rect;
    private Canvas        _canvas;

    // ─── 초기화 ──────────────────────────────────────────────────────
    void Awake()
    {
        _canvas = GetComponentInParent<Canvas>(true);
        _rect   = GetComponent<RectTransform>();
        if (_rect == null) _rect = gameObject.AddComponent<RectTransform>();

        // 컵·오버레이보다 위에 렌더링
        var ownCanvas = GetComponent<Canvas>();
        if (ownCanvas == null) ownCanvas = gameObject.AddComponent<Canvas>();
        ownCanvas.overrideSorting = true;
        ownCanvas.sortingOrder    = 200;
        if (GetComponent<UnityEngine.UI.GraphicRaycaster>() == null)
            gameObject.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        SetupRect();
        SetupBackground();
        SetupText();

        gameObject.SetActive(false);
    }

    void SetupRect()
    {
        _rect.anchorMin        = new Vector2(0.5f, 0.5f);
        _rect.anchorMax        = new Vector2(0.5f, 0.5f);
        _rect.pivot            = new Vector2(0.5f, 0.5f);
        _rect.anchoredPosition = Vector2.zero;
        _rect.sizeDelta        = new Vector2(CanvasWidth() + 300f, bannerHeight);
    }

    void SetupBackground()
    {
        if (bgImage == null) bgImage = GetComponent<Image>();
        if (bgImage == null) bgImage = gameObject.AddComponent<Image>();
        bgImage.color = bgColor;
    }

    void SetupText()
    {
        // 세로 레이아웃: subText (위) + nameText (아래)
        // subText
        if (subText == null)
        {
            var go = new GameObject("SubText");
            go.transform.SetParent(transform, false);
            subText = go.AddComponent<TextMeshProUGUI>();
            var tr = go.GetComponent<RectTransform>();
            tr.anchorMin        = new Vector2(0f, 0.55f);
            tr.anchorMax        = new Vector2(1f, 1f);
            tr.offsetMin        = new Vector2(60f, 0f);
            tr.offsetMax        = new Vector2(-60f, 0f);
        }
        subText.alignment = TextAlignmentOptions.Center;
        subText.fontSize  = fontSize * 0.45f;
        subText.color     = new Color(1f, 1f, 0.6f, 0.9f); // 연한 노랑
        subText.fontStyle = FontStyles.Normal;

        // nameText
        if (nameText == null)
        {
            var go = new GameObject("NameText");
            go.transform.SetParent(transform, false);
            nameText = go.AddComponent<TextMeshProUGUI>();
            var tr = go.GetComponent<RectTransform>();
            tr.anchorMin        = new Vector2(0f, 0f);
            tr.anchorMax        = new Vector2(1f, 0.58f);
            tr.offsetMin        = new Vector2(60f, 0f);
            tr.offsetMax        = new Vector2(-60f, 0f);
        }
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.fontSize  = fontSize;
        nameText.color     = Color.white;
        nameText.fontStyle = FontStyles.Bold;
    }

    float CanvasWidth()
    {
        if (_canvas != null)
        {
            var cr = _canvas.GetComponent<RectTransform>();
            if (cr != null) return cr.rect.width;
        }
        return Screen.width;
    }

    // ─── 공개 API ────────────────────────────────────────────────────
    /// <param name="playerName">표시할 플레이어 이름</param>
    /// <param name="round">현재 라운드</param>
    /// <param name="totalRounds">전체 라운드 수</param>
    /// <param name="onComplete">애니메이션 완료 콜백</param>
    public void Show(string playerName, int round, int totalRounds, System.Action onComplete = null)
    {
        StopAllCoroutines();
        if (subText)  subText.text  = $"Round {round} / {totalRounds}";
        if (nameText) nameText.text = $"{playerName}의 턴";
        _rect.sizeDelta = new Vector2(CanvasWidth() + 300f, bannerHeight);
        gameObject.SetActive(true);
        StartCoroutine(Animate(onComplete));
    }

    // ─── 애니메이션 ──────────────────────────────────────────────────
    IEnumerator Animate(System.Action onComplete)
    {
        float w = CanvasWidth();

        // Phase 1: 오른쪽 밖 → 중앙 (빠르게, EaseOut)
        yield return Move(w + 150f, 0f, flyInDuration, EaseOutCubic);

        // Phase 2: 중앙 → 왼쪽 일부 (아주 천천히, Linear)
        float driftEnd = -w * 0.28f;
        yield return Move(0f, driftEnd, holdDuration, Linear);

        // Phase 3: → 화면 밖 왼쪽 (빠르게, EaseIn)
        yield return Move(driftEnd, -w - 150f, flyOutDuration, EaseInCubic);

        gameObject.SetActive(false);
        onComplete?.Invoke();
    }

    IEnumerator Move(float fromX, float toX, float duration, System.Func<float, float> ease)
    {
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(duration, 0.001f);
            _rect.anchoredPosition = new Vector2(Mathf.Lerp(fromX, toX, ease(Mathf.Clamp01(t))), 0f);
            yield return null;
        }
        _rect.anchoredPosition = new Vector2(toX, 0f);
    }

    static float EaseOutCubic(float t) => 1f - Mathf.Pow(1f - t, 3f);
    static float EaseInCubic(float t)  => t * t * t;
    static float Linear(float t)       => t;
}
