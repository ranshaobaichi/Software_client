using Automation.Config;
using Network;
using UnityEngine;

namespace Automation.Bootstrap {
    public static class AutomationBootstrap {
        public static NetworkEndpointConfig ActiveConfig { get; private set; }

        public static void Init(NetworkEndpointConfig config = null) {
            ActiveConfig = config ?? new NetworkEndpointConfig();
            Application.runInBackground = true;
            _ = NetworkManager.SInstance;
            _ = AutomationRunner.Instance;
        }
    }
}
