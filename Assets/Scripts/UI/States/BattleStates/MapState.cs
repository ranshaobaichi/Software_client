using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using Constants;
using Network;
using Network.Messages;
using UI.Pages;
using UI.StateEngine;
using UI.ViewModels;
using UI.Views;

namespace UI.States {
    public class MapState : StateBase {
        [SerializeField]
        private MapView _mapView;

        private MapViewModel m_viewModel;
        private INetworkChannel m_channel;
        private int roomId;

        protected override void OnEnter() {
            base.OnEnter();

            m_viewModel = new MapViewModel();


            _mapView.Init(m_viewModel, OnNodeClickFromUI);
            var mainHandler = new Dictionary<MapResponseType, LongConnectionMainDispatchEntry> {
                    {
                            MapResponseType.MAP_INIT,
                            NetworkManager.CreateDispatchEntry<MapInitResponse>(_OnMapInitResponse)
                    }, {
                            MapResponseType.MAP_SYNC,
                            NetworkManager.CreateDispatchEntry<MapSyncResponse>(_OnMapSync)
                    }
            };

            var pushHandler = new Dictionary<int, Action>
                    { };

            m_channel = NetworkManager.SInstance.CreateLongConnection(
                    NetworkConstants.DefaultHost,
                    NetworkConstants.MapPort,
                    mainHandler,
                    pushHandler
            );
        }

        protected override void OnResume() {
            base.OnResume();
            m_channel?.Connect();
            SendMapInit();

#if UNITY_EDITOR
            Application.runInBackground = true;
#endif
        }

        protected override void OnPause() {
            base.OnPause();
            m_channel?.Disconnect();
        }

        protected override void OnExit() {
            base.OnExit();
            NetworkManager.SInstance.RemoveConnection(m_channel);
        }

        public void OnQuitButtonClicked() {
            m_StateEngine.TryRemoveTop();
        }

        public void TEST_SwitchToShopState() {
            m_StateEngine.AddTop<ShopState>();
        }

        private void _OnMapInitResponse(MapInitResponse response) {
            if (response == null || response.map == null) {
                Debug.LogWarning("MAP_INIT response is null");
                return;
            }


            m_viewModel.SetMap(response.map);
            m_viewModel.ApplySync(new MapSync[0]);
            _mapView.RefreshNodeSelectState();
        }

        public override void ReceiveMessage(Dictionary<Type, object> messages) {
            if (messages == null) {
                return;
            }

            if (messages.TryGetValue(typeof(OnlineLobbyState), out var obj) && obj is RoomInfo roomInfo) {
                roomId = roomInfo.roomId;
            }
        }

        private void _OnMapSync(MapSyncResponse response) {
            if (response?.selectStatus == null)
                return;

            m_viewModel.ApplySync(response.selectStatus);

            _mapView.RefreshNodeSelectState();
        }

        private void SendMapInit() {
            var req = new MapInitRequest {
                    type = (int)MapRequestType.MAP_INIT,
                    roomId = roomId,
                    uid = PlayerData.SInstance.basicInfo.uid
            };

            m_channel.Send(req);
        }

        public void OnNodeClickFromUI(int nodeId) {
            var req = new MapMoveRequest {
                    type = (int)MapRequestType.MAP_MOVE,
                    uid = PlayerData.SInstance.basicInfo.uid,
                    selectId = nodeId
            };

            m_channel.Send(req);
        }
    }
}