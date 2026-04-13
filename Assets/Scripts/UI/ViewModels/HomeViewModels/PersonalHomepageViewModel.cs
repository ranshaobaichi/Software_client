using System;
using Constants;
using Network;
using Network.Messages;
using UnityEngine;
using Utils;

namespace UI.ViewModels {
    public class PersonalHomepageViewModel : ViewModelBase<PersonalHomepageViewModel> {
        private string m_uid;
        private AvatarColorID m_color;

        public string Uid => m_uid;

        public AvatarColorID Color {
            get => m_color;
            private set {
                if (m_color == value) return;
                m_color = value;
                RaisePropertyChanged();
            }
        }

        public PersonalHomepageViewModel(string uid, AvatarColorID defaultColor) {
            m_uid = uid;
            m_color = defaultColor;
        }

        public void SwitchNextColor() {
            var values = (AvatarColorID[])Enum.GetValues(typeof(AvatarColorID));

            int index = Array.IndexOf(values, m_color);
            int nextIndex = (index + 1) % values.Length;

            Color = values[nextIndex];
            PlayerData.SInstance.basicInfo.color = Color;
            SendPlayerInfoToServer();
        }

        public void UpdateName(string name) {
            if (string.IsNullOrEmpty(name)) return;

            PlayerData.SInstance.basicInfo.name = name;

            SendPlayerInfoToServer();
        }

        private void SendPlayerInfoToServer() {
            var request = new EditProfileRequest(){
                    type = (int)HomeRequestType.EDIT_PROFILE,
                    uid = m_uid,
                    basicInfo = PlayerData.SInstance.basicInfo
            };

            NetworkManager.SInstance.SendShortRequest<
                    EditProfileRequest,
                    EditProfileResponse,
                    ServerNetworkFailMessage
            >(
                    NetworkConstants.HomePort,
                    request,
                    OnSuccess,
                    OnFail,
                    onError: OnError
            );
        }

        private void OnSuccess(EditProfileResponse response) {
            PlayerData.SInstance.basicInfo.color = m_color;

            ToastManager.SInstance.ShowToast("保存成功");
        }

        private void OnFail(ServerNetworkFailMessage response) {
            ToastManager.SInstance.ShowToast("保存失败");
        }

        private void OnError(NetworkErrorMessage response) {
            ToastManager.SInstance.ShowToast("网络异常");
        }
    }
}