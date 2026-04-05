using UnityEngine;
using UI.Dialog;
using UI.Page;

namespace UI {
    /// <summary>
    /// View / Widget 用于向上查找所属 Page（StateEnginePage）及其 PageController 的结构体。
    /// </summary>
    public struct UIPageFinder {
        #region Interface
        public readonly struct Interface {
            private readonly PageBase m_pageBase;
            private readonly PageController m_pageController;

            public Interface(PageBase pageBase, PageController pageController) {
                m_pageBase = pageBase;
                m_pageController = pageController;
            }

            public bool IsValid => m_pageBase != null;

            public bool IsSamePage(PageBase comparison) => m_pageBase == comparison;

            /// <summary>当前 Page 是否为 PageController 的 Home Page。</summary>
            public bool IsHome => m_pageController != null && m_pageController.IsHome;

            /// <summary>
            /// 通过 PageController 切换到指定类型的 Page。
            /// 若未能找到 PageController 则为空操作。
            /// </summary>
            public void SwitchTo<T>() where T : PageBase {
                m_pageController?.SwitchTo<T>();
            }
            
            /// <returns>Can Be Null</returns>
            public IDialogMgr GetDialogMgr() {
                if (m_pageBase == null || !m_pageBase.TryGetComponent<IDialogMgr>(out var dialogMgr)) {
                    return null;
                }
                return dialogMgr;
            }
        }
        #endregion

        private PageBase m_pageBase;
        private PageController m_pageController;
        private Transform m_curTrans;

        /// <summary>清空缓存并立即重新查找。</summary>
        public Interface Reset(Transform current) {
            m_curTrans = null;
            return Current(current);
        }

        /// <summary>若 Transform 未变则复用缓存；否则重新查找。</summary>
        public Interface Current(Transform current) {
            if (m_curTrans != current) {
                m_pageBase = null;
                m_pageController = null;
                m_curTrans = current;
            }

            if (m_pageBase == null)
                FindPage(current, out m_pageBase, out m_pageController);

            return new Interface(m_pageBase, m_pageController);
        }

        public Interface Current(MonoBehaviour mb) {
            return Current(mb != null ? mb.transform : null);
        }

        /// <summary>
        /// 从 <paramref name="t"/> 向上遍历层级。
        /// 遇到 DialogBase 时停止（Dialog 不属于 Page 体系）。
        /// 找到 StateEnginePage 后继续向上寻找 PageController。
        /// </summary>
        private static void FindPage(Transform t, out PageBase pageBase, out PageController pageController) {
            pageBase = null;
            pageController = null;

            while (t != null) {
                if (t.TryGetComponent<DialogBase>(out _)) return;

                if (pageBase == null && t.TryGetComponent<PageBase>(out var p))
                    pageBase = p;

                if (pageBase != null && t.TryGetComponent<PageController>(out var pc)) {
                    pageController = pc;
                    return;
                }

                t = t.parent;
            }
        }
    }
}
