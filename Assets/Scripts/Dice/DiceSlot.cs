using UnityEngine;
using UnityEngine.UI;
#if DOTWEEN
using DG.Tweening;
#endif

public class DiceSlot : MonoBehaviour
{
    public bool IsOccupied  { get; private set; }
    public Dice OccupiedDice { get; private set; }

    [SerializeField] private Image slotBG;

    private readonly Color emptyColor  = new Color(0.23f, 0.10f, 0.03f, 1f); // #3A1A08
    private readonly Color filledColor = new Color(0.40f, 0.22f, 0.08f, 1f);
    private Color _savedColor;

    void Awake()
    {
        if (slotBG) slotBG.color = emptyColor;
    }

    public void PlaceDice(Dice dice)
    {
        OccupiedDice = dice;
        IsOccupied   = true;
        if (slotBG) slotBG.color = filledColor;

        // 슬롯의 월드 좌표를 주사위 부모의 로컬 좌표로 변환 (부모가 다를 경우 대응)
        Vector3 targetLocal = dice.transform.parent != null
            ? dice.transform.parent.InverseTransformPoint(transform.position)
            : transform.position;

        // 회전·크기 즉시 정렬
        dice.transform.localRotation = Quaternion.identity;
        dice.transform.localScale    = Vector3.one;

#if DOTWEEN
        dice.transform.DOLocalMove(targetLocal, 0.4f).SetEase(Ease.OutCubic);
#else
        dice.transform.localPosition = targetLocal;
#endif
    }

    public void RemoveDice()
    {
        if (OccupiedDice != null)
            OccupiedDice.ReturnToOrigin();
        OccupiedDice = null;
        IsOccupied   = false;
        if (slotBG) slotBG.color = emptyColor;
    }

    public void Clear()
    {
        OccupiedDice = null;
        IsOccupied   = false;
        if (slotBG) slotBG.color = emptyColor;
    }

    // 오버레이 UI 주사위 전용 — Dice 객체 없이 슬롯만 점유
    public void OccupyWithOverlay()
    {
        OccupiedDice = null;
        IsOccupied   = true;
        if (slotBG) slotBG.color = filledColor;
    }

    // 오버레이 이미지가 자식으로 들어올 때 — 현재 색 저장 후 흰색으로
    public void OnOverlayEnter()
    {
        if (slotBG == null) return;
        _savedColor   = slotBG.color;
        slotBG.color  = Color.white;
    }

    // 오버레이 이미지가 빠져나갈 때 — 저장했던 색으로 복원
    public void OnOverlayExit()
    {
        if (slotBG == null) return;
        slotBG.color = _savedColor;
    }
}
