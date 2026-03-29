using UnityEngine;
using UnityEngine.UI;

namespace UI.SampleScene
{
    public class LoginView : MonoBehaviour
    {
        [SerializeField] private InputField _accountInput;
        [SerializeField] private InputField _passwordInput;

        private LoginViewModel _viewModel;

        public void SetViewModel(LoginViewModel viewModel)
        {
            _viewModel = viewModel;
        }

        public void Render()
        {
            _accountInput.text = _viewModel.Account;
            _passwordInput.text = _viewModel.Password;
        }


        public string GetInputAccount()
        {
            return _accountInput.text;
        }
    }
}