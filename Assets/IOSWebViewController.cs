using UnityEngine;
using System;
using System.Collections;

public class IOSWebViewController : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private RectTransform container;    // область для webview (RectTransform на Canvas)

    [Header("Sizing on OpenURL")]
    [SerializeField] private bool expandOnOpen = true;   // растягивать контейнер при OpenURL (теперь решается только в OnPageFinished)
    [SerializeField] private bool useSafeArea = true;    // true = по safe-area, false = весь экран

    [Header("iOS Behaviors")]
    [SerializeField] private bool openExternalSchemes = true;
    [SerializeField] private bool useSafariForExternal = true;
    [SerializeField] private bool allowSwipeNavGestures = true;
    [SerializeField] private bool inlineMediaPlayback = true;
    [SerializeField] private bool autoPlayMedia = false;
    [SerializeField] private string[] paymentUrlMarkers = { "payment", "/pay", "/checkout" };

    [SerializeField] GameObject loading;

    private UniWebView web;
    private bool isInitDone;
    private bool isPaymentPage;

    // исходная геометрия контейнера
    private Vector2 origAnchorMin, origAnchorMax;
    private Vector2 origOffsetMin, origOffsetMax;
    private bool originalSaved;

    // если OpenURL прилетел до инициализации
    private string pendingUrl;
    private bool pendingRememberHome;

    private string homeUrl;

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

        yield return null;

        web.ReferenceRectTransform = container;
        isInitDone = true;

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

    public void OpenURL(string url, bool rememberAsHome = false)
    {
        if (string.IsNullOrEmpty(url)) return;

        if (!isInitDone)
        {
            pendingUrl = url;
            pendingRememberHome = rememberAsHome;
            return;
        }

        if (rememberAsHome) homeUrl = url;

        web.Load(url);

        Debug.Log("loading show");
        loading.SetActive(true);
        web.Hide();

    }

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

    /// Скрыть/показать UniWebView (для оверлея Unity UI)
    public void SetOverlayVisible(bool visible)
    {
        if (web == null) return;
        if (visible) web.Show(); else web.Hide();
    }

    // ===================== INTERNAL =====================

    private void DoOpen(string url, bool rememberAsHome)
    {
        if (rememberAsHome) homeUrl = url;

        // ВАЖНО: контейнер больше не расширяем здесь!
        // Решение принимается только после загрузки в OnPageFinished.

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
        web.SetSupportMultipleWindows(true, true);
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
            web.Hide();
        };

        web.OnPageFinished += (_, statusCode, url) =>
        {
            isPaymentPage = IsPaymentUrl(url);
            InjectViewportFitCover();

            // Проверка текста на "Hello world"
            const string jsCheck = @"
                (function() {
                    try {
                        var text = (document.body && document.body.innerText) || '';
                        return text.indexOf('MELVYN NUTTALL') >= 0 ? '1' : '0';
                    } catch(e) { return '0'; }
                })();";

            web.EvaluateJavaScript(jsCheck, result =>
            {
                bool hasHelloWorld = (result != null && result.data == "1");

                if (hasHelloWorld)
                {
                    RestoreOriginalContainerLayout();
                    Debug.Log("Loading Hide");
                    loading.SetActive(false);

                }
                else
                {
                    UnityEngine.Debug.Log("Show");
                    if ((isPaymentPage || expandOnOpen) && useSafeArea)
                        ExpandContainerToSafeArea();
                    else if ((isPaymentPage || expandOnOpen))
                        ExpandContainerToFullScreen();

                }
                web.Show();
            });
        };

        web.OnLoadingErrorReceived += (_, _, _, _) =>
        {
            UnityEngine.Debug.Log("Loading hide");
            loading.SetActive(false);
            web.Hide();
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
            "document.head.appendChild(m);}";
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

    public void RestoreOriginalContainerLayout()
    {
        if (!originalSaved || container == null) return;
        container.anchorMin = origAnchorMin;
        container.anchorMax = origAnchorMax;
        container.offsetMin = origOffsetMin;
        container.offsetMax = origOffsetMax;
    }

    public void ExpandContainerToSafeArea()
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
    }

    public void ExpandContainerToFullScreen()
    {
        if (container == null) return;

        container.anchorMin = Vector2.zero;
        container.anchorMax = Vector2.one;
        container.offsetMin = Vector2.zero;
        container.offsetMax = Vector2.zero;
    }
}