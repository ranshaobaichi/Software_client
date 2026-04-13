using System;
using System.Collections.Generic;
using System.Linq;
using Network.Messages;
using Utils;

namespace UI.ViewModels {
    public class MapViewModel : ViewModelBase<MapViewModel> {

        private List<MapNode> m_map;
        public List<MapNode> Map => m_map;

        private Dictionary<int, List<MapNode>> m_layers;
        public Dictionary<int, List<MapNode>> Layers => m_layers;

        private Dictionary<int, string> m_nodeOwner = new();

        // ⭐ 新增：专门用于“状态变化通知”
        public event Action OnNodeOwnerChanged;

        // =========================
        // Map结构构建（低频）
        // =========================
        private void BuildLayers() {
            m_layers = new Dictionary<int, List<MapNode>>();

            if (m_map == null || m_map.Count == 0)
                return;

            var nodeDict = m_map.ToDictionary(n => n.nodeId, n => n);

            var indegree = new Dictionary<int, int>();

            foreach (var n in m_map)
                indegree[n.nodeId] = 0;

            foreach (var n in m_map) {
                if (n.nextId == null) continue;

                foreach (var next in n.nextId) {
                    if (indegree.ContainsKey(next))
                        indegree[next]++;
                    else
                        UnityEngine.Debug.LogError($"nextId不存在: {next}");
                }
            }

            var queue = new Queue<int>();

            foreach (var kv in indegree) {
                if (kv.Value == 0)
                    queue.Enqueue(kv.Key);
            }

            int layer = 0;

            while (queue.Count > 0) {
                int size = queue.Count;
                var list = new List<MapNode>();

                for (int i = 0; i < size; i++) {
                    var id = queue.Dequeue();

                    if (!nodeDict.TryGetValue(id, out var node))
                        continue;

                    list.Add(node);

                    if (node.nextId == null) continue;

                    foreach (var next in node.nextId) {
                        if (!indegree.ContainsKey(next)) continue;

                        indegree[next]--;

                        if (indegree[next] == 0)
                            queue.Enqueue(next);
                    }
                }

                m_layers[layer] = list;
                layer++;
            }
        }

        // =========================
        // 设置地图（低频：会重建UI）
        // =========================
        public void SetMap(List<MapNode> map) {
            m_map = map;

            BuildLayers();

            // ✔ 这里只通知结构变化（必须重建UI）
            RaisePropertyChanged(nameof(Layers));
            RaisePropertyChanged(nameof(Map));
        }

        // =========================
        // 同步状态（高频：只更新状态）
        // =========================
        public void ApplySync(MapSync[] syncs) {
            m_nodeOwner.Clear();

            foreach (var s in syncs) {
                m_nodeOwner[s.selectId] = s.uid;
            }

            // ❌ 不再触发 Map / Layers 重建（关键修复）
            // RaisePropertyChanged(nameof(Map));

            // ✔ 只通知“节点状态变了”
            OnNodeOwnerChanged?.Invoke();
        }

        // =========================
        // 查询节点归属
        // =========================
        public string GetOwner(int nodeId) {
            return m_nodeOwner.TryGetValue(nodeId, out var uid)
                ? uid
                : null;
        }
    }
}