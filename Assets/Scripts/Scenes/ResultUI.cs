using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResultUI : MonoBehaviour
{
    [Header("결과 표시")]
    [SerializeField] private Transform       resultList;       // 순위 카드들 부모
    [SerializeField] private GameObject      resultCardPrefab; // 순위 카드 프리팹

    [Header("버튼")]
    [SerializeField] private Button btnPlayAgain;
    [SerializeField] private Button btnMainMenu;

    void Start()
    {
        BuildResult();
        btnPlayAgain?.onClick.AddListener(() => GameManager.Instance?.LoadScene(GameManager.SCENE_LOBBY));
        btnMainMenu?.onClick.AddListener(() => GameManager.Instance?.LoadScene(GameManager.SCENE_MAIN));
        AudioManager.Instance?.PlayResultBGM();
    }

    void BuildResult()
    {
        // ── GameManager 또는 Players 누락 시 안전하게 종료 ──
        if (GameManager.Instance == null)
        {
            Debug.LogWarning("[ResultUI] GameManager.Instance is null. " +
                             "Play from 00_Boot scene, or ensure GameManager is in the scene.");
            return;
        }

        var players = GameManager.Instance.Players;

        if (players == null || players.Count == 0)
        {
            Debug.LogWarning("[ResultUI] Players list is empty. No results to display.");
            return;
        }

        if (resultList == null)
        {
            Debug.LogError("[ResultUI] resultList is not assigned in the Inspector.");
            return;
        }

        if (resultCardPrefab == null)
        {
            Debug.LogError("[ResultUI] resultCardPrefab is not assigned in the Inspector.");
            return;
        }

        var sorted = players.OrderByDescending(p => p.TotalScore).ToList();

        foreach (Transform t in resultList) Destroy(t.gameObject);

        for (int rank = 0; rank < sorted.Count; rank++)
        {
            var p  = sorted[rank];
            var go = Instantiate(resultCardPrefab, resultList);

            var texts = go.GetComponentsInChildren<TextMeshProUGUI>();
            // 카드 레이아웃: [0]=순위, [1]=이름, [2]=점수
            if (texts.Length > 0) texts[0].text = $"{rank + 1}위";
            if (texts.Length > 1) texts[1].text = p.name;
            if (texts.Length > 2) texts[2].text = $"{p.TotalScore}점";

            // 아바타
            var img = go.GetComponentInChildren<Image>();
            if (img && p.avatarSprite) img.sprite = p.avatarSprite;
        }
    }
}
