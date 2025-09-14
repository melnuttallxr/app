using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Collections;

public class GameScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField]
    WorldGridLayout gameField;

    [SerializeField]
    GameObject firstTask;

    [SerializeField]
    Image firstImage;
    [SerializeField]
    TextMeshProUGUI firstText;

    [SerializeField]
    GameObject secondTask;

    [SerializeField]
    Image secondImage;
    [SerializeField]
    TextMeshProUGUI secondText;

    [SerializeField]
    Image musicImage;

    [SerializeField]
    TextMeshProUGUI scoreText;
    [SerializeField]
    TextMeshProUGUI movesText;

    [SerializeField]
    Sprite[] balls;

    [SerializeField]
    LoseScript loseScript;

    [SerializeField]
    LevelScript levelScript;

    public GameObject particlePrefab;


    int score = 0;
    int moves = 5;

    int[] targets;

    int firstTargetCount = 0;
    int secondTargetCount = 0;

    bool firstDone = false;
    bool secondDone = false;


    void Start()
    {
        
    }

    public void NewGame(bool isNew = true){

        levelScript.ShowLevel();
        if (isNew){
            score = 0;
        }
        var level = SaveManger.getLevel();
        firstDone = false;
        secondDone = false;
        gameField.NewGame();

        if (level < 3){
            targets = new int[] {Random.Range(0, 3)};
            firstTargetCount = 4;
        }else if (level < 6){
            targets = new int[] {Random.Range(0, 3)};
            firstTargetCount = 5;
        }else if (level < 10){
            targets = new int[] {Random.Range(0, 3), Random.Range(0, 3)};
            firstTargetCount = 4;
            secondTargetCount = 4;
        }else{
            targets = new int[] {Random.Range(0, 3), Random.Range(0, 3)};
            firstTargetCount = 5;
            secondTargetCount = 5;
        }

        if(targets.Length == 1){
            firstTask.SetActive(true);
            secondTask.SetActive(false);
            firstImage.sprite = balls[targets[0]];
            firstText.text = "0/" + firstTargetCount;
        }else{
            firstTask.SetActive(true);
            secondTask.SetActive(true);
            firstImage.sprite = balls[targets[0]];
            secondImage.sprite = balls[targets[1]];
            firstText.text = "0/" + firstTargetCount;
            secondText.text = "0/" + secondTargetCount;
        }

        moves = 5;
        scoreText.text = score.ToString();
        movesText.text = moves.ToString();

    }


    public void chekWin(List<Cell> cells, int type, int count){
        score += count;
        moves -= 1;
        scoreText.text = score.ToString();
        movesText.text = moves.ToString();
        if(score > SaveManger.getBest()){
            SaveManger.saveBest(score);
        }

        if(targets.Length == 1){
            if(targets[0] == type && firstTargetCount <= count){
                firstDone = true;
                foreach(Cell cell in cells){
                    var transform = cell.gameObject.transform;
                    GameObject particleInstance = Instantiate(particlePrefab, transform.localPosition, Quaternion.identity);
                    ParticleSystem ps = particleInstance.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Play();
                        StartCoroutine(DestroyAfterCompletion(ps, particleInstance));
                    }
                }
                 goNextLevel();
                 return;
            }
        }else{
            if(type == targets[0] && !firstDone){
                if(count >= firstTargetCount){
                    firstDone = true;
                    firstText.text = firstTargetCount + "/" + firstTargetCount;
                    foreach(Cell cell in cells){
                    var transform = cell.gameObject.transform;
                    GameObject particleInstance = Instantiate(particlePrefab, transform.localPosition, Quaternion.identity);
                    ParticleSystem ps = particleInstance.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Play();
                        StartCoroutine(DestroyAfterCompletion(ps, particleInstance));
                    }
                    }
                    if (secondDone){
                        goNextLevel();
                        return;
                    }
                }
            }else if(type == targets[1]){
                if(count >= secondTargetCount){
                    secondDone = true;
                    secondText.text = secondTargetCount + "/" + secondTargetCount;
                    foreach(Cell cell in cells){
                    var transform = cell.gameObject.transform;
                    GameObject particleInstance = Instantiate(particlePrefab, transform.localPosition, Quaternion.identity);
                    ParticleSystem ps = particleInstance.GetComponent<ParticleSystem>();
                    if (ps != null)
                    {
                        ps.Play();
                        StartCoroutine(DestroyAfterCompletion(ps, particleInstance));
                    }
                }
                    if (firstDone){
                        goNextLevel();
                        return;
                    }
                }
            }
        }

        if(moves == 0){
            setLose();
        }
    }

    IEnumerator DestroyAfterCompletion(ParticleSystem ps, GameObject instance)
    {
        // Ждем, пока партикл-система полностью не остановится (все частицы исчезнут)
        yield return new WaitUntil(() => !ps.IsAlive(true));
        // Удаляем объект
        Destroy(instance);
    }

    void goNextLevel(){
        SaveManger.saveLevel(SaveManger.getLevel() + 1);
        NewGame(false);
        
    }

    void setLose(){
        loseScript.Show();
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
