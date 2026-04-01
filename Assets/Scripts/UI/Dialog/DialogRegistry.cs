using System;
using System.Collections.Generic;
using UnityEngine;

namespace UI.Dialog {
    [CreateAssetMenu(fileName = "DialogRegistry", menuName = "UI/Dialog Registry")]
    public class DialogRegistry : ScriptableObject {
        [SerializeField] private DialogBase[] _entries;

        private Dictionary<Type, DialogBase> m_lookup;

        public DialogBase GetPrefab(Type key) {
            EnsureLookup();
            if (m_lookup.TryGetValue(key, out var prefab))
                return prefab;
            Debug.LogError($"[DialogRegistry] {key.Name} 未注册。");
            return null;
        }

        private void EnsureLookup() {
            if (m_lookup != null) return;
            m_lookup = new Dictionary<Type, DialogBase>();
            if (_entries == null) return;
            foreach (var entry in _entries) {
                if (entry == null) {
                    Debug.LogWarning("[DialogRegistry] 存在 null 条目，已跳过。");
                    continue;
                }
                var type = entry.GetType();
                if (m_lookup.ContainsKey(type))
                    Debug.LogWarning($"[DialogRegistry] {type.Name} 重复注册，后项覆盖前项。");
                m_lookup[type] = entry;
            }
        }

#if UNITY_EDITOR
        private void OnValidate() {
            if (_entries == null) return;
            var seen = new HashSet<Type>();
            foreach (var entry in _entries) {
                if (entry == null) continue;
                if (!seen.Add(entry.GetType()))
                    Debug.LogError($"[DialogRegistry] 重复的 Dialog 类型: {entry.GetType().Name}");
            }
        }
#endif
    }
}
