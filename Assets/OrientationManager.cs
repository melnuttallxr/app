
using System.Collections;
using UnityEngine;

public class OrientationManager : MonoBehaviour
{
    // Вызови это очень рано (например, в первом экране/Bootstrap сцене)
    public void LockPortraitAtStart()
    {
#if UNITY_IOS || UNITY_ANDROID
        // Разрешаем только портрет
        Screen.autorotateToPortrait          = true;
        Screen.autorotateToPortraitUpsideDown= false;
        Screen.autorotateToLandscapeLeft     = false;
        Screen.autorotateToLandscapeRight    = false;

        // Жёстко ставим портрет сейчас
        Screen.orientation = ScreenOrientation.Portrait;
#endif
    }

    // Когда нужно разрешить и портрет, и альбомную — зови этот метод
    public void AllowPortraitAndLandscape()
    {
#if UNITY_IOS || UNITY_ANDROID
        StartCoroutine(EnableAutoRotationNextFrame());
#endif
    }

    private IEnumerator EnableAutoRotationNextFrame()
    {
        // На iOS бывает полезно подождать кадр перед включением авторотации
        yield return null;

        Screen.autorotateToPortrait           = true;
        Screen.autorotateToPortraitUpsideDown = false; // включи, если нужно
        Screen.autorotateToLandscapeLeft      = true;
        Screen.autorotateToLandscapeRight     = true;

        Screen.orientation = ScreenOrientation.AutoRotation;
    }

    // Если нужно обратно вернуть только портрет
    public void LockPortraitAgain()
    {
#if UNITY_IOS || UNITY_ANDROID
        Screen.autorotateToPortrait           = true;
        Screen.autorotateToPortraitUpsideDown = false;
        Screen.autorotateToLandscapeLeft      = false;
        Screen.autorotateToLandscapeRight     = false;

        Screen.orientation = ScreenOrientation.Portrait;
#endif
    }
}
