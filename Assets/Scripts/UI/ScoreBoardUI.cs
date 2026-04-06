using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ScoreBoardUI : MonoBehaviour
{
    [SerializeField] private Transform playerRow;
    [SerializeField] private Transform categoryList;
    [SerializeField] private Transform totalRow;

    [SerializeField] private GameObject playerCardPrefab;
    [SerializeField] private GameObject categoryRowPrefab;

    [SerializeField] private TextMeshProUGUI[] totalScoreTexts;
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private Sprite[] categoryIcons;

    private readonly List<PlayerCard> _playerCards = new List<PlayerCard>();
    private readonly List<CategoryRow> _categoryRows = new List<CategoryRow>();
    private int _playerCount;
    private Image _backgroundImage;
    private Color _defaultBackgroundColor;
    private static readonly Color MyTurnBackgroundColor = new Color(1f, 0.62f, 0.18f, 1f);

    public System.Action<int, int> OnCategorySelected;

    void Awake()
    {
        if (roundText == null)
        {
            var t = transform.Find("RoundText");
            if (t != null) roundText = t.GetComponent<TextMeshProUGUI>();
        }

        if (roundText == null)
        {
            var go = GameObject.Find("RoundText");
            if (go != null) roundText = go.GetComponent<TextMeshProUGUI>();
        }

        CacheBackgroundReference();
    }

    public void Build(List<PlayerData> players)
    {
        _playerCount = players.Count;
        CacheBackgroundReference();
        Debug.Log($"[ScoreBoardUI] Build start playerCount={_playerCount}, categoryRowPrefab={categoryRowPrefab != null}, playerCardPrefab={playerCardPrefab != null}");

        _playerCards.Clear();
        if (playerCardPrefab != null && playerRow != null)
        {
            foreach (Transform t in playerRow) Destroy(t.gameObject);
            foreach (var p in players)
            {
                var go = Instantiate(playerCardPrefab, playerRow);
                var card = go.GetComponent<PlayerCard>();
                card.Setup(p);
                _playerCards.Add(card);
            }
        }
        else if (playerRow != null)
        {
            foreach (Transform t in playerRow)
            {
                var card = t.GetComponent<PlayerCard>();
                if (card != null) _playerCards.Add(card);
            }

            for (int i = 0; i < Mathf.Min(_playerCards.Count, players.Count); i++)
                _playerCards[i].Setup(players[i]);
        }

        _categoryRows.Clear();
        Transform container = categoryList != null ? categoryList : transform;
        if (categoryRowPrefab != null)
        {
            foreach (Transform t in container) Destroy(t.gameObject);
            for (int i = 0; i < 8; i++)
            {
                var go = Instantiate(categoryRowPrefab, container);
                var row = go.GetComponent<CategoryRow>();
                if (row == null)
                {
                    Debug.LogError("[ScoreBoardUI] CategoryRow component missing.");
                    continue;
                }

                Sprite icon = categoryIcons != null && i < categoryIcons.Length ? categoryIcons[i] : null;
                row.Init(i, _playerCount, icon);
                row.OnScoreCellClicked += (catIdx, plrIdx) => OnCategorySelected?.Invoke(catIdx, plrIdx);
                _categoryRows.Add(row);
            }
        }
        else
        {
            int idx = 0;
            foreach (Transform t in container)
            {
                var row = t.GetComponent<CategoryRow>();
                if (row == null) continue;

                Sprite icon = categoryIcons != null && idx < categoryIcons.Length ? categoryIcons[idx] : null;
                row.Init(idx, _playerCount, icon);
                row.OnScoreCellClicked += (catIdx, plrIdx) => OnCategorySelected?.Invoke(catIdx, plrIdx);
                _categoryRows.Add(row);
                idx++;
            }
        }

        Debug.Log($"[ScoreBoardUI] Build complete rows={_categoryRows.Count} cards={_playerCards.Count}");
        RefreshTotalScores(players);
    }

    public void UpdateRound(int current, int total)
    {
        if (roundText != null) roundText.text = $"Round {current}/{total}";
    }

    public void SetActivePlayer(int playerIndex)
    {
        for (int i = 0; i < _playerCards.Count; i++)
            _playerCards[i].SetHighlight(i == playerIndex);

        UpdateBackgroundForTurn(playerIndex);
    }

    public void ShowPreviews(int playerIndex, int[] diceValues)
    {
        Debug.Log($"[ScoreBoardUI] ShowPreviews player={playerIndex}, rows={_categoryRows.Count}, dice={string.Join(",", diceValues)}");
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

            if (totalScoreTexts != null && i < totalScoreTexts.Length && totalScoreTexts[i] != null)
                totalScoreTexts[i].text = players[i].TotalScore.ToString();
        }
    }

    public void SetScoringMode(int playerIndex, bool on)
    {
        foreach (var row in _categoryRows)
            row.SetClickable(playerIndex, on);
    }

    public bool[] GetRecordedFlags(int playerIndex)
    {
        var flags = new bool[_categoryRows.Count];
        for (int i = 0; i < _categoryRows.Count; i++)
            flags[i] = _categoryRows[i].IsRecorded(playerIndex);
        return flags;
    }

    void CacheBackgroundReference()
    {
        if (_backgroundImage != null) return;

        var images = GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (image != null && image.name == "Background")
            {
                _backgroundImage = image;
                _defaultBackgroundColor = image.color;
                break;
            }
        }

        if (_backgroundImage != null) return;

        Transform current = transform;
        while (current != null && _backgroundImage == null)
        {
            var background = current.Find("Background");
            if (background != null)
            {
                _backgroundImage = background.GetComponent<Image>();
                if (_backgroundImage != null)
                {
                    _defaultBackgroundColor = _backgroundImage.color;
                    break;
                }
            }
            current = current.parent;
        }
    }

    void UpdateBackgroundForTurn(int activePlayerIndex)
    {
        if (_backgroundImage == null) return;

        int localPlayerIndex = 0;
        if (GameManager.Instance != null)
            localPlayerIndex = GameManager.Instance.LocalPlayerIndex;

        _backgroundImage.color = activePlayerIndex == localPlayerIndex
            ? MyTurnBackgroundColor
            : _defaultBackgroundColor;
    }
}
