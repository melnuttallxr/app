using UnityEngine;
using System;

public class IOSWebViewController : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] RectTransform container;   // область рендера webview (RectTransform в Canvas)

    [Header("iOS Behaviors")]
    [SerializeField] bool openExternalSchemes = true;   // tel:, mailto:, tg: и т.п.
    [SerializeField] bool useSafariForExternal = true;  // SafariViewController для внешних ссылок
    [SerializeField] bool allowSwipeNavGestures = true; // свайпы назад/вперёд (WKWebView)
    [SerializeField] bool inlineMediaPlayback = true;   // <video playsinline>
    [SerializeField] bool autoPlayMedia = false;        // автоплей (включать осознанно)
    [SerializeField] string[] paymentUrlMarkers = { "payment", "/pay", "/checkout" };

    private UniWebView web;
    private Rect lastSafeArea;
    private bool isPaymentPage;

    // режимы контейнера
    private bool useExpandedContainer = false;

    // исходная геометрия контейнера
    private Vector2 origAnchorMin, origAnchorMax;
    private Vector2 origOffsetMin, origOffsetMax;
    private bool originalSaved = false;

    void Awake()
    {
        web = gameObject.AddComponent<UniWebView>();
    }

    void Start()
    {
        if (container == null)
        {
            Debug.LogError("[IOSWebViewController] Container is not assigned.");
            return;
        }

        // сохраним исходные привязки контейнера
        SaveOriginalContainerLayout();

        ConfigureIOS();
        HookEvents();

        // фрейм выставим по текущему контейнеру
        UpdateWebViewFrame();
    }

    // ===================== ПУБЛИЧНОЕ API =====================

    /// <summary>
    /// Открыть URL во вью. Если rememberAsHome = true, URL будет сохранён как "домой".
    /// </summary>
    public void OpenURL(string url, bool rememberAsHome = false)
    {
        if (string.IsNullOrEmpty(url)) return;
        if (rememberAsHome) homeUrl = url;
        web.Load(url);
        web.Show();
        UpdateWebViewFrame();
    }

    /// <summary>Назначить домашний URL (для быстрого возврата).</summary>
    public void SetHomeUrl(string url) { homeUrl = url; }

    public void Reload() => web?.Reload();

    public void NavigateHome()
    {
        if (!string.IsNullOrEmpty(homeUrl)) web?.Load(homeUrl);
    }

    /// <summary>Back UX: если на платёжной и есть homeUrl — уходит "домой", иначе back/close.</summary>
    public void GoBackOrClose()
    {
        if (isPaymentPage && !string.IsNullOrEmpty(homeUrl)) { web.Load(homeUrl); return; }
        if (web != null && web.CanGoBack) web.GoBack(); else Close();
    }

    public void Close()
    {
        if (web != null) { Destroy(web); web = null; }
        gameObject.SetActive(false);
    }

    // ===================== ВНУТРЕННЕЕ =====================

    private string homeUrl; // устанавливается кодом

    void ConfigureIOS()
    {
        UniWebView.SetAllowInlinePlay(inlineMediaPlayback);
        UniWebView.SetAllowAutoPlay(autoPlayMedia);

        web.SetAllowBackForwardNavigationGestures(allowSwipeNavGestures);
        web.SetContentInsetAdjustmentBehavior(UniWebViewContentInsetAdjustmentBehavior.Always);
        web.SetOpenLinksInExternalBrowser(useSafariForExternal);
        web.SetShowToolbar(false);
        web.SetBouncesEnabled(true);
        web.BackgroundColor = Color.black;

        // стабильнее без popup-окон на мобилке
        web.SetSupportMultipleWindows(false, false);
    }

    void HookEvents()
    {
        // перехват внешних схем
        web.OnPageStarted += (view, url) => {
            if (!openExternalSchemes || string.IsNullOrEmpty(url)) return;
            if (IsExternalScheme(url))
            {
                try { Application.OpenURL(url); } catch (Exception e) { Debug.LogWarning($"Open external URL failed: {url}\n{e}"); }
                view.Stop();
            }
        };

        web.OnPageFinished += (_, __, url) => {
            isPaymentPage = IsPaymentUrl(url);
            InjectViewportFitCover();
            // Проверим содержимое и подстроим контейнер/вебвью
            CheckContentAndResize();
        };

        web.OnPageErrorReceived += (_, code, message) => {
            Debug.LogError($"Web error {code}: {message}");
        };
    }

    bool IsPaymentUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        foreach (var m in paymentUrlMarkers)
            if (url.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    bool IsExternalScheme(string url) =>
        url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("sms:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("tg:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("viber:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("weixin:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("alipays:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("intent:", StringComparison.OrdinalIgnoreCase);

    void InjectViewportFitCover()
    {
        // добавим viewport-fit=cover и тёмный фон, если сайт забыл
        var js =
            "if(!document.querySelector('meta[name=\\\"viewport\\\"]')){" +
            "var m=document.createElement('meta');" +
            "m.name='viewport';m.content='width=device-width, initial-scale=1, viewport-fit=cover';" +
            "document.head.appendChild(m);}" +
            "document.documentElement.style.background='black';" +
            "document.body.style.background='black';";
        web.EvaluateJavaScript(js, _ => { });
    }

    // === ЛОГИКА ПРОВЕРКИ КОНТЕНТА И РЕСАЙЗА ===
    void CheckContentAndResize()
    {
        // Быстро/надёжно: ищем в видимом тексте документа "Hello World"
        const string js =
            "(function(){try{" +
            "var t=(document.body&&document.body.innerText)||'';" +
            "return t.indexOf('Hello World')>=0 ? '1':'0';" +
            "}catch(e){return '0';}})();";

        web.EvaluateJavaScript(js, result => {
            bool hasHello = (result != null && result.resultCode == "0" && result.data == "1");
            if (hasHello)
            {
                // Контейнер остаётся исходным → вебвью по размеру контейнера
                useExpandedContainer = false;
                RestoreOriginalContainerLayout();
                UpdateWebViewFrame();
            }
            else
            {
                // Расширяем контейнер до всей safe-area → вебвью по контейнеру
                useExpandedContainer = true;
                ExpandContainerToSafeArea();
                UpdateWebViewFrame();
            }
        });
    }

    // === Работа с контейнером и фреймом ===
    void SaveOriginalContainerLayout()
    {
        if (container == null) return;
        origAnchorMin = container.anchorMin;
        origAnchorMax = container.anchorMax;
        origOffsetMin = container.offsetMin;
        origOffsetMax = container.offsetMax;
        originalSaved = true;
    }

    void RestoreOriginalContainerLayout()
    {
        if (!originalSaved || container == null) return;
        container.anchorMin = origAnchorMin;
        container.anchorMax = origAnchorMax;
        container.offsetMin = origOffsetMin;
        container.offsetMax = origOffsetMax;
        lastSafeArea = new Rect(0, 0, 0, 0); // форсируем пересчёт фрейма
    }

    void ExpandContainerToSafeArea()
    {
        if (container == null) return;
        var safe = Screen.safeArea;
        Vector2 amin = safe.position, amax = safe.position + safe.size;
        amin.x /= Screen.width; amin.y /= Screen.height;
        amax.x /= Screen.width; amax.y /= Screen.height;

        container.anchorMin = amin;
        container.anchorMax = amax;
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;
        lastSafeArea = new Rect(0, 0, 0, 0); // форсируем пересчёт фрейма
    }

    void Update()
    {
        // если контейнер в режиме расширения — следим за изменением safe-area (повороты, split view)
        if (useExpandedContainer)
        {
            ApplySafeAreaIfChanged();
        }
        // фрейм вебвью актуализируем каждый апдейт (лёгкая операция)
        UpdateWebViewFrame();
    }

    void ApplySafeAreaIfChanged()
    {
        if (!container) return;
        var safe = Screen.safeArea;
        if (safe == lastSafeArea) return;
        lastSafeArea = safe;

        if (useExpandedContainer)
        {
            // только в этом режиме меняем контейнер под safe-area
            Vector2 amin = safe.position, amax = safe.position + safe.size;
            amin.x /= Screen.width; amin.y /= Screen.height;
            amax.x /= Screen.width; amax.y /= Screen.height;

            container.anchorMin = amin;
            container.anchorMax = amax;
            container.offsetMin = Vector2.zero;
            container.offsetMax = Vector2.zero;
        }
    }

    void UpdateWebViewFrame()
    {
        if (web == null || container == null) return;
        var wc = new Vector3[4];
        container.GetWorldCorners(wc);
        var bl = RectTransformUtility.WorldToScreenPoint(null, wc[0]);
        var tr = RectTransformUtility.WorldToScreenPoint(null, wc[2]);
        web.Frame = new Rect(bl.x, Screen.height - tr.y, tr.x - bl.x, tr.y - bl.y);
    }

    void OnDestroy()
    {
        if (web != null) { Destroy(web); web = null; }
    }
}