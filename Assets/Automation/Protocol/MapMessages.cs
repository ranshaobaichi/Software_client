using System;
using Network.Messages;

namespace Automation.Protocol {
    [Serializable]
    public class MapNode {
        public int nodeId;
        public int type;
        public int[] nextId;
    }

    [Serializable]
    public class MapInitRequest : ClientNetworkMessage {
        public int roomId;
        public string uid;
    }

    [Serializable]
    public class MapInitResponseData {
        public MapNode[] map;
    }

    [Serializable]
    public class MapMoveRequest : ClientNetworkMessage {
        public string uid;
        public int selectId;
    }

    [Serializable]
    public class MapSyncEntry {
        public string uid;
        public int selectId;
    }

    [Serializable]
    public class MapMoveResponseData {
        public MapSyncEntry[] selectStatus;
    }
}
