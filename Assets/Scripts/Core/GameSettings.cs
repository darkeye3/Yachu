using UnityEngine;

[CreateAssetMenu(fileName = "GameSettings", menuName = "YachtDice/GameSettings")]
public class GameSettings : ScriptableObject
{
    [Header("게임 기본 설정")]
    public int maxRollCount = 3;
    public int totalRounds  = 8;
    public float turnTimeLimit = 15f;   // 제한 시간 (초) — 매 굴림마다 초기화

    [Header("주사위 설정")]
    public int diceCount     = 5;
    public int keepSlotCount = 4;

    [Header("컵 게이지")]
    public float gaugeChargeSpeed = 1.2f;  // 초당 게이지 충전량 (0~1 기준)
    public float gaugeDecaySpeed  = 0.8f;  // 버튼 뗐을 때 감소 속도

    [Header("감정 버튼")]
    public float emotionCooldown = 3f;

    [Header("AI 딜레이 (초)")]
    public float aiThinkDelay  = 1.0f;
    public float aiRollDelay   = 0.8f;
    public float aiScoreDelay  = 1.2f;
}
