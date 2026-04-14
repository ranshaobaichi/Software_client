using System;

namespace Constants {
    public static class NetworkConstants {
        public const string DefaultHost = "127.0.0.1";
        public const int LoginPort = 8765;
        public const int HomePort = 8766;
    }

    [Flags]
    public enum ServerCode {
        // service success
        SUCCESS = 1,
        
        // service fail
        FAIL = 1 << 1,
        
        // system error
        ERROR = 1 << 2,
        TIME_OUT = 1 << 3,
        DESERIALIZE_ERROR = 1 << 4,
        CONNECTION_ERROR = 1 << 5,
        
        SERVICE_SUCCESS = SUCCESS,
        SERVICE_FAIL = FAIL,
        SYSTEM_ERROR = ERROR | TIME_OUT | DESERIALIZE_ERROR | CONNECTION_ERROR,
    }

    #region Client Message Types
    public enum SampleState2MessageType {
        SUCCESS = 0,
        FAIL = 1,
    }
    #endregion

    #region PlayerData Service
    public enum PlayerAvatarColor {
        
    }
    #endregion

    #region Login Service
    public enum LoginRequestType {
        LOGIN = 0,
        REGISTER = 1,
        LOGOUT = 2,
    }
    #endregion
    
    #region Home Service
    public enum HomeRequestType {
        CREATE_ROOM = 0,
        JOIN_ROOM = 1,
        LEAVE_ROOM = 2,
        LIST_ROOMS = 3,
        SEND_MESSAGE = 4,
        HEARTBEAT = 5,
        EDIT_PROFILE = 6,
        SET_READY = 7,
        BROADCAST = 8
    }
    #endregion
}


