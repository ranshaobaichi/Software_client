using System;
using Constants;
using Network;
using Network.Messages;
using UnityEngine;
using UnityEngine.UI;
using UI.Dialog;

namespace UI.Dialogs {
    public class RegisterDialog : DialogBaseNoInput {
        [SerializeField]
        private InputField _uidInputField;
        
        public void OnRegisterClicked() {
            var request = new RegisterRequest {
                    type = (int)LoginRequestType.REGISTER,
            };
            NetworkManager.SInstance.SendShortRequestWithSuccess<RegisterRequest, RegisterResponse>(
                    NetworkConstants.LoginPort, request, _OnRegisterResponse);
        }
        
        private void _OnRegisterResponse(RegisterResponse response) {
            _uidInputField.text = response.uid;
        }
    }
}