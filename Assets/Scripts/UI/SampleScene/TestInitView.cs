using UnityEngine;
using UnityEngine.UI;

namespace UI.SampleScene {
    public class TestInitView : MonoBehaviour {
        [SerializeField]
        private Text _state1Text;

        private TestInitViewModel m_viewModel;

        public void SetViewModel(TestInitViewModel viewModel) {
            m_viewModel = viewModel;
        }

        public void Render(int number) {
            m_viewModel.Render(number);
            _state1Text.text = m_viewModel.state1Number.ToString();
        }
    }
}