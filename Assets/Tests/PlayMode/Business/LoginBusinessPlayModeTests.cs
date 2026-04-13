using System.Collections;
using System.Text.RegularExpressions;
using Constants;
using Network.Messages;
using Tests.PlayMode.Network;
using NUnit.Framework;
using UI.ViewModels;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Business {
    /// <summary>
    /// Business-level PlayMode tests for login / registration flows that mirror
    /// <see cref="LoginViewModel"/> and register dialog behavior (same ports and messages).
    /// </summary>
    public class LoginBusinessPlayModeTests : BusinessPlayModeTestBase {
        [UnityTest]
        public IEnumerator Login_EmptyUid_DoesNotHitNetwork() {
            FakeServer server = StartFakeServer(NetworkConstants.LoginPort);

            var vm = new LoginViewModel();
            vm.SaveUid("");
            LogAssert.Expect(LogType.Error, new Regex(@"Coroutine couldn't be started because"));
            vm.SendLoginMessage();

            yield return new WaitForSeconds(0.25f);

            Assert.False(server.TryDequeueReceived(out _), "Empty uid should skip SendShortRequest.");

            vm.SaveUid(null);
            LogAssert.Expect(LogType.Error, new Regex(@"Coroutine couldn't be started because"));
            vm.SendLoginMessage();

            yield return new WaitForSeconds(0.25f);

            Assert.False(server.TryDequeueReceived(out _), "Null uid should skip SendShortRequest.");
        }

        [UnityTest]
        public IEnumerator Login_Success_InitializesPlayerDataAndPersistsLastUid() {
            FakeServer server = StartFakeServer(NetworkConstants.LoginPort);

            const string uid = "biz-login-ok";
            var vm = new LoginViewModel();
            vm.SaveUid(uid);
            vm.SendLoginMessage();

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "Login request not received.");

            server.SendShortSuccess(new LoginResponse {
                playerData = new PlayerData {
                    basicInfo = new PlayerData.PlayerBasicInfo(uid, "Tester", (AvatarColorID)7)
                }
            });

            yield return WaitUntil(() => PlayerData.IsInit(), 2f, "PlayerData should initialize after login success.");

            Assert.AreEqual(uid, PlayerData.SInstance.basicInfo.uid);
            Assert.AreEqual("Tester", PlayerData.SInstance.basicInfo.name);
            LocalizeData.InitIfNot();
            Assert.AreEqual(uid, LocalizeData.SInstance.playerInfo.lastLoginUid);
        }

        [UnityTest]
        public IEnumerator Login_ServiceFail_KeepsPlayerDataUninitialized() {
            FakeServer server = StartFakeServer(NetworkConstants.LoginPort);

            var vm = new LoginViewModel();
            vm.SaveUid("biz-login-fail");
            vm.SendLoginMessage();

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "Login request not received.");

            LogAssert.Expect(LogType.Error, new Regex(@"Coroutine couldn't be started because"));
            server.SendShortFailure(new ServerNetworkFailMessage());

            yield return new WaitForSeconds(0.5f);

            Assert.False(PlayerData.IsInit(), "SERVICE_FAIL must not run PlayerData.Init.");
        }

        [UnityTest]
        public IEnumerator Login_ServiceErrorCode_KeepsPlayerDataUninitialized() {
            FakeServer server = StartFakeServer(NetworkConstants.LoginPort);

            var vm = new LoginViewModel();
            vm.SaveUid("biz-login-err");
            vm.SendLoginMessage();

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "Login request not received.");

            LogAssert.Expect(LogType.Error, new Regex(@"Coroutine couldn't be started because"));
            server.SendObject(new ShortEnvelope<ServerNetworkSuccessMessage> {
                code = (int)ServerCode.ERROR,
                data = new ServerNetworkSuccessMessage(),
                message = "login unavailable"
            });

            yield return new WaitForSeconds(0.5f);

            Assert.False(PlayerData.IsInit(), "ERROR envelope must not initialize player.");
        }

        [UnityTest]
        public IEnumerator Login_SameUidSequentialSecondLogin_OverwritesPlayerProfile() {
            FakeServer server = StartFakeServer(NetworkConstants.LoginPort);

            var vm = new LoginViewModel();
            vm.SaveUid("same-uid-user");

            vm.SendLoginMessage();
            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "First login request missing.");
            server.SendShortSuccess(new LoginResponse {
                playerData = new PlayerData {
                    basicInfo = new PlayerData.PlayerBasicInfo("same-uid-user", "First", (AvatarColorID)1)
                }
            });
            yield return WaitUntil(() => PlayerData.IsInit(), 2f, "First login did not init.");

            vm.SendLoginMessage();
            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "Second login request missing.");
            server.SendShortSuccess(new LoginResponse {
                playerData = new PlayerData {
                    basicInfo = new PlayerData.PlayerBasicInfo("same-uid-user", "SecondSession", (AvatarColorID)2)
                }
            });
            yield return WaitUntil(
                () => PlayerData.SInstance.basicInfo.name == "SecondSession",
                2f,
                "Second login did not replace profile.");

            Assert.AreEqual(2, PlayerData.SInstance.basicInfo.color);
        }

        [UnityTest]
        public IEnumerator Register_Success_DeliversUid() {
            FakeServer server = StartFakeServer(NetworkConstants.LoginPort);

            bool ok = false;
            RegisterResponse received = null;

            m_networkManager.SendShortRequestWithSuccess<RegisterRequest, RegisterResponse>(
                NetworkConstants.LoginPort,
                new RegisterRequest { type = (int)LoginRequestType.REGISTER },
                r => {
                    ok = true;
                    received = r;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "Register request missing.");

            server.SendShortSuccess(new RegisterResponse { uid = "reg-biz-001" });

            yield return WaitUntil(() => ok, 2f, "Register callback missing.");

            Assert.NotNull(received);
            Assert.AreEqual("reg-biz-001", received.uid);
        }
    }
}
