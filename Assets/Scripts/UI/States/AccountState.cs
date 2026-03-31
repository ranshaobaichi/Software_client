using UnityEngine;
using UnityEngine.UI;
using UI.StateEngine;
using UI.Account;

namespace UI.States
{
    public class AccountState : StateBase
    {
        [SerializeField] private AccountView _view;

        private AccountViewModel m_viewModel;

        protected override void OnEnter()
        {
            base.OnEnter();

            m_viewModel = new AccountViewModel();
            _view.SetViewModel(m_viewModel);
            _view.Render();
        }


        public void OnLoginClick()
        {
            string account = _view.GetAccount();
            m_StateEngine.SendMessage<AccountState, HomeState>(account);
            m_StateEngine.AddTop<HomeState>();
        }


        public void OnGoRegisterClick()
        {
            _view.ShowRegister();
        }

        public void OnRegisterClick()
        {
            _view.ShowLogin();
        }


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