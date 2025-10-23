using UnityEngine;

public class SaveManger
{
    public static void saveLevel(int level){
        PlayerPrefs.SetInt("LEVEL", level);
        // Принудительное сохранение настроек (на некоторых платформах можно не вызывать, но для надёжности лучше сохранить)
        PlayerPrefs.Save();
    }

    public static int getLevel(){
        return PlayerPrefs.GetInt("LEVEL", 0);
    }

    public static void saveBest(int best){
        PlayerPrefs.SetInt("BEST", best);
        PlayerPrefs.Save();
    }

    public static int getBest()
    {
        return PlayerPrefs.GetInt("BEST", 0);
    }
    
    public static void saveName(string name){
        PlayerPrefs.SetString("NAME", name);
        PlayerPrefs.Save();
    }

    public static string getName(){
        return PlayerPrefs.GetString("NAME", "Guest");
    }

    public static bool isMusic(){
        return  PlayerPrefs.GetInt("isMusic", 1) == 1;
    }

    public static void isMusic(bool isMusic){
        var value = 0;
        if (isMusic){
            value = 1;
        }
        PlayerPrefs.SetInt("isMusic", value);

        PlayerPrefs.Save();
    }
}