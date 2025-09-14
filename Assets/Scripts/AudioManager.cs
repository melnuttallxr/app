using UnityEngine;
using UnityEngine.UI;

public class AudioManager : MonoBehaviour
{

    [SerializeField]
    AudioSource backAudio;

    [SerializeField]
    Image musicBtn;

    [SerializeField]
    Sprite on;

    [SerializeField]
    Sprite off;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    

    public void checkPlay(){
        if(SaveManger.isMusic()){
            backAudio.Play();
            musicBtn.sprite = on;
        }else{
            musicBtn.sprite = off;
        }
    }

    public void StopMusic(){
        backAudio.Stop();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void togleMusic(){
        var isMusic = SaveManger.isMusic();
        if (isMusic){
            backAudio.Stop();
            musicBtn.sprite = off;
        }else{
            backAudio.Play();
            musicBtn.sprite = on;
        }
        SaveManger.isMusic(!isMusic);
    }
    
}
