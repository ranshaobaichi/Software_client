using Constants;
using UI.StateEngine;

namespace UI.States {
    public class ShopState : StateBase {
        public void OnQuitButtonClicked() {
            m_StateEngine.TryRemoveTop();
        }
        public void TEST_SwitchToBattle() {
            GameSceneManager.SInstance.SwitchScene(SceneType.BATTLE);
        }
    }
}