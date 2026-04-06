using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class PlayerCard : MonoBehaviour
{
    [SerializeField] private Image           avatarImage;
    [SerializeField] private Image           highlightRing;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI scoreText;

    public void Setup(PlayerData data)
    {
        if (nameText)  nameText.text  = data.name;
        if (scoreText) scoreText.text = "0";
        if (avatarImage && data.avatarSprite)
            avatarImage.sprite = data.avatarSprite;
        SetHighlight(false);
    }

    public void SetHighlight(bool on)
    {
        if (highlightRing) highlightRing.gameObject.SetActive(on);
    }

    public void UpdateScore(int score)
    {
        if (scoreText) scoreText.text = score.ToString();
    }
}
