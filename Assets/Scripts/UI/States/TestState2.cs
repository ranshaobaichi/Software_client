using UI.StateEngine;

namespace UI.States {
    public class TestState2 : StateBase {
        public void OnReturnClicked() {
            m_StateEngine.TryRemoveTop();
        }
    }
}