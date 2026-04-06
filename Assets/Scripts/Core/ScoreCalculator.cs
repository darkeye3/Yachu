using System.Linq;

/// <summary>
/// 야추 8개 카테고리 점수 계산.
/// categoryIndex: 0=Twos, 1=Threes, 2=Fours, 3=Fives, 4=Sixes,
///                5=FullHouse, 6=LargeStraight, 7=Yacht
/// </summary>
public static class ScoreCalculator
{
    // ─── 카테고리 이름 (UI용) ─────────────────────────────────────────
    public static readonly string[] CategoryNames =
    {
        "투 (2s)", "쓰리 (3s)", "포 (4s)", "파이브 (5s)", "식스 (6s)",
        "풀 하우스", "라지 스트레이트", "야 추"
    };

    // ─── 카테고리 최대 점수 (힌트용) ─────────────────────────────────
    public static readonly int[] MaxScores =
    {
        10, 15, 20, 25, 30, 25, 30, 50
    };

    /// <summary>
    /// diceValues: 5개 주사위 값 (1~6).
    /// 5번째 주사위(인덱스 4)가 1이면 와일드 — 1~6 모두 시도 후 최고 점수 반환 (동점 시 큰 수 우선).
    /// </summary>
    public static int Calculate(int categoryIndex, int[] diceValues)
    {
        int[] dice = (int[])diceValues.Clone();

        // 와일드 발동: 인덱스 4 값이 1일 때 자동으로 최적 숫자 적용
        if (dice.Length == 5 && dice[4] == 1)
        {
            int best = 0;
            for (int v = 6; v >= 1; v--)
            {
                dice[4] = v;
                int s = CalculateRaw(categoryIndex, dice);
                if (s > best) best = s;
            }
            return best;
        }

        return CalculateRaw(categoryIndex, dice);
    }

    static int CalculateRaw(int categoryIndex, int[] dice)
    {
        return categoryIndex switch
        {
            0 => CalcSameNumber(dice, 2),
            1 => CalcSameNumber(dice, 3),
            2 => CalcSameNumber(dice, 4),
            3 => CalcSameNumber(dice, 5),
            4 => CalcSameNumber(dice, 6),
            5 => CalcFullHouse(dice),
            6 => CalcLargeStraight(dice),
            7 => CalcYacht(dice),
            _ => 0
        };
    }

    // 특정 숫자의 합 (Twos~Sixes)
    static int CalcSameNumber(int[] dice, int number)
        => dice.Where(d => d == number).Sum();

    // 풀 하우스: 3+2 조합 → 두 종류 합산
    static int CalcFullHouse(int[] dice)
    {
        var groups = dice.GroupBy(d => d).Select(g => g.Count()).OrderByDescending(c => c).ToArray();
        if (groups.Length == 2 && ((groups[0] == 3 && groups[1] == 2) || (groups[0] == 2 && groups[1] == 3)))
            return dice.Sum();
        return 0;
    }

    // 라지 스트레이트: 5개 연속 (1-2-3-4-5 또는 2-3-4-5-6) → 30점
    static int CalcLargeStraight(int[] dice)
    {
        var sorted = dice.Distinct().OrderBy(d => d).ToArray();
        if (sorted.Length < 5) return 0;
        bool isSeq = true;
        for (int i = 1; i < sorted.Length; i++)
            if (sorted[i] != sorted[i - 1] + 1) { isSeq = false; break; }
        return isSeq ? 30 : 0;
    }

    // 야추: 5개 모두 같은 숫자 → 50점
    static int CalcYacht(int[] dice)
        => dice.Distinct().Count() == 1 ? 50 : 0;

    /// <summary>
    /// 모든 미기록 카테고리 중 최고 점수를 주는 카테고리 인덱스 반환.
    /// 타임아웃 자동 기록에 사용.
    /// </summary>
    public static int BestCategory(int[] diceValues, bool[] recorded)
    {
        int best = -1, bestScore = -1;
        for (int i = 0; i < 8; i++)
        {
            if (recorded[i]) continue;
            int s = Calculate(i, diceValues);
            if (s > bestScore) { bestScore = s; best = i; }
        }
        // 모두 0이면 첫 번째 미기록 카테고리 반환
        if (best < 0)
            for (int i = 0; i < 8; i++)
                if (!recorded[i]) return i;
        return best;
    }
}
