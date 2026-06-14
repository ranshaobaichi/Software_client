using System;

namespace Network.Messages {
    /// <summary>
    /// 虚构战斗模块协议 — 仅供 api-sync eval（文档→代码）测试。
    /// 对应飞书：接口文档-战斗（客户端请求）+ 类型约束-战斗（枚举）。
    /// 勿接入业务代码。
    /// </summary>

    public enum BattleTestActionKind {
        move = 0,
        skill = 1,
        defend = 2,
    }

    [Serializable]
    public class BattleTestActionRequest : ClientNetworkMessage {
        public string uid;
        public int turnIndex;
        public BattleTestActionKind actionKind;
        public string targetId;
    }
}
