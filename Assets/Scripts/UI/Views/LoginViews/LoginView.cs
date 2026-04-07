using UnityEngine;
using UI.Dialogs;
using UI.StateEngine;
using UI.ViewModels;

namespace UI.Views {
    public class LoginView : MonoBehaviour {
        private LoginViewModel m_viewModel;

        public void SetViewModel(LoginViewModel vm) { m_viewModel = vm; }
        public void Render(string uid = null) {
            if (m_viewModel == null) {
                return;
            }

            if (uid != null) {
                m_viewModel.SaveUid(uid);
            }
        }

        #region Callbacks
        public void OnLoginClicked() => m_viewModel.SendLoginMessage();

        public void OnRegisterClicked() {
            var pageFinder = new UIPageFinder();
            pageFinder.Current(this).GetDialogMgr().OpenDialog<RegisterDialog>();
        }

        public void OnInputEnd(string input) => Render(input);
        public void OnInputSubmit(string uid) {
            if (string.IsNullOrEmpty(uid)) {
                return;
            }
            Render(uid);
            m_viewModel.SendLoginMessage();
        }
        #endregion
    }
}