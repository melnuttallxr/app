using UnityEngine;
using DG.Tweening;

public class UIRotationAnimation : MonoBehaviour
{
    [Tooltip("Время (в секундах) для совершения одного полного оборота (360°)")]
    public float rotationDuration = 2f;

    private RectTransform rectTransform;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();

        // Запускаем бесконечное вращение по оси Z (360° за rotationDuration секунд)
        rectTransform.DORotate(new Vector3(0, 0, 360), rotationDuration, RotateMode.FastBeyond360)
            .SetLoops(-1, LoopType.Restart)
            .SetEase(Ease.Linear);
    }
}