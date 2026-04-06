using UnityEngine;
using UnityEngine.UI;
#if DOTWEEN
using DG.Tweening;
#endif

public class WildPopup : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private Button[]   numButtons; // 1~6

    private System.Action<int> _onSelected;

    void Awake() => gameObject.SetActive(false);

    public void Show(System.Action<int> onSelected)
    {
        _onSelected = onSelected;
        gameObject.SetActive(true);

        // 팝인
        panel.transform.localScale = Vector3.zero;
#if DOTWEEN
        panel.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack);
#else
        panel.transform.localScale = Vector3.one;
#endif

        for (int i = 0; i < numButtons.Length; i++)
        {
            int val = i + 1;
            numButtons[i].onClick.RemoveAllListeners();
            numButtons[i].onClick.AddListener(() => Select(val));
        }
    }

    void Select(int value)
    {
        _onSelected?.Invoke(value);
        Hide();
    }

    void Hide()
    {
#if DOTWEEN
        panel.transform.DOScale(0f, 0.15f)
            .OnComplete(() => gameObject.SetActive(false));
#else
        gameObject.SetActive(false);
#endif
    }
}
