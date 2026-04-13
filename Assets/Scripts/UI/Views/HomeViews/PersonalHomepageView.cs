using Constants;
using UnityEngine;
using UnityEngine.UI;
using UI.ViewModels;

namespace UI.Views {
    public class PersonalHomepageView : ViewBase<PersonalHomepageViewModel> {
        [SerializeField]
        private Image _avatarImage;


        public void Init(PersonalHomepageViewModel viewModel) {
            SetViewModel(viewModel);
        }

 
        protected override void Render() {
            if (ViewModel == null) return;

            _avatarImage.color =
                    AvatarColorConfig.GetColor(ViewModel.Color);
        }
        
        public void OnNextAvatarColorClicked() {
            ViewModel?.SwitchNextColor();
 
        }
    }
}