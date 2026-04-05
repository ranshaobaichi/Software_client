using UnityEngine;
using System;
using System.Collections.Generic;
using UI.StateEngine;

namespace UI.SampleScene {
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
    }
}