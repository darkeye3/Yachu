/// <summary>
/// ScoreCell Quad에 붙는 경량 데이터 홀더.
/// ScoreBoardInputHandler가 Raycast hit 후 이 컴포넌트를 통해
/// 어떤 카테고리·플레이어 셀인지 식별한다.
/// </summary>
public class ScoreCellObject : UnityEngine.MonoBehaviour
{
    public int  CategoryIndex;
    public int  PlayerIndex;
    public bool IsClickable;
}
