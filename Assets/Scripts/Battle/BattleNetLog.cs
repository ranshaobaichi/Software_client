using System.Diagnostics;

namespace Battle {
    /// <summary>
    /// 战斗网络日志。宏 <c>BATTLE_NET_LOG</c> 开启后：
    /// - 完整收发包由 <see cref="Network.NetworkPacketLog"/> 记录（wire + serialized/parsed）；
    /// - wire 与 serialized 分别写入 Assets/Logs/battle_net_wire_*.log / battle_net_serialized_*.log。
    /// </summary>
    public static class BattleNetLog {
        [Conditional("BATTLE_NET_LOG")]
        public static void Log(string message) {
            Network.BattleNetTrace.WriteNote(message);
        }
    }
}
