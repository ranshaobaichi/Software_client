using UnityEngine.UI;
using UnityEngine;
using System.Collections.Generic;
using Constants;
using Network;
using Network.Messages;
using UI.Dialog;
using UI.Models;
using Utils;

namespace UI.Dialogs.HomePageDialogs {
    public class JoinRoomDialog : DialogBase<RoomModel> {
        [SerializeField]
        private Text _roomHeadCountText;
        [SerializeField]
        private Text _roomIdText;
        [SerializeField]
        private List<Image> _playerAvatarImages;

        private int m_inputRoomId;

        public void OnInputEnd(string input) {
            int.TryParse(input, out m_inputRoomId);
        }
        public void OnJoinRoomClicked() => _SendJoinRoomRequest();
        public void OnInputSubmit(string input) {
            if (int.TryParse(input, out m_inputRoomId)) {
                _SendJoinRoomRequest();
            } else {
                ToastManager.SInstance.ShowToast("请输入有效的房间号");
            }
        }

        protected override void OnRender(RoomModel input) {
            if (input != null) {
                m_inputRoomId = input.roomId;
                _roomIdText.text = input.roomId.ToString();
                _roomHeadCountText.text = $"{input.currentPlayersCount}/{input.maxPlayersCount}";
                for (var i = 0; i < _playerAvatarImages.Count; i++) {
                    _playerAvatarImages[i].gameObject.SetActive(i < input.currentPlayersCount);
                }
            } else {
                m_inputRoomId = -1;
                _roomHeadCountText.text = "";
                foreach (var playerAvatarImage in _playerAvatarImages) {
                    playerAvatarImage.gameObject.SetActive(false);
                }
            }
        }

        private void _OnJoinRoomSuccess(JoinRoomResponse response) => Close(response.roomInfo);
        private void _OnJoinRoomFail(ServerNetworkFailMessage response) => Close(null);
        private void _OnJoinRoomError(NetworkErrorMessage error) => Close(null);

        private void _SendJoinRoomRequest() {
            if (m_inputRoomId <= 0) {
                ToastManager.SInstance.ShowToast("请输入有效的房间号");
                return;
            }
            
            var request = new JoinRoomRequest {
                    type = (int)HomeRequestType.JOIN_ROOM,
                    roomId = m_inputRoomId,
                    uid = PlayerData.SInstance.basicInfo.uid
            };
            NetworkManager.SInstance
                    .SendShortRequest<JoinRoomRequest, JoinRoomResponse, ServerNetworkFailMessage>(
                            NetworkConstants.HomePort, request, _OnJoinRoomSuccess, _OnJoinRoomFail,
                            onError: _OnJoinRoomError
                    );
        }
    }
}