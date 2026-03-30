using System;
using Constants;

namespace Network.Messages {
    #region Client Message Types
    public abstract class ClientNetworkMessage {
        public int type;
    }
    #endregion
    
    #region Server Message Types
    public abstract class ServerNetworkMessages {}
    [Serializable]
    public class ServerNetworkSuccessMessage : ServerNetworkMessages {}
    [Serializable]
    public class ServerNetworkFailMessage : ServerNetworkMessages {}

    public class NetworkErrorMessage {
        public ServerCode code;
        public string message;
    }
    
    /// <summary>
    /// Wire-format envelope for all server responses.
    /// See <see cref="ServerEnvelope"/> for code usage.
    /// </summary>
    [Serializable]
    public class ServerEnvelope {
        public int code;
        public string data;
        public string message;
    }
    #endregion

    #region Sample Message Types
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
    #endregion
}