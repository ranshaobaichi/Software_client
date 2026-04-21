using System.Collections.Generic;
using System.Linq;
using Constants;
using Network;
using Network.Messages;
using Utils;

namespace UI.ViewModels {
    public class MapViewModel : ViewModelBase<MapViewModel> {
        private ServerMapNode[] m_map;
        public ServerMapNode[] Map => m_map;

        private Dictionary<int, List<ServerMapNode>> m_layers;
        public Dictionary<int, List<ServerMapNode>> Layers => m_layers;

        public void RequestMap(int roomId) {
            var req = new MapRequest {
                    type = (int)BattleRequestType.GET_MAP,
                    roomId = roomId
            };

            NetworkManager.SInstance.SendShortRequest<
                    MapRequest,
                    MapResponse,
                    ServerNetworkFailMessage
            >(
                    NetworkConstants.HomePort,
                    req,
                    OnSuccess,
                    OnFail,
                    onError: OnError
            );
        }

        private void OnSuccess(MapResponse response) {
            m_map = response.map;

            BuildLayers(); // ⭐关键

            RaisePropertyChanged(nameof(Layers));
        }

        private void OnFail(ServerNetworkFailMessage msg) { }
        private void OnError(NetworkErrorMessage msg) { }

        private void BuildLayers()
        {
            m_layers = new Dictionary<int, List<ServerMapNode>>();

            if (m_map == null || m_map.Length == 0)
                return;

            var nodeDict = m_map.ToDictionary(n => n.nodeId, n => n);

            var indegree = new Dictionary<string, int>();
            foreach (var n in m_map)
                indegree[n.nodeId] = 0;

            foreach (var n in m_map)
            {
                if (n.nextIds == null) continue;

                foreach (var next in n.nextIds)
                {
                    if (indegree.ContainsKey(next))
                        indegree[next]++;
                }
            }

            var queue = new Queue<string>();

            foreach (var kv in indegree)
            {
                if (kv.Value == 0)
                    queue.Enqueue(kv.Key);
            }

            int layer = 0;

            while (queue.Count > 0)
            {
                int size = queue.Count;
                var list = new List<ServerMapNode>();

                for (int i = 0; i < size; i++)
                {
                    var id = queue.Dequeue();
                    var node = nodeDict[id];

                    list.Add(node);

                    if (node.nextIds == null) continue;

                    foreach (var next in node.nextIds)
                    {
                        indegree[next]--;
                        if (indegree[next] == 0)
                            queue.Enqueue(next);
                    }
                }

                m_layers[layer] = list;
                layer++;
            }
        }
        public void TestLocalMap()
        {
            m_map = CreateTestMap();

            BuildLayers();

            RaisePropertyChanged(nameof(Layers));
        }
        private ServerMapNode[] CreateTestMap()
        {
            return new ServerMapNode[]
            {
                    new ServerMapNode
                    {
                            nodeId = "0",
                            type = NodeType.NORMAL,
                            nextIds = new string[] { "1", "2", "3" }
                    },

                    new ServerMapNode
                    {
                            nodeId = "1",
                            type = NodeType.NORMAL,
                            nextIds = new string[] { "4", "5" }
                    },

                    new ServerMapNode
                    {
                            nodeId = "2",
                            type = NodeType.ELITE,
                            nextIds = new string[] { "5" }
                    },

                    new ServerMapNode
                    {
                            nodeId = "3",
                            type = NodeType.EVENT,
                            nextIds = new string[] { "6" }
                    },

                    new ServerMapNode
                    {
                            nodeId = "4",
                            type = NodeType.NORMAL,
                            nextIds = new string[] { "7" }
                    },

                    new ServerMapNode
                    {
                            nodeId = "5",
                            type = NodeType.ELITE,
                            nextIds = new string[] { "7", "8" }
                    },

                    new ServerMapNode
                    {
                            nodeId = "6",
                            type = NodeType.EVENT,
                            nextIds = new string[] { "8" }
                    },

                    new ServerMapNode
                    {
                            nodeId = "7",
                            type = NodeType.NORMAL,
                            nextIds = new string[] { "9" }
                    },

                    new ServerMapNode
                    {
                            nodeId = "8",
                            type = NodeType.ELITE,
                            nextIds = new string[] { "9" }
                    },

                    new ServerMapNode
                    {
                            nodeId = "9",
                            type = NodeType.BOSS,
                            nextIds = new string[] { }
                    }
            };
        }
    }
}