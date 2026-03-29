using UnityEngine;
using UnityEngine.UI;
using UI.StateEngine;
using UI.SampleScene;

namespace UI.States
{
    public class LoginState : StateBase
    {
        [SerializeField] private LoginView _view;
        [SerializeField] private Button _loginBtn;
        [SerializeField] private Button _goRegisterBtn;

        private LoginViewModel _viewModel;

        protected override void OnEnter()
        {
            base.OnEnter();

            _viewModel = new LoginViewModel();
            _view.SetViewModel(_viewModel);
            _viewModel.SetData("", "");
            _view.Render();

            _loginBtn.onClick.AddListener(OnLoginClick);
            _goRegisterBtn.onClick.AddListener(OnGoRegisterClick);
        }

        private void OnLoginClick()
        {
            // ==============================================
            // 🔥 【完全和样例一样】
            // 从 View 直接拿输入，不需要任何绑定！！！
            // ==============================================
            string inputAccount = _view.GetInputAccount();

            Debug.Log("从View取账号：" + inputAccount);

            m_StateEngine.SendMessage<LoginState, HomeState>(inputAccount);
            m_StateEngine.AddTop<HomeState>();
        }

        private void OnGoRegisterClick()
        {
            m_StateEngine.AddTop<RegisterState>();
        }

        protected override void OnExit()
        {
            base.OnExit();
            _loginBtn.onClick.RemoveAllListeners();
            _goRegisterBtn.onClick.RemoveAllListeners();
        }
    }
}