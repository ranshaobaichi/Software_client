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
    /// Wire-format envelope for all server responses.
    /// Use <see cref="ServerEnvelope{T}"/> to deserialize data directly.
    /// </summary>
    [Serializable]
    public class ServerEnvelope<T> where T : class {
        public int code;
        public T data;
        public string message;
    }

    #endregion

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

    #endregion

    #region Home Service

    [Serializable]
    public class CreateRoomRequest : ClientNetworkMessage {
        public string uid;
        public int maximumPeople;
    }

    [Serializable]
    public class CreateRoomResponse : ServerNetworkSuccessMessage {
        public int roomId;
    }

    [Serializable]
    public class JoinRoomRequest : ClientNetworkMessage {
        public string uid;
        public int roomId;
    }

    [Serializable]
    public class RefreshRoomRequest : ClientNetworkMessage { }

    [Serializable]
    public class RoomInfo {
        public int roomId;
        public int maximumPeople;
        public List<PlayerData.PlayerBasicInfo> basicInfos;
    }

    [Serializable]
    public class RefreshRoomResponse : ServerNetworkSuccessMessage {
        public List<RoomInfo> roomInfos;
    }

    [Serializable]
    public class EditProfileRequest : ClientNetworkMessage {
        public string uid;
        public PlayerData.PlayerBasicInfo basicInfo;
    }

    [Serializable]
    public class EditProfileResponse : ServerNetworkSuccessMessage {
        public AvatarColorID color;
    }

    [Serializable]
    public class SyncPlayerColor : ServerNetworkSuccessMessage {
        public string uid;
        public AvatarColorID color;
    }

    #endregion

    #region Map Service
    [Serializable]
    public class MapRequest : ClientNetworkMessage {
        public int roomId;
    }
    [Serializable]
    public class MapResponse : ServerNetworkSuccessMessage {
        public ServerMapNode[] map;
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