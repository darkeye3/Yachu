using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// 3D 월드 오브젝트 기반 점수판.
/// TurnManager의 ScoreBoardUI 자리를 직접 대체한다.
/// 씬 좌측에 배치하며, Physics Raycast 클릭은 ScoreBoardInputHandler가 처리한다.
///
/// 레이아웃 (top-down 카메라 기준):
///   X축: 라벨 → 플레이어 셀 (화면 우측 방향)
///   Z축: 헤더 → 카테고리 행 → 합계 행 (화면 하단 방향)
/// </summary>
public class ScoreBoardObject : MonoBehaviour
{
    // ─── Inspector ───────────────────────────────────────────────────
    [Header("머티리얼 / 폰트")]
    [SerializeField] private Material     cellMaterial;   // Unlit/Color 셰이더
    [SerializeField] private TMP_FontAsset boardFont;     // TextMeshPro 3D 폰트 (null 허용)

    [Header("셀 크기 (월드 단위)")]
    [SerializeField] private float cellWidth   = 0.90f;
    [SerializeField] private float cellHeight  = 0.72f;
    [SerializeField] private float labelWidth  = 1.60f;

    [Header("레이어")]
    [SerializeField] private int scoreBoardLayerIndex = 8; // "ScoreBoard" 레이어 번호

    // ─── 런타임 상태 ─────────────────────────────────────────────────
    private List<PlayerCardObject>   _playerCards  = new List<PlayerCardObject>();
    private List<CategoryRowObject>  _categoryRows = new List<CategoryRowObject>();
    private int                      _playerCount;

    // 합계 텍스트 (TMP 3D)
    private TMP_Text[] _totalTexts;

    // 라운드 표시 텍스트
    private TMP_Text _roundText;

    // ─── 이벤트 (TurnManager가 구독) ─────────────────────────────────
    public System.Action<int, int> OnCategorySelected; // (categoryIndex, playerIndex)

    // ─── Build ───────────────────────────────────────────────────────
    /// <summary>
    /// TurnManager.InitCoroutine() 에서 호출.
    /// 플레이어 카드와 카테고리 행 오브젝트를 동적으로 생성한다.
    /// </summary>
    public void Build(List<PlayerData> players)
    {
        _playerCount = players.Count;

        // 기존 자식 제거 (재빌드 대응)
        foreach (Transform t in transform)
            Destroy(t.gameObject);
        _playerCards.Clear();
        _categoryRows.Clear();

        EnsureMaterial();

        // 보드 총 너비 = 라벨 + 플레이어 수 * 셀 너비
        float boardWidth = labelWidth + _playerCount * cellWidth;
        // 헤더 + 8카테고리 + 합계 = 10행
        float boardHeight = (10) * cellHeight;

        // 보드 시작 Z (헤더가 가장 위)
        // 보드 중심이 transform.position이 되도록 오프셋 계산
        float startZ = -(boardHeight * 0.5f) + cellHeight * 0.5f;

        // ── 배경판 ────────────────────────────────────────────────
        BuildBackgroundPanel(boardWidth, boardHeight);

        // ── 라운드 텍스트 ─────────────────────────────────────────
        float roundZ = startZ - cellHeight;
        BuildRoundText(boardWidth, roundZ);

        // ── 플레이어 헤더 행 ──────────────────────────────────────
        float headerZ = startZ;
        BuildPlayerHeaders(players, headerZ);

        // ── 카테고리 행 8개 ───────────────────────────────────────
        for (int i = 0; i < 8; i++)
        {
            float rowZ = startZ + (i + 1) * cellHeight;
            BuildCategoryRow(i, rowZ);
        }

        // ── 합계 행 ───────────────────────────────────────────────
        float totalZ = startZ + 9 * cellHeight;
        BuildTotalRow(players, totalZ);

        Debug.Log($"[ScoreBoardObject] Build 완료 — 플레이어={_playerCount}, 카테고리={_categoryRows.Count}");
    }

    // ─── TurnManager 요구 API ─────────────────────────────────────────

    public void UpdateRound(int current, int total)
    {
        if (_roundText) _roundText.text = $"Round {current} / {total}";
    }

    public void SetActivePlayer(int playerIndex)
    {
        for (int i = 0; i < _playerCards.Count; i++)
            _playerCards[i].SetHighlight(i == playerIndex);

        foreach (var row in _categoryRows)
            row.SetActiveHighlight(playerIndex, true);
    }

    public void SetScoringMode(int playerIndex, bool on)
    {
        foreach (var row in _categoryRows)
            row.SetClickable(playerIndex, on);
    }

    public void ShowPreviews(int playerIndex, int[] diceValues)
    {
        for (int i = 0; i < _categoryRows.Count; i++)
        {
            if (_categoryRows[i].IsRecorded(playerIndex)) continue;
            int score = ScoreCalculator.Calculate(i, diceValues);
            _categoryRows[i].ShowPreview(playerIndex, score);
        }
    }

    public void ClearAllPreviews(int playerIndex)
    {
        foreach (var row in _categoryRows)
            row.ClearPreview(playerIndex);
    }

    public void ConfirmScore(int categoryIndex, int playerIndex, int score)
    {
        if (categoryIndex < 0 || categoryIndex >= _categoryRows.Count) return;
        _categoryRows[categoryIndex].ConfirmScore(playerIndex, score);
    }

    public void RefreshTotalScores(List<PlayerData> players)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (i < _playerCards.Count)
                _playerCards[i].UpdateScore(players[i].TotalScore);
            if (_totalTexts != null && i < _totalTexts.Length && _totalTexts[i] != null)
                _totalTexts[i].text = players[i].TotalScore.ToString();
        }
    }

    public bool[] GetRecordedFlags(int playerIndex)
    {
        var flags = new bool[_categoryRows.Count];
        for (int i = 0; i < _categoryRows.Count; i++)
            flags[i] = _categoryRows[i].IsRecorded(playerIndex);
        return flags;
    }

    // ─── InputHandler 콜백 ────────────────────────────────────────────
    /// ScoreBoardInputHandler가 Raycast hit 후 호출한다.
    public void HandleCellClicked(int categoryIndex, int playerIndex)
    {
        OnCategorySelected?.Invoke(categoryIndex, playerIndex);
    }

    // ─── 내부 빌드 헬퍼 ──────────────────────────────────────────────

    void BuildBackgroundPanel(float w, float h)
    {
        var bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "BoardBackground";
        bg.transform.SetParent(transform, false);
        bg.transform.localPosition = Vector3.zero;
        bg.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        bg.transform.localScale    = new Vector3(w + 0.2f, h + 0.3f, 1f);

        var mr = bg.GetComponent<MeshRenderer>();
        mr.sharedMaterial = cellMaterial;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_Color", new Color(0.18f, 0.18f, 0.22f));
        mr.SetPropertyBlock(mpb);

        Destroy(bg.GetComponent<MeshCollider>());
    }

    void BuildRoundText(float boardWidth, float z)
    {
        var go = new GameObject("RoundText");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(labelWidth * 0.5f, 0.02f, z);
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale    = Vector3.one;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text               = "Round 1 / 8";
        tmp.fontSize           = 0.28f;
        tmp.color              = new Color(0.9f, 0.9f, 0.9f);
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        if (boardFont != null) tmp.font = boardFont;
        tmp.sortingOrder = 2;
        _roundText = tmp;
    }

    void BuildPlayerHeaders(List<PlayerData> players, float z)
    {
        // 라벨 영역 헤더 ("족보" 텍스트)
        var labelHeaderGO = CreateFlatCell("Header_Label",
            new Vector3(0f, 0.01f, z), labelWidth - 0.04f, cellHeight - 0.04f,
            new Color(0.22f, 0.22f, 0.28f));
        AttachText(labelHeaderGO, "족보", 0.20f, Color.white);

        // 플레이어 카드
        float startX = labelWidth * 0.5f + cellWidth * 0.5f;
        for (int i = 0; i < players.Count; i++)
        {
            float x = startX + i * cellWidth;
            var cardGO = new GameObject($"PlayerCard_{i}");
            cardGO.transform.SetParent(transform, false);
            cardGO.transform.localPosition = new Vector3(x, 0.01f, z);

            var card = cardGO.AddComponent<PlayerCardObject>();
            card.Init(players[i], cellWidth - 0.04f, cellHeight,
                      cellMaterial, boardFont);
            _playerCards.Add(card);
        }
    }

    void BuildCategoryRow(int categoryIndex, float z)
    {
        var rowGO = new GameObject($"CategoryRow_{categoryIndex}");
        rowGO.transform.SetParent(transform, false);
        rowGO.transform.localPosition = new Vector3(0f, 0.01f, z);

        var row = rowGO.AddComponent<CategoryRowObject>();
        row.Init(categoryIndex, _playerCount,
                 labelWidth, cellWidth, cellHeight - 0.04f,
                 cellMaterial, boardFont, scoreBoardLayerIndex);

        // 이벤트 연결: CategoryRow → ScoreBoardObject → TurnManager
        row.OnScoreCellClicked += (catIdx, plrIdx) =>
            OnCategorySelected?.Invoke(catIdx, plrIdx);

        _categoryRows.Add(row);
    }

    void BuildTotalRow(List<PlayerData> players, float z)
    {
        // 라벨
        var labelGO = CreateFlatCell("Total_Label",
            new Vector3(0f, 0.01f, z), labelWidth - 0.04f, cellHeight - 0.04f,
            new Color(0.15f, 0.35f, 0.15f));
        AttachText(labelGO, "합계", 0.20f, Color.white);

        // 플레이어별 합계
        _totalTexts = new TMP_Text[players.Count];
        float startX = labelWidth * 0.5f + cellWidth * 0.5f;
        for (int i = 0; i < players.Count; i++)
        {
            float x = startX + i * cellWidth;
            var cellGO = CreateFlatCell($"Total_Cell_{i}",
                new Vector3(x, 0.01f, z), cellWidth - 0.04f, cellHeight - 0.04f,
                new Color(0.75f, 0.95f, 0.75f));
            var tmp = AttachText(cellGO, "0", 0.24f, new Color(0.05f, 0.05f, 0.05f));
            _totalTexts[i] = tmp;
        }
    }

    // ─── 유틸 헬퍼 ───────────────────────────────────────────────────

    void EnsureMaterial()
    {
        if (cellMaterial != null) return;
        cellMaterial = new Material(Shader.Find("Unlit/Color"))
        {
            color = Color.white
        };
        Debug.LogWarning("[ScoreBoardObject] cellMaterial 미연결 — Unlit/Color 기본 머티리얼 생성.");
    }

    /// 색이 있는 평면 셀 (BoxCollider 없음)
    GameObject CreateFlatCell(string goName, Vector3 localPos,
                              float w, float h, Color bgColor)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = goName;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale    = new Vector3(w, h, 1f);

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = cellMaterial;
        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_Color", bgColor);
        mr.SetPropertyBlock(mpb);

        Destroy(go.GetComponent<MeshCollider>());
        return go;
    }

    TMP_Text AttachText(GameObject parent, string label,
                        float fontSize, Color color)
    {
        var go = new GameObject("Text");
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale    = Vector3.one;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text               = label;
        tmp.fontSize           = fontSize;
        tmp.color              = color;
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = false;
        if (boardFont != null) tmp.font = boardFont;
        tmp.sortingOrder = 2;
        return tmp;
    }
}
