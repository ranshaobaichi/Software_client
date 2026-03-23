using System;

namespace Network.Messages {
    [Serializable]
    public class ResponseStatusBase {
        public bool status;
    }

    [Serializable]
    public class SuccessResponse : ResponseStatusBase {
        public string data;
    }

    [Serializable]
    public class ErrorResponse : ResponseStatusBase {
        public int errorCode;
        public string message;
    }

    /// <summary>
    /// Wire-format envelope for all server responses.
    /// <c>status</c> discriminates the two cases:
    /// true  → <c>data</c> carries the business payload JSON;
    /// false → <c>errorCode</c> and <c>message</c> describe the failure.
    /// </summary>
    [Serializable]
    public class ServerEnvelope {
        public bool status;
        public string data;
        public int errorCode;
        public string message;
    }

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