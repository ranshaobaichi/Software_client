using UnityEngine;
using System;
using System.Collections.Generic;
using UI.StateEngine;

namespace UI.SampleScene {
    public class TestInitState : StateBase {
        [SerializeField]
        private TestInitView _view;

        private TestInitViewModel m_viewModel;

        protected override void OnEnter() {
            base.OnEnter();
            m_viewModel = new TestInitViewModel();
            MVVMFactory.Bind(_view, m_viewModel);
        }

        public override void ReceiveMessage(Dictionary<Type, object> messages) {
            if (messages == null) {
                return;
            }

            if (messages.TryGetValue(typeof(TestState1), out var numberObj) && numberObj is int number) {
                m_viewModel.SetState1Number(number);
            }
        }
    }
}
