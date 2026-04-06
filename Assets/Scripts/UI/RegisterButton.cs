using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 족보 등록 버튼. TurnManager가 상태를 제어한다.
/// </summary>
public class RegisterButton : MonoBehaviour
{
    [SerializeField] private Button          btn;
    [SerializeField] private TextMeshProUGUI label;

    public System.Action OnRegisterClicked;

    void Awake()
    {
        if (btn == null) btn = GetComponent<Button>();
        btn.onClick.AddListener(() => OnRegisterClicked?.Invoke());
        SetInteractable(false);
    }

    public void SetInteractable(bool on)
    {
        btn.interactable = on;
    }

    public void SetLabel(string text)
    {
        if (label) label.text = text;
    }
}
