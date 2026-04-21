using UI.StateEngine;
using UI.ViewModels;
using UI.Views;
using UnityEngine;

namespace UI.States {
    public class MapState : StateBase {
        [SerializeField]
        private MapView _mapView;

        private MapViewModel m_viewModel;

        protected override void OnEnter() {
            base.OnEnter();

            m_viewModel = new MapViewModel();


            _mapView.SetViewModel(m_viewModel);


           // m_viewModel.RequestMap(0);
            m_viewModel.TestLocalMap();
        }

        public void OnQuitButtonClicked() {
            m_StateEngine.TryRemoveTop();
        }

        public void TEST_SwitchToShopState() {
            m_StateEngine.AddTop<ShopState>();
        }
    }
}