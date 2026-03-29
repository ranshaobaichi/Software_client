using UnityEngine;
using UnityEngine.UI;

namespace UI.SampleScene
{
    public class HomeView : MonoBehaviour
    {
        [SerializeField] private Text _userNameText;
        private HomeViewModel m_viewModel;

        public void SetViewModel(HomeViewModel viewModel)
        {
            m_viewModel = viewModel;
        }

        public void Render()
        {
            _userNameText.text = m_viewModel.UserName;
        }
    }
}
