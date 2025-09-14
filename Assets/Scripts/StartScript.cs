using UnityEngine;
using TMPro;

public class StartScript : MonoBehaviour
{

    [SerializeField]
    TextMeshProUGUI levelText;
    [SerializeField]
    TextMeshProUGUI bestText;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        levelText.text = (SaveManger.getLevel() + 1).ToString();
        bestText.text = SaveManger.getBest().ToString();
    }
}
