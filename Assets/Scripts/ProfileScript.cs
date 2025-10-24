using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class ProfileScript : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Image profileImage;
    [SerializeField] private TextMeshProUGUI bestText;
    [SerializeField] private RectTransform rTransform;

    [Header("Images")]
    [SerializeField] private Sprite defaultProfileImage;

    [SerializeField] IOSWebViewController policy;

    private const string ProfileFileName = "profile.png";
    private string ProfilePath => Path.Combine(Application.persistentDataPath, ProfileFileName);

    // === PUBLIC API ===

    public void Show()
    {
        bestText.text = SaveManger.getBest().ToString();
        nameInput.text = SaveManger.getName();
        nameInput.ForceLabelUpdate();

        LoadProfileImageIfAny();

        gameObject.SetActive(true);
        rTransform.localScale = Vector3.zero;
        rTransform.DOScale(Vector3.one, 0.5f).SetEase(Ease.OutBack);
    }

    public void Hide()
    {
        SaveManger.saveName(nameInput.text);
        gameObject.SetActive(false);
        if (policy) policy.SetOverlayVisible(true);
    }

    public void okPressed() => Hide();

    /// Открыть камеру, сделать фото, сохранить и установить как аватар.
    public void openCameraAndSetPrifileImage()
    {
        StartCoroutine(CaptureFromCameraOnce());
    }

    // === INTERNALS ===

    private void LoadProfileImageIfAny()
    {
        try
        {
            if (File.Exists(ProfilePath))
            {
                bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
                var png = File.ReadAllBytes(ProfilePath);
                if (tex.LoadImage(png))
                {
                    ApplyTextureToAvatar(tex);
                    return;
                }
            }
        }
        catch { /* ignore */ }

        profileImage.sprite = defaultProfileImage;
        profileImage.preserveAspect = true;
    }

    private IEnumerator CaptureFromCameraOnce()
    {
        // 1) Разрешение
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
            if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
            {
                Debug.LogWarning("Camera permission denied");
                yield break;
            }
        }

        // 2) Выбор устройства (предпочтительно фронталка)
        var devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogWarning("No camera devices found");
            yield break;
        }
        int index = 0;
        for (int i = 0; i < devices.Length; i++)
            if (devices[i].isFrontFacing) { index = i; break; }

        var camTex = new WebCamTexture(devices[index].name, 1280, 720, 30);
        camTex.Play();

        // 3) Дождаться валидного размера
        const float warmupTimeout = 8f;
        float t = 0f;
        while ((camTex.width <= 16 || camTex.height <= 16) && t < warmupTimeout)
        {
            t += Time.deltaTime;
            yield return null;
        }
        if (camTex.width <= 16 || camTex.height <= 16)
        {
            Debug.LogWarning("Camera warm-up failed");
            camTex.Stop();
            yield break;
        }

        // 4) Дождаться пары реальных кадров (борьба с «чёрным кадром»)
        int good = 0; t = 0f;
        while (good < 2 && t < 2f)
        {
            if (camTex.didUpdateThisFrame) good++;
            t += Time.deltaTime;
            yield return null;
        }

        // Сохраняем ориентацию/зеркало ДО остановки камеры
        int rawAngle = camTex.videoRotationAngle;             // 0/90/180/270
        bool vMirror = camTex.videoVerticallyMirrored;        // true/false
        bool isFront = devices[index].isFrontFacing;

        // 5) Снимаем кадр через RenderTexture → ReadPixels (надёжно на iOS/Metal и Android)
        int w = Mathf.Max(1, camTex.width);
        int h = Mathf.Max(1, camTex.height);
        var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
        if (!rt.IsCreated()) rt.Create();

        Graphics.Blit(camTex, rt);
        // гарантируем кадр рендера, чтобы ReadPixels не ушёл «в никуда»
        yield return new WaitForEndOfFrame();

        var prev = RenderTexture.active;
        RenderTexture.active = rt;

        int rw = rt.width;
        int rh = rt.height;
        var rect = new Rect(0, 0, rw, rh);

        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        var snap = new Texture2D(rw, rh, TextureFormat.RGBA32, false, linear);

        // читаем строго по размеру RT
        snap.ReadPixels(rect, 0, 0, false);
        snap.Apply();

        RenderTexture.active = prev;
        rt.Release();
        camTex.Stop();

        // 6) Правильная ориентация и зеркала
        var oriented = CorrectOrientation(snap, rawAngle, vMirror, isFront);

        // 7) Сохранить и показать
        SaveTexturePng(oriented, ProfilePath);
        ApplyTextureToAvatar(oriented);
    }

    private void ApplyTextureToAvatar(Texture2D tex)
    {
        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        profileImage.sprite = sprite;
        profileImage.preserveAspect = true;
    }

    private void SaveTexturePng(Texture2D tex, string path)
    {
        try
        {
            var png = tex.EncodeToPNG();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, png);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save profile image: {e.Message}");
        }
    }

    // ==== ОРИЕНТАЦИЯ/ЗЕРКАЛО ====

    /// Корректно поворачивает и «раззеркаливает» фронталку.
    private Texture2D CorrectOrientation(Texture2D src, int rotationAngle, bool verticallyMirrored, bool isFrontCamera)
    {
        Texture2D tex = src;

        // На реальных девайсах применять угол в ОБРАТНУЮ сторону (360 - angle).
        int angle = ((360 - (rotationAngle % 360)) + 360) % 360;
        if (angle == 90)       tex = Rotate90(tex);
        else if (angle == 180) tex = Rotate180(tex);
        else if (angle == 270) tex = Rotate270(tex);

        // Вертикальный флип от устройства
        if (verticallyMirrored)
            tex = FlipY(tex);

        // Фронталка обычно зеркалит по X — приводим к немирроренному виду
        if (isFrontCamera)
            tex = FlipX(tex);

        return tex;
    }

    private Texture2D FlipX(Texture2D src)
    {
        int w = src.width, h = src.height;
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false, QualitySettings.activeColorSpace == ColorSpace.Linear);
        var s = src.GetPixels32();
        var d = new Color32[s.Length];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                d[y * w + (w - 1 - x)] = s[y * w + x];

        dst.SetPixels32(d); dst.Apply();
        return dst;
    }

    private Texture2D FlipY(Texture2D src)
    {
        int w = src.width, h = src.height;
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false, QualitySettings.activeColorSpace == ColorSpace.Linear);
        var s = src.GetPixels32();
        var d = new Color32[s.Length];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                d[(h - 1 - y) * w + x] = s[y * w + x];

        dst.SetPixels32(d); dst.Apply();
        return dst;
    }

    private Texture2D Rotate90(Texture2D src)
    {
        int w = src.width, h = src.height;
        var dst = new Texture2D(h, w, TextureFormat.RGBA32, false, QualitySettings.activeColorSpace == ColorSpace.Linear);
        var s = src.GetPixels32();
        var d = new Color32[s.Length];

        // (x, y) -> (y, w-1-x)
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                d[x * h + (h - 1 - y)] = s[y * w + x];

        dst.SetPixels32(d); dst.Apply();
        return dst;
    }

    private Texture2D Rotate180(Texture2D src)
    {
        int w = src.width, h = src.height;
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false, QualitySettings.activeColorSpace == ColorSpace.Linear);
        var s = src.GetPixels32();
        var d = new Color32[s.Length];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                d[(h - 1 - y) * w + (w - 1 - x)] = s[y * w + x];

        dst.SetPixels32(d); dst.Apply();
        return dst;
    }

    private Texture2D Rotate270(Texture2D src)
    {
        int w = src.width, h = src.height;
        var dst = new Texture2D(h, w, TextureFormat.RGBA32, false, QualitySettings.activeColorSpace == ColorSpace.Linear);
        var s = src.GetPixels32();
        var d = new Color32[s.Length];

        // (x, y) -> (w-1-x, y) (эквивалент -90)
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                d[(w - 1 - x) * h + y] = s[y * w + x];

        dst.SetPixels32(d); dst.Apply();
        return dst;
    }
}