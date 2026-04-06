using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// DiceArea 오브젝트에 붙이세요.
/// 주사위를 실제 화면(world) 좌표 기준으로 굴려서 벽에 튕기게 합니다.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DicePhysicsRoller : MonoBehaviour
{
    [Header("물리 파라미터")]
    [SerializeField] private float launchSpeedMin  = 400f;   // 초기 속력 최솟값 (world px/s)
    [SerializeField] private float launchSpeedMax  = 700f;   // 초기 속력 최댓값 (world px/s)
    [SerializeField] private float angularSpeedMax = 800f;   // 최대 각속도 (deg/s)
    [SerializeField] private float linearDamping   = 0.985f; // 선속도 감쇠 (1에 가까울수록 오래 굴러감)
    [SerializeField] private float angularDamping  = 0.980f; // 각속도 감쇠
    [SerializeField] private float bounceLoss      = 0.72f;  // 벽 반사 시 속력 유지 비율

    private RectTransform _boundsRect;

    void Awake()
    {
        _boundsRect = GetComponent<RectTransform>();
    }

    public IEnumerator RollAll(Dice[] dices, float duration)
    {
        // ── 바운드를 world space corners로 계산 ──────────────────────
        var corners = new Vector3[4];
        _boundsRect.GetWorldCorners(corners);
        // corners[0]=bottomLeft, [1]=topLeft, [2]=topRight, [3]=bottomRight
        float xMin = corners[0].x;
        float xMax = corners[2].x;
        float yMin = corners[0].y;
        float yMax = corners[2].y;

        // 주사위 world 반크기 추정 (rect 픽셀 vs world 픽셀 비율)
        float worldPerRect = (xMax - xMin) / Mathf.Max(_boundsRect.rect.width, 1f);
        float halfSize     = 40f * worldPerRect;

        Debug.Log($"[DicePhysicsRoller] Bounds x={xMin:F0}~{xMax:F0} y={yMin:F0}~{yMax:F0} halfSize={halfSize:F1}");

        // ── 각 주사위 초기 상태 ──────────────────────────────────────
        var states = new PhysState[dices.Length];
        for (int i = 0; i < dices.Length; i++)
        {
            var rt  = dices[i].GetComponent<RectTransform>();
            var dir = Random.insideUnitCircle.normalized;
            float spd = Random.Range(launchSpeedMin, launchSpeedMax);

            states[i] = new PhysState
            {
                rt          = rt,
                worldPos    = (Vector2)rt.position,
                originWorld = (Vector2)rt.position,
                vel         = dir * spd,
                rot         = rt.localEulerAngles.z,
                angVel      = Random.Range(-angularSpeedMax, angularSpeedMax),
            };
        }

        float elapsed  = 0f;
        float settleAt = duration * 0.78f;   // 78% 지점부터 복귀 시작

        while (elapsed < duration)
        {
            float dt = Time.deltaTime;
            elapsed += dt;

            float linDamp = Mathf.Pow(linearDamping,  dt * 60f);
            float angDamp = Mathf.Pow(angularDamping, dt * 60f);

            for (int i = 0; i < states.Length; i++)
            {
                var s = states[i];

                if (elapsed < settleAt)
                {
                    // ── 물리 단계 ──────────────────────────────────
                    s.vel    *= linDamp;
                    s.angVel *= angDamp;
                    s.worldPos += s.vel * dt;
                    s.rot      += s.angVel * dt;

                    // 벽 바운스
                    if (s.worldPos.x + halfSize > xMax)
                    {
                        s.worldPos.x = xMax - halfSize;
                        s.vel.x      = -Mathf.Abs(s.vel.x) * bounceLoss;
                        s.angVel    *= -0.65f;
                    }
                    if (s.worldPos.x - halfSize < xMin)
                    {
                        s.worldPos.x = xMin + halfSize;
                        s.vel.x      =  Mathf.Abs(s.vel.x) * bounceLoss;
                        s.angVel    *= -0.65f;
                    }
                    if (s.worldPos.y + halfSize > yMax)
                    {
                        s.worldPos.y = yMax - halfSize;
                        s.vel.y      = -Mathf.Abs(s.vel.y) * bounceLoss;
                    }
                    if (s.worldPos.y - halfSize < yMin)
                    {
                        s.worldPos.y = yMin + halfSize;
                        s.vel.y      =  Mathf.Abs(s.vel.y) * bounceLoss;
                    }
                }
                else
                {
                    // ── 복귀 단계: EaseOutCubic ────────────────────
                    float t    = (elapsed - settleAt) / (duration - settleAt);
                    float ease = 1f - Mathf.Pow(1f - Mathf.Clamp01(t), 3f);
                    s.worldPos = Vector2.Lerp(s.worldPos, s.originWorld, ease);
                    s.rot      = Mathf.LerpAngle(s.rot, 0f, ease);
                }

                s.rt.position         = new Vector3(s.worldPos.x, s.worldPos.y, s.rt.position.z);
                s.rt.localEulerAngles = new Vector3(0f, 0f, s.rot);
            }

            yield return null;
        }

        // 최종 스냅
        foreach (var s in states)
        {
            s.rt.position         = new Vector3(s.originWorld.x, s.originWorld.y, s.rt.position.z);
            s.rt.localEulerAngles = Vector3.zero;
        }
    }

    class PhysState
    {
        public RectTransform rt;
        public Vector2       worldPos;
        public Vector2       originWorld;
        public Vector2       vel;
        public float         rot;
        public float         angVel;
    }
}
