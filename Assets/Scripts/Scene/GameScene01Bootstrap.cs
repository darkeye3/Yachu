using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 03_1_Game 씬을 런타임에 완전히 프로그래밍으로 구성하는 부트스트랩.
///
/// 사용법:
///   1. 빈 씬(03_1_Game)을 만든다.
///   2. 빈 GameObject를 만들고 이 컴포넌트를 붙인다.
///   3. Inspector에서 normalDicePrefab / diceShakerPrefab 을 연결한다.
///   4. Play Mode 진입 시 씬 전체가 자동 구성된다.
///
/// 레이아웃:
///   Canvas (1920×1080, Screen Space Overlay)
///   ├── LeftPanel  (0~30%)  : 점수판 (ScoreBoardUI)
///   └── RightPanel (30~100%): 보드 RawImage2 + CupArea(RawImage1)
///
/// 3D 월드:
///   DiceWorld GameObject
///   ├── DiceBox3D   — 물리 방 + 컵 + 보드 주사위
///   └── DiceAreaCameraSetup — BoardCamera→RawImage2, CupCamera→RawImage1
/// </summary>
[DefaultExecutionOrder(-100)]
public class GameScene01Bootstrap : MonoBehaviour
{
    [Header("주사위 프리팹 (필수)")]
    [SerializeField] private GameObject normalDicePrefab;
    [SerializeField] private GameObject wildDicePrefab;   // null 이면 normalDicePrefab 사용
    [SerializeField] private GameObject diceShakerPrefab;

    [Header("게임 설정 (선택 — 없으면 기본값)")]
    [SerializeField] private GameSettings gameSettings;

    // ================================================================
    // 진입점
    // ================================================================
    void Awake()
    {
        if (normalDicePrefab == null || diceShakerPrefab == null)
        {
            Debug.LogError("[Bootstrap] normalDicePrefab / diceShakerPrefab 을 Inspector에 연결하세요!");
            return;
        }
        BuildScene();
    }

    // ================================================================
    // 씬 전체 구성
    // ================================================================
    void BuildScene()
    {
        // 1. Canvas + EventSystem
        var canvas = BuildCanvas();
        BuildEventSystem();

        // 2. 왼쪽 패널: 점수판
        var leftPanel = BuildLeftPanel(canvas.transform);

        // 3. 오른쪽 패널: 보드 영역
        var rightPanel = BuildRightPanel(canvas.transform);

        // ★ "DiceBox3DDisplay" RawImage — DiceAreaCameraSetup이 Awake에서 찾아 연결
        BuildBoardRawImage(rightPanel.transform);

        // ★ "CupArea" GO — DiceAreaCameraSetup이 Awake에서 찾아 CupDiceView 삽입
        BuildCupArea(rightPanel.transform);

        // 오버레이 UI (Canvas 직속 자식)
        var timerGO   = BuildTimerUI(canvas.transform);
        var regBtnGO  = BuildRegisterButton(canvas.transform);
        var bannerGO  = BuildTurnBanner(canvas.transform);
        var celebGO   = BuildCelebrationBanner(canvas.transform);

        // 4. 점수판 UI (LeftPanel 안)
        var sbUI = BuildScoreBoardUI(leftPanel.transform);

        // 5. 3D 주사위 방
        //    CupArea / DiceBox3DDisplay 가 이미 존재해야 DiceAreaCameraSetup이 찾을 수 있다.
        var diceBox3D = BuildDiceWorld();

        // 6. DiceController
        var diceCtrl = BuildDiceController(diceBox3D);

        // 7. DiceCup 컴포넌트를 CupArea에 부착
        var cupAreaGO = GameObject.Find("CupArea");
        BuildDiceCupComponent(cupAreaGO, diceCtrl);

        // 8. TurnManager
        BuildTurnManager(null);

        // 9. NetworkDiceRPC 항상 생성 (온라인/오프라인 무관, 내부에서 처리)
        BuildNetworkDiceRPC();

        Debug.Log("[Bootstrap] 03_1_Game 씬 구성 완료");
    }

    // ================================================================
    // Canvas / EventSystem
    // ================================================================
    Canvas BuildCanvas()
    {
        var go     = new GameObject("Canvas");
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 0;

        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        go.AddComponent<GraphicRaycaster>();
        return canvas;
    }

    void BuildEventSystem()
    {
        if (FindObjectOfType<UnityEngine.EventSystems.EventSystem>() != null) return;
        var go = new GameObject("EventSystem");
        go.AddComponent<UnityEngine.EventSystems.EventSystem>();
        go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
    }

    // ================================================================
    // 패널
    // ================================================================
    GameObject BuildLeftPanel(Transform canvasT)
    {
        var go = MakeGO("LeftPanel", canvasT);
        Anchor(go, 0f, 0f, 0.30f, 1f);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.09f, 0.09f, 0.13f);
        return go;
    }

    GameObject BuildRightPanel(Transform canvasT)
    {
        var go = MakeGO("RightPanel", canvasT);
        Anchor(go, 0.30f, 0f, 1f, 1f);
        var bg = go.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.08f);
        return go;
    }

    // ================================================================
    // 보드 RawImage (이름: "DiceBox3DDisplay")
    // ================================================================
    void BuildBoardRawImage(Transform rightPanelT)
    {
        var go = MakeGO("DiceBox3DDisplay", rightPanelT);
        StretchFill(go);
        var raw = go.AddComponent<RawImage>();
        raw.color          = Color.white;
        raw.raycastTarget  = false;
        go.SetActive(false); // DiceAreaCameraSetup.Awake()가 RT 연결 후 활성화
    }

    // ================================================================
    // 컵 영역 (이름: "CupArea")
    // DiceAreaCameraSetup.SetupCupCamera()가 CupBG(0)와 CupRing 사이에
    // CupDiceView(1)를 삽입한다.
    // ================================================================
    void BuildCupArea(Transform rightPanelT)
    {
        // ── CupArea 루트 ─────────────────────────────────────────────
        var cupAreaGO = MakeGO("CupArea", rightPanelT);
        var rt = cupAreaGO.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 0.5f);
        rt.anchorMax        = new Vector2(1f, 0.5f);
        rt.pivot            = new Vector2(1f, 0.5f);
        rt.anchoredPosition = new Vector2(-12f, 0f);
        rt.sizeDelta        = new Vector2(220f, 390f);

        // 투명 이미지 + Button (DiceCup 입력용)
        var areaImg       = cupAreaGO.AddComponent<Image>();
        areaImg.color     = Color.clear;
        cupAreaGO.AddComponent<Button>();
        cupAreaGO.AddComponent<CanvasGroup>();

        // Unity 버전에 따라 Knob.psd 경로가 다름 — 실패 시 null(기본 흰색 사각형) 사용
        Sprite knob = null;
        try { knob = Resources.GetBuiltinResource<Sprite>("UI/Skin/Knob.psd"); } catch { }

        // ── CupBG (index 0): 어두운 원형 배경 ────────────────────────
        var bgGO  = MakeGO("CupBG", cupAreaGO.transform);
        StretchFill(bgGO);
        var bgImg   = bgGO.AddComponent<Image>();
        bgImg.color = new Color(0.15f, 0.08f, 0.02f);
        bgImg.sprite         = knob;
        bgImg.raycastTarget  = false;

        // ── [index 1]: DiceAreaCameraSetup이 CupDiceView 를 삽입한다 ──

        // ── CupRing (index 1 → 삽입 후 index 2): 장식 링 ─────────────
        var ringGO  = MakeGO("CupRing", cupAreaGO.transform);
        StretchFill(ringGO);
        var ringImg   = ringGO.AddComponent<Image>();
        ringImg.color = new Color(0.50f, 0.30f, 0.08f, 0.55f);
        ringImg.sprite        = knob;
        ringImg.raycastTarget = false;

        // ── GaugeRing (RadialGauge) ───────────────────────────────────
        var gaugeGO  = MakeGO("GaugeRing", cupAreaGO.transform);
        StretchFill(gaugeGO);
        var gaugeImg   = gaugeGO.AddComponent<Image>();
        gaugeImg.sprite       = knob;
        gaugeImg.color        = new Color(1f, 0.84f, 0f, 0.85f);
        gaugeImg.raycastTarget = false;
        gaugeGO.AddComponent<RadialGauge>(); // Awake에서 Image 자동 탐색

        // ── 굴리기 횟수 뱃지 ─────────────────────────────────────────
        var badgeGO = MakeGO("RollCountBadge", cupAreaGO.transform);
        var badgeRt = badgeGO.GetComponent<RectTransform>();
        badgeRt.anchorMin       = new Vector2(0f, 0f);
        badgeRt.anchorMax       = new Vector2(1f, 0f);
        badgeRt.pivot           = new Vector2(0.5f, 0f);
        badgeRt.anchoredPosition = new Vector2(0f, 6f);
        badgeRt.sizeDelta        = new Vector2(0f, 26f);
        var badgeTmp = TMP(badgeGO, "굴리기 3회 남음", 14f, Color.white, TextAlignmentOptions.Center);

        // ── 힌트 텍스트 ───────────────────────────────────────────────
        var hintGO = MakeGO("HintText", cupAreaGO.transform);
        var hintRt = hintGO.GetComponent<RectTransform>();
        hintRt.anchorMin = new Vector2(0.1f, 0.3f);
        hintRt.anchorMax = new Vector2(0.9f, 0.7f);
        hintRt.offsetMin = hintRt.offsetMax = Vector2.zero;
        TMP(hintGO, "눌러서\n굴리기", 20f, new Color(1f, 1f, 1f, 0.65f), TextAlignmentOptions.Center);
        hintGO.SetActive(false);
    }

    // ================================================================
    // DiceCup 컴포넌트 부착 (CupArea에)
    // ================================================================
    void BuildDiceCupComponent(GameObject cupAreaGO, DiceController dc)
    {
        if (cupAreaGO == null) return;

        var radialGauge   = cupAreaGO.GetComponentInChildren<RadialGauge>(true);
        var rollCountText = cupAreaGO.transform.Find("RollCountBadge")?.GetComponent<TextMeshProUGUI>();

        // ── DiceCup ──────────────────────────────────────────────────
        var diceCup = cupAreaGO.AddComponent<DiceCup>();
        Set(diceCup, "cupImage",       cupAreaGO.transform.Find("CupBG")?.GetComponent<Image>());
        Set(diceCup, "cupButton",      cupAreaGO.GetComponent<Button>());
        Set(diceCup, "cupCanvasGroup", cupAreaGO.GetComponent<CanvasGroup>());
        Set(diceCup, "gauge",          radialGauge);
        Set(diceCup, "rollCountText",  rollCountText);
        Set(diceCup, "hintText",       cupAreaGO.transform.Find("HintText")?.GetComponent<TextMeshProUGUI>());
        Set(diceCup, "diceController", dc);

    }

    // ================================================================
    // 3D 주사위 방
    // ================================================================
    DiceBox3D BuildDiceWorld()
    {
        var go = new GameObject("DiceWorld");
        // AddComponent는 즉시 Awake를 호출한다.
        // DiceBox3D는 Start()에서 월드를 빌드하도록 수정됐으므로
        // 여기서 reflection으로 SerializeField를 먼저 세팅한 후 Start()에서 사용한다.
        var db3D = go.AddComponent<DiceBox3D>();
        Set(db3D, "normalDicePrefab", normalDicePrefab);
        Set(db3D, "wildDicePrefab",   wildDicePrefab != null ? wildDicePrefab : normalDicePrefab);
        Set(db3D, "diceShakerPrefab", diceShakerPrefab);

        // DiceAreaCameraSetup은 씬 하이어라키에 이미 존재 — Bootstrap에서 중복 생성 금지

        return db3D;
    }

    // ================================================================
    // DiceController
    // ================================================================
    DiceController BuildDiceController(DiceBox3D diceBox3D)
    {
        var go = new GameObject("DiceController");
        var dc = go.AddComponent<DiceController>();
        Set(dc, "diceBox3D", diceBox3D);
        return dc;
    }

    // ================================================================
    // 점수판 UI
    // ================================================================
    ScoreBoardUI BuildScoreBoardUI(Transform leftPanelT)
    {
        var go = MakeGO("ScoreBoard", leftPanelT);
        StretchFill(go);
        var sbUI = go.AddComponent<ScoreBoardUI>();

        // ── RoundText ────────────────────────────────────────────────
        var roundGO = MakeGO("RoundText", go.transform);
        var roundRt = roundGO.GetComponent<RectTransform>();
        roundRt.anchorMin        = new Vector2(0f, 1f);
        roundRt.anchorMax        = new Vector2(1f, 1f);
        roundRt.pivot            = new Vector2(0.5f, 1f);
        roundRt.anchoredPosition = new Vector2(0f, -6f);
        roundRt.sizeDelta        = new Vector2(0f, 34f);
        var roundTmp = TMP(roundGO, "Round 1/8", 20f, new Color(0.88f, 0.88f, 0.75f), TextAlignmentOptions.Center);

        // ── 플레이어 헤더 행 ─────────────────────────────────────────
        var headerGO = MakeGO("PlayerHeader", go.transform);
        var headerRt = headerGO.GetComponent<RectTransform>();
        headerRt.anchorMin        = new Vector2(0f, 1f);
        headerRt.anchorMax        = new Vector2(1f, 1f);
        headerRt.pivot            = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0f, -42f);
        headerRt.sizeDelta        = new Vector2(0f, 68f);
        BuildPlayerCards(headerGO.transform, 4);

        // ── 카테고리 목록 ────────────────────────────────────────────
        var catGO = MakeGO("CategoryList", go.transform);
        var catRt = catGO.GetComponent<RectTransform>();
        catRt.anchorMin = new Vector2(0f, 0f);
        catRt.anchorMax = new Vector2(1f, 1f);
        catRt.offsetMin = new Vector2(0f, 68f);   // 아래 TotalRow 높이
        catRt.offsetMax = new Vector2(0f, -114f); // 위 round+header 공간
        var vlg = catGO.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight    = true;
        vlg.childControlWidth     = true;
        vlg.childForceExpandHeight = true;
        vlg.childForceExpandWidth  = true;
        vlg.spacing = 1f;
        vlg.padding = new RectOffset(3, 3, 2, 2);
        BuildCategoryRows(catGO.transform, 4);

        // ── 합계 행 ──────────────────────────────────────────────────
        var totalGO = MakeGO("TotalRow", go.transform);
        var totalRt = totalGO.GetComponent<RectTransform>();
        totalRt.anchorMin        = new Vector2(0f, 0f);
        totalRt.anchorMax        = new Vector2(1f, 0f);
        totalRt.pivot            = new Vector2(0.5f, 0f);
        totalRt.anchoredPosition = new Vector2(0f, 4f);
        totalRt.sizeDelta        = new Vector2(0f, 60f);
        totalGO.AddComponent<Image>().color = new Color(0.12f, 0.28f, 0.12f);

        var totalHlg = totalGO.AddComponent<HorizontalLayoutGroup>();
        totalHlg.childControlHeight    = true;
        totalHlg.childControlWidth     = true;
        totalHlg.childForceExpandHeight = true;
        totalHlg.childForceExpandWidth  = false;
        totalHlg.spacing = 1f;

        // 라벨 셀
        var tlGO = MakeGO("TotalLabel", totalGO.transform);
        tlGO.AddComponent<LayoutElement>().preferredWidth = 148f;
        tlGO.AddComponent<Image>().color = new Color(0.09f, 0.22f, 0.09f);
        TMPOver(tlGO, "합계", 19f, Color.white, TextAlignmentOptions.Center);

        // 플레이어별 합계 텍스트 (최대 4명)
        var totalTexts = new TextMeshProUGUI[4];
        for (int i = 0; i < 4; i++)
        {
            var cGO = MakeGO($"TotalCell_{i}", totalGO.transform);
            cGO.AddComponent<LayoutElement>().flexibleWidth = 1f;
            cGO.AddComponent<Image>().color = new Color(0.16f, 0.82f, 0.16f, 0.9f);
            totalTexts[i] = TMPOver(cGO, "0", 22f, new Color(0.05f, 0.05f, 0.05f), TextAlignmentOptions.Center);
        }

        // ScoreBoardUI 필드 연결
        Set(sbUI, "roundText",       roundTmp);
        Set(sbUI, "playerRow",       headerGO.transform);
        Set(sbUI, "categoryList",    catGO.transform);
        Set(sbUI, "totalRow",        totalGO.transform);
        Set(sbUI, "totalScoreTexts", totalTexts);

        return sbUI;
    }

    void BuildPlayerCards(Transform parentT, int count)
    {
        var hlg = parentT.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.childControlHeight    = true;
        hlg.childControlWidth     = true;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth  = false;
        hlg.spacing = 1f;

        // 족보 라벨 스페이서
        var spacerGO = MakeGO("HeaderSpacer", parentT);
        spacerGO.AddComponent<LayoutElement>().preferredWidth = 148f;
        spacerGO.AddComponent<Image>().color = new Color(0.12f, 0.12f, 0.17f);
        TMPOver(spacerGO, "족보", 15f, new Color(0.72f, 0.72f, 0.72f), TextAlignmentOptions.Center);

        // 플레이어 카드
        for (int i = 0; i < count; i++)
        {
            var cardGO = MakeGO($"PlayerCard_{i}", parentT);
            cardGO.AddComponent<LayoutElement>().flexibleWidth = 1f;
            cardGO.AddComponent<Image>().color = new Color(0.18f, 0.18f, 0.24f);
            var card = cardGO.AddComponent<PlayerCard>();

            var nameGO = MakeGO("NameText", cardGO.transform);
            var nameRt = nameGO.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.48f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = nameRt.offsetMax = Vector2.zero;
            var nameTmp = TMP(nameGO, $"Player{i + 1}", 15f, Color.white, TextAlignmentOptions.Center);

            var scoreGO = MakeGO("ScoreText", cardGO.transform);
            var scoreRt = scoreGO.GetComponent<RectTransform>();
            scoreRt.anchorMin = new Vector2(0f, 0f);
            scoreRt.anchorMax = new Vector2(1f, 0.48f);
            scoreRt.offsetMin = scoreRt.offsetMax = Vector2.zero;
            var scoreTmp = TMP(scoreGO, "0", 19f, new Color(0.9f, 0.9f, 0.5f), TextAlignmentOptions.Center);

            Set(card, "nameText",  nameTmp);
            Set(card, "scoreText", scoreTmp);
        }
    }

    void BuildCategoryRows(Transform parentT, int playerSlots)
    {
        for (int row = 0; row < 8; row++)
        {
            var rowGO  = MakeGO($"CategoryRow_{row}", parentT);
            rowGO.AddComponent<Image>().color = row % 2 == 0
                ? new Color(0.13f, 0.13f, 0.18f)
                : new Color(0.16f, 0.16f, 0.21f);

            var hlg = rowGO.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlHeight    = true;
            hlg.childControlWidth     = true;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth  = false;
            hlg.spacing = 1f;
            hlg.padding = new RectOffset(0, 0, 1, 1);

            var rowComp = rowGO.AddComponent<CategoryRow>();

            // 카테고리 이름 셀
            var labelGO = MakeGO("CategoryLabel", rowGO.transform);
            labelGO.AddComponent<LayoutElement>().preferredWidth = 148f;
            labelGO.AddComponent<Image>().color = new Color(0.10f, 0.10f, 0.15f);
            var labelTmp = TMPOver(labelGO, ScoreCalculator.CategoryNames[row], 13f,
                                   new Color(0.85f, 0.85f, 0.85f), TextAlignmentOptions.MidlineLeft);
            labelTmp.margin = new Vector4(6f, 0f, 0f, 0f);

            // 점수 버튼 셀 (플레이어당 1개)
            for (int p = 0; p < playerSlots; p++)
            {
                var cellGO = MakeGO($"ScoreCell_{p}", rowGO.transform);
                cellGO.AddComponent<LayoutElement>().flexibleWidth = 1f;
                cellGO.AddComponent<Image>().color = Color.white;
                cellGO.AddComponent<Button>();
                // TMP는 CategoryRow.Init()이 자동 생성
            }

            // categoryName 필드 reflection 설정
            Set(rowComp, "categoryName", labelTmp);
        }
    }

    // ================================================================
    // 오버레이 UI
    // ================================================================
    GameObject BuildTimerUI(Transform canvasT)
    {
        var go = MakeGO("TimerUI", canvasT);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(1f, 1f);
        rt.anchorMax        = new Vector2(1f, 1f);
        rt.pivot            = new Vector2(1f, 1f);
        rt.anchoredPosition = new Vector2(-18f, -18f);
        rt.sizeDelta        = new Vector2(110f, 72f);
        go.AddComponent<Image>().color = new Color(0.14f, 0.14f, 0.18f, 0.92f);
        go.AddComponent<TimerUI>();

        var iconGO = MakeGO("CloverIcon", go.transform);
        var iconRt = iconGO.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.35f);
        iconRt.anchorMax = new Vector2(0.35f, 1f);
        iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
        iconGO.AddComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f);

        var textGO = MakeGO("TimerText", go.transform);
        var textRt = textGO.GetComponent<RectTransform>();
        textRt.anchorMin = new Vector2(0.3f, 0f);
        textRt.anchorMax = new Vector2(1f, 1f);
        textRt.offsetMin = textRt.offsetMax = Vector2.zero;
        TMP(textGO, "60", 36f, new Color(0.2f, 0.2f, 0.2f), TextAlignmentOptions.Center);

        return go;
    }

    GameObject BuildRegisterButton(Transform canvasT)
    {
        var go = MakeGO("RegisterButton", canvasT);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin        = new Vector2(0.5f, 0f);
        rt.anchorMax        = new Vector2(0.5f, 0f);
        rt.pivot            = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 20f);
        rt.sizeDelta        = new Vector2(260f, 56f);
        go.AddComponent<Image>().color = new Color(0.30f, 0.14f, 0.04f);
        go.AddComponent<Button>();
        go.AddComponent<RegisterButton>();

        var labelGO = MakeGO("Label", go.transform);
        StretchFill(labelGO);
        TMP(labelGO, "족보 등록", 22f, Color.white, TextAlignmentOptions.Center);

        return go;
    }

    GameObject BuildTurnBanner(Transform canvasT)
    {
        // TurnBannerUI.Awake()가 배경 + 텍스트를 자동 생성한다
        var go = MakeGO("TurnBanner", canvasT);
        go.AddComponent<TurnBannerUI>();
        return go;
    }

    GameObject BuildCelebrationBanner(Transform canvasT)
    {
        var go = MakeGO("CelebrationBanner", canvasT);
        go.AddComponent<CelebrationBannerUI>();
        return go;
    }

    GameObject BuildWildPopup(Transform canvasT)
    {
        var go = MakeGO("WildPopup", canvasT);
        StretchFill(go);
        // Image는 먼저 추가 (WildPopup보다 먼저)
        go.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.58f);

        // Panel — WildPopup 컴포넌트 추가 전에 모든 자식 빌드
        // (WildPopup.Awake가 SetActive(false)를 호출하므로 자식 TMP가 null 반환됨)
        var panelGO = MakeGO("Panel", go.transform);
        var panelRt = panelGO.GetComponent<RectTransform>();
        panelRt.anchorMin        = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax        = new Vector2(0.5f, 0.5f);
        panelRt.pivot            = new Vector2(0.5f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta        = new Vector2(380f, 290f);
        panelGO.AddComponent<Image>().color = new Color(0.12f, 0.08f, 0.04f);

        var titleGO = MakeGO("Title", panelGO.transform);
        var titleRt = titleGO.GetComponent<RectTransform>();
        titleRt.anchorMin = new Vector2(0f, 0.72f);
        titleRt.anchorMax = new Vector2(1f, 1f);
        titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;
        TMP(titleGO, "와일드 눈 선택", 22f, Color.white, TextAlignmentOptions.Center);

        // 숫자 버튼 6개 (2행 × 3열)
        var btns = new Button[6];
        for (int i = 0; i < 6; i++)
        {
            float x = (i % 3) * 118f - 118f;
            float y = (i < 3) ? 28f : -52f;
            var btnGO = MakeGO($"NumBtn_{i + 1}", panelGO.transform);
            var btnRt = btnGO.GetComponent<RectTransform>();
            btnRt.anchorMin        = new Vector2(0.5f, 0.5f);
            btnRt.anchorMax        = new Vector2(0.5f, 0.5f);
            btnRt.pivot            = new Vector2(0.5f, 0.5f);
            btnRt.anchoredPosition = new Vector2(x, y);
            btnRt.sizeDelta        = new Vector2(104f, 72f);
            btnGO.AddComponent<Image>().color = new Color(0.58f, 0.34f, 0.10f);
            btns[i] = btnGO.AddComponent<Button>();
            TMPOver(btnGO, (i + 1).ToString(), 30f, Color.white, TextAlignmentOptions.Center);
        }

        // 자식 빌드 완료 후 WildPopup 컴포넌트 추가 (Awake에서 SetActive(false) 호출)
        var popup = go.AddComponent<WildPopup>();
        Set(popup, "panel",      panelGO);
        Set(popup, "numButtons", btns);

        return go;
    }

    // ================================================================
    // TurnManager
    // ================================================================
    void BuildTurnManager(WildPopup wildPopup)
    {
        var go = new GameObject("TurnManager");
        go.AddComponent<AIPlayer>();
        var tm = go.AddComponent<TurnManager>();

        // TurnManager.InitCoroutine()이 FindObjectOfType으로 대부분의 레퍼런스를
        // 자동 탐색하지만, WildPopup은 inactive라서 수동 연결이 필요하다.
        if (wildPopup != null) Set(tm, "wildPopup", wildPopup);
        if (gameSettings != null) Set(tm, "settings", gameSettings);
    }

    // ================================================================
    // NetworkDiceRPC — RaiseEvent 방식, PhotonView 불필요
    // ================================================================
    void BuildNetworkDiceRPC()
    {
        var go = new GameObject("NetworkDiceRPC");
        go.AddComponent<NetworkDiceRPC>();
        Debug.Log($"[Bootstrap] NetworkDiceRPC 생성 — InRoom={Photon.Pun.PhotonNetwork.InRoom}, IsOnline={GameManager.Instance?.IsOnline}");
    }

    // ================================================================
    // 유틸리티 헬퍼
    // ================================================================

    /// RectTransform을 가진 자식 GO 생성
    /// new GameObject() 후 AddComponent<RectTransform>이 아닌
    /// 생성 시점에 RectTransform을 포함해야 TMP AddComponent가 정상 동작함
    static GameObject MakeGO(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        if (parent != null) go.transform.SetParent(parent, false);
        return go;
    }

    /// anchorMin/Max 설정으로 부모에 꽉 채우기
    static void StretchFill(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// 단순 앵커 설정
    static void Anchor(GameObject go, float xMin, float yMin, float xMax, float yMax)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(xMin, yMin);
        rt.anchorMax = new Vector2(xMax, yMax);
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    /// TextMeshProUGUI 추가 (go에 Image 등 Graphic이 없어야 함)
    static TextMeshProUGUI TMP(GameObject go, string text, float size, Color color,
                               TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = text;
        tmp.fontSize           = size;
        tmp.color              = color;
        tmp.alignment          = align;
        tmp.enableWordWrapping = false;
        return tmp;
    }

    /// Image가 이미 있는 GO 위에 텍스트 올리기 — child GO에 TMP를 붙여 반환
    /// (Image + TMP 동일 GO 금지: 두 Graphic이 CanvasRenderer 충돌)
    static TextMeshProUGUI TMPOver(GameObject bg, string text, float size, Color color,
                                   TextAlignmentOptions align = TextAlignmentOptions.Center)
    {
        var child = MakeGO("Label", bg.transform);
        StretchFill(child);
        return TMP(child, text, size, color, align);
    }

    /// Reflection으로 private/SerializeField 설정
    static void Set(object obj, string field, object value)
    {
        if (obj == null) return;
        var f = obj.GetType().GetField(field,
            BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
        if (f != null)
            f.SetValue(obj, value);
        else
            Debug.LogWarning($"[Bootstrap] 필드 없음: {obj.GetType().Name}.{field}");
    }
}
