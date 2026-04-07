using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using Constants;

public class GameSceneManager : MonoBehaviour {
    #region Singleton
    private static GameSceneManager s_instance;

    public static GameSceneManager SInstance {
        get {
            if (s_instance == null) {
                s_instance = FindObjectOfType<GameSceneManager>();
                if (s_instance == null) {
                    var go = new GameObject("[SceneManager]");
                    s_instance = go.AddComponent<GameSceneManager>();
                }
            }

            return s_instance;
        }
    }

    public void Awake() {
        if (s_instance != null && s_instance != this) {
            Destroy(gameObject);
            return;
        }

        s_instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region Structs
    [Serializable]
    public class SceneChangeConfig {
        public bool enableTransition = true;

        [Min(0f)]
        public float fadeOutDuration = 0.2f;

        [Min(0f)]
        public float holdBlackDuration = 0f;

        [Min(0f)]
        public float fadeInDuration = 0.2f;

        public bool useUnscaledTime = true;

        public static SceneChangeConfig defaultEnabled => new SceneChangeConfig {
            enableTransition = true,
            fadeOutDuration = 0.2f,
            holdBlackDuration = 0f,
            fadeInDuration = 0.2f,
            useUnscaledTime = true
        };

        public static SceneChangeConfig defaultDisabled => new SceneChangeConfig {
            enableTransition = false,
            fadeOutDuration = 0f,
            holdBlackDuration = 0f,
            fadeInDuration = 0f,
            useUnscaledTime = true
        };
    }
    #endregion
    
    private readonly Dictionary<SceneType, string> m_sceneNameMap = new Dictionary<SceneType, string> {
        { SceneType.LOGIN, "LoginScene" },
        { SceneType.HOME, "HomeScene" }
    };
    private Canvas m_fadeCanvas;
    private Image m_fadeImage;
    private Coroutine m_transitionCoroutine;

    /// <summary>
    /// 按场景类型切换到目标场景（同步入口，内部通过异步加载流程执行转场）。
    /// </summary>
    /// <param name="sceneType">目标场景类型。</param>
    /// <param name="mode">场景加载模式，默认为 Single。</param>
    public void SwitchScene(SceneType sceneType, LoadSceneMode mode = LoadSceneMode.Single) {
        var sceneName = GetSceneName(sceneType);
        SwitchSceneInternal(sceneName, mode, SceneChangeConfig.defaultEnabled);
    }

    /// <summary>
    /// 按场景类型切换到目标场景，并使用指定的切场景配置。
    /// </summary>
    /// <param name="sceneType">目标场景类型。</param>
    /// <param name="config">切场景配置；传入 null 时将使用默认配置。</param>
    /// <param name="mode">场景加载模式，默认为 Single。</param>
    public void SwitchScene(SceneType sceneType, SceneChangeConfig config, LoadSceneMode mode = LoadSceneMode.Single) {
        var sceneName = GetSceneName(sceneType);
        SwitchSceneInternal(sceneName, mode, config);
    }

    /// <summary>
    /// 按场景类型异步切换到目标场景，并返回底层异步加载操作。
    /// </summary>
    /// <param name="sceneType">目标场景类型。</param>
    /// <param name="mode">场景加载模式，默认为 Single。</param>
    /// <returns>场景加载的异步操作对象。</returns>
    public AsyncOperation SwitchSceneAsync(SceneType sceneType, LoadSceneMode mode = LoadSceneMode.Single) {
        var sceneName = GetSceneName(sceneType);
        return SwitchSceneAsyncInternal(sceneName, mode, SceneChangeConfig.defaultEnabled);
    }

    /// <summary>
    /// 按场景类型异步切换到目标场景，并使用指定的切场景配置。
    /// </summary>
    /// <param name="sceneType">目标场景类型。</param>
    /// <param name="config">切场景配置；传入 null 时将使用默认配置。</param>
    /// <param name="mode">场景加载模式，默认为 Single。</param>
    /// <returns>场景加载的异步操作对象。</returns>
    public AsyncOperation SwitchSceneAsync(SceneType sceneType,
            SceneChangeConfig config,
            LoadSceneMode mode = LoadSceneMode.Single
    ) {
        var sceneName = GetSceneName(sceneType);
        return SwitchSceneAsyncInternal(sceneName, mode, config);
    }

    public string GetSceneName(SceneType sceneType) {
        if (m_sceneNameMap.TryGetValue(sceneType, out var sceneName) && !string.IsNullOrWhiteSpace(sceneName)) {
            return sceneName;
        }

        // 默认使用 enum 名称作为场景名，便于快速接入。
        return sceneType.ToString();
    }

    private void SwitchSceneInternal(string sceneName, LoadSceneMode mode, SceneChangeConfig config) {
        var targetConfig = config ?? SceneChangeConfig.defaultEnabled;
        if (!targetConfig.enableTransition) {
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName, mode);
            return;
        }

        if (m_transitionCoroutine != null) {
            StopCoroutine(m_transitionCoroutine);
        }

        m_transitionCoroutine = StartCoroutine(PlayTransitionAndLoadScene(sceneName, mode, targetConfig));
    }

    private AsyncOperation SwitchSceneAsyncInternal(string sceneName, LoadSceneMode mode, SceneChangeConfig config) {
        var targetConfig = config ?? SceneChangeConfig.defaultEnabled;
        if (!targetConfig.enableTransition) {
            return UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, mode);
        }

        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, mode);
        if (op == null) {
            return null;
        }

        op.allowSceneActivation = false;

        if (m_transitionCoroutine != null) {
            StopCoroutine(m_transitionCoroutine);
        }

        m_transitionCoroutine = StartCoroutine(PlayTransitionAndActivate(op, targetConfig));
        return op;
    }

    private IEnumerator PlayTransitionAndLoadScene(string sceneName, LoadSceneMode mode, SceneChangeConfig config) {
        EnsureFadeOverlay();
        yield return FadeTo(1f, config.fadeOutDuration, config.useUnscaledTime);

        if (config.holdBlackDuration > 0f) {
            yield return WaitFor(config.holdBlackDuration, config.useUnscaledTime);
        }

        var op = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(sceneName, mode);
        if (op != null) {
            while (!op.isDone) {
                yield return null;
            }
        }

        yield return FadeTo(0f, config.fadeInDuration, config.useUnscaledTime);
        m_transitionCoroutine = null;
    }

    private IEnumerator PlayTransitionAndActivate(AsyncOperation op, SceneChangeConfig config) {
        EnsureFadeOverlay();
        yield return FadeTo(1f, config.fadeOutDuration, config.useUnscaledTime);

        if (config.holdBlackDuration > 0f) {
            yield return WaitFor(config.holdBlackDuration, config.useUnscaledTime);
        }

        op.allowSceneActivation = true;
        while (!op.isDone) {
            yield return null;
        }

        yield return FadeTo(0f, config.fadeInDuration, config.useUnscaledTime);
        m_transitionCoroutine = null;
    }

    private IEnumerator FadeTo(float targetAlpha, float duration, bool useUnscaledTime) {
        EnsureFadeOverlay();
        var startColor = m_fadeImage.color;
        var startAlpha = startColor.a;

        if (duration <= 0f) {
            startColor.a = targetAlpha;
            m_fadeImage.color = startColor;
            m_fadeCanvas.enabled = targetAlpha > 0f;
            yield break;
        }

        m_fadeCanvas.enabled = true;
        float elapsed = 0f;
        while (elapsed < duration) {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            var c = m_fadeImage.color;
            c.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            m_fadeImage.color = c;
            yield return null;
        }

        var finalColor = m_fadeImage.color;
        finalColor.a = targetAlpha;
        m_fadeImage.color = finalColor;
        m_fadeCanvas.enabled = targetAlpha > 0f;
    }

    private IEnumerator WaitFor(float seconds, bool useUnscaledTime) {
        if (!useUnscaledTime) {
            yield return new WaitForSeconds(seconds);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds) {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void EnsureFadeOverlay() {
        if (m_fadeCanvas != null && m_fadeImage != null) {
            return;
        }

        var overlayObject = new GameObject("SceneTransitionOverlay");
        overlayObject.transform.SetParent(transform, false);

        m_fadeCanvas = overlayObject.AddComponent<Canvas>();
        m_fadeCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        m_fadeCanvas.sortingOrder = short.MaxValue;
        overlayObject.AddComponent<CanvasScaler>();
        overlayObject.AddComponent<GraphicRaycaster>();

        var imageObject = new GameObject("FadeImage");
        imageObject.transform.SetParent(overlayObject.transform, false);
        var rect = imageObject.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        m_fadeImage = imageObject.AddComponent<Image>();
        m_fadeImage.color = new Color(0f, 0f, 0f, 0f);
        m_fadeCanvas.enabled = false;
    }
}