using UnityEngine;
using UnityEngine.UI;
using UI.StateEngine;

namespace UI.SampleScene {
    public class TestInitView : MonoBehaviour {
        [SerializeField]
        private Text _state1Text;

        [SerializeField]
        private Text _dialog1Text;

        private TestInitViewModel m_viewModel;
        private UIStateFinder m_stateFinder;
        private UIPageFinder m_pageFinder;

        public void SetViewModel(TestInitViewModel viewModel) {
            m_viewModel = viewModel;
            m_stateFinder = new UIStateFinder();
            m_pageFinder = new UIPageFinder();
        }

        public void Render(int number) {
            m_viewModel.Render(number);
            _state1Text.text = m_viewModel.state1Number.ToString();
        }

        public void OnDialog1Clicked() {
            var input = new TestDialogInputData {
                    initIndex = -1,
                    strVal = "test input"
            };
            m_pageFinder.Current(this).GetDialogMgr()
                    ?.OpenDialog<TestDialog, TestDialogInputData>(input,
                            result => _dialog1Text.text = result.intVal.ToString());
        }

        public void OnState1Clicked() {
            m_stateFinder.Current(this).AddTop<TestState1>();
        }

        public void OnState2Clicked() {
            m_stateFinder.Current(this).AddTop<TestState2>();
        }
    }
}