using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;
using Constants;
using Network;
using Network.Messages;
using UI.Pages;
using UI.StateEngine;

namespace UI.States {
    public class OnlineRoomState : StateBase {
        [SerializeField]
        private Text _roomIdText;
        [SerializeField]
        private Image _readyStatusImage;

        private int m_roomID;
        private bool m_isReady;
        private INetworkChannel m_channel;

        public void TEST_SwitchToBattlePage() {
            var pageFinder = new UIPageFinder();
            pageFinder.Current(this).SwitchTo<BattlePage>();
        }
        
        private void OnApplicationQuit() {
            var request = new LeaveRoomRequest {
                    type = (int)HomeRequestType.LEAVE_ROOM,
                    uid = PlayerData.SInstance.basicInfo.uid
            };
            m_channel.Send(request);
        }

        #region State Overrides and Message Handling
        public override void ReceiveMessage(Dictionary<Type, object> messages) {
            if (messages == null) {
                return;
            }

            if (messages.TryGetValue(typeof(OnlineLobbyState), out var obj) && obj is RoomInfo roomInfo) {
                _RefreshRoom(roomInfo);
            }
        }

        protected override void OnEnter() {
            var mainHandler =
                    new Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> {
                            {
                                    HomeRequestType.BROADCAST,
                                    NetworkManager.CreateDispatchEntry<BroadcastRoomStatusResponse>(
                                            _OnBroadcastRoomStatusResponse)
                            }, {
                                    HomeRequestType.LEAVE_ROOM,
                                    NetworkManager.CreateDispatchEntry<LeaveRoomResponse>(_OnLeaveRoomResponse)
                            }
                    };

            var pushHandler = new Dictionary<int, Action>();
            m_channel = NetworkManager.SInstance.CreateLongConnection(
                    NetworkConstants.DefaultHost,
                    NetworkConstants.HomePort,
                    mainHandler,
                    pushHandler,
                    dispatchPushWhenMainTypeUnknown: true
            );
        }

        protected override void OnResume() {
            base.OnResume();
            m_channel?.Connect();

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
        #endregion

        #region Button Callbacks
        public void OnQuitButtonClicked() {
            var request = new LeaveRoomRequest {
                    type = (int)HomeRequestType.LEAVE_ROOM,
                    uid = PlayerData.SInstance.basicInfo.uid
            };
            m_channel.Send(request);
        }

        public void OnReadyButtonClicked() {
            m_isReady = !m_isReady;
            var request = new SetReadyStatusRequest {
                    type = (int)HomeRequestType.SET_READY,
                    uid = PlayerData.SInstance.basicInfo.uid,
                    ready = m_isReady
            };
            m_channel.Send(request);
        }
        #endregion
        
        private void _RefreshRoom(RoomInfo roomInfo) {
            m_isReady = false;
            foreach (var uid in roomInfo.readyUids) {
                if (uid == PlayerData.SInstance.basicInfo.uid) {
                    m_isReady = true;
                    break;
                }
            }
            _roomIdText.text = roomInfo.roomId.ToString();
            _readyStatusImage.color = m_isReady ? Color.green : Color.red;
        }

        private void _OnBroadcastRoomStatusResponse(BroadcastRoomStatusResponse response) {
            if (response?.roomInfo == null) {
                return;
            }

            var roomInfo = response.roomInfo;
            _RefreshRoom(roomInfo);
        }

        private void _OnLeaveRoomResponse(LeaveRoomResponse response) {
            if (response == null) {
                return;
            }
            
            m_StateEngine.TryRemoveTop();
        }
    }
}