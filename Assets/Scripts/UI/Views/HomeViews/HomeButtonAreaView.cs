using Constants;
using Network;
using Network.Messages;
using UI.StateEngine;
using UI.States;
using UI.ViewModels;

namespace UI.Views {
    public class HomeButtonAreaView : ViewBase<HomeButtonAreaViewModel> {
        private UIStateFinder m_stateFinder;

        protected override void Init() {
            base.Init();
            m_stateFinder = new UIStateFinder();
        }

        #region Button Callbacks
        public void OnQuitGameClicked() {
            NetworkManager.SInstance.SendShortRequest(NetworkConstants.LoginPort,
                    new LogoutRequest {
                            type = (int)LoginRequestType.LOGOUT,
                            uid = PlayerData.SInstance.basicInfo.uid
                    },
                    blockOnConnect: true);

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        public void OnSwitchOnlineLobbyClicked() { m_stateFinder.Current(this).AddTop<OnlineLobbyState>(); }
        #endregion
    }
}