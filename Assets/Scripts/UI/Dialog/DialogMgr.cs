using UnityEngine;
using System;

namespace UI.Dialog {
    public class DialogMgr : IDialogMgr {
        private DialogRegistry m_registry;
        private Camera[] m_screenshotCameras;

        public struct Builder {
            public DialogRegistry registry;
            public Camera[] screenshotCameras;
        }

        public static DialogMgr Create(Builder builder) {
            return new DialogMgr {
                    m_registry = builder.registry,
                    m_screenshotCameras = builder.screenshotCameras,
            };
        }

        public int OpenDialog<TDialog>(object input = null,
                Action<DialogResult> onClose = null,
                Transform parent = null,
                bool useBlur = true
        )
                where TDialog : DialogBase {
            return OpenDialog(typeof(TDialog), input, onClose, parent, useBlur);
        }

        public int OpenDialog<TDialog, TInput>(TInput input,
                Action<DialogResult> onClose = null,
                Transform parent = null,
                bool useBlur = true
        )
                where TDialog : DialogBase<TInput>
                where TInput : class {
            var prefab = m_registry?.GetPrefab(typeof(TDialog));
            if (prefab == null) return -1;
            if (!ValidateParent(parent)) return -1;
            
            // 在 Instantiate 前捕获屏幕，避免 Dialog UI 出现在截图中
            var blurTex = useBlur && m_screenshotCameras is { Length: > 0 }
                    ? DialogBlurHelper.CaptureBlur(m_screenshotCameras)
                    : null;

            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            if (instance is not TDialog) {
                Debug.LogError($"[DialogMgr] Prefab 组件类型与 {typeof(TDialog).Name} 不匹配。");
                UnityEngine.Object.Destroy(instance.gameObject);
                return -1;
            }

            return FinishOpen(instance, input, onClose, blurTex);
        }

        public int OpenDialog(Type dialogType,
                object input = null,
                Action<DialogResult> onClose = null,
                Transform parent = null,
                bool useBlur = true
        ) {
            var prefab = m_registry?.GetPrefab(dialogType);
            if (prefab == null) return -1;
            if (!ValidateParent(parent)) return -1;


            // 在 Instantiate 前捕获屏幕，避免 Dialog UI 出现在截图中
            var blurTex = useBlur && m_screenshotCameras is { Length: > 0 }
                    ? DialogBlurHelper.CaptureBlur(m_screenshotCameras)
                    : null;

            var instance = UnityEngine.Object.Instantiate(prefab, parent);
            return FinishOpen(instance, input, onClose, blurTex);
        }

        private int FinishOpen(DialogBase dialog, object input, Action<DialogResult> onClose, Texture2D blurTex) {
            int dialogId = dialog.gameObject.GetInstanceID();

            // 将捕获好的模糊纹理应用到 blurTarget（此时 Dialog 已经存在于场景中）

            Sprite sprite = null;
            if (blurTex != null)
                sprite = DialogBlurHelper.ApplyBlur(dialog.GetBlurTarget(), blurTex);

            dialog.Framework_Init(dialogId, onClose, input, blurTex, sprite);
            return dialogId;
        }

        private static bool ValidateParent(Transform parent) {
            if (parent != null && !parent.Equals(null)) return true;
            Debug.LogError("[DialogMgr] parent 为 null 或已被销毁，无法打开 Dialog。");
            return false;
        }
    }
}