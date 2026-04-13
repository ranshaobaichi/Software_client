using UnityEngine;
using Constants;
using Network;
using Network.Messages;
using UI.Dialog;
using UI.Dialogs.HomePageDialogs;
using UI.Models;
using UI.StateEngine;
using UI.ViewModels;
using UI.Views;

namespace UI.States {
    public class OnlineLobbyState : StateBase {
        [SerializeField]
        private RoomItemView _roomItemPrefab;

        [SerializeField]
        private Transform _roomItemParent;

        private UIPageFinder m_pageFinder;

        #region Button Callbacks

        public void OnCreateRoomButtonClicked() {
            m_pageFinder.Current(this).GetDialogMgr().OpenDialog<CreateRoomDialog>(onClose: _OnCloseCreateRoomDialog);
        }

        public void OnRefreshButtonClicked() => _RefreshRoomList();

        public void OnJoinRoomButtonClicked() {
            m_pageFinder.Current(this).GetDialogMgr().OpenDialog<JoinRoomDialog>(onClose: _OnCloseJoinRoomDialog);
        }

        public void OnRoomItemClicked(RoomModel roomModel) {
            m_pageFinder.Current(this).GetDialogMgr()
                    .OpenDialog<JoinRoomDialog>(input: roomModel, onClose: _OnCloseJoinRoomDialog);
        }

        public void OnLeaveRoomButtonClicked() {
            m_StateEngine.TryRemoveTop();
        }

        #endregion

        private void _OnCloseJoinRoomDialog(DialogResult result) {
            if (result.intVal == 1) {
                m_StateEngine.AddTop<OnlineRoomState>();
            }
            else if (result.intVal == 0) {
                _RefreshRoomList();
            }
        }

        private void _OnCloseCreateRoomDialog(DialogResult result) {
            if (result.intVal != 0) {
                m_StateEngine.SendMessage<OnlineLobbyState, OnlineRoomState>(result.intVal);
                m_StateEngine.AddTop<OnlineRoomState>();
            }
            else if (result.intVal == 0) {
                _RefreshRoomList();
            }
        }

        private void _RefreshRoomList() {
            var request = new RefreshRoomRequest {
                    type = (int)HomeRequestType.LIST_ROOMS,
            };
            NetworkManager.SInstance.SendShortRequestWithSuccess<RefreshRoomRequest, RefreshRoomResponse>(
                    NetworkConstants.HomePort, request, _RenderRoomList);
        }

        private void _RenderRoomList(RefreshRoomResponse refreshRoomResponse) {
            for (var i = _roomItemParent.childCount - 1; i >= 0; i--) {
                Destroy(_roomItemParent.GetChild(i).gameObject);
            }

            foreach (var info in refreshRoomResponse.roomInfos) {
                var roomModel = new RoomModel(info.roomId, info.maximumPeople, info.basicInfos);
                var roomItem = Instantiate(_roomItemPrefab, _roomItemParent);
                var viewmodel = new RoomItemViewModel(roomModel);
                roomItem.Init(viewmodel, OnRoomItemClicked);
            }
        }
    }
}