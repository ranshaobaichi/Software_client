using UnityEngine;
using System;
using System.Collections.Generic;
using UI.SampleScene;
using UI.StateEngine;

namespace UI.States {
    public class TestInitState : StateBase {
        [SerializeField]
        private TestInitView _view;

        protected override void OnEnter() {
            base.OnEnter();
            var viewModel = new TestInitViewModel();
            _view.SetViewModel(viewModel);
            _view.Render(-1);
        }

        public override void ReceiveMessage(Dictionary<Type, object> messages) {
            if (messages == null) {
                return;
            }

            if (messages.TryGetValue(typeof(TestState1), out var numberObj) && numberObj is int number) {
                _view.Render(number);
            }
        }

        public void OnEnterState1() {
            m_StateEngine.AddTop<TestState1>();
        }

        public void OnEnterState2() {
            m_StateEngine.AddTop<TestState2>();
        }
    }
}