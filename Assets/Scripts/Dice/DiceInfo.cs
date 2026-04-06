/// <summary>
/// 주사위 1개의 공유 상태. DiceController가 소유하고 다른 컴포넌트는 참조만 한다.
/// </summary>
public enum DiceState
{
    InCup,    // 컵 안에 있음 — 컵 주사위 ON, 보드 주사위 OFF, 오버레이 없음
    Throwing, // 보드로 던져지는 중 — 컵 주사위 OFF, 보드 주사위 ON(물리), 오버레이 없음
    OnBoard,  // 정렬 완료 — 컵 주사위 OFF, 보드 주사위 OFF, 오버레이 있음
    Kept,     // 슬롯에 보관 — 컵 주사위 OFF, 보드 주사위 OFF, 오버레이 슬롯에 있음
}

public class DiceInfo
{
    public int       Value = 1;
    public DiceState State = DiceState.InCup;

    public bool IsKept => State == DiceState.Kept;

    // 네트워크 수신 시 강제 Keep 상태 설정
    public bool IsKeptForced
    {
        set { if (value) State = DiceState.Kept; else if (State == DiceState.Kept) State = DiceState.OnBoard; }
    }
}
