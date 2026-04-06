using System.Linq;
using UnityEngine;

/// <summary>
/// AI 플레이어 의사결정 로직.
/// TurnManager에서 Easy/Normal/Hard 분기로 호출됨.
/// </summary>
public class AIPlayer : MonoBehaviour
{
    // ─── 다시 굴릴지 판단 ────────────────────────────────────────────
    public bool ShouldReroll(PlayerData player, int[] dice, int rollCount, int maxRolls)
    {
        if (rollCount >= maxRolls) return false;

        var diff = player.aiDifficulty;

        // Easy: 50% 확률로 재굴림
        if (diff == AIDifficulty.Easy)
            return Random.value < 0.5f;

        // Normal: 최고 점수가 30점 미만이면 재굴림
        bool[] recorded  = new bool[8];
        int    bestScore = BestPossibleScore(dice, recorded);
        if (diff == AIDifficulty.Normal)
            return bestScore < 30;

        // Hard: 야추·라지스트레이트 노리거나, 풀하우스 1개 부족 시 재굴림
        return !HasGoodHand(dice);
    }

    // ─── Keep할 주사위 결정 ──────────────────────────────────────────
    public bool[] DecideKeep(PlayerData player, int[] dice)
    {
        var keep = new bool[dice.Length];

        // 와일드(인덱스 4)는 항상 유지
        keep[4] = true;

        switch (player.aiDifficulty)
        {
            case AIDifficulty.Easy:
                // 가장 많은 숫자만 유지
                KeepMostFrequent(dice, keep);
                break;
            case AIDifficulty.Normal:
                KeepForBestCategory(dice, keep);
                break;
            case AIDifficulty.Hard:
                KeepForBestCategory(dice, keep, aggressive: true);
                break;
        }
        return keep;
    }

    // ─── 카테고리 선택 ───────────────────────────────────────────────
    public int ChooseCategory(PlayerData player, int[] dice, bool[] recorded)
    {
        // 점수가 가장 높은 미기록 카테고리 선택
        int bestCat = -1, bestScore = -1;
        for (int i = 0; i < 8; i++)
        {
            if (recorded[i]) continue;
            int score = ScoreCalculator.Calculate(i, dice);

            // Hard: 야추(50점) 우선
            if (player.aiDifficulty == AIDifficulty.Hard && i == 7 && score == 50)
                return 7;

            if (score > bestScore) { bestScore = score; bestCat = i; }
        }

        // 모두 0이면 첫 미기록 카테고리
        if (bestScore == 0)
            return ScoreCalculator.BestCategory(dice, recorded);

        return bestCat >= 0 ? bestCat : 0;
    }

    // ─── 내부 헬퍼 ───────────────────────────────────────────────────

    void KeepMostFrequent(int[] dice, bool[] keep)
    {
        int topNum = dice.Take(4).GroupBy(d => d)
                         .OrderByDescending(g => g.Count())
                         .First().Key;
        for (int i = 0; i < 4; i++)
            keep[i] = dice[i] == topNum;
    }

    void KeepForBestCategory(int[] dice, bool[] keep, bool aggressive = false)
    {
        // 야추 가능성: 4개 이상 같은 숫자 → 모두 유지
        var groups = dice.Take(4).GroupBy(d => d)
                         .OrderByDescending(g => g.Count()).ToArray();
        int topCount = groups[0].Count();
        int topNum   = groups[0].Key;

        if (topCount >= 3 || (aggressive && topCount >= 2))
        {
            for (int i = 0; i < 4; i++)
                keep[i] = dice[i] == topNum;
            return;
        }

        // 스트레이트 가능성: 중복 없는 연속 숫자 유지
        var distinct = dice.Take(4).Distinct().OrderBy(d => d).ToArray();
        if (distinct.Length >= 3)
        {
            bool seq = true;
            for (int i = 1; i < distinct.Length; i++)
                if (distinct[i] != distinct[i-1] + 1) { seq = false; break; }
            if (seq)
            {
                for (int i = 0; i < 4; i++)
                    keep[i] = distinct.Contains(dice[i]);
                return;
            }
        }

        // 기본: 가장 높은 숫자 유지
        int maxVal = dice.Take(4).Max();
        for (int i = 0; i < 4; i++)
            keep[i] = dice[i] == maxVal;
    }

    bool HasGoodHand(int[] dice)
    {
        // 야추 or 4오브어카인드 → 충분히 좋은 패
        var groups = dice.GroupBy(d => d).OrderByDescending(g => g.Count()).ToArray();
        return groups[0].Count() >= 4;
    }

    int BestPossibleScore(int[] dice, bool[] recorded)
    {
        int best = 0;
        for (int i = 0; i < 8; i++)
        {
            if (recorded[i]) continue;
            int s = ScoreCalculator.Calculate(i, dice);
            if (s > best) best = s;
        }
        return best;
    }
}
