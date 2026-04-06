using UnityEngine;
using UnityEngine.UI;

public class RadialGauge : MonoBehaviour
{
    [SerializeField] private Image gaugeImage;

    private static readonly Color ColorEmpty = new Color(1f, 0.84f, 0f);  // 금색 #FFD700
    private static readonly Color ColorFull  = new Color(1f, 0.13f, 0f);  // 빨강 #FF2100

    public float GaugeValue { get; private set; }

    void Awake()
    {
        // Inspector 미연결 시 자동 탐색
        if (gaugeImage == null)
            gaugeImage = GetComponentInChildren<Image>(true);

        if (gaugeImage)
        {
            gaugeImage.type       = Image.Type.Filled;
            gaugeImage.fillMethod = Image.FillMethod.Vertical;
            gaugeImage.fillOrigin = (int)Image.OriginVertical.Bottom;
        }
        SetValue(0f);
    }

    public void SetValue(float v)
    {
        GaugeValue = Mathf.Clamp01(v);
        if (gaugeImage == null) return;
        gaugeImage.fillAmount = GaugeValue;
        gaugeImage.color      = Color.Lerp(ColorEmpty, ColorFull, GaugeValue);
    }

    void Update()
    {
        // RadialGauge는 DiceCup에서 값을 받아 SetValue로만 갱신
    }
}
