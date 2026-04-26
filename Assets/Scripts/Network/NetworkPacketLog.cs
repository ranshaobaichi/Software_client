using System.Diagnostics;
using System.Text;
using Constants;

namespace Network {
    /// <summary>
    /// 战斗长连接报文日志。宏 <c>BATTLE_NET_LOG</c> 开启后：
    /// wire → Assets/Logs/battle_net_wire_*.log，serialized → battle_net_serialized_*.log。
    /// </summary>
    public static class NetworkPacketLog {
        [Conditional("BATTLE_NET_LOG")]
        public static void LogSendWire(int port, byte[] wire) {
            if (!IsBattlePort(port) || wire == null || wire.Length == 0) {
                return;
            }

            BattleNetTrace.WriteWire("C→S", FormatWire(wire));
        }

        [Conditional("BATTLE_NET_LOG")]
        public static void LogSendSerialized(int port, string typeName, string serializedJson) {
            if (!IsBattlePort(port) || string.IsNullOrEmpty(serializedJson)) {
                return;
            }

            var label = string.IsNullOrEmpty(typeName) ? "unknown" : typeName;
            BattleNetTrace.WriteSerialized("C→S", $"[{label}] {serializedJson}");
        }

        [Conditional("BATTLE_NET_LOG")]
        public static void LogRecvWire(int port, byte[] wire) {
            if (!IsBattlePort(port) || wire == null || wire.Length == 0) {
                return;
            }

            BattleNetTrace.WriteWire("S→C", FormatWire(wire));
        }

        [Conditional("BATTLE_NET_LOG")]
        public static void LogRecvEnvelope(int port, int mainType, string envelopeJson, string pushSummary) {
            if (!IsBattlePort(port)) {
                return;
            }

            var push = string.IsNullOrEmpty(pushSummary) ? string.Empty : $" push={pushSummary}";
            BattleNetTrace.WriteSerialized("S→C", $"[LongEnvelope type={mainType}]{push} {envelopeJson}");
        }

        [Conditional("BATTLE_NET_LOG")]
        public static void LogRecvParsed(int port, int mainType, string dataTypeName, string dataJson) {
            if (!IsBattlePort(port) || string.IsNullOrEmpty(dataJson)) {
                return;
            }

            var label = string.IsNullOrEmpty(dataTypeName) ? "unknown" : dataTypeName;
            BattleNetTrace.WriteSerialized("S→C", $"[type={mainType} {label}] {dataJson}");
        }

        [Conditional("BATTLE_NET_LOG")]
        public static void LogRecvPush(int port, int pushMarker) {
            if (!IsBattlePort(port)) {
                return;
            }

            BattleNetTrace.WriteSerialized("S→C push", $"marker={pushMarker}");
        }

        private static bool IsBattlePort(int port) {
            return port == NetworkConstants.BattlePort;
        }

        private static string FormatWire(byte[] wire) {
            var text = TrimWireText(Encoding.UTF8.GetString(wire));
            return $"({wire.Length} bytes) {text}";
        }

        internal static string TrimWireText(string text) {
            if (string.IsNullOrEmpty(text)) {
                return string.Empty;
            }

            if (text[0] == '\uFEFF') {
                text = text.Substring(1);
            }

            return text.TrimEnd('\r', '\n', ' ', '\t');
        }
    }
}
