using UnityEngine;
using System;
using System.Collections.Generic;
using UI.Page;

namespace UI.StateEngine {
    /// <summary>
    /// Information of state, set in inspector.
    /// </summary>
    [Serializable]
    public class StateRegistration {
        public RectTransform parent;
        public StateBase state;
    }

    public class StateEngine : MonoBehaviour, IStateEngine {
        public bool isEmpty => m_stack.Count == 0;
        public int count => m_stack.Count;

        [SerializeField]
        private StateRegistration _initState;

        [SerializeField]
        private List<StateRegistration> _registrations = new List<StateRegistration>();

        private readonly Stack<StateBase> m_stack = new Stack<StateBase>();
        private readonly Dictionary<Type, StateBase> m_stateCache = new Dictionary<Type, StateBase>();
        private bool m_busy;

        private IStateLifecycleNotifier m_notifier;

        // outside key: toType, inside key: fromType, value: message
        private readonly Dictionary<Type, Dictionary<Type, object>> m_statesMessages =
                new Dictionary<Type, Dictionary<Type, object>>();

        public void Initialize() {
            // set notifier for debug
            // m_notifier = new StateEngineDebugNotifier();

            // preload all prefabs and cache by type for quick instantiation later
            var allPrefabs = new List<StateRegistration>(_registrations) { _initState };
            foreach (var reg in allPrefabs) {
                if (reg == null || reg.state == null) {
                    Debug.LogWarning("[StateEngine] 存在 null Prefab 注册项，已跳过。");
                    continue;
                }

                var stateBase = Instantiate(reg.state, reg.parent);
                stateBase.SetStateEngine(this);
                stateBase.gameObject.SetActive(false);

                var stateType = stateBase.GetType();
                if (!m_stateCache.TryAdd(stateType, stateBase)) {
                    Debug.LogWarning($"[StateEngine] 类型 {stateType.Name} 已注册，重复项被忽略。");
                    Destroy(stateBase.gameObject);
                }
            }

            if (!isEmpty) {
                Clear();
            }

            if (_initState?.state != null)
                AddTop(_initState.state.GetType());
        }

        public void RegisterState(StateBase state) {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var type = state.GetType();
            if (m_stateCache.ContainsKey(type))
                Debug.LogWarning($"[StateEngine] RegisterState: 类型 {type.Name} 已存在，将覆盖。");
            state.SetStateEngine(this);
            m_stateCache[type] = state;
        }

        public StateBase Peek() => m_stack.Count > 0 ? m_stack.Peek() : null;

        public bool Contains<T>() where T : StateBase {
            if (!m_stateCache.TryGetValue(typeof(T), out var state)) return false;
            return ContainsInstance(state);
        }

        public void SendMessage<T1, T2>(object message) where T1 : StateBase where T2 : StateBase {
            SendMessage(typeof(T1), typeof(T2), message);
        }

        public void SendMessage(Type fromType, Type toType, object message) {
            if (!m_statesMessages.TryGetValue(toType, out var fromDict)) {
                fromDict = new Dictionary<Type, object>();
                m_statesMessages[toType] = fromDict;
            }

            fromDict[fromType] = message;
        }

        #region IStateEngine Implementation
        public bool AddTop<T>() where T : StateBase {
            var state = GetCachedState<T>();
            if (state == null) {
                return false;
            }

            return AddTopInternal(state);
        }

        public bool AddTop(Type stateType) {
            if (stateType == null) {
                return false;
            }

            if (!m_stateCache.TryGetValue(stateType, out var state)) {
                Debug.LogError($"[StateEngine] 类型 {stateType.Name} 未在缓存中找到，请先通过 Inspector 注册或调用 RegisterState()。");
                return false;
            }

            return AddTopInternal(state);
        }

        public bool TryRemoveTop() {
            if (isEmpty) return false;

            AssertNotBusy();
            SetBusy(true);
            try {
                var top = m_stack.Peek();
                var isLastStateInPage = (m_stack.Count == 1);

                top.DoPause();
                m_notifier?.OnStatePause(top);

                m_stack.Pop();
                top.DoExit();
                m_notifier?.OnStateExit(top);
                m_statesMessages.Remove(top.GetType());
                top.gameObject.SetActive(false);

                if (isLastStateInPage) {
             
                    var finder = new UIPageFinder();
                    var pageInterface = finder.Current(top.transform);
                    if (pageInterface.IsValid && !pageInterface.IsHome) {
                        pageInterface.PopPage();
                        return true;
                    }
                } else {
                
                    var newTop = m_stack.Peek();
                    newTop.gameObject.SetActive(true);
                    newTop.DoResume();
                    newTop.ReceiveMessage(GetMessage(newTop.GetType()));
                    m_notifier?.OnStateResume(newTop);
                }

                return true;
            }
            finally {
                SetBusy(false);
            }
        }

        public bool ReplaceTop<T>() where T : StateBase {
            var newTop = GetCachedState<T>();
            if (newTop == null) {
                return false;
            }

            if (isEmpty) {
                AddTopInternal(newTop);
                return false;
            }

            if (ContainsInstance(newTop)) {
                Debug.LogError($"[StateEngine] ReplaceTop 失败：{typeof(T).Name} 已在栈中。");
                return false;
            }

            AssertNotBusy();
            SetBusy(true);
            try {
                var old = m_stack.Peek();
                old.DoPause();
                m_notifier?.OnStatePause(old);

                m_stack.Pop();
                old.DoExit();
                m_statesMessages.Remove(old.GetType());
                m_notifier?.OnStateExit(old);
                old.gameObject.SetActive(false);

                m_stack.Push(newTop);
                newTop.gameObject.SetActive(true);
                var newType = newTop.GetType();
                newTop.DoEnter();
                m_notifier?.OnStateEnter(newTop);
                newTop.DoResume();
                newTop.ReceiveMessage(GetMessage(newType));
                m_notifier?.OnStateResume(newTop);
                return true;
            }
            finally {
                SetBusy(false);
            }
        }

        public bool TryRemoveTo<T>() where T : StateBase {
            if (!m_stateCache.TryGetValue(typeof(T), out var target)) return false;
            if (!ContainsInstance(target)) return false;
            if (ReferenceEquals(m_stack.Peek(), target)) return true;

            AssertNotBusy();
            SetBusy(true);
            try {
                var originTop = m_stack.Peek();
                while (m_stack.Count > 0 && !ReferenceEquals(m_stack.Peek(), target)) {
                    var top = m_stack.Peek();
                    m_stack.Pop();

                    if (top == originTop) {
                        top.DoPause();
                        m_notifier?.OnStatePause(top);
                    }

                    top.DoExit();
                    m_statesMessages.Remove(top.GetType());
                    m_notifier?.OnStateExit(top);
                    top.gameObject.SetActive(false);
                }

                if (!isEmpty) {
                    var newTop = m_stack.Peek();
                    newTop.gameObject.SetActive(true);
                    newTop.DoResume();
                    newTop.ReceiveMessage(GetMessage(newTop.GetType()));
                    m_notifier?.OnStateResume(newTop);
                }

                return true;
            }
            finally {
                SetBusy(false);
            }
        }

        public void Clear() {
            if (isEmpty) return;

            AssertNotBusy();
            SetBusy(true);
            try {
                var originTop = m_stack.Peek();
                while (m_stack.Count > 0) {
                    var top = m_stack.Peek();
                    if (top == originTop) {
                        top.DoPause();
                        m_notifier?.OnStatePause(top);
                    }

                    m_stack.Pop();
                    top.DoExit();
                    m_notifier?.OnStateExit(top);
                    top.gameObject.SetActive(false);
                }
            }
            finally {
                m_statesMessages.Clear();
                SetBusy(false);
            }
        }
        #endregion

        #region Inner Api
        private bool AddTopInternal(StateBase state) {
            if (ContainsInstance(state)) {
                Debug.LogError($"[StateEngine] AddTop 失败：{state.GetType().Name} 已在栈中。");
                return false;
            }

            AssertNotBusy();
            SetBusy(true);
            try {
                if (!isEmpty) {
                    var oldTop = m_stack.Peek();
                    oldTop.DoPause();
                    oldTop.gameObject.SetActive(false);
                    m_notifier?.OnStatePause(oldTop);
                }

                m_stack.Push(state);
                state.gameObject.SetActive(true);
                var stateType = state.GetType();
                state.DoEnter();
                m_notifier?.OnStateEnter(state);
                state.DoResume();
                state.ReceiveMessage(GetMessage(stateType));
                m_notifier?.OnStateResume(state);
                return true;
            }
            finally {
                SetBusy(false);
            }
        }

        private bool ContainsInstance(StateBase state) {
            foreach (var s in m_stack)
                if (ReferenceEquals(s, state))
                    return true;
            return false;
        }

        private StateBase GetCachedState<T>() where T : StateBase {
            if (m_stateCache.TryGetValue(typeof(T), out var state)) return state;
            Debug.LogError($"[StateEngine] 类型 {typeof(T).Name} 未在缓存中找到，请先通过 Inspector 注册或调用 RegisterState()。");
            return null;
        }

        private void AssertNotBusy() {
            if (m_busy)
                throw new InvalidOperationException(
                        "[StateEngine] 检测到重入调用：引擎正忙，拒绝本次操作。请避免在生命周期回调中直接调用引擎 API。");
        }

        private void SetBusy(bool value) => m_busy = value;

        private Dictionary<Type, object> GetMessage(Type type) { // Can be null
            if (m_statesMessages.Remove(type, out var dict))
                return dict;
            return null;
        }
        #endregion
    }
}