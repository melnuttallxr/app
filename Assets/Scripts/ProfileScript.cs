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
    [SerializeField] private Sprite defaultProfileImage;   // дефолтный аватар (можно круглую PNG)

    private const string ProfileFileName = "profile.png";
    private string ProfilePath => Path.Combine(Application.persistentDataPath, ProfileFileName);

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

    // КНОПКА КАМЕРЫ
    public void openCameraAndSetPrifileImage()
    {
        StartCoroutine(CaptureFromCameraOnce());
    }

    // === Internals ===

    private void LoadProfileImageIfAny()
    {
        // показываем сохранённое фото, иначе дефолт
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

        // Первая фронтальная, если есть
        var devices = WebCamTexture.devices;
        if (devices == null || devices.Length == 0)
        {
            Debug.LogWarning("No camera devices");
            yield break;
        }
        int index = 0;
        for (int i = 0; i < devices.Length; i++)
            if (devices[i].isFrontFacing) { index = i; break; }

        var camTex = new WebCamTexture(devices[index].name, 1024, 1024, 30);
        camTex.Play();

        // Прогрев
        float t = 0f, timeout = 5f;
        while (camTex.width <= 16 || camTex.height <= 16)
        {
            if ((t += Time.deltaTime) > timeout) { camTex.Stop(); yield break; }
            yield return null;
        }

        // Снимок
        var raw = new Texture2D(camTex.width, camTex.height, TextureFormat.RGBA32, false);
        raw.SetPixels(camTex.GetPixels());
        raw.Apply();
        camTex.Stop();

        // Квадрат + круглая маска
        var square = CropCenterSquare(raw);
        var circular = MakeCircular(square);
        SaveTexturePng(circular, ProfilePath);

        SetAvatarFromTexture(circular);
    }

    private void SetAvatarFromTexture(Texture2D tex)
    {
        // создаём спрайт из итоговой (круглой) текстуры
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

    private Texture2D CropCenterSquare(Texture2D src)
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

    private Texture2D MakeCircular(Texture2D square)
    {
        int size = square.width; // == height
        var pixels = square.GetPixels();
        float r = size * 0.5f;
        float r2 = r * r;
        Vector2 c = new Vector2(r - 0.5f, r - 0.5f); // центр между пикселями

        for (int y = 0; y < size; y++)
        {
            float dy = y - c.y;
            for (int x = 0; x < size; x++)
            {
                float dx = x - c.x;
                int i = y * size + x;

                // если за пределами круга — делаем прозрачным
                if (dx * dx + dy * dy > r2)
                {
                    var col = pixels[i];
                    col.a = 0f;
                    pixels[i] = col;
                }
            }
        }

        var dst = new Texture2D(size, size, TextureFormat.RGBA32, false);
        dst.SetPixels(pixels);
        dst.Apply();
        return dst;
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
}