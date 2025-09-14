using UnityEngine;
using TMPro;
using System.Collections;
using DG.Tweening;


public class LevelScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField]
    TextMeshProUGUI levelText;

    [Header("Параметры анимации")]
    [Tooltip("Продолжительность анимации показа (масштаб от 0 до 1)")]
    public float scaleUpDuration = 0.5f;
    [Tooltip("Продолжительность анимации скрытия (масштаб от 1 до 0)")]
    public float scaleDownDuration = 0.5f;

    [Header("Параметры интерполяции")]
    [Tooltip("Функция интерполяции для показа")]
    public Ease scaleUpEase = Ease.OutBack;
    [Tooltip("Функция интерполяции для скрытия")]
    public Ease scaleDownEase = Ease.InBack;

    public RectTransform rectTransform;

    void Start()
    {
        

    }

    public void ShowLevel(){
        gameObject.SetActive(true);
        rectTransform.localScale = Vector3.zero;
        levelText.text = "Level " + (SaveManger.getLevel() + 1).ToString();
        rectTransform.DOScale(Vector3.one, scaleUpDuration).SetEase(scaleUpEase);
        StartCoroutine(userWait());
    }

    IEnumerator userWait(){

        yield return new WaitForSeconds(2.5f);
        rectTransform.DOScale(Vector3.zero, scaleDownDuration)
            .SetEase(scaleDownEase)
            .OnComplete(() =>
            {
                gameObject.SetActive(false);
            });
        
    }
}
