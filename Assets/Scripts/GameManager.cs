using UnityEngine;

public class GameManager : MonoBehaviour
{

    [SerializeField]
    IOSWebViewController webViewController;

    [SerializeField]
    GameObject startScreen;

    [SerializeField]
    GameObject gameScreen;

    [SerializeField]
    GameObject levelScreen;

    [SerializeField] 
    GameObject loseScreen;

    [SerializeField]
    AudioManager audioManager;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        ShowStart();
        webViewController.OpenURL("https://www.olx.ua/uk/");
    }

    public void ShowStart(){
        audioManager.StopMusic();
        startScreen.SetActive(true);
        gameScreen.SetActive(false);
        levelScreen.SetActive(false);
        var loseScript = loseScreen.GetComponent<LoseScript>();
        loseScript.Hide();
    }


    public void ShowGame(){
        startScreen.SetActive(false);
        gameScreen.SetActive(true);
        levelScreen.SetActive(false);
        var loseScript = loseScreen.GetComponent<LoseScript>();
        loseScript.Hide();
    }

    public void StartGame(){
        audioManager.checkPlay();
        ShowGame();
        var gameScript = gameScreen.GetComponent<GameScript>();
        gameScript.NewGame();
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
