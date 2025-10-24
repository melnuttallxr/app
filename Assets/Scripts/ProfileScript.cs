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
    [SerializeField] private Image profileImage;           // сюда ставим аватар
    [SerializeField] private TextMeshProUGUI bestText;
    [SerializeField] private RectTransform rTransform;

    [Header("Images")]
    [SerializeField] private Sprite defaultProfileImage;   // дефолтная аватарка (можно круглую PNG)

    private const string ProfileFileName = "profile.png";
    private string ProfilePath => Path.Combine(Application.persistentDataPath, ProfileFileName);

    // ==== Public ====

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
    }

    public void okPressed() => Hide();

    /// Кнопка «Камера»: сделать фото, сохранить и установить.
    public void openCameraAndSetPrifileImage()
    {
        StartCoroutine(CaptureFromCameraOnce());
    }

    // ==== Internals ====

    private void LoadProfileImageIfAny()
    {
        if (File.Exists(ProfilePath))
        {
            try
            {
                bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, linear);
                var png = File.ReadAllBytes(ProfilePath);
                if (tex.LoadImage(png))
                {
                    SetAvatarFromTexture(tex);
                    return;
                }
            }
            catch { /* ignore */ }
        }

        profileImage.sprite = defaultProfileImage;
        profileImage.preserveAspect = true;
    }

    private IEnumerator CaptureFromCameraOnce()
{
    // Разрешение
    if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
    {
        yield return Application.RequestUserAuthorization(UserAuthorization.WebCam);
        if (!Application.HasUserAuthorization(UserAuthorization.WebCam))
        {
            Debug.LogWarning("Camera permission denied");
            yield break;
        }
    }

    // Выбрать фронталку, если есть
    var devices = WebCamTexture.devices;
    if (devices == null || devices.Length == 0)
    {
        Debug.LogWarning("No camera devices");
        yield break;
    }
    int index = 0;
    for (int i = 0; i < devices.Length; i++)
        if (devices[i].isFrontFacing) { index = i; break; }

    var camTex = new WebCamTexture(devices[index].name, 1280, 720, 30);
    camTex.Play();

    // Прогрев: дождаться валидного размера
    const float timeout = 8f;
    float t = 0f;
    while ((camTex.width <= 16 || camTex.height <= 16) && t < timeout)
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

    // Дождаться пары реальных кадров (борьба с чёрным кадром)
    int goodFrames = 0; t = 0f;
    while (goodFrames < 2 && t < 2f)
    {
        if (camTex.didUpdateThisFrame) goodFrames++;
        t += Time.deltaTime;
        yield return null;
    }

    // Создаём RT по фактическим размерам на этот кадр
    int w = Mathf.Max(1, camTex.width);
    int h = Mathf.Max(1, camTex.height);
    var rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
    if (!rt.IsCreated()) rt.Create();

    // Блит и ждём конец кадра, чтобы гарантировать валидные пиксели на Metal
    Graphics.Blit(camTex, rt);
    yield return new WaitForEndOfFrame();

    // Активируем RT и читаем строго его размеры (не camTex!)
    var prev = RenderTexture.active;
    RenderTexture.active = rt;

    int rw = rt.width;
    int rh = rt.height;
    var rect = new Rect(0, 0, rw, rh);

    bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
    var snap = new Texture2D(rw, rh, TextureFormat.RGBA32, false, linear);

    // Safety clamp на всякий случай (редкие дёрганья размера)
    rect.width  = Mathf.Clamp(rect.width,  0, rw);
    rect.height = Mathf.Clamp(rect.height, 0, rh);

    // Чтение пикселей
    snap.ReadPixels(rect, 0, 0, false);
    snap.Apply();

    // Чистим
    RenderTexture.active = prev;
    rt.Release();
    camTex.Stop();

    bool isFront = devices[index].isFrontFacing;

    // Ориентация/зеркало
    var oriented = CorrectOrientation(snap, camTex.videoRotationAngle, camTex.videoVerticallyMirrored, isFront);

    // Квадрат + круг
    var square = CropCenterSquare(oriented);
    var circular = MakeCircular(square);

    SaveTexturePng(circular, ProfilePath);
    SetAvatarFromTexture(circular);
}

    private void SetAvatarFromTexture(Texture2D tex)
    {
        var sprite = Sprite.Create(
            tex,
            new Rect(0, 0, tex.width, tex.height),
            new Vector2(0.5f, 0.5f),
            100f
        );
        profileImage.sprite = sprite;
        profileImage.type = Image.Type.Simple;
        profileImage.preserveAspect = true;
    }

    // --- ОРИЕНТАЦИЯ/ЗЕРКАЛО ---

    private Texture2D CorrectOrientation(Texture2D src, int rotationAngle, bool verticallyMirrored, bool isFrontCamera)
{
    Texture2D tex = src;

    // На реальных устройствах Apply нужно угол в ОБРАТНУЮ сторону:
    // если устройство говорит 90 — нам нужно повернуть на 270 (или 360-90)
    int angle = ((360 - (rotationAngle % 360)) + 360) % 360;

    if (angle == 90)       tex = Rotate90(tex);
    else if (angle == 180) tex = Rotate180(tex);
    else if (angle == 270) tex = Rotate270(tex);

    // После поворота применяем флаги зеркал
    // 1) Вертикальный флип от устройства (делает картинку «вверх правильно»)
    if (verticallyMirrored)
        tex = FlipY(tex);

    // 2) Фронталка обычно зеркальна по горизонтали — убираем «зеркало»,
    //    чтобы фото было как тебя видят другие (не как в зеркале).
    if (isFrontCamera)
        tex = FlipX(tex);

    return tex;
}

    private Texture2D FlipX(Texture2D src)
    {
        int w = src.width, h = src.height;
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false, QualitySettings.activeColorSpace == ColorSpace.Linear);
        var srcPix = src.GetPixels32();
        var dstPix = new Color32[srcPix.Length];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dstPix[y * w + (w - 1 - x)] = srcPix[y * w + x];

        dst.SetPixels32(dstPix); dst.Apply();
        return dst;
    }

    private Texture2D FlipY(Texture2D src)
    {
        int w = src.width, h = src.height;
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false, QualitySettings.activeColorSpace == ColorSpace.Linear);
        var srcPix = src.GetPixels32();
        var dstPix = new Color32[srcPix.Length];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dstPix[(h - 1 - y) * w + x] = srcPix[y * w + x];

        dst.SetPixels32(dstPix); dst.Apply();
        return dst;
    }

    private Texture2D Rotate90(Texture2D src)
    {
        int w = src.width, h = src.height;
        var dst = new Texture2D(h, w, TextureFormat.RGBA32, false, QualitySettings.activeColorSpace == ColorSpace.Linear);
        var srcPix = src.GetPixels32();
        var dstPix = new Color32[srcPix.Length];

        // (x, y) -> (y, w-1-x)
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dstPix[x * h + (h - 1 - y)] = srcPix[y * w + x];

        dst.SetPixels32(dstPix); dst.Apply();
        return dst;
    }

    private Texture2D Rotate180(Texture2D src)
    {
        int w = src.width, h = src.height;
        var dst = new Texture2D(w, h, TextureFormat.RGBA32, false, QualitySettings.activeColorSpace == ColorSpace.Linear);
        var srcPix = src.GetPixels32();
        var dstPix = new Color32[srcPix.Length];

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dstPix[(h - 1 - y) * w + (w - 1 - x)] = srcPix[y * w + x];

        dst.SetPixels32(dstPix); dst.Apply();
        return dst;
    }

    private Texture2D Rotate270(Texture2D src)
    {
        int w = src.width, h = src.height;
        var dst = new Texture2D(h, w, TextureFormat.RGBA32, false, QualitySettings.activeColorSpace == ColorSpace.Linear);
        var srcPix = src.GetPixels32();
        var dstPix = new Color32[srcPix.Length];

        // (x, y) -> (w-1-x, y)  (эквивалент -90)
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                dstPix[(w - 1 - x) * h + y] = srcPix[y * w + x];

        dst.SetPixels32(dstPix); dst.Apply();
        return dst;
    }

    // --- КРОП/КРУГ ---

    private Texture2D CropCenterSquare(Texture2D src)
    {
        int size = Mathf.Min(src.width, src.height);
        int x = (src.width - size) / 2;
        int y = (src.height - size) / 2;

        var pixels = src.GetPixels(x, y, size, size);
        var dst = new Texture2D(size, size, TextureFormat.RGBA32, false, QualitySettings.activeColorSpace == ColorSpace.Linear);
        dst.SetPixels(pixels);
        dst.Apply();
        return dst;
    }

    private Texture2D MakeCircular(Texture2D square)
    {
        int size = square.width; // == height
        var pixels = square.GetPixels();
        float r = size * 0.5f;
        float r2 = r * r;
        Vector2 c = new Vector2(r - 0.5f, r - 0.5f);

        for (int y = 0; y < size; y++)
        {
            float dy = y - c.y;
            for (int x = 0; x < size; x++)
            {
                float dx = x - c.x;
                int i = y * size + x;
                if (dx * dx + dy * dy > r2)
                {
                    var col = pixels[i];
                    col.a = 0f;
                    pixels[i] = col;
                }
            }
        }

        var dst = new Texture2D(size, size, TextureFormat.RGBA32, false, QualitySettings.activeColorSpace == ColorSpace.Linear);
        dst.SetPixels(pixels);
        dst.Apply();
        return dst;
    }

    private void SaveTexturePng(Texture2D tex, string path)
    {
        try
        {
            var png = tex.EncodeToPNG();
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(path, png);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Failed to save profile image: {e.Message}");
        }
    }
}