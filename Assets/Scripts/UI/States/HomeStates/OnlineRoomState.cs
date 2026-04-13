using System;
using System.Collections.Generic;
using UI.Pages;
using UI.StateEngine;

namespace UI.States {
    public class OnlineRoomState : StateBase {
        private int m_roomID;

        public override void ReceiveMessage(Dictionary<Type, object> messages) {
            if (messages == null) {
                return;
            }

            if (messages.TryGetValue(typeof(OnlineLobbyState), out var numberObj) && numberObj is int roomID) {
                m_roomID = roomID;
            }
        }

        public void OnQuitButtonClicked() {
            m_StateEngine.TryRemoveTop();
        }

        public void TEST_SwitchToBattlePage() {
            var pageFinder = new UIPageFinder();
            pageFinder.Current(this).SwitchTo<BattlePage>();
        }
    }
}