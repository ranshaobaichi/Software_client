using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI.Account
{
    public class AccountView : MonoBehaviour
    {

        [Header("面板")]
        [SerializeField] private GameObject _loginPanel;
        [SerializeField] private GameObject _registerPanel;


        [Header("输入框")]
        [SerializeField] private InputField _accountInputLogin;
        [SerializeField] private InputField _accountInputRegister;

        private AccountViewModel m_viewModel;

        public void SetViewModel(AccountViewModel vm)
        {
            m_viewModel = vm;
        }

        public void Render()
        {
            if (m_viewModel == null) return;
            _accountInputLogin.text = m_viewModel.Account;
            _accountInputRegister.text = m_viewModel.Account;
        }

        public void OnLoginAccountChanged(string value)
        {
            if (m_viewModel == null) return;
            m_viewModel.Account = value;
        }

        public string GetAccount()
        {
            return m_viewModel != null ? m_viewModel.Account : "";
        }

        public void ShowLogin()
        {
            _loginPanel.SetActive(true);
            _registerPanel.SetActive(false);
        }

        public void ShowRegister()
        {
            _loginPanel.SetActive(false);
            _registerPanel.SetActive(true);
        }
    }
}