using System;
using System.Collections.Generic;
using Constants;

namespace Network.Messages {
    #region Client Message Types
    public abstract class ClientNetworkMessage {
        public int type;
    }
    #endregion

    #region Server Message Types
    public abstract class ServerNetworkMessages { }

    [Serializable]
    public class ServerNetworkSuccessMessage : ServerNetworkMessages { }

    [Serializable]
    public class ServerNetworkFailMessage : ServerNetworkMessages { }

    public class NetworkErrorMessage {
        public ServerCode code;
        public string message;
    }
    
    /// <summary>
    /// Wire-format envelope for all short connection server responses.
    /// Use <see cref="ShortEnvelope{T}"/> to deserialize data directly.
    /// </summary>
    [Serializable]
    public class ShortEnvelope<T> where T : class {
        public int code;
        public T data;
        public string message;
    }

    /// <summary>
    /// Wire-format envelope for all long-connection server payloads.
    /// </summary>
    [Serializable]
    public class LongEnvelope<T> : ServerNetworkMessages {
        public int type;
        public T data;
        public List<int> pushMessages;
    }

    /// <summary>
    /// Wire-format probe for long-connection inbound dispatch: only outer <c>type</c> and <c>pushMessages</c>.
    /// Do not use <see cref="LongEnvelope{ServerNetworkMessages}"/> for probing — <see cref="ServerNetworkMessages"/> is abstract,
    /// so <see cref="JsonUtility"/> cannot materialize <c>data</c> and it will always appear null.
    /// </summary>
    [Serializable]
    public class LongConnectionProbeEnvelope {
        public int type;
        public List<int> pushMessages;
    }
    #endregion

    #region Structures
    [Serializable]
    public class RoomInfo {
        public int roomId;
        public int maximumPeople;
        public List<PlayerData.PlayerBasicInfo> basicInfos;
        public List<string> readyUids;
    }
    #endregion

    #region Short Connection Service
    #region Login Service
    [Serializable]
    public class LoginRequest : ClientNetworkMessage {
        public string uid;
    }

    [Serializable]
    public class LoginResponse : ServerNetworkSuccessMessage {
        public PlayerData playerData;
    }

    [Serializable]
    public class RegisterRequest : ClientNetworkMessage { }

    [Serializable]
    public class RegisterResponse : ServerNetworkSuccessMessage {
        public string uid;
    }

    [Serializable]
    public class LogoutRequest : ClientNetworkMessage {
        public string uid;
    }
    #endregion

    #region Home Service
    [Serializable]
    public class CreateRoomRequest : ClientNetworkMessage {
        public string uid;
        public int maximumPeople;
    }

    [Serializable]
    public class CreateRoomResponse : ServerNetworkSuccessMessage {
        public RoomInfo roomInfo;
    }

    [Serializable]
    public class JoinRoomRequest : ClientNetworkMessage {
        public string uid;
        public int roomId;
    }

    [Serializable]
    public class JoinRoomResponse : ServerNetworkSuccessMessage {
        public RoomInfo roomInfo;
    }

    [Serializable]
    public class RefreshRoomRequest : ClientNetworkMessage { }

    [Serializable]
    public class RefreshRoomResponse : ServerNetworkSuccessMessage {
        public List<RoomInfo> roomInfos;
    }
    #endregion
    #endregion

    #region Long Connection Service
    #region Room Service
    [Serializable]
    public class SetReadyStatusRequest : ClientNetworkMessage {
        public string uid;
        public bool ready;
    }

    [Serializable]
    public class BroadcastRoomStatusResponse : ServerNetworkMessages {
        public RoomInfo roomInfo;
    }
    
    [Serializable]
    public class LeaveRoomRequest : ClientNetworkMessage {
        public string uid;
    }
    [Serializable]
    public class LeaveRoomResponse : ServerNetworkMessages { }
    #endregion
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