using Constants;
using Network;
using Network.Messages;
using Utils;

namespace UI.ViewModels {
    public class LoginViewModel : ViewModelBase<LoginViewModel> {
        private string m_account;

        public void SaveUid(string uid) { m_account = uid; }

        public void SendLoginMessage() {
            if (string.IsNullOrEmpty(m_account)) {
                ToastManager.SInstance.ShowToast("请输入uid");
                return;
            }

            var request = new LoginRequest {
                    type = (int)LoginRequestType.LOGIN,
                    uid = m_account
            };
            NetworkManager.SInstance.SendShortRequest<LoginRequest, LoginResponse, ServerNetworkFailMessage>(
                    NetworkConstants.LoginPort, request, _OnLoginResponseSuccess, _OnLoginResponseFail,
                    onError: _OnLoginResponseError, blockOnConnect: true);
        }

        private void _OnLoginResponseSuccess(LoginResponse response) {
            PlayerData.Init(response.playerData);
            if (PlayerData.SInstance == null) {
                ToastManager.SInstance.ShowToast("登录失败");
                return;
            }

            LocalizeData.SInstance.playerInfo.lastLoginUid = m_account;
            GameSceneManager.SInstance.SwitchScene(SceneType.HOME);
        }

        private void _OnLoginResponseFail(ServerNetworkFailMessage response) {
            ToastManager.SInstance.ShowToast("登录失败");
        }

        private void _OnLoginResponseError(NetworkErrorMessage response) { ToastManager.SInstance.ShowToast("登录失败"); }
    }
}