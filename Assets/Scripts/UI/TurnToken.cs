using UnityEngine;
using UnityEngine.UI;
#if DOTWEEN
using DG.Tweening;
#endif

public class TurnToken : MonoBehaviour
{
    [SerializeField] private Image tokenAvatar;

    public void Refresh(Sprite avatarSprite)
    {
        if (tokenAvatar && avatarSprite)
            tokenAvatar.sprite = avatarSprite;

        // 팝인 애니메이션
        transform.localScale = Vector3.zero;
#if DOTWEEN
        transform.DOScale(1f, 0.3f).SetEase(Ease.OutBack);
#else
        transform.localScale = Vector3.one;
#endif
    }
}
