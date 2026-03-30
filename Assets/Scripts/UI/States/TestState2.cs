using UnityEngine;
using System;
using UI.StateEngine;
using Constants;
using Network;
using Network.Messages;

namespace UI.States {
    public class TestState2 : StateBase {
        #region Inner Classes
        [Serializable]
        private class SampleState2ClientMessage : ClientNetworkMessage {
            public string content;
        }
        [Serializable]
        private class SampleState2ServerSuccessMessage : ServerNetworkSuccessMessage {
            
        }
        [Serializable]
        private class SampleState2ServerFailMessage : ServerNetworkFailMessage {
            
        }
        #endregion
        
        public void SendSuccess() {
            var succRequest = new SampleState2ClientMessage {
                    type = (int)SampleState2MessageType.SUCCESS,
                    content = "Success"
            };
            NetworkManager.SInstance.SendShortRequestWithSuccess<SampleState2ClientMessage, SampleState2ServerSuccessMessage>(
                    NetworkConstants.DefaultPort, succRequest, OnSuccess);
        }
        
        public void SendFail() {
            var failRequest = new SampleState2ClientMessage {
                    type = (int)SampleState2MessageType.FAIL,
                    content = "Fail"
            };
            NetworkManager.SInstance.SendShortRequestWithFailure<SampleState2ClientMessage, SampleState2ServerFailMessage>(
                    NetworkConstants.DefaultPort, failRequest, OnFail);
        }

        
        public void OnReturnClicked() {
            m_StateEngine.TryRemoveTop();
        }

        private static void OnSuccess(SampleState2ServerSuccessMessage msg) {
            Debug.Log("Success");
        }

        private static void OnFail(SampleState2ServerFailMessage msg) {
            Debug.Log("Fail");
        }
    }
}