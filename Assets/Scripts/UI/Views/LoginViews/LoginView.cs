using UI.Dialogs;
using UI.StateEngine;
using UI.ViewModels;

namespace UI.Views {
    public class LoginView : ViewBase<LoginViewModel> {
        private void _SaveUid(string uid) => ViewModel?.SaveUid(uid);

        #region Callbacks
        public void OnLoginClicked() => ViewModel.SendLoginMessage();

        public void OnRegisterClicked() {
            var pageFinder = new UIPageFinder();
            pageFinder.Current(this).GetDialogMgr().OpenDialog<RegisterDialog>();
        }

        public void OnInputEnd(string input) => _SaveUid(input);

        public void OnInputSubmit(string uid) {
            if (string.IsNullOrEmpty(uid)) {
                return;
            }

            _SaveUid(uid);
            ViewModel.SendLoginMessage();
        }
        #endregion
    }
}