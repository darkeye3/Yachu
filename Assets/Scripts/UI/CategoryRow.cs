using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CategoryRow : MonoBehaviour
{
    // ─── Inspector 연결 ──────────────────────────────────────────────
    [SerializeField] private Image           categoryIcon;
    [SerializeField] private TextMeshProUGUI categoryName;
    [SerializeField] private TextMeshProUGUI[] scoreCells;  // 플레이어 수만큼
    [SerializeField] private Button[]          scoreButtons;

    // ─── 색상 상수 ───────────────────────────────────────────────────
    private static readonly Color PreviewColor      = new Color(0.18f, 0.48f, 0.21f); // 텍스트: 초록
    private static readonly Color PreviewZero       = Color.gray;                      // 텍스트: 회색
    private static readonly Color ConfirmedColor    = Color.black;                     // 텍스트: 검정

    private static readonly Color BtnNormalScore    = new Color(0.85f, 1.00f, 0.85f); // 버튼: 연한 초록
    private static readonly Color BtnNormalZero     = new Color(0.88f, 0.88f, 0.88f); // 버튼: 연한 회색
    private static readonly Color BtnDefaultNormal  = Color.white;                    // 버튼: 기본 흰색

    public int CategoryIndex { get; private set; }

    // ─── 이벤트: (categoryIndex, playerIndex) ────────────────────────
    public System.Action<int, int> OnScoreCellClicked;

    // ─── 초기화 ──────────────────────────────────────────────────────
    public void Init(int categoryIndex, int playerCount, Sprite icon = null)
    {
        CategoryIndex = categoryIndex;

        if (categoryName) categoryName.text = ScoreCalculator.CategoryNames[categoryIndex];
        if (categoryIcon && icon) categoryIcon.sprite = icon;

        // ── scoreButtons 미연결 시 직접 자식에서 자동 탐색 ──────────
        bool noButtons = scoreButtons == null || scoreButtons.Length == 0
                         || System.Array.TrueForAll(scoreButtons, b => b == null);
        if (noButtons)
        {
            var list = new System.Collections.Generic.List<Button>();
            for (int i = 0; i < transform.childCount; i++)
            {
                var btn = transform.GetChild(i).GetComponent<Button>();
                if (btn != null) list.Add(btn);
            }
            scoreButtons = list.ToArray();
        }
        Debug.Log($"[CategoryRow {categoryIndex}] scoreButtons={scoreButtons?.Length}");

        // ── scoreCells: null 요소 포함 시에도 재생성 ─────────────────
        bool noCells = scoreCells == null || scoreCells.Length == 0
                       || System.Array.Exists(scoreCells, c => c == null);
        if (noCells)
        {
            scoreCells = new TextMeshProUGUI[scoreButtons.Length];
            for (int i = 0; i < scoreButtons.Length; i++)
            {
                if (scoreButtons[i] == null) continue;

                // 기존 TMP 자식 탐색
                var existing = scoreButtons[i].GetComponentInChildren<TextMeshProUGUI>(true);
                if (existing != null)
                {
                    scoreCells[i] = existing;
                    Debug.Log($"[CategoryRow {categoryIndex}] 버튼[{i}] 기존 TMP 사용: {existing.name}");
                }
                else
                {
                    // 없으면 자식 오브젝트로 TMP 동적 생성
                    var go = new GameObject("ScoreText");
                    go.transform.SetParent(scoreButtons[i].transform, false);
                    var rt = go.AddComponent<RectTransform>();
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.one;
                    rt.offsetMin = Vector2.zero;
                    rt.offsetMax = Vector2.zero;
                    var tmp = go.AddComponent<TextMeshProUGUI>();
                    tmp.alignment   = TextAlignmentOptions.Center;
                    tmp.fontSize    = 22;
                    tmp.color       = Color.white; // 주황 배경 위에서 잘 보이도록 흰색
                    scoreCells[i]   = tmp;
                    Debug.Log($"[CategoryRow {categoryIndex}] 버튼[{i}] ScoreText 동적 생성");
                }
            }
        }

        // ── 플레이어 수에 따라 셀 활성화 ────────────────────────────
        int count = Mathf.Min(scoreCells.Length, scoreButtons.Length);
        for (int i = 0; i < count; i++)
        {
            bool active = i < playerCount;
            if (scoreCells[i])   scoreCells[i].gameObject.SetActive(active);
            if (scoreButtons[i]) scoreButtons[i].gameObject.SetActive(active);

            if (active)
            {
                scoreCells[i].text  = "";
                scoreCells[i].color = ConfirmedColor;
                int captured = i;
                scoreButtons[i].onClick.RemoveAllListeners();
                scoreButtons[i].onClick.AddListener(() => OnScoreCellClicked?.Invoke(CategoryIndex, captured));
                scoreButtons[i].interactable = false;
            }
        }
    }

    // ─── 점수 미리보기 (초록 숫자) ───────────────────────────────────
    public void ShowPreview(int playerIndex, int score)
    {
        bool valid = IsValidIndex(playerIndex);
        Debug.Log($"[CategoryRow {CategoryIndex}] ShowPreview player={playerIndex} score={score} valid={valid}");
        if (!valid) return;
        var cell = scoreCells[playerIndex];
        cell.text  = score == 0 ? "0" : score.ToString();
        var previewColor = score == 0 ? PreviewZero : PreviewColor;
        previewColor.a = 0.5f;
        cell.color = previewColor;
        // 버튼 배경색: 점수 있으면 연한 초록, 0점이면 연한 회색
        SetButtonNormalColor(scoreButtons[playerIndex], score > 0 ? BtnNormalScore : BtnNormalZero);
        scoreButtons[playerIndex].interactable = true;
    }

    // ─── 점수 확정 (검정 숫자, 버튼 비활성) ─────────────────────────
    public void ConfirmScore(int playerIndex, int score)
    {
        bool valid = IsValidIndex(playerIndex);
        Debug.Log($"[CategoryRow {CategoryIndex}] ConfirmScore player={playerIndex} score={score} valid={valid} scoreCells={scoreCells?.Length}");
        if (!valid) return;
        var confirmed = ConfirmedColor;
        confirmed.a = 1f;
        scoreCells[playerIndex].text  = score.ToString();
        scoreCells[playerIndex].color = confirmed;
        scoreButtons[playerIndex].interactable = false;
    }

    // ─── 미리보기 초기화 ─────────────────────────────────────────────
    public void ClearPreview(int playerIndex)
    {
        if (!IsValidIndex(playerIndex)) return;
        // 이미 확정된 카테고리면 건드리지 않음 (검정 텍스트 + 내용 있음)
        if (IsRecorded(playerIndex)) return;

        scoreCells[playerIndex].text  = "";
        scoreCells[playerIndex].color = ConfirmedColor;
        SetButtonNormalColor(scoreButtons[playerIndex], BtnDefaultNormal);
        scoreButtons[playerIndex].interactable = false;
    }

    // ─── 모든 플레이어 미리보기 초기화 ──────────────────────────────
    public void ClearAllPreviews(int playerCount)
    {
        for (int i = 0; i < playerCount; i++) ClearPreview(i);
    }

    // ─── 버튼 활성 제어 ─────────────────────────────────────────────
    public void SetClickable(int playerIndex, bool on)
    {
        if (!IsValidIndex(playerIndex)) return;
        // 이미 확정된 카테고리는 항상 비활성
        if (IsRecorded(playerIndex))
        {
            scoreButtons[playerIndex].interactable = false;
            return;
        }
        scoreButtons[playerIndex].interactable = on;
    }

    bool IsValidIndex(int i)
        => i >= 0 && i < scoreCells.Length && scoreCells[i] != null;

    public bool IsRecorded(int playerIndex)
    {
        if (!IsValidIndex(playerIndex)) return false;
        // 확정 = 버튼 비활성 + 텍스트 있음 + alpha=1 (미리보기는 alpha=0.5)
        return !scoreButtons[playerIndex].interactable
               && scoreCells[playerIndex].text != ""
               && scoreCells[playerIndex].color.a >= 0.99f;
    }

    // ─── 버튼 Normal 색 변경 헬퍼 ───────────────────────────────────
    void SetButtonNormalColor(Button btn, Color color)
    {
        var cb = btn.colors;
        cb.normalColor = color;
        btn.colors = cb;
    }
}
