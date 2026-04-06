using UnityEngine;

/// <summary>
/// BoardGame 프리팹의 Plane을 BoardCamera 가시 영역에 맞게 스케일하고,
/// 그 테두리에 물리 벽(투명 BoxCollider)을 생성.
/// </summary>
public class DiceBoardCreator : MonoBehaviour
{
    [Header("── 보드 프리팹 ──────────────────────────")]
    [SerializeField] private GameObject boardPrefab;
    [SerializeField] private string     planeName   = "GameBoard"; // 프리팹 안 Plane 오브젝트 이름
    [SerializeField] private float      wallHeight  = 10f;       // 물리 벽 높이
    [SerializeField] private float      wallThickness = 0.5f;    // 물리 벽 두께

    public Transform BoardDiceParent { get; private set; }

    // 카메라 가시 영역 (SpawnDice 등 외부에서 참조 가능)
    public float VisibleWidth  { get; private set; }
    public float VisibleHeight { get; private set; }

    void Awake()
    {
        var diceParent = new GameObject("BoardDiceParent");
        diceParent.transform.SetParent(transform);
        diceParent.transform.position = DiceBox3D.RoomCenter;
        BoardDiceParent = diceParent.transform;
    }

    void Start()
    {
        if (boardPrefab == null)
        {
            Debug.LogWarning("[DiceBoardCreator] boardPrefab 미연결");
            return;
        }

        if (!CalcCameraArea(out float visW, out float visH))
        {
            Debug.LogWarning("[DiceBoardCreator] BoardCamera를 찾을 수 없습니다.");
            return;
        }

        VisibleWidth  = visW;
        VisibleHeight = visH;

        var board = Instantiate(boardPrefab, DiceBox3D.RoomCenter, Quaternion.identity, transform);
        board.name = "DiceBoard";

        FitPlane(board, visW, visH);
        BuildWalls(visW, visH);
        ApplySlippyMat(board);

        Debug.Log($"[DiceBoardCreator] 완료 — Plane {visW:F2}×{visH:F2}, 벽 높이 {wallHeight}");
    }

    // ── Plane 스케일 조정 ────────────────────────────────────────────
    void FitPlane(GameObject board, float visW, float visH)
    {
        var planeT = FindDeep(board.transform, planeName);
        if (planeT == null)
        {
            Debug.LogWarning($"[DiceBoardCreator] '{planeName}' 오브젝트를 프리팹에서 찾을 수 없습니다.");
            return;
        }

        // 현재 스케일을 1,1,1로 초기화한 뒤 실제 메시 바운드를 측정
        var originalScale = planeT.localScale;
        planeT.localScale = Vector3.one;

        var r = planeT.GetComponent<Renderer>();
        if (r == null) r = planeT.GetComponentInChildren<Renderer>();
        if (r == null)
        {
            Debug.LogWarning($"[DiceBoardCreator] '{planeName}'에 Renderer가 없습니다.");
            planeT.localScale = originalScale;
            return;
        }

        float meshW = r.bounds.size.x;
        float meshH = r.bounds.size.z;
        Debug.Log($"[DiceBoardCreator] GameBoard 실제 메시 크기 x={meshW:F2} z={meshH:F2}");

        planeT.localScale = new Vector3(visW / meshW, 1f, visH / meshH);
    }

    // ── 물리 벽 생성 (Plane 테두리) ──────────────────────────────────
    void BuildWalls(float visW, float visH)
    {
        var wallRoot = new GameObject("PhysicsWalls");
        wallRoot.transform.SetParent(transform);
        wallRoot.transform.position = DiceBox3D.RoomCenter;

        float hw = visW * 0.5f;
        float hh = visH * 0.5f;
        float wt = wallThickness;
        float wh = wallHeight;

        // 바닥
        AddWall(wallRoot, "Floor",  new Vector3(visW + wt*2, wt, visH + wt*2), new Vector3(0, -wt * 0.5f, 0));
        // 북/남 벽
        AddWall(wallRoot, "WallN",  new Vector3(visW + wt*2, wh, wt),          new Vector3(0, wh * 0.5f, -hh - wt * 0.5f));
        AddWall(wallRoot, "WallS",  new Vector3(visW + wt*2, wh, wt),          new Vector3(0, wh * 0.5f,  hh + wt * 0.5f));
        // 동/서 벽
        AddWall(wallRoot, "WallW",  new Vector3(wt, wh, visH + wt*2),          new Vector3(-hw - wt * 0.5f, wh * 0.5f, 0));
        AddWall(wallRoot, "WallE",  new Vector3(wt, wh, visH + wt*2),          new Vector3( hw + wt * 0.5f, wh * 0.5f, 0));
    }

    void AddWall(GameObject parent, string wallName, Vector3 size, Vector3 localPos)
    {
        var go = new GameObject(wallName);
        go.transform.SetParent(parent.transform);
        go.transform.localPosition = localPos;

        var mat = new PhysicMaterial($"{wallName}Bounce")
        {
            bounciness      = 0.85f,
            dynamicFriction = 0.1f,
            staticFriction  = 0.1f,
            bounceCombine   = PhysicMaterialCombine.Maximum,
            frictionCombine = PhysicMaterialCombine.Minimum,
        };
        go.AddComponent<BoxCollider>().size     = size;
        go.GetComponent<BoxCollider>().material = mat;
    }

    // ── 유틸 ─────────────────────────────────────────────────────────
    bool CalcCameraArea(out float visW, out float visH)
    {
        visW = visH = 0f;
        var camGO = GameObject.Find("BoardCamera");
        if (camGO == null) return false;
        var cam = camGO.GetComponent<Camera>();
        if (cam == null || !cam.orthographic) return false;

        visH = 2f * cam.orthographicSize;
        visW = visH * cam.aspect;
        return true;
    }

    void ApplySlippyMat(GameObject board)
    {
        var mat = new PhysicMaterial("BoardSlippy")
        {
            dynamicFriction = 0.02f,
            staticFriction  = 0.02f,
            bounciness      = 0.4f,
            frictionCombine = PhysicMaterialCombine.Minimum,
            bounceCombine   = PhysicMaterialCombine.Maximum,
        };
        foreach (var col in board.GetComponentsInChildren<Collider>())
            col.material = mat;
    }

    static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform child in root)
        {
            var result = FindDeep(child, name);
            if (result != null) return result;
        }
        return null;
    }
}
