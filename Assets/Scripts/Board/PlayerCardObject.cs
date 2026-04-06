using UnityEngine;
using TMPro;

/// <summary>
/// 점수판 상단의 플레이어 헤더 카드 (3D 월드 오브젝트 버전).
/// 이름 셀과 합계 점수 셀을 Quad로 구성한다.
/// </summary>
public class PlayerCardObject : MonoBehaviour
{
    // ─── 런타임 참조 ─────────────────────────────────────────────────
    private MeshRenderer _nameCellRenderer;
    private MeshRenderer _scoreCellRenderer;
    private TMP_Text     _nameText;
    private TMP_Text     _scoreText;

    // ─── 색상 ────────────────────────────────────────────────────────
    private static readonly Color ColorNormal    = new Color(0.25f, 0.55f, 0.85f); // 파란 헤더
    private static readonly Color ColorHighlight = new Color(0.95f, 0.75f, 0.10f); // 노란 하이라이트
    private static readonly Color ColorText      = Color.white;

    private static readonly MaterialPropertyBlock _mpb = new MaterialPropertyBlock();

    // ─── 초기화 ──────────────────────────────────────────────────────
    /// <summary>
    /// ScoreBoardObject.Build() 내부에서 호출.
    /// cellWidth, cellHeight: 셀 한 칸 크기 (월드 단위).
    /// material: 공유 Unlit/Color 머티리얼.
    /// font: TMP 3D 폰트.
    /// </summary>
    public void Init(PlayerData data, float cellWidth, float cellHeight,
                     Material material, TMP_FontAsset font)
    {
        // ── 이름 셀 ──────────────────────────────────────────────
        var nameGO = CreateQuad("NameCell", Vector3.zero,
                                cellWidth, cellHeight, material, font,
                                data.name, 0.22f);
        _nameCellRenderer = nameGO.GetComponent<MeshRenderer>();
        _nameText         = nameGO.GetComponentInChildren<TMP_Text>();

        // ── 점수 셀 ──────────────────────────────────────────────
        var scoreGO = CreateQuad("ScoreCell", new Vector3(0f, 0f, cellHeight),
                                 cellWidth, cellHeight * 0.6f, material, font,
                                 "0", 0.18f);
        _scoreCellRenderer = scoreGO.GetComponent<MeshRenderer>();
        _scoreText         = scoreGO.GetComponentInChildren<TMP_Text>();

        SetHighlight(false);
    }

    // ─── 공개 API ─────────────────────────────────────────────────────
    public void SetHighlight(bool on)
    {
        SetColor(_nameCellRenderer,  on ? ColorHighlight : ColorNormal);
        SetColor(_scoreCellRenderer, on ? ColorHighlight : ColorNormal);
    }

    public void UpdateScore(int score)
    {
        if (_scoreText) _scoreText.text = score.ToString();
    }

    // ─── 내부 헬퍼 ───────────────────────────────────────────────────

    GameObject CreateQuad(string goName, Vector3 localOffset,
                          float w, float h, Material mat,
                          TMP_FontAsset font, string label, float fontSize)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = goName;
        go.transform.SetParent(transform, false);
        go.transform.localPosition = localOffset;
        // top-down 카메라: Quad 기본 노멀은 +Z。회전해서 +Y(위쪽)로 향하게
        go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        go.transform.localScale    = new Vector3(w, h, 1f);

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = mat;

        // 콜라이더 불필요 (헤더는 클릭 대상 아님)
        Object.Destroy(go.GetComponent<MeshCollider>());

        // TMP 3D 텍스트
        AttachTMPText(go, font, label, fontSize);
        return go;
    }

    void AttachTMPText(GameObject parent, TMP_FontAsset font,
                      string label, float fontSize)
    {
        var textGO = new GameObject("Label");
        textGO.transform.SetParent(parent.transform, false);
        // Quad 로컬: Quad는 XY 평면, top-down 카메라에서 보이려면 +Z 방향 텍스트
        textGO.transform.localPosition = new Vector3(0f, 0f, -0.01f);
        textGO.transform.localRotation = Quaternion.identity;
        textGO.transform.localScale    = Vector3.one;

        var tmp = textGO.AddComponent<TextMeshPro>();
        tmp.text              = label;
        tmp.fontSize          = fontSize;
        tmp.color             = ColorText;
        tmp.alignment         = TextAlignmentOptions.Center;
        tmp.overflowMode      = TextOverflowModes.Ellipsis;
        tmp.enableWordWrapping = false;
        if (font != null) tmp.font = font;

        // 렌더 순서: 텍스트가 Quad 위에 오도록
        tmp.sortingOrder = 1;
    }

    static void SetColor(MeshRenderer r, Color c)
    {
        if (r == null) return;
        _mpb.SetColor("_Color", c);
        r.SetPropertyBlock(_mpb);
    }
}
