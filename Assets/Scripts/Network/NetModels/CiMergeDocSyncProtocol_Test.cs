using System;

namespace Network.Messages {
    /// <summary>
    /// GitHub Actions PR merge 后自动写回飞书文档 — 联调测试用协议。
    /// 含 enum、数据 struct、客户端消息 class、功能接口。勿接入业务代码。
    /// </summary>

    public enum MergeDocSyncTestKind {
        ping = 0,
        ack = 1,
    }

    [Serializable]
    public class MergeDocSyncTestPayload {
        public string traceId;
        public int seq;
        public MergeDocSyncTestKind kind;
    }

    [Serializable]
    public class MergeDocSyncTestRequest : ClientNetworkMessage {
        public string uid;
        public MergeDocSyncTestPayload payload;
    }

    public interface IMergeDocSyncTestHandler {
        bool TryHandle(MergeDocSyncTestRequest request);
    }
}
