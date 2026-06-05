using System;
using System.Collections;
using Automation.Config;
using Automation.Util;
using Constants;
using Network;
using Network.Messages;

namespace Automation.Sessions {
    public class AuthSession {
        readonly NetworkEndpointConfig m_config;

        public AuthSession(NetworkEndpointConfig config) {
            m_config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public IEnumerator Register(Action<string> onUid = null) {
            bool done = false;
            string uid = null;
            NetworkErrorMessage error = null;

            NetworkManager.SInstance.SendShortRequestWithSuccess<RegisterRequest, RegisterResponse>(
                    m_config.LoginPort,
                    new RegisterRequest { type = (int)LoginRequestType.REGISTER },
                    response => {
                        uid = response?.uid;
                        done = true;
                    },
                    m_config.Host,
                    onError: err => {
                        error = err;
                        done = true;
                    },
                    timeoutSeconds: m_config.DefaultTimeoutSeconds,
                    blockOnConnect: true);

            yield return AutomationAwaiter.WaitUntil(() => done, m_config.DefaultTimeoutSeconds,
                    "Register request timed out");

            if (error != null)
                throw new AutomationException(error.message ?? "Register failed", error);

            if (string.IsNullOrEmpty(uid))
                throw new AutomationException("Register returned empty uid");

            onUid?.Invoke(uid);
        }

        public IEnumerator Login(string uid) {
            if (string.IsNullOrEmpty(uid))
                throw new AutomationException("Login uid is required");

            bool done = false;
            LoginResponse loginResponse = null;
            NetworkErrorMessage error = null;
            bool failed = false;

            NetworkManager.SInstance.SendShortRequest<LoginRequest, LoginResponse, ServerNetworkFailMessage>(
                    m_config.LoginPort,
                    new LoginRequest {
                            type = (int)LoginRequestType.LOGIN,
                            uid = uid
                    },
                    response => {
                        loginResponse = response;
                        done = true;
                    },
                    _ => {
                        failed = true;
                        done = true;
                    },
                    m_config.Host,
                    timeoutSeconds: m_config.DefaultTimeoutSeconds,
                    onError: err => {
                        error = err;
                        done = true;
                    },
                    blockOnConnect: true);

            yield return AutomationAwaiter.WaitUntil(() => done, m_config.DefaultTimeoutSeconds,
                    "Login request timed out");

            if (error != null)
                throw new AutomationException(error.message ?? "Login failed", error);
            if (failed)
                throw new AutomationException("Login service failure");
            if (loginResponse?.playerData == null)
                throw new AutomationException("Login returned empty playerData");

            PlayerData.Init(loginResponse.playerData);
        }

        public IEnumerator RegisterAndLogin(Action<string> onUid = null) {
            string uid = null;
            yield return Register(value => uid = value);
            onUid?.Invoke(uid);
            yield return Login(uid);
        }

        public IEnumerator Logout() {
            if (!PlayerData.IsInit())
                yield break;

            string uid = PlayerData.SInstance.basicInfo.uid;
            bool done = false;
            NetworkErrorMessage error = null;

            NetworkManager.SInstance.SendShortRequestWithSuccess<LogoutRequest, ServerNetworkSuccessMessage>(
                    m_config.LoginPort,
                    new LogoutRequest {
                            type = (int)LoginRequestType.LOGOUT,
                            uid = uid
                    },
                    _ => { done = true; },
                    m_config.Host,
                    onError: err => {
                        error = err;
                        done = true;
                    },
                    timeoutSeconds: m_config.DefaultTimeoutSeconds,
                    blockOnConnect: true);

            yield return AutomationAwaiter.WaitUntil(() => done, m_config.DefaultTimeoutSeconds,
                    "Logout request timed out");

            if (error != null)
                throw new AutomationException(error.message ?? "Logout failed", error);

            PlayerData.Clear();
        }
    }
}
