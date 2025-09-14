using UnityEngine;
using TMPro;
using DG.Tweening;

public class LoseScript : MonoBehaviour
{
    
    [SerializeField]
    TextMeshProUGUI levelText;
    [SerializeField]
    TextMeshProUGUI bestText;

    [SerializeField]
    RectTransform rTransform;    

    public void Show(){

        levelText.text = (SaveManger.getLevel() + 1).ToString();
        bestText.text = SaveManger.getBest().ToString();
        gameObject.SetActive(true);
        rTransform.localScale = Vector3.zero;
        rTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);

        

    }

    public void Hide(){
        gameObject.SetActive(false);
    }
}
