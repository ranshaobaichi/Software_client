using UnityEngine;
using UnityEngine.UI;

namespace UI.Account
{
    public class AccountView : MonoBehaviour
    {
        [Header("面板")]
        public GameObject loginPanel;
        public GameObject registerPanel;

        [Header("输入框")]
        public InputField accountInput_Login;
        public InputField accountInput_Register;

        private AccountViewModel _viewModel;

        public void SetViewModel(AccountViewModel vm)
        {
            _viewModel = vm;
            Render();
        }

        public void Render()
        {
            if (_viewModel == null) return;
            accountInput_Login.text = _viewModel.Account;
            accountInput_Register.text = _viewModel.Account;
        }

        public void OnLoginAccountChanged(string value)
        {
            if (_viewModel == null) return;
            _viewModel.Account = value;
        }

        public string GetAccount()
        {
            return _viewModel != null ? _viewModel.Account : "";
        }

        public void ShowLogin()
        {
            loginPanel.SetActive(true);
            registerPanel.SetActive(false);
        }

        public void ShowRegister()
        {
            loginPanel.SetActive(false);
            registerPanel.SetActive(true);
        }
    }
}