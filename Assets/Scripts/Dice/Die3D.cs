using System.Collections;
using UnityEngine;

/// <summary>
/// 3D 주사위 1개. DiceBox3D가 생성·관리합니다.
/// GetTopFace()는 로컬 축 방향과 월드 UP 내적으로 판정 → FBX 방향에 독립적.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Die3D : MonoBehaviour
{
    // ─────────────────────────────────────────────────────────────────
    // 로컬 6축 방향 → 면 번호 매핑
    // Dice_6 FBX를 씬에 놓고 면 1이 위를 향할 때 어떤 로컬 축이 +Y와 가장
    // 가까운지 확인한 뒤 Inspector에서 조정.
    // 기본값: 표준 주사위 (1↑=+Y, 6↓=-Y, 2→+X방향 … 등 일반적 배치)
    // ─────────────────────────────────────────────────────────────────
    [Header("로컬 축 → 면 번호 매핑 (FBX 맞게 조정)")]
    // ── 로컬 축 → 면 번호 (Dice_6 FBX 기준, 변경 금지) ─────────────
    // 면1(-90,0,0) → +Z 위  면2(0,0,0) → +Y 위
    // 면3(0,0,-90) → -X 위  면4(0,0,90) → +X 위
    // 면5(180,0,0) → -Y 위  면6(90,0,0) → -Z 위
    [Tooltip("+Y 로컬 축이 위를 향할 때의 면 번호")]
    [SerializeField] public int faceLocalUp      = 2;
    [Tooltip("-Y 로컬 축이 위를 향할 때의 면 번호")]
    [SerializeField] public int faceLocalDown     = 5;
    [Tooltip("+X 로컬 축이 위를 향할 때의 면 번호")]
    [SerializeField] public int faceLocalRight    = 4;
    [Tooltip("-X 로컬 축이 위를 향할 때의 면 번호")]
    [SerializeField] public int faceLocalLeft     = 3;
    [Tooltip("+Z 로컬 축이 위를 향할 때의 면 번호")]
    [SerializeField] public int faceLocalForward  = 1;
    [Tooltip("-Z 로컬 축이 위를 향할 때의 면 번호")]
    [SerializeField] public int faceLocalBack     = 6;

    // SnapToFace 용: 각 면이 위를 향하는 Euler 각도
    // GetTopFace 와 독립적. 스냅 후 비주얼 정렬에만 사용.
    // ── SnapToFace 용 Euler 각도 (Dice_6 FBX 실측값, 변경 금지) ─────
    [Header("SnapToFace 용 Euler 각도 (Dice_6 FBX 실측)")]
    [SerializeField] private Vector3[] faceUpEulers = new Vector3[]
    {
        Vector3.zero,                      // 0 미사용
        new Vector3(-90f,   0f,   0f),     // 1
        new Vector3(  0f,   0f,   0f),     // 2
        new Vector3(  0f,   0f, -90f),     // 3
        new Vector3(  0f,   0f,  90f),     // 4
        new Vector3(180f,   0f,   0f),     // 5
        new Vector3( 90f,   0f,   0f),     // 6
    };

    [Header("Keep 머티리얼 (없으면 색 유지)")]
    [SerializeField] private Material keptMaterial;

    public bool IsWild { get; set; }
    public bool IsKept { get; private set; }
    public int  Value  { get; set; } = 1;

    private Rigidbody    _rb;
    private MeshRenderer _mr;
    private Material     _originalMaterial;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _mr = GetComponent<MeshRenderer>();
        if (_mr != null) _originalMaterial = _mr.sharedMaterial;

        _rb.mass        = 1f;
        _rb.drag        = 0.3f;
        _rb.angularDrag = 0.3f;
        _rb.isKinematic = true;
    }

    // ── 굴리기 ────────────────────────────────────────────────────────
    public void Roll()
    {
        IsKept = false;
        _rb.isKinematic     = false;
        _rb.velocity        = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        Vector3 dir = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(0.3f, 1f),
            Random.Range(-1f, 1f)
        ).normalized;

        _rb.AddForce(dir * Random.Range(4f, 7f), ForceMode.Impulse);
        _rb.AddTorque(Random.insideUnitSphere * Random.Range(10f, 20f), ForceMode.Impulse);

        RestoreOriginalMaterial();
    }

    // ── 물리 정지 후 윗면 감지 ───────────────────────────────────────
    /// <summary>
    /// 메시가 붙은 transform 기준으로 6축 중 월드 UP과 내적이 가장 큰 방향을 찾아
    /// 매핑된 면 번호 반환. (루트가 아닌 메시 자식 기준이므로 FBX 회전 오프셋 무관)
    /// </summary>
    public int GetTopFace()
    {
        // 메시가 있는 transform 사용 (자식일 수도 있으므로)
        Transform t = _mr != null ? _mr.transform : transform;
        Vector3 worldUp = Vector3.up;

        var axes = new (Vector3 worldDir, int face)[]
        {
            ( t.up,       faceLocalUp      ),
            (-t.up,       faceLocalDown     ),
            ( t.right,    faceLocalRight    ),
            (-t.right,    faceLocalLeft     ),
            ( t.forward,  faceLocalForward  ),
            (-t.forward,  faceLocalBack     ),
        };

        int   bestFace = 1;
        float bestDot  = -2f;
        foreach (var (worldDir, face) in axes)
        {
            float dot = Vector3.Dot(worldDir, worldUp);
            if (dot > bestDot) { bestDot = dot; bestFace = face; }
        }

        // 캘리브레이션 확인용 로그 (맞으면 비활성화 가능)
        Debug.Log($"[Die3D] {name} GetTopFace={bestFace}  " +
                  $"+Y={Vector3.Dot(t.up,worldUp):F2} -Y={Vector3.Dot(-t.up,worldUp):F2} " +
                  $"+X={Vector3.Dot(t.right,worldUp):F2} -X={Vector3.Dot(-t.right,worldUp):F2} " +
                  $"+Z={Vector3.Dot(t.forward,worldUp):F2} -Z={Vector3.Dot(-t.forward,worldUp):F2}");

        return bestFace;
    }

    // ── 캘리브레이션 도우미 (Inspector 우클릭 → 현재 윗면 축 확인) ──
    [ContextMenu("현재 윗면 축 로그 출력")]
    void LogTopAxisDebug()
    {
        Transform t = _mr != null ? _mr.transform : transform;
        Vector3 up = Vector3.up;
        Debug.Log($"[Die3D] {name} 축별 worldUp 내적:\n" +
                  $"  +Y(faceLocalUp={faceLocalUp}):       {Vector3.Dot( t.up,      up):F3}\n" +
                  $"  -Y(faceLocalDown={faceLocalDown}):     {Vector3.Dot(-t.up,      up):F3}\n" +
                  $"  +X(faceLocalRight={faceLocalRight}):   {Vector3.Dot( t.right,   up):F3}\n" +
                  $"  -X(faceLocalLeft={faceLocalLeft}):     {Vector3.Dot(-t.right,   up):F3}\n" +
                  $"  +Z(faceLocalForward={faceLocalForward}): {Vector3.Dot( t.forward, up):F3}\n" +
                  $"  -Z(faceLocalBack={faceLocalBack}):     {Vector3.Dot(-t.forward, up):F3}");
    }

    // ── 정착 여부 ────────────────────────────────────────────────────
    public bool IsSettled()
    {
        if (_rb == null || _rb.isKinematic) return true;
        return _rb.velocity.magnitude < 0.08f &&
               _rb.angularVelocity.magnitude < 0.15f;
    }

    // ── 비주얼 클린업 (값은 이미 결정된 상태) ────────────────────────
    /// <summary>물리가 결정한 같은 면으로 회전 정렬. 값 조작 없음.</summary>
    public void SnapToFace(int face)
    {
        if (face < 1 || face > 6) return;
        _rb.isKinematic     = true;
        _rb.velocity        = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;
        transform.rotation  = Quaternion.Euler(faceUpEulers[face]);
    }

    /// <summary>face가 위를 향하는 목표 Quaternion 반환 (transform 변경 없음)</summary>
    public Quaternion FaceUpRotation(int face)
    {
        if (face < 1 || face > 6) return transform.rotation;
        return Quaternion.Euler(faceUpEulers[face]);
    }

    // ── Keep 표시 ────────────────────────────────────────────────────
    public void SetKept(bool kept)
    {
        IsKept = kept;
        if (_mr == null) return;
        if (kept && keptMaterial != null) _mr.material = keptMaterial;
        else RestoreOriginalMaterial();
    }

    // ── 턴 리셋 ──────────────────────────────────────────────────────
    public void ResetForNewTurn(Vector3 worldPosition)
    {
        _rb.isKinematic     = true;
        _rb.velocity        = Vector3.zero;
        _rb.angularVelocity = Vector3.zero;

        transform.position = worldPosition;
        transform.rotation = Quaternion.Euler(faceUpEulers[1]);

        IsKept = false;
        Value  = 1;
        RestoreOriginalMaterial();
    }

    void RestoreOriginalMaterial()
    {
        if (_mr != null && _originalMaterial != null)
            _mr.material = _originalMaterial;
    }
}
