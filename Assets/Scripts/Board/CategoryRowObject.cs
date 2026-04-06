using UnityEngine;
using TMPro;

/// <summary>
/// 점수판 카테고리 1행 (3D 월드 오브젝트 버전).
/// 라벨 셀 1개 + 플레이어 수만큼 점수 셀로 구성된다.
/// </summary>
public class CategoryRowObject : MonoBehaviour
{
    // ─── 색상 상수 ───────────────────────────────────────────────────
    private static readonly Color BgDefault      = new Color(0.92f, 0.92f, 0.92f);
    private static readonly Color BgPreviewScore = new Color(0.75f, 0.95f, 0.75f);
    private static readonly Color BgPreviewZero  = new Color(0.83f, 0.83f, 0.83f);
    private static readonly Color BgActive       = new Color(1.00f, 0.97f, 0.72f);
    private static readonly Color BgLabel        = new Color(0.30f, 0.30f, 0.35f);
    private static readonly Color BgConfirmed    = new Color(0.88f, 0.95f, 0.88f);

    private static readonly Color TextDefault    = new Color(0.45f, 0.45f, 0.45f);
    private static readonly Color TextPreview    = new Color(0.10f, 0.55f, 0.15f);
    private static readonly Color TextZero       = new Color(0.55f, 0.55f, 0.55f);
    private static readonly Color TextConfirmed  = new Color(0.05f, 0.05f, 0.05f);
    private static readonly Color TextLabel      = Color.white;

    private static readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

    // ─── 런타임 ──────────────────────────────────────────────────────
    private int           _categoryIndex;
    private int           _playerCount;

    private MeshRenderer  _labelRenderer;
    private MeshRenderer[] _cellRenderers;
    private TMP_Text[]     _cellTexts;
    private bool[]         _recorded;
    private bool[]         _clickable;

    // ─── 이벤트 ──────────────────────────────────────────────────────
    // ScoreBoardObject가 구독해 TurnManager로 전달
    public System.Action<int, int> OnScoreCellClicked; // (categoryIndex, playerIndex)

    // ─── 초기화 ──────────────────────────────────────────────────────
    /// <param name="categoryIndex">0~7</param>
    /// <param name="playerCount">2~4</param>
    /// <param name="labelWidth">라벨 셀 너비 (월드 단위)</param>
    /// <param name="cellWidth">점수 셀 1개 너비</param>
    /// <param name="cellHeight">행 높이</param>
    /// <param name="material">공유 Unlit/Color 머티리얼</param>
    /// <param name="font">TMP 3D 폰트 (null 허용)</param>
    /// <param name="scoreBoardLayer">ScoreBoard 레이어 번호</param>
    public void Init(int categoryIndex, int playerCount,
                     float labelWidth, float cellWidth, float cellHeight,
                     Material material, TMP_FontAsset font, int scoreBoardLayer)
    {
        _categoryIndex = categoryIndex;
        _playerCount   = playerCount;
        _recorded      = new bool[playerCount];
        _clickable     = new bool[playerCount];
        _cellRenderers = new MeshRenderer[playerCount];
        _cellTexts     = new TMP_Text[playerCount];

        // ── 라벨 셀 ──────────────────────────────────────────────
        float labelX = -labelWidth * 0.5f;
        var labelGO = CreateCell("Label", new Vector3(labelX, 0f, 0f),
                                 labelWidth, cellHeight, material, font,
                                 ScoreCalculator.CategoryNames[categoryIndex],
                                 0.18f, TextLabel);
        _labelRenderer = labelGO.GetComponent<MeshRenderer>();
        SetBg(_labelRenderer, BgLabel);
        // 라벨은 클릭 대상 아님 → collider 제거
        var lc = labelGO.GetComponent<Collider>();
        if (lc) Object.Destroy(lc);

        // ── 점수 셀 ──────────────────────────────────────────────
        float startX = labelWidth * 0.5f + cellWidth * 0.5f;
        for (int i = 0; i < playerCount; i++)
        {
            float x = startX + i * cellWidth;
            var cellGO = CreateCell($"ScoreCell_{i}", new Vector3(x, 0f, 0f),
                                    cellWidth - 0.04f, cellHeight - 0.04f,
                                    material, font, "", 0.22f, TextDefault);

            _cellRenderers[i] = cellGO.GetComponent<MeshRenderer>();
            _cellTexts[i]     = cellGO.GetComponentInChildren<TMP_Text>();
            SetBg(_cellRenderers[i], BgDefault);

            // ScoreCellObject 부착
            var cell = cellGO.AddComponent<ScoreCellObject>();
            cell.CategoryIndex = categoryIndex;
            cell.PlayerIndex   = i;
            cell.IsClickable   = false;

            // BoxCollider (Raycast용) — Quad의 기본 MeshCollider를 교체
            var mc = cellGO.GetComponent<MeshCollider>();
            if (mc) Object.Destroy(mc);
            var bc = cellGO.AddComponent<BoxCollider>();
            bc.size   = new Vector3(1f, 1f, 0.05f);  // Quad scale 적용 전 로컬 크기
            bc.center = Vector3.zero;

            // ScoreBoard 레이어 적용
            cellGO.layer = scoreBoardLayer;
        }
    }

    // ─── 미리보기 ─────────────────────────────────────────────────────
    public void ShowPreview(int playerIndex, int score)
    {
        if (!IsValidIndex(playerIndex) || _recorded[playerIndex]) return;
        _cellTexts[playerIndex].text  = score == 0 ? "0" : score.ToString();
        _cellTexts[playerIndex].color = score == 0 ? TextZero : TextPreview;
        SetBg(_cellRenderers[playerIndex], score == 0 ? BgPreviewZero : BgPreviewScore);
        SetCellClickable(playerIndex, true);
    }

    public void ClearPreview(int playerIndex)
    {
        if (!IsValidIndex(playerIndex) || _recorded[playerIndex]) return;
        _cellTexts[playerIndex].text  = "";
        _cellTexts[playerIndex].color = TextDefault;
        SetBg(_cellRenderers[playerIndex], BgDefault);
        SetCellClickable(playerIndex, false);
    }

    // ─── 점수 확정 ────────────────────────────────────────────────────
    public void ConfirmScore(int playerIndex, int score)
    {
        if (!IsValidIndex(playerIndex)) return;
        _recorded[playerIndex]        = true;
        _cellTexts[playerIndex].text  = score.ToString();
        _cellTexts[playerIndex].color = TextConfirmed;
        SetBg(_cellRenderers[playerIndex], BgConfirmed);
        SetCellClickable(playerIndex, false);
    }

    // ─── 클릭 허용 제어 (Scoring 모드) ────────────────────────────────
    public void SetClickable(int playerIndex, bool on)
    {
        if (!IsValidIndex(playerIndex)) return;
        if (_recorded[playerIndex]) { SetCellClickable(playerIndex, false); return; }
        SetCellClickable(playerIndex, on);
    }

    // ─── 활성 플레이어 하이라이트 ─────────────────────────────────────
    public void SetActiveHighlight(int playerIndex, bool on)
    {
        if (!IsValidIndex(playerIndex) || _recorded[playerIndex]) return;
        SetBg(_cellRenderers[playerIndex], on ? BgActive : BgDefault);
    }

    // ─── 조회 ─────────────────────────────────────────────────────────
    public bool IsRecorded(int playerIndex)
        => IsValidIndex(playerIndex) && _recorded[playerIndex];

    // ─── 내부 헬퍼 ───────────────────────────────────────────────────

    bool IsValidIndex(int i) => i >= 0 && i < _playerCount;

    void SetCellClickable(int playerIndex, bool on)
    {
        _clickable[playerIndex] = on;
        var cell = _cellRenderers[playerIndex]
                   .GetComponent<ScoreCellObject>();
        if (cell != null) cell.IsClickable = on;
    }

    // ScoreBoardInputHandler가 hit 후 호출
    public void NotifyCellClicked(int playerIndex)
    {
        if (!IsValidIndex(playerIndex)) return;
        if (!_clickable[playerIndex]) return;
        OnScoreCellClicked?.Invoke(_categoryIndex, playerIndex);
    }

    GameObject CreateCell(string goName, Vector3 localPos,
                          float w, float h, Material mat,
                          TMP_FontAsset font, string label,
                          float fontSize, Color textColor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = goName;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        // top-down 카메라용: Quad 노멀을 +Y 방향으로
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale    = new Vector3(w, h, 1f);

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        // 기본 MeshCollider 제거 (BoxCollider로 대체 예정)
        var mc = go.GetComponent<MeshCollider>();
        if (mc) Object.Destroy(mc);

        AttachTMPText(go, font, label, fontSize, textColor);
        return go;
    }

    void AttachTMPText(GameObject parent, TMP_FontAsset font,
                       string label, float fontSize, Color textColor)
    {
        var textGO = new GameObject("Text");
        textGO.transform.SetParent(parent.transform, false);
        textGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        textGO.transform.localRotation = Quaternion.identity;
        textGO.transform.localScale    = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshPro>();
        tmp.text               = label;
        tmp.fontSize           = fontSize;
        tmp.color              = textColor;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.overflowMode       = TextOverflowModes.Ellipsis;
        tmp.enableWordWrapping = false;
        if (font != null) tmp.font = font;
        tmp.sortingOrder = 1;
    }

    static void SetBg(MeshRenderer r, Color c)
    {
        if (r == null) return;
        _mpb.SetColor("_Color", c);
        r.SetPropertyBlock(_mpb);
    }
}
