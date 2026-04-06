using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 씬에 미리 배치된 카메라에 RenderTexture를 연결하는 컨트롤러.
/// 카메라 위치/크기/배경색은 Inspector 또는 Scene 뷰에서 직접 조절.
/// RenderTexture 해상도만 여기서 설정.
/// </summary>
public class DiceCameraController : MonoBehaviour
{
    [Header("── 보드 카메라 ──────────────────────────")]
    [Tooltip("씬에 배치된 보드 카메라 (위치·크기는 Camera 컴포넌트에서 조절)")]
    [SerializeField] private Camera   boardCamera;
    [Tooltip("보드 RenderTexture 가로 해상도")]
    [SerializeField] private int      boardRtWidth  = 1024;
    [Tooltip("보드 RenderTexture 세로 해상도")]
    [SerializeField] private int      boardRtHeight = 1024;
    [Tooltip("보드 화면을 표시할 RawImage (DiceBox3DDisplay)")]
    [SerializeField] private RawImage boardDisplay;

    [Header("── 컵 카메라 ──────────────────────────────")]
    [Tooltip("씬에 배치된 컵 카메라 (위치·크기는 Camera 컴포넌트에서 조절)")]
    [SerializeField] private Camera   cupCamera;
    [Tooltip("컵 RenderTexture 가로 해상도")]
    [SerializeField] private int      cupRtWidth  = 512;
    [Tooltip("컵 RenderTexture 세로 해상도")]
    [SerializeField] private int      cupRtHeight = 512;
    [Tooltip("컵 화면을 표시할 RawImage (CupDiceView)")]
    [SerializeField] private RawImage cupDisplay;

    // DiceCup이 참조할 수 있도록 public 프로퍼티로 노출
    public RawImage CupDiceView => cupDisplay;

    private RenderTexture _boardRT;
    private RenderTexture _cupRT;

    void Awake()
    {
        SetupBoardCamera();
        SetupCupCamera();
    }

    void SetupBoardCamera()
    {
        if (boardCamera == null)
        {
            Debug.LogWarning("[DiceCameraController] boardCamera가 연결되지 않았습니다.");
            return;
        }

        // 에디터에서 미리 할당된 RT가 있으면 그대로 사용, 없으면 런타임 생성
        if (boardCamera.targetTexture != null)
        {
            _boardRT = boardCamera.targetTexture;
        }
        else
        {
            _boardRT = new RenderTexture(boardRtWidth, boardRtHeight, 24, RenderTextureFormat.ARGB32) { name = "BoardRT" };
            boardCamera.targetTexture = _boardRT;
        }

        if (boardDisplay != null)
        {
            boardDisplay.texture = _boardRT;
            boardDisplay.gameObject.SetActive(true);
        }
        else Debug.LogWarning("[DiceCameraController] boardDisplay(DiceBox3DDisplay)가 연결되지 않았습니다.");

        Debug.Log($"[DiceCameraController] 보드 카메라 연결 완료 — RT={_boardRT.width}×{_boardRT.height}");
    }

    void SetupCupCamera()
    {
        if (cupCamera == null)
        {
            Debug.LogWarning("[DiceCameraController] cupCamera가 연결되지 않았습니다.");
            return;
        }

        if (cupCamera.targetTexture != null)
        {
            _cupRT = cupCamera.targetTexture;
        }
        else
        {
            _cupRT = new RenderTexture(cupRtWidth, cupRtHeight, 24, RenderTextureFormat.ARGB32) { name = "CupRT" };
            cupCamera.targetTexture = _cupRT;
        }

        if (cupDisplay != null)
            cupDisplay.texture = _cupRT;
        else
            Debug.LogWarning("[DiceCameraController] cupDisplay(CupDiceView)가 연결되지 않았습니다.");

        Debug.Log($"[DiceCameraController] 컵 카메라 연결 완료 — RT={_cupRT.width}×{_cupRT.height}");
    }

    void OnDestroy()
    {
        if (_boardRT != null) { _boardRT.Release(); Destroy(_boardRT); }
        if (_cupRT   != null) { _cupRT.Release();   Destroy(_cupRT);   }
    }
}
