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
        ResolveRoundTextReference();
        CacheBackgroundReference();
    }

    public void Build(List<PlayerData> players)
    {
        _playerCount = players.Count;
        CacheBackgroundReference();
        Debug.Log($"[ScoreBoardUI] Build start playerCount={_playerCount}, categoryRowPrefab={categoryRowPrefab != null}, playerCardPrefab={playerCardPrefab != null}");

        BuildPlayerCards(players);
        BuildCategoryRows();

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

    public void ShowZeroPreviews(int playerIndex)
    {
        for (int i = 0; i < _categoryRows.Count; i++)
        {
            if (_categoryRows[i].IsRecorded(playerIndex)) continue;
            _categoryRows[i].ShowPreview(playerIndex, 0);
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

        _backgroundImage = FindBackgroundInChildren();

        if (_backgroundImage != null) return;

        _backgroundImage = FindBackgroundInParents();
    }

    void UpdateBackgroundForTurn(int activePlayerIndex)
    {
        if (_backgroundImage == null) return;

        _backgroundImage.color = activePlayerIndex == GetLocalPlayerIndex()
            ? MyTurnBackgroundColor
            : _defaultBackgroundColor;
    }

    void ResolveRoundTextReference()
    {
        if (roundText != null) return;

        var localText = transform.Find("RoundText");
        if (localText != null)
        {
            roundText = localText.GetComponent<TextMeshProUGUI>();
            if (roundText != null) return;
        }

        var globalText = GameObject.Find("RoundText");
        if (globalText != null)
            roundText = globalText.GetComponent<TextMeshProUGUI>();
    }

    void BuildPlayerCards(List<PlayerData> players)
    {
        _playerCards.Clear();
        if (playerRow == null) return;

        if (playerCardPrefab != null)
        {
            foreach (Transform child in playerRow) Destroy(child.gameObject);
            foreach (var player in players)
            {
                var instance = Instantiate(playerCardPrefab, playerRow);
                var card = instance.GetComponent<PlayerCard>();
                card.Setup(player);
                _playerCards.Add(card);
            }
            return;
        }

        foreach (Transform child in playerRow)
        {
            var card = child.GetComponent<PlayerCard>();
            if (card != null) _playerCards.Add(card);
        }

        for (int i = 0; i < Mathf.Min(_playerCards.Count, players.Count); i++)
            _playerCards[i].Setup(players[i]);
    }

    void BuildCategoryRows()
    {
        _categoryRows.Clear();
        Transform container = categoryList != null ? categoryList : transform;

        if (categoryRowPrefab != null)
        {
            foreach (Transform child in container) Destroy(child.gameObject);
            for (int i = 0; i < 8; i++)
            {
                var instance = Instantiate(categoryRowPrefab, container);
                RegisterCategoryRow(instance.GetComponent<CategoryRow>(), i);
            }
            return;
        }

        int index = 0;
        foreach (Transform child in container)
        {
            var row = child.GetComponent<CategoryRow>();
            if (row == null) continue;
            RegisterCategoryRow(row, index++);
        }
    }

    void RegisterCategoryRow(CategoryRow row, int categoryIndex)
    {
        if (row == null)
        {
            Debug.LogError("[ScoreBoardUI] CategoryRow component missing.");
            return;
        }

        Sprite icon = categoryIcons != null && categoryIndex < categoryIcons.Length ? categoryIcons[categoryIndex] : null;
        row.Init(categoryIndex, _playerCount, icon);
        row.OnScoreCellClicked -= HandleScoreCellClicked;
        row.OnScoreCellClicked += HandleScoreCellClicked;
        _categoryRows.Add(row);
    }

    void HandleScoreCellClicked(int categoryIndex, int playerIndex)
    {
        OnCategorySelected?.Invoke(categoryIndex, playerIndex);
    }

    Image FindBackgroundInChildren()
    {
        var images = GetComponentsInChildren<Image>(true);
        foreach (var image in images)
        {
            if (image == null || image.name != "Background") continue;
            _defaultBackgroundColor = image.color;
            return image;
        }

        return null;
    }

    Image FindBackgroundInParents()
    {
        Transform current = transform;
        while (current != null)
        {
            var background = current.Find("Background");
            if (background != null)
            {
                var image = background.GetComponent<Image>();
                if (image != null)
                {
                    _defaultBackgroundColor = image.color;
                    return image;
                }
            }
            current = current.parent;
        }

        return null;
    }

    int GetLocalPlayerIndex()
    {
        return GameManager.Instance != null ? GameManager.Instance.LocalPlayerIndex : 0;
    }
}
