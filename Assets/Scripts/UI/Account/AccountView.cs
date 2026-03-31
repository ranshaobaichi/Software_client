using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace UI.Account
{
    public class AccountView : MonoBehaviour
    {
        [Header("面板")]
        public GameObject loginPanel;
        public GameObject registerPanel;

        [FormerlySerializedAs("accountInput_Login")]
        [Header("输入框")]
        public InputField accountInputLogin;
        [FormerlySerializedAs("accountInput_Register")]
        public InputField accountInputRegister;

        private AccountViewModel m_viewModel;

        public void SetViewModel(AccountViewModel vm)
        {
            m_viewModel = vm;
            Render();
        }

        public void Render()
        {
            if (m_viewModel == null) return;
            accountInputLogin.text = m_viewModel.Account;
            accountInputRegister.text = m_viewModel.Account;
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