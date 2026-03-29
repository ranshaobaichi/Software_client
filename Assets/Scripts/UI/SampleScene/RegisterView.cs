using UnityEngine;
using UnityEngine.UI;

namespace UI.SampleScene
{
    public class RegisterView : MonoBehaviour
    {
        [SerializeField]
        private InputField _accountInput;

        [SerializeField]
        private InputField _passwordInput;

        private RegisterViewModel m_viewModel;

        public void SetViewModel(RegisterViewModel viewModel)
        {
            m_viewModel = viewModel;
        }

        public void Render()
        {
            _accountInput.text = m_viewModel.account;
            _passwordInput.text = m_viewModel.password;
        }
    }
}