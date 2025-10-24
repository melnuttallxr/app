using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class ProfileScript : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private Image profileImage;              // UI Image для аватара
    [SerializeField] private TextMeshProUGUI bestText;
    [SerializeField] private RectTransform rTransform;

    [Header("Defaults")]
    [SerializeField] private Sprite defaultProfileImage;      // дефолтная аватарка (можно круглую PNG)

    private const string ProfileFileName = "profile.png";
    private string ProfilePath => Path.Combine(Application.persistentDataPath, ProfileFileName);

    // === ПУБЛИЧНО ===

    public void Show()
    {
        // данные
        bestText.text = SaveManger.getBest().ToString();
        nameInput.text = SaveManger.getName();
        nameInput.ForceLabelUpdate();

        // аватар
        LoadProfileImageIfAny();

        // анимация
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

    /// Кнопка «Сменить фото» — откроет системное окно камеры (iOS/Android).
    public void OpenCamera()
    {
#if UNITY_IOS || UNITY_ANDROID
        if (!NativeCamera.DeviceHasCamera())
        {
            Debug.LogWarning("Device has no camera");
            return;
        }
        if (NativeCamera.IsCameraBusy())
            return;

        const int targetMax = 1024; // max размер загружаемой текстуры (iOS скейлит; на Android без эффекта) — см. README
        NativeCamera.TakePicture((path) =>
        {
            if (string.IsNullOrEmpty(path))
                return; // пользователь отменил

            // грузим изображение в Texture2D
            Texture2D tex = NativeCamera.LoadImageAtPath(path, targetMax);
            if (tex == null)
            {
                Debug.LogWarning("Failed to load photo at: " + path);
                return;
            }

            // квадрат + вырез круга (прозрачные углы)
            var square = CropCenterSquare(tex);
            var circular = MakeCircular(square);

            // сохраняем в persistentDataPath (PNG)
            SaveTexturePng(circular, ProfilePath);

            // ставим в UI
            SetAvatarFromTexture(circular);

        }, targetMax, true, NativeCamera.PreferredCamera.Front); // просим фронталку (не на всех девайсах гарантировано)
#else
        Debug.LogWarning("NativeCamera поддерживается только на iOS/Android. В Editor фото не снимается.");
#endif
    }

    /// Опционально: очистить фото и вернуть дефолтную картинку
    public void ResetPhoto()
    {
        try { if (File.Exists(ProfilePath)) File.Delete(ProfilePath); } catch { }
        profileImage.sprite = defaultProfileImage;
        profileImage.preserveAspect = true;
    }

    // === ВНУТРЕННЕЕ ===

    private void LoadProfileImageIfAny()
    {
        if (File.Exists(ProfilePath))
        {
            try
            {
                var png = File.ReadAllBytes(ProfilePath);
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
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

    private void SetAvatarFromTexture(Texture2D tex)
    {
        var sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                   new Vector2(0.5f, 0.5f), 100f);
        profileImage.sprite = sprite;
        profileImage.type = Image.Type.Simple;
        profileImage.preserveAspect = true;
    }

    private static Texture2D CropCenterSquare(Texture2D src)
    {
        int size = Mathf.Min(src.width, src.height);
        int x = (src.width - size) / 2;
        int y = (src.height - size) / 2;

        var pixels = src.GetPixels(x, y, size, size);
        var dst = new Texture2D(size, size, TextureFormat.RGBA32, false);
        dst.SetPixels(pixels);
        dst.Apply();
        return dst;
    }

    private static Texture2D MakeCircular(Texture2D square)
    {
        int size = square.width; // == height
        var pixels = square.GetPixels();
        float r = size * 0.5f;
        float r2 = r * r;
        var c = new Vector2(r - 0.5f, r - 0.5f);

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
                    col.a = 0f;      // обнуляем альфу вне круга
                    pixels[i] = col;
                }
            }
        }

        var dst = new Texture2D(size, size, TextureFormat.RGBA32, false);
        dst.SetPixels(pixels);
        dst.Apply();
        return dst;
    }

    private static void SaveTexturePng(Texture2D tex, string path)
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
            Debug.LogError("Failed to save profile image: " + e.Message);
        }
    }
}