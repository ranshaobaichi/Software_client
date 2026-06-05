using System;
using System.Collections;
using System.Collections.Generic;
using Automation.Config;
using Automation.Protocol;
using Automation.Rules;
using Automation.Util;
using Network;
using Network.Messages;

namespace Automation.Sessions {
    public class MapSession {
        readonly NetworkEndpointConfig m_config;

        INetworkChannel m_channel;
        MapNode[] m_map;
        int m_selectedRouteId = -1;
        readonly SignalAwaiter m_initSignal = new SignalAwaiter();
        readonly SignalAwaiter m_commitSignal = new SignalAwaiter();

        public MapNode[] Map => m_map;
        public int SelectedRouteId => m_selectedRouteId;

        public MapSession(NetworkEndpointConfig config) {
            m_config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public IEnumerator Connect() {
            if (m_channel != null && m_channel.IsConnected)
                yield break;

            if (m_channel == null) {
                var mainHandlers = new Dictionary<int, LongConnectionMainDispatchEntry> {
                        {
                                (int)MapResponseType.MAP_INIT,
                                NetworkManager.CreateDispatchEntry<MapInitResponseData>(OnMapInit)
                        },
                        {
                                (int)MapResponseType.MAP_SYNC,
                                NetworkManager.CreateDispatchEntry<MapMoveResponseData>(_ => { })
                        }
                };

                var pushHandlers = new Dictionary<int, Action> {
                        { (int)MapPushMessageType.MAP_SYNC, () => m_commitSignal.Signal() }
                };

                m_channel = NetworkManager.SInstance.CreateLongConnection(
                        m_config.Host,
                        m_config.MapPort,
                        mainHandlers,
                        pushHandlers,
                        dispatchPushWhenMainTypeUnknown: true);
            }

            if (!m_channel.IsConnected)
                m_channel.Connect();

            yield return AutomationAwaiter.WaitUntil(() => m_channel.IsConnected, m_config.DefaultTimeoutSeconds,
                    "Map long connection failed");
        }

        public IEnumerator InitMap(int roomId) {
            if (!PlayerData.IsInit())
                throw new AutomationException("Player must be logged in before map operations");

            yield return Connect();
            m_initSignal.Reset();
            m_commitSignal.Reset();
            m_map = null;

            m_channel.Send(new MapInitRequest {
                    type = (int)MapRequestType.MAP_INIT,
                    roomId = roomId,
                    uid = PlayerData.SInstance.basicInfo.uid
            });

            yield return AutomationAwaiter.WaitForSignal(m_initSignal, m_config.DefaultTimeoutSeconds,
                    "Timed out waiting for MAP_INIT response");
        }

        public IEnumerator SelectDefaultRoute() {
            EnsureMapLoaded();
            int selectId = MapRouteHelper.FirstRoot(m_map);
            if (selectId < 0)
                throw new AutomationException("Could not resolve default map root node");

            yield return SelectRoute(selectId);
        }

        public IEnumerator SelectRoute(int selectId) {
            EnsureMapLoaded();
            if (selectId < 0)
                throw new AutomationException("selectId must be non-negative");

            m_selectedRouteId = selectId;
            m_commitSignal.Reset();

            m_channel.Send(new MapMoveRequest {
                    type = (int)MapRequestType.MAP_MOVE,
                    uid = PlayerData.SInstance.basicInfo.uid,
                    selectId = selectId
            });

            yield return AutomationAwaiter.WaitForNextFrame();
        }

        public IEnumerator WaitForMapCommit() {
            yield return AutomationAwaiter.WaitForSignal(m_commitSignal, m_config.DefaultTimeoutSeconds,
                    "Timed out waiting for MAP_SYNC commit (pushMessages contains 1)");
        }

        public void Disconnect() {
            if (m_channel == null)
                return;

            NetworkManager.SInstance.RemoveConnection(m_channel);
            m_channel = null;
            m_map = null;
            m_selectedRouteId = -1;
            m_initSignal.Reset();
            m_commitSignal.Reset();
        }

        void OnMapInit(MapInitResponseData data) {
            if (data?.map == null)
                return;
            m_map = data.map;
            m_initSignal.Signal();
        }

        void EnsureMapLoaded() {
            if (m_channel == null)
                throw new AutomationException("Map long connection is not open");
            if (m_map == null || m_map.Length == 0)
                throw new AutomationException("Map is not initialized");
        }
    }
}
