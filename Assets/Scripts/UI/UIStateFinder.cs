using UI.Dialog;
using UI.Page;
using UnityEngine;
using UI.StateEngine;

namespace UI {
    /// <summary>
    /// View / Widget 用于向上查找所属 State 并调用 StateEngine 操作的结构体。
    /// 持久化存储在字段中，通过 Current() 做缓存复用，通过 Reset() 强制重新查找。
    /// </summary>
    public struct UIStateFinder {
        #region Interface
        /// <summary>
        /// 指向某个 State 的操作句柄。IsValid 为 false 时所有操作均为空操作。
        /// </summary>
        public readonly struct Interface {
            private readonly StateBase m_state;

            public Interface(StateBase state) {
                m_state = state;
            }

            public bool IsValid => m_state != null;

            public bool IsSameState(StateBase comparison) => m_state == comparison;

            /// <summary>
            /// 以当前 State 为发送方，向 <typeparamref name="TTo"/> 发送消息。
            /// </summary>
            public bool SendMessage<TTo>(object msg) where TTo : StateBase {
                if (m_state == null) return false;
                var engine = m_state.GetStateEngine();
                if (engine == null) return false;
                engine.SendMessage(m_state.GetType(), typeof(TTo), msg);
                return true;
            }

            public bool AddTop<T>() where T : StateBase {
                if (m_state == null) return false;
                return m_state.GetStateEngine()?.AddTop<T>() ?? false;
            }

            public bool TryRemoveTop() {
                if (m_state == null) return false;
                return m_state.GetStateEngine()?.TryRemoveTop() ?? false;
            }

            public bool ReplaceTop<T>() where T : StateBase {
                if (m_state == null) return false;
                return m_state.GetStateEngine()?.ReplaceTop<T>() ?? false;
            }

            public bool TryRemoveTo<T>() where T : StateBase {
                if (m_state == null) return false;
                return m_state.GetStateEngine()?.TryRemoveTo<T>() ?? false;
            }
        }
        #endregion

        private StateBase m_state;
        private Transform m_curTrans;

        /// <summary>清空缓存并立即重新查找。</summary>
        public Interface Reset(Transform current) {
            m_curTrans = null;
            return Current(current);
        }

        /// <summary>若 Transform 未变则复用缓存；否则重新查找。</summary>
        public Interface Current(Transform current) {
            if (m_curTrans != current) {
                m_state = null;
                m_curTrans = current;
            }

            if (m_state == null)
                m_state = FindState(current);

            return new Interface(m_state);
        }

        public Interface Current(MonoBehaviour mb) {
            return Current(mb != null ? mb.transform : null);
        }

        /// <summary>
        /// 从 <paramref name="t"/> 向上遍历层级。
        /// 遇到 StateEnginePage 或 DialogBase 时停止（说明已越界）。
        /// </summary>
        private static StateBase FindState(Transform t) {
            while (t != null) {
                if (t.TryGetComponent<StateBase>(out var state)) {
                    return state;
                }
                if (t.TryGetComponent<PageBase>(out _) || t.TryGetComponent<DialogBase>(out _)) {
                    break;
                }
                t = t.parent;
            }
            return null;
        }
    }
}
