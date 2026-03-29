using UnityEngine;
using UnityEngine.UI;
using UI.StateEngine;
using UI.Account;

namespace UI.States
{
    public class AccountState : StateBase
    {
        [SerializeField] private AccountView _view;

        private AccountViewModel _viewModel;

        protected override void OnEnter()
        {
            base.OnEnter();

            _viewModel = new AccountViewModel();
            _view.SetViewModel(_viewModel);
            _view.Render();
        }

        // 登录按钮（在Unity里绑定）
        public void OnLoginClick()
        {
            string account = _view.GetAccount();
            m_StateEngine.SendMessage<AccountState, HomeState>(account);
            m_StateEngine.AddTop<HomeState>();
        }

        // 去注册按钮（Unity里绑定）
        public void OnGoRegisterClick()
        {
            _view.ShowRegister();
        }

        // 注册按钮（Unity里绑定）
        public void OnRegisterClick()
        {
            _view.ShowLogin();
        }

        // 去登录按钮（Unity里绑定）
        public void OnGoLoginClick()
        {
            _view.ShowLogin();
        }

        protected override void OnExit()
        {
            base.OnExit();
        }
    }
}