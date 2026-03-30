using System;

namespace Constants {
    public static class NetworkConstants {
        public const string DefaultHost = "127.0.0.1";
        public const int DefaultPort = 8765;

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
}
