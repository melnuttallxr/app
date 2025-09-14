using UnityEngine;
using DG.Tweening;

public class UIPulsatingEffect : MonoBehaviour
{
    [Tooltip("Фактор увеличения масштаба (например, 1.2 - увеличение на 20%)")]
    public float pulseScaleFactor = 1.2f;
    [Tooltip("Продолжительность одного цикла пульсации (увеличение или уменьшение)")]
    public float pulseDuration = 0.5f;

    private Vector3 originalScale;
    private RectTransform rectTransform;

    void Start()
    {
        // Получаем RectTransform UI-элемента
        rectTransform = GetComponent<RectTransform>();
        originalScale = rectTransform.localScale;
        
        // Анимируем масштаб UI-элемента: увеличиваем до originalScale * pulseScaleFactor и затем обратно
        rectTransform.DOScale(originalScale * pulseScaleFactor, pulseDuration)
                     .SetLoops(-1, LoopType.Yoyo)
                     .SetEase(Ease.InOutSine);
    }
}