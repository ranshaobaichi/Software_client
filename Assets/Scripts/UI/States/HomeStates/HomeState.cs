using UnityEngine;
using UI.StateEngine;
using UI.ViewModels;
using UI.Views;

namespace UI.States {
    public class HomeState : StateBase {
        [SerializeField]
        private PersonalHomepageView _personalHomepageView;
        [SerializeField]
        private HomeButtonAreaView _homeButtonAreaView;
        
        protected override void OnEnter() {
            base.OnEnter();
            
            var personalHomepageViewModel = new PersonalHomepageViewModel();
            _personalHomepageView.SetViewModel(personalHomepageViewModel);
            
            var homeButtonAreaViewModel = new HomeButtonAreaViewModel();
            _homeButtonAreaView.SetViewModel(homeButtonAreaViewModel);
        }
    }
}