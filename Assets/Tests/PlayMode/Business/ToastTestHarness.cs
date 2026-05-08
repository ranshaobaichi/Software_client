using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using Utils;

namespace Tests.PlayMode.Business {
    /// <summary>
    /// Minimal runtime ToastManager + prefab so <see cref="UI.ViewModels.LoginViewModel"/> failure paths
    /// that call <see cref="ToastManager.ShowToast"/> do not null-reference in PlayMode tests.
    /// </summary>
    public static class ToastTestHarness {
        public static void EnsureInstalled() {
            // Avoid ToastManager.SInstance getter: it logs an error when nothing is registered yet.
            if (Object.FindObjectOfType<ToastManager>() != null)
                return;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                    ?? Resources.GetBuiltinResource<Font>("Arial.ttf");

            var prefabRoot = new GameObject("TestToastPrefab");
            prefabRoot.SetActive(false);
            var prefabRect = prefabRoot.AddComponent<RectTransform>();

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(prefabRoot.transform, false);
            var txt = textGo.AddComponent<Text>();
            txt.font = font;
            txt.fontSize = 14;
            txt.color = Color.white;

            var toastComp = prefabRoot.AddComponent<Toast>();
            typeof(Toast).GetField("_text", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(toastComp, txt);

            var canvasGo = new GameObject("TestToastCanvas");
            Object.DontDestroyOnLoad(canvasGo);
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasGo.AddComponent<CanvasScaler>();
            canvasGo.AddComponent<GraphicRaycaster>();
            var parentRt = canvasGo.GetComponent<RectTransform>();

            var mgrGo = new GameObject("TestToastManager");
            mgrGo.transform.SetParent(canvasGo.transform, false);
            var mgr = mgrGo.AddComponent<ToastManager>();
            var tmType = typeof(ToastManager);
            tmType.GetField("_toastPrefab", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(mgr, prefabRoot);
            tmType.GetField("_toastParent", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.SetValue(mgr, parentRt);
        }
    }
}
