using UnityEngine;
using UnityEngine.UI;
using System;

namespace UI.Dialog {
    public abstract class DialogBase : MonoBehaviour {
        public int DialogId { get; private set; }

        [SerializeField]
        private Image _blurTarget;
        
        private Texture2D m_blurTexture;
        private Sprite m_blurSprite;
        private Action<DialogResult> m_onClose;

        protected abstract void OnInit(object input);

        public Image GetBlurTarget() => _blurTarget;

        protected void Close(DialogResult result) {
            Destroy(m_blurSprite);
            Destroy(m_blurTexture);
            m_onClose?.Invoke(result);
            Destroy(gameObject);
        }
        public void Close() => Close(DialogResult.Empty);

        internal void Framework_Init(int dialogId, Action<DialogResult> onClose, object input, Texture2D blurTex, Sprite blur) {
            DialogId = dialogId;
            m_onClose = onClose;
            m_blurTexture = blurTex;
            m_blurSprite = blur;
            OnInit(input);
        }
    }

    public abstract class DialogBaseNoInput : DialogBase {
        protected sealed override void OnInit(object input) {
            OnRender();
        }

        protected virtual void OnRender() { }
    }

    public abstract class DialogBase<TInput> : DialogBase where TInput : class {
        protected sealed override void OnInit(object input) {
            OnRender(input as TInput);
        }

        protected abstract void OnRender(TInput input);
    }
}
