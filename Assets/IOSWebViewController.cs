using UnityEngine;
using System;
using System.Collections;

public class IOSWebViewController : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private RectTransform container;    // область для webview (RectTransform на Canvas)

    [Header("Sizing on OpenURL")]
    [SerializeField] private bool expandOnOpen = true;   // растягивать контейнер при OpenURL
    [SerializeField] private bool useSafeArea = true;    // true = по safe-area, false = весь экран, включая вырезы

    [Header("iOS Behaviors")]
    [SerializeField] private bool openExternalSchemes = true;   // tel:, mailto:, tg:, и т.п.
    [SerializeField] private bool useSafariForExternal = true;  // SafariVC для внешних ссылок
    [SerializeField] private bool allowSwipeNavGestures = true; // свайпы назад/вперёд
    [SerializeField] private bool inlineMediaPlayback = true;   // <video playsinline>
    [SerializeField] private bool autoPlayMedia = false;        // автоплей (включать осознанно)
    [SerializeField] private string[] paymentUrlMarkers = { "payment", "/pay", "/checkout" };

    private UniWebView web;
    private bool isInitDone;
    private bool isPaymentPage;

    // исходная геометрия контейнера (можно вернуть при необходимости)
    private Vector2 origAnchorMin, origAnchorMax;
    private Vector2 origOffsetMin, origOffsetMax;
    private bool originalSaved;

    // если OpenURL прилетел до инициализации
    private string pendingUrl;
    private bool pendingRememberHome;

    private string homeUrl; // опционально устанавливается из кода/через OpenURL(..., rememberAsHome:true)

    private void Awake()
    {
        web = gameObject.AddComponent<UniWebView>();
    }

    private IEnumerator Start()
    {
        if (container == null)
        {
            Debug.LogError("[IOSWebViewController] Container is not assigned.");
            yield break;
        }

        SaveOriginalContainerLayout();
        ConfigureIOS();
        HookEvents();

        // ждём кадр, чтобы Canvas/RectTransform стабилизировались
        yield return null;

        // ключевое — привязать web к контейнеру (дальше он сам будет подстраиваться)
        web.ReferenceRectTransform = container;

        isInitDone = true;

        // если пользоват. успел вызвать OpenURL раньше — загрузим теперь
        if (!string.IsNullOrEmpty(pendingUrl))
        {
            DoOpen(pendingUrl, pendingRememberHome);
            pendingUrl = null;
        }
    }

    private void OnDestroy()
    {
        if (web != null) { Destroy(web); web = null; }
    }

    // ===================== PUBLIC API =====================

    /// Открыть URL (без автозагрузки в Start). Если rememberAsHome = true — запоминаем как "домой".
    public void OpenURL(string url, bool rememberAsHome = false)
    {
        if (string.IsNullOrEmpty(url)) return;

        if (!isInitDone)
        {
            // запомним, выполним сразу после инициализации
            pendingUrl = url;
            pendingRememberHome = rememberAsHome;
            return;
        }

        DoOpen(url, rememberAsHome);
    }

    /// Установить "домашний" URL (без немедленной загрузки).
    public void SetHomeUrl(string url)
    {
        if (!string.IsNullOrEmpty(url))
            homeUrl = url;
    }

    public void Reload() => web?.Reload();

    public void NavigateHome()
    {
        if (!string.IsNullOrEmpty(homeUrl))
            web?.Load(homeUrl);
    }

    /// UX: если на платёжной и есть homeUrl — уходим домой, иначе back/close.
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

    // ===================== INTERNAL =====================

    private void DoOpen(string url, bool rememberAsHome)
    {
        if (rememberAsHome) homeUrl = url;

        // По вашему требованию — растянуть контейнер ПЕРЕД загрузкой
        if (expandOnOpen)
        {
            if (useSafeArea) ExpandContainerToSafeArea();
            else ExpandContainerToFullScreen();
        }

        // Показ + загрузка
        web.Show();
        web.Load(url);
    }

    private void ConfigureIOS()
    {
        UniWebView.SetAllowInlinePlay(inlineMediaPlayback);
        UniWebView.SetAllowAutoPlay(autoPlayMedia);

        web.SetAllowBackForwardNavigationGestures(allowSwipeNavGestures);
        web.SetContentInsetAdjustmentBehavior(UniWebViewContentInsetAdjustmentBehavior.Always);
        web.SetOpenLinksInExternalBrowser(useSafariForExternal);
        web.SetShowToolbar(false);
        web.SetBouncesEnabled(true);
        web.BackgroundColor = Color.black;
        web.SetSupportMultipleWindows(false, false);
    }

    private void HookEvents()
    {
        web.OnPageStarted += (view, url) =>
        {
            if (!openExternalSchemes || string.IsNullOrEmpty(url)) return;

            if (IsExternalScheme(url))
            {
                try { Application.OpenURL(url); }
                catch (Exception e) { Debug.LogWarning($"[Web] Open external URL failed: {url}\n{e}"); }
                view.Stop();
            }
        };

        web.OnPageFinished += (_, statusCode, url) =>
        {
            isPaymentPage = IsPaymentUrl(url);
            InjectViewportFitCover();

            // если хотите ещё и на платёжных растягивать (вдруг приехали туда не из OpenURL)
            if (isPaymentPage && expandOnOpen)
            {
                if (useSafeArea) ExpandContainerToSafeArea();
                else ExpandContainerToFullScreen();
            }
        };

        web.OnPageErrorReceived += (_, code, message) =>
        {
            Debug.LogError($"[Web] Error: {code} {message}");
        };
    }

    private bool IsPaymentUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return false;
        foreach (var m in paymentUrlMarkers)
        {
            if (url.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static bool IsExternalScheme(string url) =>
        url.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("tel:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("sms:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("tg:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("viber:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("whatsapp:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("weixin:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("alipays:", StringComparison.OrdinalIgnoreCase) ||
        url.StartsWith("intent:", StringComparison.OrdinalIgnoreCase);

    private void InjectViewportFitCover()
    {
        var js =
            "if(!document.querySelector('meta[name=\\\"viewport\\\"]')){" +
            "var m=document.createElement('meta');" +
            "m.name='viewport';m.content='width=device-width, initial-scale=1, viewport-fit=cover';" +
            "document.head.appendChild(m);}" +
            "document.documentElement.style.background='black';" +
            "document.body.style.background='black';";
        web.EvaluateJavaScript(js, _ => { });
    }

    // ===== Работа с контейнером =====

    private void SaveOriginalContainerLayout()
    {
        if (container == null) return;
        origAnchorMin = container.anchorMin;
        origAnchorMax = container.anchorMax;
        origOffsetMin = container.offsetMin;
        origOffsetMax = container.offsetMax;
        originalSaved = true;
    }

    /// Восстановить исходные привязки (если вдруг понадобится).
    public void RestoreOriginalContainerLayout()
    {
        if (!originalSaved || container == null) return;
        container.anchorMin = origAnchorMin;
        container.anchorMax = origAnchorMax;
        container.offsetMin = origOffsetMin;
        container.offsetMax = origOffsetMax;
    }

    /// Растянуть контейнер по **safe-area**.
    public void ExpandContainerToSafeArea()
    {
        if (container == null) return;

        var safe = Screen.safeArea;
        Vector2 amin = safe.position, amax = safe.position + safe.size;
        amin.x /= Screen.width;  amin.y /= Screen.height;
        amax.x /= Screen.width;  amax.y /= Screen.height;

        container.anchorMin = amin;
        container.anchorMax = amax;
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;
    }

    /// Растянуть контейнер на **весь экран**, включая зоны вырезов/динамических островов.
    public void ExpandContainerToFullScreen()
    {
        if (container == null) return;

        container.anchorMin = Vector2.zero;      // (0,0)
        container.anchorMax = Vector2.one;       // (1,1)
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;
    }
}