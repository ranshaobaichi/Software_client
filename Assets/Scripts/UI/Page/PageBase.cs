using UnityEngine;
using UnityEngine.UI;

namespace UI.Page {
    [RequireComponent(typeof(Canvas))]
    [RequireComponent(typeof(GraphicRaycaster))]
    [RequireComponent(typeof(CanvasScaler))]
    public abstract class PageBase : MonoBehaviour {
        public StateEngine.StateEngine StateEngine => _stateEngine;
        [SerializeField]
        private StateEngine.StateEngine _stateEngine;

        private bool m_initialized;

        public void InitIfNot(Camera ca) {
            if (m_initialized) return;
            m_initialized = true;

            if (_stateEngine == null) {
                Debug.LogError("[PageBase] _stateEngine 引用为 null。");
                return;
            }

            var canvas = GetComponent<Canvas>();
            canvas.worldCamera = ca;
            _stateEngine.Initialize();
            OnInitialize(ca);
        }

        protected virtual void OnInitialize(Camera ca) { }

        public virtual void OnHide() { }
        public virtual void OnShow() { }
    }
}
