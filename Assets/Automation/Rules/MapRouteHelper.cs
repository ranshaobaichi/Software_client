using System.Collections.Generic;
using Automation.Protocol;

namespace Automation.Rules {
    public static class MapRouteHelper {
        /// <summary>
        /// Returns the first map node with no incoming edges (server map_first_root).
        /// </summary>
        public static int FirstRoot(MapNode[] map) {
            if (map == null || map.Length == 0)
                return -1;

            var incoming = new HashSet<int>();
            for (var i = 0; i < map.Length; i++) {
                MapNode node = map[i];
                if (node?.nextId == null)
                    continue;
                for (var j = 0; j < node.nextId.Length; j++)
                    incoming.Add(node.nextId[j]);
            }

            for (var i = 0; i < map.Length; i++) {
                MapNode node = map[i];
                if (node == null)
                    continue;
                if (!incoming.Contains(node.nodeId))
                    return node.nodeId;
            }

            return -1;
        }
    }
}
