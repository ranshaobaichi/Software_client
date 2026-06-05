using System;
using System.Collections;
using System.Collections.Generic;
using Automation.Config;
using Automation.Protocol;
using Automation.Util;
using Network;

namespace Automation.Sessions {
    public class BattleSession {
        readonly NetworkEndpointConfig m_config;

        INetworkChannel m_channel;
        readonly SignalAwaiter m_battleStartSignal = new SignalAwaiter();
        readonly SignalAwaiter m_battleEndSignal = new SignalAwaiter();

        public bool BattleStarted => m_battleStartSignal.IsSignaled;
        public bool BattleEnded => m_battleEndSignal.IsSignaled;

        public BattleSession(NetworkEndpointConfig config) {
            m_config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public IEnumerator Connect() {
            if (m_channel != null && m_channel.IsConnected)
                yield break;

            if (m_channel == null) {
                var mainHandlers = new Dictionary<int, LongConnectionMainDispatchEntry> {
                        {
                                (int)BattleResponseType.BATTLE_WAIT,
                                NetworkManager.CreateDispatchEntry<BattleWaitResponseData>(_ => { })
                        },
                        {
                                (int)BattleResponseType.BATTLE_FRAME,
                                NetworkManager.CreateDispatchEntry<BattleFrameResponseData>(_ => { })
                        }
                };

                var pushHandlers = new Dictionary<int, Action> {
                        { (int)BattlePushMessageType.BATTLE_START, () => m_battleStartSignal.Signal() },
                        { (int)BattlePushMessageType.BATTLE_END, () => m_battleEndSignal.Signal() }
                };

                m_channel = NetworkManager.SInstance.CreateLongConnection(
                        m_config.Host,
                        m_config.BattlePort,
                        mainHandlers,
                        pushHandlers,
                        dispatchPushWhenMainTypeUnknown: true);
            }

            if (!m_channel.IsConnected)
                m_channel.Connect();

            yield return AutomationAwaiter.WaitUntil(() => m_channel.IsConnected, m_config.DefaultTimeoutSeconds,
                    "Battle long connection failed");
        }

        public IEnumerator PlayerReady() {
            if (!PlayerData.IsInit())
                throw new AutomationException("Player must be logged in before battle operations");

            yield return Connect();
            m_battleStartSignal.Reset();
            m_battleEndSignal.Reset();

            m_channel.Send(new BattlePlayerReadyRequest {
                    type = (int)BattleRequestType.PLAYER_READY,
                    uid = PlayerData.SInstance.basicInfo.uid
            });

            yield return AutomationAwaiter.WaitForNextFrame();
        }

        public IEnumerator WaitForBattleStart(LobbySession lobbyForPoll = null) {
            if (m_battleStartSignal.IsSignaled)
                yield break;

            float deadline = UnityEngine.Time.realtimeSinceStartup + m_config.DefaultTimeoutSeconds;
            while (UnityEngine.Time.realtimeSinceStartup < deadline) {
                if (m_battleStartSignal.IsSignaled)
                    yield break;

                if (lobbyForPoll != null) {
                    GetStateStatusResponse status = null;
                    yield return lobbyForPoll.GetStateStatus(value => status = value);
                    if (status != null && status.roomPhase >= (int)RoomPhase.BATTLE)
                        yield break;
                }

                yield return null;
            }

            throw new AutomationException("Timed out waiting for BATTLE_START");
        }

        public IEnumerator SendPositionSync(float x = 0f, float y = 0f, float dirX = 1f, float dirY = 0f) {
            EnsureConnected();
            m_channel.Send(new BattlePositionSyncRequest {
                    type = (int)BattleRequestType.POSITION_SYNC,
                    uid = PlayerData.SInstance.basicInfo.uid,
                    playerPosition = new BattleVector2(x, y),
                    playerDirection = new BattleVector2(dirX, dirY),
                    enemyPositions = Array.Empty<BattleEnemyPositionEntry>()
            });
            yield return AutomationAwaiter.WaitForNextFrame();
        }

        public IEnumerator SendShoot(float dirX = 1f, float dirY = 0f) {
            EnsureConnected();
            m_channel.Send(new BattlePlayerShootRequest {
                    type = (int)BattleRequestType.PLAYER_SHOOT,
                    uid = PlayerData.SInstance.basicInfo.uid,
                    direction = new BattleVector2(dirX, dirY)
            });
            yield return AutomationAwaiter.WaitForNextFrame();
        }

        public IEnumerator IdleUntilEnd(float? timeoutSeconds = null) {
            EnsureConnected();
            float timeout = timeoutSeconds ?? m_config.BattleIdleTimeoutSeconds;
            float deadline = UnityEngine.Time.realtimeSinceStartup + timeout;
            float nextSync = UnityEngine.Time.realtimeSinceStartup;

            while (!m_battleEndSignal.IsSignaled) {
                if (UnityEngine.Time.realtimeSinceStartup >= deadline)
                    throw new AutomationException("Timed out waiting for BATTLE_END");

                if (UnityEngine.Time.realtimeSinceStartup >= nextSync) {
                    yield return SendPositionSync();
                    nextSync = UnityEngine.Time.realtimeSinceStartup + m_config.PositionSyncIntervalSeconds;
                }

                yield return null;
            }
        }

        public void Disconnect() {
            if (m_channel == null)
                return;

            NetworkManager.SInstance.RemoveConnection(m_channel);
            m_channel = null;
            m_battleStartSignal.Reset();
            m_battleEndSignal.Reset();
        }

        void EnsureConnected() {
            if (m_channel == null || !m_channel.IsConnected)
                throw new AutomationException("Battle long connection is not open");
        }
    }
}
