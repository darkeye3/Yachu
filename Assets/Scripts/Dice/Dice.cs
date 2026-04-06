using System.Collections;
using UnityEngine;
using UnityEngine.UI;
#if DOTWEEN
using DG.Tweening;
#endif
using TMPro;

public class Dice : MonoBehaviour
{
    // ─── Inspector 연결 ──────────────────────────────────────────────
    [SerializeField] private Image     diceBG;
    [SerializeField] private Image     faceImage;
    [SerializeField] private Image     keepOverlay;
    [SerializeField] private Image     lockIcon;
    [SerializeField] private GameObject wildIndicator;
    [SerializeField] private Sprite[]  faceSprites;     // 인덱스 0=face_1 ~ 5=face_6

    [Header("색상")]
    [SerializeField] private Color normalColor = new Color(0.91f, 0.35f, 0.10f); // #E85A1A
    [SerializeField] private Color wildColor   = new Color(0.18f, 0.48f, 0.21f); // #2E7A35

    // ─── 상태 ────────────────────────────────────────────────────────
    public int  Value    { get; private set; }
    public bool IsKept   { get; private set; }
    public bool IsWild   { get; set; }

    private Vector2    _originPos;   // DiceArea 내 원래 위치
    private Button     _button;
    private DiceFaceUI _faceUI;

    public Vector2 OriginPos => _originPos;

    // ─── 이벤트 ──────────────────────────────────────────────────────
    public System.Action<Dice> OnDiceClicked;

    // ─── 초기화 ──────────────────────────────────────────────────────
    void Awake()
    {
        _button = GetComponent<Button>();
        if (_button) _button.onClick.AddListener(OnClick);
        _faceUI = GetComponentInChildren<DiceFaceUI>(true);
    }

    public void Init(bool isWild, Vector2 originPos)
    {
        IsWild = isWild;
        // originPos가 0이면 현재 씬에 배치된 위치를 원래 위치로 사용
        _originPos = (originPos == Vector2.zero) ? (Vector2)transform.localPosition : originPos;
        IsKept = false;

        if (diceBG) diceBG.color = IsWild ? wildColor : normalColor;
        if (keepOverlay) keepOverlay.gameObject.SetActive(false);
        if (lockIcon)    lockIcon.gameObject.SetActive(false);
        if (wildIndicator) wildIndicator.SetActive(false);

        SetFace(1);
    }

    // ─── 페이스 표시 ─────────────────────────────────────────────────
    public void SetFace(int value)
    {
        Value = value;

        if (_faceUI != null)
        {
            // DiceFaceUI가 있으면 점으로 표시, faceImage는 흰 배경으로만 사용
            _faceUI.SetValue(value);
            if (faceImage != null) { faceImage.sprite = null; faceImage.color = Color.white; }
        }
        else if (faceImage != null && faceSprites != null && faceSprites.Length > 0)
        {
            int idx = Mathf.Clamp(value - 1, 0, faceSprites.Length - 1);
            faceImage.sprite = faceSprites[idx];
        }
    }

    // ─── 굴림 애니메이션 ─────────────────────────────────────────────
    public IEnumerator RollAnimation(int finalValue, float duration = 0.6f)
    {
        float elapsed      = 0f;
        float flipInterval = 0.07f;
        float nextFlip     = 0f;

        // motion-blur 효과: 굴리는 동안 점 반투명
        _faceUI?.BeginRoll();

        // 흔들림 + 회전
#if DOTWEEN
        transform.DOShakePosition(duration, strength: new Vector3(8, 8, 0),
                                  vibrato: 15, randomness: 90f);
        transform.DORotate(new Vector3(0, 0, Random.Range(-360f, 360f)),
                           duration, RotateMode.FastBeyond360);
#else
        StartCoroutine(ShakeFallback(duration));
#endif

        // 슬롯머신 페이스 교체 (DiceFaceUI 없을 때만)
        while (elapsed < duration - 0.1f)
        {
            if (_faceUI == null && elapsed >= nextFlip)
            {
                SetFace(Random.Range(1, 7));
                nextFlip += flipInterval;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        // 최종 값 확정
        Value = finalValue;
        if (_faceUI != null)
            _faceUI.EndRoll(finalValue);
        else
            SetFace(finalValue);
        transform.localRotation = Quaternion.identity;

        // 착지 바운스
#if DOTWEEN
        transform.DOScale(1.15f, 0.08f).SetLoops(2, LoopType.Yoyo);
#else
        StartCoroutine(BounceFallback());
#endif

        // 와일드=1 표시
        if (wildIndicator)
            wildIndicator.SetActive(IsWild && Value == 1);
    }

    // ─── 물리 롤러용: 위치/회전 없이 면만 교체 ──────────────────────
    public IEnumerator RollAnimationFaceOnly(int finalValue, float duration)
    {
        _faceUI?.BeginRoll();

        float elapsed      = 0f;
        float flipInterval = 0.07f;
        float nextFlip     = 0f;

        while (elapsed < duration - 0.12f)
        {
            // DiceFaceUI 없을 때만 슬롯머신 면 교체
            if (_faceUI == null && elapsed >= nextFlip)
            {
                SetFace(Random.Range(1, 7));
                nextFlip += flipInterval;
            }
            elapsed += Time.deltaTime;
            yield return null;
        }

        Value = finalValue;
        if (_faceUI != null) _faceUI.EndRoll(finalValue);
        else                 SetFace(finalValue);

        if (wildIndicator) wildIndicator.SetActive(IsWild && Value == 1);
    }

    // ─── Keep 토글 ───────────────────────────────────────────────────
    public void ToggleKeep()
    {
        IsKept = !IsKept;

        if (IsKept)
        {
            if (keepOverlay) keepOverlay.gameObject.SetActive(true);
            if (lockIcon)    lockIcon.gameObject.SetActive(true);
#if DOTWEEN
            transform.DOScale(1.1f, 0.15f);
            transform.DOLocalMoveY(transform.localPosition.y + 8f, 0.15f);
#endif
            AudioManager.Play("dice_keep");
        }
        else
        {
            if (keepOverlay) keepOverlay.gameObject.SetActive(false);
            if (lockIcon)    lockIcon.gameObject.SetActive(false);
#if DOTWEEN
            transform.DOScale(1.0f, 0.1f);
            // 위치 복귀는 ReturnToOrigin()에서 처리하므로 여기서 DOLocalMoveY 제거
#endif
            AudioManager.Play("dice_keep");
        }
    }

    // ─── 원래 위치로 복귀 ────────────────────────────────────────────
    public void ReturnToOrigin()
    {
        transform.localRotation = Quaternion.identity;
        transform.localScale    = Vector3.one;
#if DOTWEEN
        transform.DOLocalMove(_originPos, 0.4f).SetEase(Ease.OutCubic);
#else
        transform.localPosition = _originPos;
#endif
    }

#if !DOTWEEN
    System.Collections.IEnumerator ShakeFallback(float duration)
    {
        float t           = 0f;
        float totalSpin   = Random.Range(-540f, 540f); // 굴리는 방향 랜덤
        Vector3 originPos = transform.localPosition;

        while (t < duration)
        {
            float progress = t / duration;
            // 위치 흔들기
            transform.localPosition = originPos + (Vector3)(Random.insideUnitCircle * 7f);
            // 스핀: 포물선 커브 (중간에 가장 빠르게, 시작/끝은 천천히)
            float spin = Mathf.Sin(progress * Mathf.PI) * totalSpin;
            transform.localRotation = Quaternion.Euler(0f, 0f, spin);
            t += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = originPos;
        transform.localRotation = Quaternion.identity;
    }
    System.Collections.IEnumerator BounceFallback()
    {
        float t = 0f;
        while (t < 0.16f) { float s = 1f + Mathf.Sin(t / 0.16f * Mathf.PI) * 0.15f; transform.localScale = Vector3.one * s; t += Time.deltaTime; yield return null; }
        transform.localScale = Vector3.one;
    }
#endif

    // ─── Keep 강제 해제 (턴 시작 시) ────────────────────────────────
    public void ForceUnkeep()
    {
        if (!IsKept) return;
        IsKept = false;
        if (keepOverlay) keepOverlay.gameObject.SetActive(false);
        if (lockIcon)    lockIcon.gameObject.SetActive(false);
        transform.localScale    = Vector3.one;
        transform.localPosition = _originPos;
    }

    // ─── 버튼 클릭 ───────────────────────────────────────────────────
    void OnClick()
    {
        OnDiceClicked?.Invoke(this);
    }

    public void SetInteractable(bool on)
    {
        if (_button) _button.interactable = on;
    }

    /// <summary>3D 모드일 때 배경/면 이미지를 숨기고 버튼만 남김</summary>
    public void SetBackgroundVisible(bool visible)
    {
        if (diceBG)    diceBG.enabled    = visible;
        if (faceImage) faceImage.enabled = visible;
        // DiceFaceUI 도트 숨김 (가장 중요)
        if (_faceUI)   _faceUI.gameObject.SetActive(visible);
        // keepOverlay / lockIcon 은 keep 피드백에 필요하므로 유지
    }
}
