using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 보드 전용 카메라 + 컵 전용 카메라 두 개를 생성하여 각각을 UI에 표시.
///
/// ┌─ 보드 카메라 ──────────────────────────────────────────────────────┐
/// │ 위치: (4, 15, 0), ortho=4  → 보드(X≈4) O, 컵(X=11) X             │
/// │ RenderTexture → DiceBox3DDisplay (DiceArea 첫 번째 자식) 활성화    │
/// └───────────────────────────────────────────────────────────────────┘
/// ┌─ 컵 카메라 ────────────────────────────────────────────────────────┐
/// │ 위치: (11, 12, 0), ortho=2.5 → 컵 주사위(X≈9.65~12.33)만 봄      │
/// │ RenderTexture → CupArea 안 CupBG-CupRing 사이 새 RawImage          │
/// └───────────────────────────────────────────────────────────────────┘
///
/// DiceBox3D 오브젝트(또는 씬의 아무 오브젝트)에 추가.
/// </summary>
public class DiceAreaCameraSetup : MonoBehaviour
{
    // ── 보드 카메라 설정 ────────────────────────────────────────────
    [Header("보드 카메라")]
    [Tooltip("보드(X≈4)만 보이도록 ortho를 좁게 설정. 컵(X=11) 제외")]
    [SerializeField] private Vector3 boardCamPos      = new Vector3(4f, 15f, 0f);
    [SerializeField] private float   boardOrthoSize   = 4f;   // X 범위 ≈ [-1.3, 9.3]
    // 투명 배경: 빈 영역은 뒤의 BoardBG UI 색상이 비쳐 보임
    [SerializeField] private Color   boardBgColor     = new Color(0f, 0f, 0f, 0f);
    [SerializeField] private int     boardRtWidth     = 1024;
    [SerializeField] private int     boardRtHeight    = 768;

    // ── 컵 카메라 설정 ──────────────────────────────────────────────
    [Header("컵 카메라")]
    [Tooltip("컵(X≈11)만 보이도록 좁은 ortho. 보드 바닥(X=4)은 범위 밖")]
    [SerializeField] private Vector3 cupCamPos        = new Vector3(11f, 12f, 0f);
    [SerializeField] private float   cupOrthoSize     = 2.5f;
    // 투명 배경: 컵 주사위만 보이고 CupBG(어두운 갈색)가 비쳐 보임
    [SerializeField] private Color   cupBgColor       = new Color(0.15f, 0.08f, 0.02f, 1f);
    [SerializeField] private int     cupRtWidth       = 200;
    [SerializeField] private int     cupRtHeight      = 320;

    // ── 런타임 ──────────────────────────────────────────────────────
    private RenderTexture _boardRT;
    private RenderTexture _cupRT;

    /// <summary>SetupCupCamera()가 생성한 CupDiceView RawImage (DiceCup이 참조)</summary>
    public RawImage CupDiceView { get; private set; }

    void Awake()
    {
        SetupBoardCamera();
        SetupCupCamera();
    }

    // ── 보드 카메라 ──────────────────────────────────────────────────
    void SetupBoardCamera()
    {
        // 전용 카메라 생성
        var camGO = new GameObject("BoardCamera");
        camGO.transform.SetParent(transform, false);
        camGO.transform.position = boardCamPos;
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = boardBgColor;
        cam.orthographic     = true;
        cam.orthographicSize = boardOrthoSize;
        cam.nearClipPlane    = 0.1f;
        cam.farClipPlane     = 25f;
        cam.depth            = 0f;

        _boardRT = new RenderTexture(boardRtWidth, boardRtHeight, 24, RenderTextureFormat.ARGB32) { name = "BoardRT" };
        cam.targetTexture = _boardRT;

        // DiceArea 안의 DiceBox3DDisplay(비활성) 찾기
        RawImage display = FindRawImage("DiceBox3DDisplay");
        if (display != null)
        {
            display.texture = _boardRT;
            display.gameObject.SetActive(true);
            // DiceArea 첫 번째 자식으로 유지 (다른 2D 주사위보다 뒤)
            display.transform.SetAsFirstSibling();
            Debug.Log("[DiceAreaCameraSetup] 보드 카메라 → DiceBox3DDisplay 연결 완료");
        }
        else
        {
            Debug.LogWarning("[DiceAreaCameraSetup] DiceBox3DDisplay RawImage를 찾을 수 없습니다.");
        }
    }

    // ── 컵 카메라 ────────────────────────────────────────────────────
    void SetupCupCamera()
    {
        // 컵 카메라 생성
        var camGO = new GameObject("CupCamera");
        camGO.transform.SetParent(transform, false);
        camGO.transform.position = cupCamPos;
        camGO.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags       = CameraClearFlags.SolidColor;
        cam.backgroundColor  = cupBgColor;
        cam.orthographic     = true;
        cam.orthographicSize = cupOrthoSize;
        cam.nearClipPlane    = 0.1f;
        cam.farClipPlane     = 20f;
        cam.depth            = 1f;   // 보드 카메라보다 나중에 렌더

        _cupRT = new RenderTexture(cupRtWidth, cupRtHeight, 24) { name = "CupRT" };
        cam.targetTexture = _cupRT;

        // CupArea 탐색
        var cupAreaGO = GameObject.Find("CupArea");
        if (cupAreaGO == null)
        {
            Debug.LogWarning("[DiceAreaCameraSetup] CupArea를 찾을 수 없습니다.");
            return;
        }

        // CupBG(index 0)와 CupRing(index 1) 사이에 컵 RawImage 삽입
        var cupRawGO = new GameObject("CupDiceView");
        cupRawGO.transform.SetParent(cupAreaGO.transform, false);

        var rt = cupRawGO.AddComponent<RectTransform>();
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.offsetMin        = Vector2.zero;
        rt.offsetMax        = Vector2.zero;

        var rawImg = cupRawGO.AddComponent<RawImage>();
        rawImg.texture      = _cupRT;
        rawImg.raycastTarget = false;
        CupDiceView = rawImg;   // DiceCup이 참조

        // CupBG(0번) 다음, CupRing(이제 2번) 앞에 위치 → 인덱스 1
        cupRawGO.transform.SetSiblingIndex(1);

        Debug.Log("[DiceAreaCameraSetup] 컵 카메라 → CupArea/CupDiceView 연결 완료");
    }

    // ── 유틸 ─────────────────────────────────────────────────────────
    /// <summary>비활성 오브젝트 포함하여 이름으로 RawImage 검색</summary>
    static RawImage FindRawImage(string goName)
    {
        foreach (var ri in Resources.FindObjectsOfTypeAll<RawImage>())
            if (ri.name == goName) return ri;
        return null;
    }

    void OnDestroy()
    {
        if (_boardRT != null) { _boardRT.Release(); Object.Destroy(_boardRT); }
        if (_cupRT   != null) { _cupRT  .Release(); Object.Destroy(_cupRT  ); }
    }
}
