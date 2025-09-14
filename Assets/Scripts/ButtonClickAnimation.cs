using UnityEngine;
using DG.Tweening;
using UnityEngine.EventSystems;

public class ButtonClickAnimation : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    public RectTransform targetTransform;

    public float scaleDownFactor = 0.9f;
    public float animationDuration = 0.1f;

    private void Awake()
    {
        if (targetTransform == null)
        {
            targetTransform = GetComponent<RectTransform>();
        }
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (targetTransform != null)
        {
            targetTransform.DOScale(scaleDownFactor, animationDuration);
        }
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (targetTransform != null)
        {
            targetTransform.DOScale(1f, animationDuration);
        }
    }
}