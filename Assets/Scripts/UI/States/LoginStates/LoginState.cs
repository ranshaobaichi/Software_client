using UnityEngine;
using UI.StateEngine;
using UI.ViewModels;
using UI.Views;

namespace UI.States {
    public class LoginState : StateBase {
        [SerializeField]
        private LoginView _view;

        protected override void OnEnter() {
            base.OnEnter();

            var viewModel = new LoginViewModel();
            _view.SetViewModel(viewModel);
        }

        protected override void OnExit() {
            base.OnExit();
            LocalizeData.SaveData();
        }
    }
}