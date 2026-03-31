using UnityEngine;
using UnityEngine.UI;
using UI.StateEngine;
using UI.SampleScene;
using System.Collections.Generic;

namespace UI.States
{
    public class HomeState : StateBase
    {
        [SerializeField] private HomeView _view;
        [SerializeField] private Button _logoutBtn;
        private HomeViewModel _viewModel;

        protected override void OnEnter()
        {
            base.OnEnter();
            _viewModel = new HomeViewModel();
            _view.SetViewModel(_viewModel);
            _viewModel.SetData("未登录");
            _view.Render();
            _logoutBtn.onClick.AddListener(OnLogoutClick);
        }

        /*public override void ReceiveMessage(Dictionary<System.Type, object> messages)
        {
            if (messages == null) return;
            if (messages.TryGetValue(typeof(LoginState), out var obj) && obj is string account)
            {
                _viewModel.SetData(account);
                _view.Render();
            }
        }
*/
        private void OnLogoutClick()
        {
            m_StateEngine.TryRemoveTop();
        }

        protected override void OnExit()
        {
            base.OnExit();
            _logoutBtn.onClick.RemoveAllListeners();
        }
    }
}