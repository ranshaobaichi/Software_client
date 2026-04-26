using System;
using System.Threading;
using Constants;
using Network;
using Network.Messages;
using UnityEngine;

namespace Battle {
    /// <summary>
    /// Background thread that sends <see cref="PositionSyncRequest"/> at a fixed rate after battle start.
    /// </summary>
    public sealed class BattleSyncUploader : IDisposable {
        private readonly BattleSyncSnapshot m_snapshot;
        private readonly Func<INetworkChannel> m_channelProvider;
        private readonly Func<string> m_uidProvider;
        private readonly Action m_onPacketSent;

        private Thread m_thread;
        private volatile bool m_running;
        private float m_sendIntervalSeconds;
        private string m_uid = string.Empty;

        public BattleSyncUploader(
                BattleSyncSnapshot snapshot,
                Func<INetworkChannel> channelProvider,
                Func<string> uidProvider,
                Action onPacketSent,
                float sendRateHz = 20f
        ) {
            m_snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            m_channelProvider = channelProvider ?? throw new ArgumentNullException(nameof(channelProvider));
            m_uidProvider = uidProvider ?? throw new ArgumentNullException(nameof(uidProvider));
            m_onPacketSent = onPacketSent;
            m_sendIntervalSeconds = 1f / Math.Max(1f, sendRateHz);
        }

        public void SetSendRateHz(float hz) {
            m_sendIntervalSeconds = 1f / Math.Max(1f, hz);
        }

        public void Start() {
            if (m_running) {
                return;
            }

            m_uid = m_uidProvider?.Invoke() ?? string.Empty;
            m_running = true;
            m_thread = new Thread(_UploadLoop) {
                IsBackground = true,
                Name = "BattleSyncUploader"
            };
            m_thread.Start();
        }

        public void Stop() {
            if (!m_running) {
                return;
            }

            m_running = false;
            try {
                m_thread?.Join(500);
            } catch {
                // ignored
            }

            m_thread = null;
        }

        public void Dispose() {
            Stop();
        }

        private void _UploadLoop() {
            var sleepMs = (int)Math.Max(1, m_sendIntervalSeconds * 1000);
            while (m_running) {
                try {
                    if (!m_snapshot.TryCopy(out var playerPosition, out var enemyPositions)) {
                        Thread.Sleep(sleepMs);
                        continue;
                    }

                    var channel = m_channelProvider?.Invoke();
                    if (channel == null || !channel.IsConnected) {
                        Thread.Sleep(sleepMs);
                        continue;
                    }

                    channel.Send(new PositionSyncRequest {
                            type = (int)BattleRequestType.POSITION_SYNC,
                            uid = string.IsNullOrEmpty(m_uid) ? m_uidProvider?.Invoke() ?? string.Empty : m_uid,
                            playerPosition = playerPosition,
                            enemyPositions = enemyPositions
                    });
                    m_onPacketSent?.Invoke();
                } catch (Exception ex) {
                    UnityEngine.Debug.LogWarning("[BattleSyncUploader] Send failed: " + ex.Message);
                }

                Thread.Sleep(sleepMs);
            }
        }
    }
}
