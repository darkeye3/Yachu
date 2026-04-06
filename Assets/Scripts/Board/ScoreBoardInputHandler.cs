using UnityEngine;

/// <summary>
/// Physics Raycast 기반 점수판 클릭 감지.
/// 씬에 독립 오브젝트로 배치하고 ScoreBoardObject를 연결한다.
/// </summary>
public class ScoreBoardInputHandler : MonoBehaviour
{
    [SerializeField] private ScoreBoardObject scoreBoardRef;
    [SerializeField] private LayerMask        scoreBoardLayer;
    [SerializeField] private float            rayDistance = 30f;

    void Start()
    {
        if (scoreBoardRef == null)
            scoreBoardRef = FindObjectOfType<ScoreBoardObject>();

        if (scoreBoardRef == null)
            Debug.LogWarning("[ScoreBoardInputHandler] ScoreBoardObject를 찾을 수 없습니다.");
    }

    void Update()
    {
        if (scoreBoardRef == null) return;
        if (!Input.GetMouseButtonDown(0)) return;

        var cam = Camera.main;
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, rayDistance, scoreBoardLayer))
            return;

        var cell = hit.collider.GetComponent<ScoreCellObject>();
        if (cell == null || !cell.IsClickable) return;

        scoreBoardRef.HandleCellClicked(cell.CategoryIndex, cell.PlayerIndex);
    }
}
