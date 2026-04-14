using UnityEngine;
using UnityEngine.UI;
using Constants;
using Network;
using Network.Messages;
using UI.Dialog;
using Utils;

namespace UI.Dialogs.HomePageDialogs {
    public class CreateRoomDialog : DialogBaseNoInput {
        [SerializeField]
        private Text _maxPlayerNumText;

        private int m_maxPlayerNum = 5;

        public void SetMaxPlayerNum(int num) {
            m_maxPlayerNum = num;
            _maxPlayerNumText.text = num.ToString();
        }

        public void OnCreateRoomClicked() {
            var request = new CreateRoomRequest {
                    type = (int)HomeRequestType.CREATE_ROOM,
                    uid = PlayerData.SInstance.basicInfo.uid,
                    maximumPeople = m_maxPlayerNum
            };
            NetworkManager.SInstance
                    .SendShortRequest<CreateRoomRequest, CreateRoomResponse, ServerNetworkFailMessage>(
                            NetworkConstants.HomePort, request, _OnCreateRoomSuccess, _OnCreateRoomFail,
                            onError: _OnCreateRoomError
                    );
        }

        protected override void OnRender() {
            base.OnRender();
            _maxPlayerNumText.text = "";
        }

        private void _OnCreateRoomSuccess(CreateRoomResponse response) => Close(response.roomId);

        private void _OnCreateRoomFail(ServerNetworkFailMessage response) {
            ToastManager.SInstance.ShowToast("创建房间失败");
            Close(false);
        }

        private void _OnCreateRoomError(NetworkErrorMessage error) => Close(false);
    }
}