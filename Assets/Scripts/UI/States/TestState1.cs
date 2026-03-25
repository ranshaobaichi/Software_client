using UnityEngine;
using UI.StateEngine;

namespace UI.States {
    public class TestState1 : StateBase {
        [SerializeField]
        private int _chooseNumber = -1;
        public void OnReturnClicked() {
            m_StateEngine.TryRemoveTop();
        }

        public void ChooseNumber(int number) {
            _chooseNumber = number;
            OnReturnClicked();
        }

        protected override void OnExit() {
            base.OnExit();
            m_StateEngine.SendMessage<TestState1, TestInitState>(_chooseNumber );
        }
    }
}