using UnityEngine;

public enum AIDifficulty { Easy, Normal, Hard }

[System.Serializable]
public class PlayerData
{
    public string      name;
    public bool        isAI;
    public AIDifficulty aiDifficulty;
    public Sprite      avatarSprite;

    // -1 = 미기록, 0 이상 = 확정 점수
    public int[] scores;

    public bool IsAllFilled
    {
        get
        {
            if (scores == null) return false;
            foreach (int s in scores)
                if (s < 0) return false;
            return true;
        }
    }

    public int TotalScore
    {
        get
        {
            if (scores == null) return 0;
            int total = 0;
            foreach (int s in scores)
                if (s > 0) total += s;
            return total;
        }
    }

    public void Init(int categoryCount)
    {
        scores = new int[categoryCount];
        for (int i = 0; i < scores.Length; i++)
            scores[i] = -1;
    }
}
