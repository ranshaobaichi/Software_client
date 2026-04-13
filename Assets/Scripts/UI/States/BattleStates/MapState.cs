using UI.StateEngine;

namespace UI.States {
    public class MapState : StateBase {
        public void OnQuitButtonClicked() {
            m_StateEngine.TryRemoveTop();
        }

        public void TEST_SwitchToShopState() {
            m_StateEngine.AddTop<ShopState>();
        }
    }
}