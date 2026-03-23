namespace Constants {
    public static class NetworkConstants {
        public const string DefaultHost = "127.0.0.1";
        public const int DefaultPort = 8765;
        
        #region ERROR CODE
        public enum ErrorCode {
            CLIENT_DESERIALIZE_ERROR = 408
        }
        #endregion
    }
}
