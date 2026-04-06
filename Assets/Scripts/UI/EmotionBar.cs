using UnityEngine;
using UnityEngine.UI;
#if DOTWEEN
using DG.Tweening;
#endif

public class EmotionBar : MonoBehaviour
{
    [SerializeField] private Button[]      emotionButtons;   // 5개
    [SerializeField] private Sprite[]      emotionSprites;   // 5개 이모지
    [SerializeField] private Transform     popupParent;      // 팝업이 붙을 Canvas
    [SerializeField] private GameObject    emotionPopupPrefab;

    private float[] _cooldowns;
    private const float CooldownTime = 3f;

    void Start()
    {
        _cooldowns = new float[emotionButtons.Length];
        for (int i = 0; i < emotionButtons.Length; i++)
        {
            int idx = i;
            emotionButtons[i].onClick.AddListener(() => OnEmotionClick(idx));
        }
    }

    void Update()
    {
        for (int i = 0; i < _cooldowns.Length; i++)
        {
            if (_cooldowns[i] > 0f)
            {
                _cooldowns[i] -= Time.deltaTime;
                var cg = emotionButtons[i].GetComponent<CanvasGroup>();
                if (cg) cg.alpha = 1f;
            }
        }
    }

    public void OnEmotionClick(int idx)
    {
        if (_cooldowns[idx] > 0f) return;
        _cooldowns[idx] = CooldownTime;
        AudioManager.Play("emotion");
        if (idx < emotionSprites.Length)
            ShowEmotionPopup(emotionSprites[idx]);
    }

    void ShowEmotionPopup(Sprite sprite)
    {
        if (emotionPopupPrefab == null || popupParent == null) return;

        var popup = Instantiate(emotionPopupPrefab, popupParent);
        var img   = popup.GetComponent<Image>();
        if (img) img.sprite = sprite;

        popup.transform.localScale = Vector3.zero;
#if DOTWEEN
        popup.transform.DOScale(1.2f, 0.3f).SetEase(Ease.OutBack);

        var cg = popup.GetComponent<CanvasGroup>();
        if (cg == null) cg = popup.AddComponent<CanvasGroup>();
        cg.DOFade(0f, 0.5f).SetDelay(0.8f)
          .OnComplete(() => Destroy(popup));
#else
        popup.transform.localScale = Vector3.one * 1.2f;
        Destroy(popup, 1.3f);
#endif
    }
}
