using System;

namespace Automation.Config {
    [Serializable]
    public class NetworkEndpointConfig {
        public string Host = "127.0.0.1";
        public int LoginPort = 8765;
        public int HomePort = 8766;
        public int ShopPort = 8767;
        public int MapPort = 8768;
        public int BattlePort = 8769;

        public float DefaultTimeoutSeconds = 10f;
        public float BattleIdleTimeoutSeconds = 120f;
        public float PositionSyncIntervalSeconds = 0.5f;
    }
}
