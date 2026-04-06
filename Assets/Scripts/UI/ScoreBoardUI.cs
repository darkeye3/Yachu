using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ScoreBoardUI : MonoBehaviour
{
    // ─── Inspector 연결 ──────────────────────────────────────────────
    [SerializeField] private Transform     playerRow;
    [SerializeField] private Transform     categoryList;
    [SerializeField] private Transform     totalRow;

    [SerializeField] private GameObject    playerCardPrefab;
    [SerializeField] private GameObject    categoryRowPrefab;

    [SerializeField] private TextMeshProUGUI[] totalScoreTexts;  // 플레이어별 합계

    [SerializeField] private TextMeshProUGUI roundText;          // 'Round 1/8'
    [SerializeField] private Sprite[]        categoryIcons;      // 8개 카테고리 아이콘

    // ─── 런타임 참조 ─────────────────────────────────────────────────
    private List<PlayerCard>   _playerCards  = new List<PlayerCard>();
    private List<CategoryRow>  _categoryRows = new List<CategoryRow>();
    private int _playerCount;

    // ─── 이벤트 ──────────────────────────────────────────────────────
    public System.Action<int, int> OnCategorySelected; // (categoryIndex, playerIndex)

    // ─── 초기화 ──────────────────────────────────────────────────────
    void Awake()
    {
        // roundText Inspector 미연결 시 이름으로 자동 탐색
        if (roundText == null)
        {
            var t = transform.Find("RoundText");
            if (t != null) roundText = t.GetComponent<TextMeshProUGUI>();
        }
        if (roundText == null)
        {
            // 씬 전체에서 "RoundText" 이름 탐색
            var go = GameObject.Find("RoundText");
            if (go != null) roundText = go.GetComponent<TextMeshProUGUI>();
        }
    }

    // ─── 빌드 ────────────────────────────────────────────────────────
    public void Build(List<PlayerData> players)
    {
        _playerCount = players.Count;
        Debug.Log($"[ScoreBoardUI] Build 시작 — 플레이어={_playerCount}, categoryRowPrefab={categoryRowPrefab != null}, playerCardPrefab={playerCardPrefab != null}");

        // ── PlayerCard: 프리팹 있으면 동적 생성, 없으면 씬에 있는 것 사용 ──
        _playerCards.Clear();
        if (playerCardPrefab != null && playerRow != null)
        {
            foreach (Transform t in playerRow) Destroy(t.gameObject);
            foreach (var p in players)
            {
                var go   = Instantiate(playerCardPrefab, playerRow);
                var card = go.GetComponent<PlayerCard>();
                card.Setup(p);
                _playerCards.Add(card);
            }
        }
        else if (playerRow != null)
        {
            // 씬에 이미 있는 PlayerCard 컴포넌트 사용
            foreach (Transform t in playerRow)
            {
                var card = t.GetComponent<PlayerCard>();
                if (card != null) _playerCards.Add(card);
            }
            for (int i = 0; i < Mathf.Min(_playerCards.Count, players.Count); i++)
                _playerCards[i].Setup(players[i]);
        }

        // ── CategoryRow: 프리팹 있으면 동적 생성, 없으면 씬에 있는 것 사용 ──
        _categoryRows.Clear();
        Transform container = categoryList != null ? categoryList : transform;
        if (categoryRowPrefab != null)
        {
            foreach (Transform t in container) Destroy(t.gameObject);
            for (int i = 0; i < 8; i++)
            {
                var go  = Instantiate(categoryRowPrefab, container);
                var row = go.GetComponent<CategoryRow>();
                if (row == null) { Debug.LogError("[ScoreBoardUI] CategoryRow 컴포넌트 없음!"); continue; }
                Sprite icon = (categoryIcons != null && i < categoryIcons.Length) ? categoryIcons[i] : null;
                row.Init(i, _playerCount, icon);
                row.OnScoreCellClicked += (catIdx, plrIdx) => OnCategorySelected?.Invoke(catIdx, plrIdx);
                _categoryRows.Add(row);
            }
        }
        else
        {
            // 씬에 이미 있는 CategoryRow 컴포넌트 사용
            int idx = 0;
            foreach (Transform t in container)
            {
                var row = t.GetComponent<CategoryRow>();
                if (row == null) continue;
                Sprite icon = (categoryIcons != null && idx < categoryIcons.Length) ? categoryIcons[idx] : null;
                row.Init(idx, _playerCount, icon);
                row.OnScoreCellClicked += (catIdx, plrIdx) => OnCategorySelected?.Invoke(catIdx, plrIdx);
                _categoryRows.Add(row);
                idx++;
            }
        }

        Debug.Log($"[ScoreBoardUI] Build 완료 — _categoryRows={_categoryRows.Count}개, _playerCards={_playerCards.Count}개");
        RefreshTotalScores(players);
    }

    // ─── 라운드 표시 갱신 ────────────────────────────────────────────
    public void UpdateRound(int current, int total)
    {
        if (roundText) roundText.text = $"Round {current}/{total}";
    }

    // ─── 현재 턴 하이라이트 ──────────────────────────────────────────
    public void SetActivePlayer(int playerIndex)
    {
        for (int i = 0; i < _playerCards.Count; i++)
            _playerCards[i].SetHighlight(i == playerIndex);
    }

    // ─── 미리보기 표시 ───────────────────────────────────────────────
    public void ShowPreviews(int playerIndex, int[] diceValues)
    {
        Debug.Log($"[ScoreBoardUI] ShowPreviews 호출 — player={playerIndex}, rows={_categoryRows.Count}, 주사위={string.Join(",", diceValues)}");
        for (int i = 0; i < _categoryRows.Count; i++)
        {
            if (_categoryRows[i].IsRecorded(playerIndex)) continue;
            int score = ScoreCalculator.Calculate(i, diceValues);
            Debug.Log($"  카테고리[{i}] {ScoreCalculator.CategoryNames[i]} → {score}점");
            _categoryRows[i].ShowPreview(playerIndex, score);
        }
    }

    public void ClearAllPreviews(int playerIndex)
    {
        foreach (var row in _categoryRows)
            row.ClearPreview(playerIndex);
    }

    // ─── 점수 확정 ───────────────────────────────────────────────────
    public void ConfirmScore(int categoryIndex, int playerIndex, int score)
    {
        if (categoryIndex < 0 || categoryIndex >= _categoryRows.Count) return;
        _categoryRows[categoryIndex].ConfirmScore(playerIndex, score);
    }

    // ─── 합계 갱신 ───────────────────────────────────────────────────
    public void RefreshTotalScores(List<PlayerData> players)
    {
        for (int i = 0; i < players.Count; i++)
        {
            if (i < _playerCards.Count)
                _playerCards[i].UpdateScore(players[i].TotalScore);
            if (totalScoreTexts != null && i < totalScoreTexts.Length && totalScoreTexts[i])
                totalScoreTexts[i].text = players[i].TotalScore.ToString();
        }
    }

    // ─── 카테고리 클릭 가능 여부 ─────────────────────────────────────
    public void SetScoringMode(int playerIndex, bool on)
    {
        foreach (var row in _categoryRows)
            row.SetClickable(playerIndex, on);
    }

    // ─── 기록 여부 배열 반환 ─────────────────────────────────────────
    public bool[] GetRecordedFlags(int playerIndex)
    {
        var flags = new bool[_categoryRows.Count];
        for (int i = 0; i < _categoryRows.Count; i++)
            flags[i] = _categoryRows[i].IsRecorded(playerIndex);
        return flags;
    }
}
