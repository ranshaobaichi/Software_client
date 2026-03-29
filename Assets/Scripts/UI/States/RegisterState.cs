using UnityEngine;
using UnityEngine.UI;
using UI.StateEngine;
using UI.SampleScene; 
namespace UI.States
{
    public class RegisterState : StateBase
    {
        [SerializeField]
        private RegisterView _view;

        [SerializeField]
        private Button _confirmBtn;

        [SerializeField]
        private Button _backBtn;

        private RegisterViewModel m_viewModel;

        protected override void OnEnter()
        {
            base.OnEnter();

            // MVVM 初始化（和Login完全一样）
            m_viewModel = new RegisterViewModel();
            _view.SetViewModel(m_viewModel);
            m_viewModel.SetData("", "");
            _view.Render();

            _confirmBtn.onClick.AddListener(OnConfirmRegister);
            _backBtn.onClick.AddListener(OnBackClick);
        }

        private void OnConfirmRegister()
        {
            Debug.Log("注册成功：" + m_viewModel.account);
            m_StateEngine.TryRemoveTop();
        }

        private void OnBackClick()
        {
            m_StateEngine.TryRemoveTop();
        }

        protected override void OnExit()
        {
            base.OnExit();
            _confirmBtn.onClick.RemoveAllListeners();
            _backBtn.onClick.RemoveAllListeners();
        }
    }
}