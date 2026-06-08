using UnityEngine;
using System;
using System.Collections.Generic;

namespace UI.Page {
    public class PageController : MonoBehaviour {
        [Serializable]
        public class PageInfo {
            public bool destoryWhenLeave = true;
            public PageBase PageBase;
        }

        public PageBase CurrentPageBase { get; private set; }
        public Type CurrentPageType { get; private set; }
        public bool IsHome => CurrentPageType == m_homePageType;

        [SerializeField] private PageInfo _homePageInfo;
        [SerializeField] private PageInfo[] _pages;
        [SerializeField] private Transform _container;
        [SerializeField] private Camera _camera;

        private Dictionary<Type, PageInfo> m_lookup;
        private Dictionary<Type, PageBase> m_instanceCache;
        private Type m_homePageType;
        private bool m_initialized;

        private Stack<PageBase> m_pageStack = new Stack<PageBase>();
        private Stack<Type> m_pageTypeStack = new Stack<Type>();

        private void Start() {
            m_lookup = new Dictionary<Type, PageInfo>();
            m_instanceCache = new Dictionary<Type, PageBase>();

            if (_homePageInfo?.PageBase == null) {
                Debug.LogError("[PageController] _homePageInfo 未配置。");
                return;
            }

            m_homePageType = _homePageInfo.PageBase.GetType();
            m_lookup[m_homePageType] = _homePageInfo;

            if (_pages != null) {
                foreach (var info in _pages) {
                    if (info?.PageBase == null) continue;
                    var type = info.PageBase.GetType();
                    if (m_lookup.ContainsKey(type))
                        Debug.LogWarning($"[PageController] {type.Name} 重复注册，后项覆盖前项。");
                    m_lookup[type] = info;
                }
            }

            m_initialized = true;

            // 初始化主页
            SwitchTo(m_homePageType);

            // 压入栈保证退栈安全
            if (CurrentPageBase != null) {
                m_pageStack.Push(CurrentPageBase);
                m_pageTypeStack.Push(CurrentPageType);
            }
        }

        // 普通切换，不压栈
        public void SwitchTo<T>() where T : PageBase => SwitchTo(typeof(T));

        public void SwitchTo(Type pageType) {
            if (!m_initialized) return;
            if (CurrentPageType == pageType) return;

            if (!m_lookup.TryGetValue(pageType, out var targetInfo)) return;

            // 离开当前 Page
            if (CurrentPageBase != null) {
                var currentInfo = m_lookup[CurrentPageType];
                if (currentInfo.destoryWhenLeave) {
                    CurrentPageBase.StateEngine.Clear();
                    Destroy(CurrentPageBase.gameObject);
                } else {
                    CurrentPageBase.OnHide();
                    CurrentPageBase.gameObject.SetActive(false);
                }
                CurrentPageBase = null;
            }

            // 进入目标 Page
            PageBase instance;
            if (m_instanceCache.TryGetValue(pageType, out var cached)) {
                instance = cached;
                instance.gameObject.SetActive(true);
                instance.OnShow();
            } else {
                var container = _container ?? transform;
                instance = Instantiate(targetInfo.PageBase, container);
                instance.InitIfNot(_camera);
                if (!targetInfo.destoryWhenLeave)
                    m_instanceCache[pageType] = instance;
            }

            CurrentPageBase = instance;
            CurrentPageType = pageType;
        }

        // 压入新页面
        public void PushPage<T>() where T : PageBase {
            if (CurrentPageBase != null) {
                CurrentPageBase.OnHide();
                CurrentPageBase.gameObject.SetActive(false);
                m_pageStack.Push(CurrentPageBase);
                m_pageTypeStack.Push(CurrentPageType);
            }

            CurrentPageBase = null;
            CurrentPageType = null;

            SwitchTo<T>();
        }

        // 弹出当前页面，显示上一页
        public void PopPage() {
            var currentInfo = m_lookup[CurrentPageType];
            if (currentInfo.destoryWhenLeave) {
                m_instanceCache.Remove(CurrentPageType);
                CurrentPageBase.StateEngine.Clear();
                Destroy(CurrentPageBase.gameObject);
            } else {
                CurrentPageBase.OnHide();
                CurrentPageBase.gameObject.SetActive(false);
            }
            CurrentPageBase = null;
            CurrentPageType = null;

            if (m_pageStack.Count > 0) {
                var prevPage = m_pageStack.Pop();
                var prevType = m_pageTypeStack.Pop();

                if (prevPage != null) {
                    CurrentPageBase = prevPage;
                    CurrentPageType = prevType;
                    CurrentPageBase.gameObject.SetActive(true);
                    CurrentPageBase.OnShow();
                } else {
                    SwitchTo(m_homePageType);
                }
            } else {
                SwitchTo(m_homePageType);
            }
        }
    }
}