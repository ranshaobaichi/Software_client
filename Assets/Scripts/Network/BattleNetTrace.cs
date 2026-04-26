using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace Network {
    /// <summary>
    /// 战斗网络 trace：Console + 双文件（wire / serialized）。宏 <c>BATTLE_NET_LOG</c> 开启时生效。
    /// 目录：<c>Assets/Logs/</c>
    /// </summary>
    public static class BattleNetTrace {
        private static readonly object s_lock = new object();
        private static string s_sessionStamp;
        private static string s_wireFilePath;
        private static string s_serializedFilePath;
        private static bool s_announcedPaths;

        private enum LogTarget {
            Wire,
            Serialized
        }

        [System.Diagnostics.Conditional("BATTLE_NET_LOG")]
        public static void WriteWire(string direction, string payload) {
            WriteToFile(LogTarget.Wire, $"{direction} {payload}");
        }

        [System.Diagnostics.Conditional("BATTLE_NET_LOG")]
        public static void WriteSerialized(string direction, string payload) {
            WriteToFile(LogTarget.Serialized, $"{direction} {payload}");
        }

        [System.Diagnostics.Conditional("BATTLE_NET_LOG")]
        public static void WriteNote(string message) {
            WriteToFile(LogTarget.Wire, $"--- {message}");
        }

        private static void WriteToFile(LogTarget target, string body) {
            var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {body}";
            Debug.Log($"[BattleNet] {body}");
            EnsureFiles();
            var targetPath = target == LogTarget.Wire ? s_wireFilePath : s_serializedFilePath;
            lock (s_lock) {
                File.AppendAllText(targetPath, line + Environment.NewLine, Encoding.UTF8);
            }

            AnnouncePathsOnce();
        }

        private static void EnsureFiles() {
            if (s_wireFilePath != null && s_serializedFilePath != null) {
                return;
            }

            s_sessionStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var dir = Path.Combine(Application.dataPath, "Logs");
            Directory.CreateDirectory(dir);

            s_wireFilePath = Path.Combine(dir, $"battle_net_wire_{s_sessionStamp}.log");
            s_serializedFilePath = Path.Combine(dir, $"battle_net_serialized_{s_sessionStamp}.log");

            var header = $"=== Battle net trace started {DateTime.Now:O}{Environment.NewLine}";
            lock (s_lock) {
                File.WriteAllText(s_wireFilePath, header, Encoding.UTF8);
                File.WriteAllText(s_serializedFilePath, header, Encoding.UTF8);
            }
        }

        private static void AnnouncePathsOnce() {
            if (s_announcedPaths) {
                return;
            }

            s_announcedPaths = true;
            Debug.Log($"[BattleNet] Wire log: {s_wireFilePath}");
            Debug.Log($"[BattleNet] Serialized log: {s_serializedFilePath}");
        }
    }
}
