using Constants;
using UnityEngine;
using UnityEngine.UI;
using UI.ViewModels;

namespace UI.Views {
    public class PersonalHomepageView : ViewBase<PersonalHomepageViewModel> {
        [SerializeField]
        private Image _avatarImage;

        [SerializeField]
        private InputField _uid;

        [SerializeField]
        private InputField _id;

        public void Init(PersonalHomepageViewModel viewModel) {
            SetViewModel(viewModel);
        }

        protected override void Render() {
            if (ViewModel == null) return;

            _avatarImage.color =
                    AvatarColorConfig.GetColor(ViewModel.Color);

            if (_uid.text != ViewModel.Uid)
                _uid.text = ViewModel.Uid;
        }

        public void OnNextAvatarColorClicked() {
            ViewModel?.SwitchNextColor();
        }


        public void OnIdChanged() {
            if (_id == null || ViewModel == null) return;

            ViewModel.UpdateName(_id.text);
        }
    }
}