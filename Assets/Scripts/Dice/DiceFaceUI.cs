using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주사위 면에 눈(dot)을 코드로 그려주는 컴포넌트.
/// Dice 오브젝트 안의 faceImage GameObject에 붙이면 됩니다.
/// Awake에서 점 Image들을 자동 생성하고, SetValue(1~6)으로 갱신합니다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DiceFaceUI : MonoBehaviour
{
    [Header("디자인")]
    [SerializeField] private Color dotColor     = new Color(0.10f, 0.10f, 0.10f); // 거의 검정
    [SerializeField] private float dotSizeRatio = 0.22f;  // 면 크기 대비 점 지름 비율

    // ─── 9개 점 위치 (면 크기에 대한 비율, 중심 기준) ────────────────
    // 인덱스: 0=TL  1=TC  2=TR
    //        3=ML  4=MC  5=MR
    //        6=BL  7=BC  8=BR
    private static readonly Vector2[] Positions =
    {
        new Vector2(-0.28f,  0.28f), // 0 TL
        new Vector2( 0.00f,  0.28f), // 1 TC  (사용 안 함)
        new Vector2( 0.28f,  0.28f), // 2 TR
        new Vector2(-0.28f,  0.00f), // 3 ML
        new Vector2( 0.00f,  0.00f), // 4 MC
        new Vector2( 0.28f,  0.00f), // 5 MR
        new Vector2(-0.28f, -0.28f), // 6 BL
        new Vector2( 0.00f, -0.28f), // 7 BC  (사용 안 함)
        new Vector2( 0.28f, -0.28f), // 8 BR
    };

    // 각 면(1~6)에서 활성화할 점 인덱스
    private static readonly int[][] FaceDots =
    {
        new int[] { },                   // 0 unused
        new int[] { 4 },                 // 1: 중앙
        new int[] { 2, 6 },             // 2: 우상, 좌하
        new int[] { 2, 4, 6 },          // 3: 우상, 중앙, 좌하
        new int[] { 0, 2, 6, 8 },       // 4: 네 꼭짓점
        new int[] { 0, 2, 4, 6, 8 },    // 5: 네 꼭짓점 + 중앙
        new int[] { 0, 2, 3, 5, 6, 8 }, // 6: 양쪽 세 줄
    };

    private Image[]       _dots;
    private RectTransform _rect;
    private Sprite        _circleSprite;
    private int           _currentValue = 0;

    // ─── 초기화 ──────────────────────────────────────────────────────
    void Awake()
    {
        _rect         = GetComponent<RectTransform>();
        _circleSprite = MakeCircleSprite(64);
        CreateDots();
        SetValue(0);
    }

    void CreateDots()
    {
        _dots = new Image[Positions.Length];
        for (int i = 0; i < Positions.Length; i++)
        {
            var go  = new GameObject($"Dot{i}");
            go.transform.SetParent(transform, false);

            var img = go.AddComponent<Image>();
            img.sprite        = _circleSprite;
            img.color         = dotColor;
            img.raycastTarget = false;

            var rt       = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);

            _dots[i] = img;
        }
        RefreshLayout();
    }

    void RefreshLayout()
    {
        if (_dots == null) return;
        float side  = Mathf.Min(_rect.rect.width, _rect.rect.height);
        if (side <= 0f) side = 80f;   // Awake 타이밍에 rect가 0일 때 기본값
        float dotSz = side * dotSizeRatio;

        for (int i = 0; i < _dots.Length; i++)
        {
            var rt = _dots[i].GetComponent<RectTransform>();
            rt.sizeDelta        = new Vector2(dotSz, dotSz);
            rt.anchoredPosition = Positions[i] * side;
        }
    }

    // ─── 공개 API ────────────────────────────────────────────────────
    public void SetValue(int value)
    {
        _currentValue = value;
        if (_dots == null) return;

        foreach (var d in _dots) d.gameObject.SetActive(false);

        if (value < 1 || value > 6) return;
        foreach (int idx in FaceDots[value])
            _dots[idx].gameObject.SetActive(true);
    }

    public void SetDotColor(Color c)
    {
        dotColor = c;
        if (_dots == null) return;
        foreach (var d in _dots) d.color = c;
    }

    /// <summary>굴리기 시작 — 모든 점을 반투명으로 표시해 motion-blur 효과</summary>
    public void BeginRoll()
    {
        if (_dots == null) return;
        Color ghost = new Color(dotColor.r, dotColor.g, dotColor.b, 0.20f);
        foreach (var d in _dots)
        {
            d.gameObject.SetActive(true);
            d.color = ghost;
        }
    }

    /// <summary>굴리기 끝 — 정확한 면으로 복원</summary>
    public void EndRoll(int value)
    {
        if (_dots == null) return;
        foreach (var d in _dots) d.color = dotColor;
        SetValue(value);
    }

    // ─── 레이아웃 변경 시 재계산 ─────────────────────────────────────
    void OnRectTransformDimensionsChange()
    {
        RefreshLayout();
    }

    // ─── 원형 Sprite 런타임 생성 ─────────────────────────────────────
    static Sprite MakeCircleSprite(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[size * size];
        float r = size * 0.5f;

        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx   = x + 0.5f - r;
            float dy   = y + 0.5f - r;
            float dist = Mathf.Sqrt(dx * dx + dy * dy);
            byte  a    = (byte)(Mathf.Clamp01(r - dist) * 255f);
            pixels[y * size + x] = new Color32(255, 255, 255, a);
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
