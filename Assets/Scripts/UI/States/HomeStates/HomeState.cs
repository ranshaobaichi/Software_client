using Constants;
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

            var uid = LocalizeData.SInstance.playerInfo.lastLoginUid;
            var color = (AvatarColorID)PlayerData.SInstance.basicInfo.color;

            var personalHomepageViewModel =
                    new PersonalHomepageViewModel(uid, color);

            _personalHomepageView.SetViewModel(personalHomepageViewModel);

            var homeButtonAreaViewModel = new HomeButtonAreaViewModel();
            _homeButtonAreaView.SetViewModel(homeButtonAreaViewModel);
        }
    }
}