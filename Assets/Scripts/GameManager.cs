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
    GameObject profileScreen;

    [SerializeField]
    AudioManager audioManager;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Awake()
    {
        FindObjectOfType<OrientationManager>().LockPortraitAtStart();
    }
    
    void Start()
    {
        ShowStart();
        webViewController.OpenURL("https://appmatic.space/policy", true);
    }

    public void ShowStart(){
        audioManager.StopMusic();
        startScreen.SetActive(true);
        gameScreen.SetActive(false);
        levelScreen.SetActive(false);
        var loseScript = loseScreen.GetComponent<LoseScript>();
        loseScript.Hide();
        webViewController.SetOverlayVisible(true);

    }


    public void ShowGame(){
        startScreen.SetActive(false);
        gameScreen.SetActive(true);
        levelScreen.SetActive(false);
        var loseScript = loseScreen.GetComponent<LoseScript>();
        loseScript.Hide();
        webViewController.SetOverlayVisible(false);

    }

    public void StartGame(){
        audioManager.checkPlay();
        ShowGame();
        var gameScript = gameScreen.GetComponent<GameScript>();
        gameScript.NewGame();

    }

    public void ShowProfile()
    {
        var profileScript = profileScreen.GetComponent<ProfileScript>();
        profileScript.Show();
        webViewController.SetOverlayVisible(false);

    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
