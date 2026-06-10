using System;

namespace Network.Messages {
    /// <summary>
    /// 虚构商店协议，专用于 GitHub Actions api-doc-sync 联调测试；不接入业务逻辑。
    /// </summary>

    public enum ShopGitActionsTestRequestType {
        PROBE = 99,
    }

    [Serializable]
    public class ShopGitActionsTestProbeRequest : ClientNetworkMessage {
        public string uid;
        public string probeToken;
    }

    [Serializable]
    public class ShopGitActionsTestProbeResponse {
        public bool ok;
        public string message;
        public long serverTimeUnix;
    }
}
