using System;
using System.Collections;
using System.Collections.Generic;
using Automation.Config;
using Automation.Rules;
using Automation.Util;
using Constants;
using Network;
using Network.Messages;

namespace Automation.Sessions {
    public class ShopSession {
        readonly NetworkEndpointConfig m_config;

        INetworkChannel m_channel;
        ShopSyncResponse m_lastSync;
        readonly SignalAwaiter m_syncSignal = new SignalAwaiter();

        public ShopSyncResponse LastSync => m_lastSync;

        public ShopSession(NetworkEndpointConfig config) {
            m_config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// No-op: shop phase advance is driven by any player's MAP_INIT (SHOP→MAP), not a shop opcode.
        /// Orchestrator should call <see cref="MapSession.InitMap"/> next instead of shop commands.
        /// </summary>
        public IEnumerator Skip() {
            yield break;
        }

        public IEnumerator Connect() {
            if (m_channel != null && m_channel.IsConnected)
                yield break;

            if (m_channel == null) {
                var mainHandlers = new Dictionary<ShopResponseType, LongConnectionMainDispatchEntry> {
                        {
                                ShopResponseType.SHOP_SYNC,
                                NetworkManager.CreateDispatchEntry<ShopSyncResponse>(OnSync)
                        }
                };

                m_channel = NetworkManager.SInstance.CreateLongConnection(
                        m_config.Host,
                        m_config.ShopPort,
                        mainHandlers,
                        new Dictionary<int, Action>());
            }

            if (!m_channel.IsConnected)
                m_channel.Connect();

            yield return AutomationAwaiter.WaitUntil(() => m_channel.IsConnected, m_config.DefaultTimeoutSeconds,
                    "Shop long connection failed");
        }

        public IEnumerator Init() {
            yield return Connect();
            m_syncSignal.Reset();
            m_channel.Send(new ShopInitRequest {
                    type = (int)ShopRequestType.SHOP_INIT,
                    uid = PlayerData.SInstance.basicInfo.uid
            });

            yield return AutomationAwaiter.WaitForSignal(m_syncSignal, m_config.DefaultTimeoutSeconds,
                    "Timed out waiting for SHOP_INIT sync");
        }

        public IEnumerator AutoBuyFirst() {
            yield return Init();

            ShopItem target = ShopPurchaseRules.FindFirstAffordable(m_lastSync?.items,
                    PlayerData.SInstance.basicInfo.uid);
            if (target == null || string.IsNullOrEmpty(target.itemId))
                yield break;

            m_syncSignal.Reset();
            m_channel.Send(new ShopBuyRequest {
                    type = (int)ShopRequestType.SHOP_BUY_ITEM,
                    uid = PlayerData.SInstance.basicInfo.uid,
                    itemId = target.itemId
            });

            yield return AutomationAwaiter.WaitForSignal(m_syncSignal, m_config.DefaultTimeoutSeconds,
                    "Timed out waiting for SHOP_BUY sync");
        }

        public void Disconnect() {
            if (m_channel == null)
                return;

            NetworkManager.SInstance.RemoveConnection(m_channel);
            m_channel = null;
            m_lastSync = null;
            m_syncSignal.Reset();
        }

        void OnSync(ShopSyncResponse response) {
            if (response == null)
                return;
            m_lastSync = response;
            m_syncSignal.Signal();
        }
    }
}
