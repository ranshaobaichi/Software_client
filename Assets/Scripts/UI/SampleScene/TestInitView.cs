using UnityEngine;
using UnityEngine.UI;
using UI.StateEngine;
using UI.Views;

namespace UI.SampleScene {
    public class TestInitView : ViewBase<TestInitViewModel> {
        [SerializeField]
        private Text _state1Text;

        [SerializeField]
        private Text _dialog1Text;

        private UIStateFinder m_stateFinder;
        private UIPageFinder m_pageFinder;

        protected override void Init() {
            base.Init();
            m_stateFinder = new UIStateFinder();
            m_pageFinder = new UIPageFinder();
        }

        protected override void Render() {
            base.Render();
            _state1Text.text = ViewModel.state1Number.ToString();
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

        public void OnState1Clicked() { m_stateFinder.Current(this).AddTop<TestState1>(); }

        public void OnState2Clicked() { m_stateFinder.Current(this).AddTop<TestState2>(); }
    }
}