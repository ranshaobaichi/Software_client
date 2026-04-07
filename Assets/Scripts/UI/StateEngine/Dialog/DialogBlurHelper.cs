using UnityEngine;
using UnityEngine.UI;

namespace UI.Dialog {
    public static class DialogBlurHelper {
        private static readonly int BlurSize = Shader.PropertyToID("_BlurSize");
        private static readonly int BlurDirection = Shader.PropertyToID("_BlurDirection");
        private const int BlurIterations = 2;
        private const float BlurSpread = 1.5f;
        private const int DownSample = 2;

        /// <summary>
        /// 在 Dialog 实例化之前调用：捕获当前屏幕并进行高斯模糊，返回模糊后的 Texture2D。
        /// 返回 null 表示捕获失败。
        /// </summary>
        public static Texture2D CaptureBlur(Camera[] cameras) {
            Camera cam = null;
            if (cameras is { Length: > 0 }) {
                foreach (var t in cameras) {
                    if (t != null) {
                        cam = t;
                        break;
                    }
                }
            }

            if (cam == null) {
                cam = Camera.main;
            }

            if (cam == null) {
                Debug.LogWarning("[DialogBlurHelper] 无可用摄像机，跳过模糊截屏。");
                return null;
            }

            int w = Screen.width / DownSample;
            int h = Screen.height / DownSample;

            var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            var previousTargetTexture = cam.targetTexture;
            try {
                cam.targetTexture = rt;
                cam.Render();
            }
            finally {
                cam.targetTexture = previousTargetTexture;
            }

            var blurShader = Shader.Find("Hidden/DialogBlur");
            if (blurShader == null) {
                Debug.LogWarning("[DialogBlurHelper] 未找到 Hidden/DialogBlur Shader，将使用未模糊截屏。");
                var fallbackTex = RenderTextureToTexture2D(rt);
                RenderTexture.ReleaseTemporary(rt);
                return fallbackTex;
            }

            var blurMat = new Material(blurShader);
            for (int i = 0; i < BlurIterations; i++) {
                var tmp = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                blurMat.SetFloat(BlurSize, BlurSpread);
                blurMat.SetVector(BlurDirection, new Vector4(1, 0, 0, 0));
                Graphics.Blit(rt, tmp, blurMat);

                var tmp2 = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                blurMat.SetVector(BlurDirection, new Vector4(0, 1, 0, 0));
                Graphics.Blit(tmp, tmp2, blurMat);

                RenderTexture.ReleaseTemporary(rt);
                RenderTexture.ReleaseTemporary(tmp);
                rt = tmp2;
            }

            var finalTex = RenderTextureToTexture2D(rt);
            RenderTexture.ReleaseTemporary(rt);
            Object.Destroy(blurMat);
            return finalTex;
        }

        /// <summary>
        /// 在 Dialog 实例化之后调用：将预先捕获的模糊纹理应用到 blurTarget Image 上。
        /// </summary>
        public static Sprite ApplyBlur(Image target, Texture2D blurTex) {
            if (target == null || blurTex == null) {
                return null;
            }
            var sprite = Sprite.Create(
                    blurTex,
                    new Rect(0, 0, blurTex.width, blurTex.height),
                    new Vector2(0.5f, 0.5f)
            );
            target.sprite = sprite;
            target.enabled = true;
            return sprite;
        }

        private static Texture2D RenderTextureToTexture2D(RenderTexture rt) {
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.ARGB32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            return tex;
        }
    }
}