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

        [SerializeField]
        private PageInfo _homePageInfo;
        [SerializeField]
        private PageInfo[] _pages;
        [SerializeField]
        private Transform _container;
        [SerializeField]
        private Camera _camera;

        private Dictionary<Type, PageInfo> m_lookup;
        private Dictionary<Type, PageBase> m_instanceCache;
        private Type m_homePageType;
        private bool m_initialized;

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
                    if (info?.PageBase == null) {
                        Debug.LogWarning("[PageController] 存在 null 注册项，已跳过。");
                        continue;
                    }

                    var type = info.PageBase.GetType();
                    if (m_lookup.ContainsKey(type))
                        Debug.LogWarning($"[PageController] {type.Name} 重复注册，后项覆盖前项。");
                    m_lookup[type] = info;
                }
            }

            m_initialized = true;
            SwitchTo(m_homePageType);
        }

        public void SwitchTo<T>() where T : PageBase {
            SwitchTo(typeof(T));
        }

        public void SwitchTo(Type pageType) {
            if (!m_initialized) {
                Debug.LogError("[PageController] 尚未初始化，无法切换页面。");
                return;
            }

            if (CurrentPageBase != null && CurrentPageType == pageType)
                return;

            if (!m_lookup.TryGetValue(pageType, out var targetInfo)) {
                Debug.LogError($"[PageController] {pageType.Name} 未注册，无法切换。");
                return;
            }

            // --- 离开当前 Page ---
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

            // --- 进入目标 Page ---
            if (m_instanceCache.TryGetValue(pageType, out var cached)) {
                cached.gameObject.SetActive(true);
                cached.OnShow();
                CurrentPageBase = cached;
            } else {
                var container = _container != null ? _container : transform;
                if (_container == null)
                    Debug.LogWarning("[PageController] _container 为 null，将 Instantiate 到场景根节点。");

                var instance = Instantiate(targetInfo.PageBase, container);
                if (instance == null) {
                    Debug.LogError($"[PageController] Instantiate {pageType.Name} 失败。");
                    return;
                }

                instance.InitIfNot(_camera);
                CurrentPageBase = instance;

                if (!targetInfo.destoryWhenLeave) {
                    m_instanceCache[pageType] = instance;
                }
            }

            CurrentPageType = pageType;
        }
    }
}
