using UnityEngine;
using UnityEngine.UI;
using UI.ViewModels;

namespace UI.Views {
    public class PersonalHomepageView : MonoBehaviour {
        [SerializeField]
        private Text _uidText;
        [SerializeField]
        private Text _nameText;
        [SerializeField]
        private Image _avatarImage;
        
        private PersonalHomepageViewModel m_viewModel;
        public void SetViewModel(PersonalHomepageViewModel viewModel) {
            m_viewModel = viewModel;
        }
    }
}