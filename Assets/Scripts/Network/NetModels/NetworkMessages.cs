using System;

namespace Network.Messages {
    [Serializable]
    public class NetPlayer {
        public string id;
        public float x;
        public float y;
    }

    [Serializable]
    public class WelcomeMsg {
        public string type;
        public string id;
    }

    [Serializable]
    public class SnapshotMsg {
        public string type;
        public NetPlayer[] players;
    }

    [Serializable]
    public class MoveMsg {
        public string type = "move";
        public float x;
        public float y;
    }

    [Serializable]
    public class GameMessage {
        public string type;
        public string id;
        public NetPlayer[] players;
    }
}
