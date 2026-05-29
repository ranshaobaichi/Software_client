using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using NUnit.Framework;
using UnityEngine.TestTools;
using Constants;
using Network;
using Network.Messages;

namespace Tests.PlayMode.Network {
    [Serializable]
    public class TestShortFailureResponse : ServerNetworkFailMessage {
        public string reason;
    }

    /// <summary>
    /// PlayMode tests for short/long connection behavior in <see cref="NetworkManager"/>.
    /// </summary>
    public class NetworkManagerPlayModeTests : NetworkPlayModeTestBase {
        private static RoomInfo BuildRoomInfo(int roomId, int maximumPeople, params string[] readyUids) {
            return new RoomInfo {
                roomId = roomId,
                maximumPeople = maximumPeople,
                basicInfos = new List<PlayerData.PlayerBasicInfo> {
                    new PlayerData.PlayerBasicInfo("u1", "player-1", 1),
                    new PlayerData.PlayerBasicInfo("u2", "player-2", 2)
                },
                readyUids = new List<string>(readyUids ?? Array.Empty<string>())
            };
        }

        [UnityTest]
        public IEnumerator SendShortRequest_WithSuccess_DispatchesOnSuccess() {
            FakeServer server = StartFakeServer();

            var request = new LoginRequest {
                type = (int)LoginRequestType.LOGIN,
                uid = "test-user"
            };

            bool successCalled = false;
            LoginResponse receivedSuccess = null;

            bool errorCalled = false;

            m_networkManager.SendShortRequestWithSuccess<LoginRequest, LoginResponse>(
                server.Port,
                request,
                response => {
                    successCalled = true;
                    receivedSuccess = response;
                },
                onError: _ => { errorCalled = true; },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            // 1) Assert that the fake server received the client request correctly.
            byte[] clientRaw = null;
            yield return WaitUntil(
                () => server.TryDequeueReceived(out clientRaw),
                timeoutSeconds: 2f,
                timeoutMessage: "FakeServer did not receive client request in time.");

            var serializer = new JsonMessageSerializer();
            var receivedRequest = serializer.Deserialize(clientRaw, typeof(LoginRequest)) as LoginRequest;
            Assert.NotNull(receivedRequest);
            Assert.AreEqual(request.type, receivedRequest.type);
            Assert.AreEqual(request.uid, receivedRequest.uid);

            // 2) Send a valid short success envelope back to the client.
            var loginResponse = new LoginResponse {
                playerData = new PlayerData {
                    basicInfo = new PlayerData.PlayerBasicInfo(
                        uid: request.uid,
                        name: "tester",
                        color: 123)
                }
            };
            server.SendShortSuccess(loginResponse);

            // 3) Wait until NetworkManager pumps and calls onSuccess.
            yield return WaitUntil(
                () => successCalled,
                timeoutSeconds: 2f,
                timeoutMessage: "NetworkManager onSuccess was not called in time.");

            Assert.False(errorCalled, "onError should not be called for SERVICE_SUCCESS.");
            Assert.NotNull(receivedSuccess, "onSuccess should provide a non-null response.");
            Assert.NotNull(receivedSuccess.playerData);
            Assert.NotNull(receivedSuccess.playerData.basicInfo);
            Assert.AreEqual(request.uid, receivedSuccess.playerData.basicInfo.uid);
        }

        [UnityTest]
        public IEnumerator SendShortRequest_WithFailure_DispatchesOnFailure() {
            FakeServer server = StartFakeServer();

            var request = new LoginRequest {
                type = (int)LoginRequestType.LOGIN,
                uid = "test-user-fail"
            };

            bool failureCalled = false;
            TestShortFailureResponse receivedFailure = null;

            bool successOrErrorCalled = false;

            m_networkManager.SendShortRequestWithFailure<LoginRequest, TestShortFailureResponse>(
                server.Port,
                request,
                onFailure: failure => {
                    failureCalled = true;
                    receivedFailure = failure;
                },
                onError: _ => { successOrErrorCalled = true; },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            // Assert that server receives client request.
            yield return WaitUntil(
                () => server.TryDequeueReceived(out _),
                timeoutSeconds: 2f,
                timeoutMessage: "FakeServer did not receive client request in time.");

            // Send failure envelope.
            server.SendShortFailure(new TestShortFailureResponse { reason = "denied" });

            // Wait for onFailure callback.
            yield return WaitUntil(
                () => failureCalled,
                timeoutSeconds: 2f,
                timeoutMessage: "NetworkManager onFailure was not called in time.");

            Assert.False(successOrErrorCalled, "onError should not be called for SERVICE_FAIL.");
            Assert.NotNull(receivedFailure);
            Assert.AreEqual("denied", receivedFailure.reason);
        }

        [UnityTest]
        public IEnumerator SendShortRequest_WhenNoResponse_TriggersTimeout() {
            FakeServer server = StartFakeServer();

            var request = new LoginRequest {
                type = (int)LoginRequestType.LOGIN,
                uid = "timeout-user"
            };

            bool successCalled = false;
            bool errorCalled = false;

            // DefaultOnTimeoutAction logs: "[NetworkManager] Timeout"
            LogAssert.Expect(LogType.Error, "[NetworkManager] Timeout");

            m_networkManager.SendShortRequestWithSuccess<LoginRequest, LoginResponse>(
                server.Port,
                request,
                _ => { successCalled = true; },
                onError: _ => { errorCalled = true; },
                timeoutSeconds: 0.5f,
                blockOnConnect: true);

            // Ensure request is sent; we intentionally do not respond.
            yield return WaitUntil(
                () => server.TryDequeueReceived(out _),
                timeoutSeconds: 2f,
                timeoutMessage: "FakeServer did not receive the request in time.");

            // Wait a bit longer than timeoutSeconds so Update() can prune pending requests.
            yield return new WaitForSeconds(1f);

            Assert.False(successCalled, "onSuccess should not be called after timeout.");
            Assert.False(errorCalled, "onError should not be called when only timeout happens.");
        }

        [UnityTest]
        public IEnumerator CreateLongConnection_KnownMainType_DispatchesMainAndPush() {
            FakeServer server = StartFakeServer();

            bool mainCalled = false;
            LeaveRoomResponse mainPayload = null;

            bool pushCalled = false;
            const int pushMarker = 42;

            var mainHandlers = new Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> {
                {
                    HomeRequestType.LEAVE_ROOM,
                    NetworkManager.CreateDispatchEntry<LeaveRoomResponse>(payload => {
                        mainCalled = true;
                        mainPayload = payload;
                    })
                }
            };

            var pushHandlers = new Dictionary<int, Action> {
                { pushMarker, () => { pushCalled = true; } }
            };

            var ch = m_networkManager.CreateLongConnection(
                NetworkConstants.DefaultHost,
                server.Port,
                mainHandlers,
                pushHandlers,
                dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();

            // Wait until TCP connection is established.
            yield return WaitUntil(
                () => server.IsClientConnected,
                timeoutSeconds: 2f,
                timeoutMessage: "FakeServer did not accept client connection in time.");

            // Send long frame with known main type and probe push marker.
            server.SendObject(new LongEnvelope<LeaveRoomResponse> {
                type = (int)HomeRequestType.LEAVE_ROOM,
                data = new LeaveRoomResponse(),
                pushMessages = new List<int> { pushMarker }
            });

            yield return WaitUntil(
                () => mainCalled && pushCalled,
                timeoutSeconds: 2f,
                timeoutMessage: "Long connection did not dispatch main/push handlers in time.");

            Assert.NotNull(mainPayload, "Main handler should receive a non-null payload instance for empty response types.");
        }

        [UnityTest]
        public IEnumerator CreateLongConnection_UnknownMainType_DispatchesPushWhenEnabled() {
            FakeServer server = StartFakeServer();

            bool mainCalled = false;
            bool pushCalled = false;
            const int pushMarker = 99;

            var mainHandlers = new Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> {
                {
                    HomeRequestType.LEAVE_ROOM,
                    NetworkManager.CreateDispatchEntry<LeaveRoomResponse>(_ => { mainCalled = true; })
                }
            };

            var pushHandlers = new Dictionary<int, Action> {
                { pushMarker, () => { pushCalled = true; } }
            };

            var ch = m_networkManager.CreateLongConnection(
                NetworkConstants.DefaultHost,
                server.Port,
                mainHandlers,
                pushHandlers,
                dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();

            yield return WaitUntil(
                () => server.IsClientConnected,
                timeoutSeconds: 2f,
                timeoutMessage: "FakeServer did not accept client connection in time.");

            // Unknown main type, but probe.pushMessages contains pushMarker.
            server.SendObject(new LongEnvelope<LeaveRoomResponse> {
                type = 123456,
                data = new LeaveRoomResponse(),
                pushMessages = new List<int> { pushMarker }
            });

            yield return WaitUntil(
                () => pushCalled,
                timeoutSeconds: 2f,
                timeoutMessage: "Push handler should be dispatched when main type is unknown and dispatchPushWhenMainTypeUnknown=true.");

            Assert.False(mainCalled, "Main handler should not be called when outer type is unknown.");
        }

        [UnityTest]
        public IEnumerator CreateLongConnection_UnknownMainType_DoesNotDispatchPushWhenDisabled() {
            FakeServer server = StartFakeServer();

            bool mainCalled = false;
            bool pushCalled = false;
            const int pushMarker = 7;

            var mainHandlers = new Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> {
                {
                    HomeRequestType.LEAVE_ROOM,
                    NetworkManager.CreateDispatchEntry<LeaveRoomResponse>(_ => { mainCalled = true; })
                }
            };

            var pushHandlers = new Dictionary<int, Action> {
                { pushMarker, () => { pushCalled = true; } }
            };

            var ch = m_networkManager.CreateLongConnection(
                NetworkConstants.DefaultHost,
                server.Port,
                mainHandlers,
                pushHandlers,
                dispatchPushWhenMainTypeUnknown: false);

            ch.Connect();

            yield return WaitUntil(
                () => server.IsClientConnected,
                timeoutSeconds: 2f,
                timeoutMessage: "FakeServer did not accept client connection in time.");

            server.SendObject(new LongEnvelope<LeaveRoomResponse> {
                type = 888888,
                data = new LeaveRoomResponse(),
                pushMessages = new List<int> { pushMarker }
            });

            // Give m_networkManager.Update() a few frames; push should not fire.
            yield return null;
            yield return null;
            yield return null;

            Assert.False(mainCalled, "Main handler should not be called for unknown main type.");
            Assert.False(pushCalled, "Push handler should not be dispatched when dispatchPushWhenMainTypeUnknown=false.");
        }

        [UnityTest]
        public IEnumerator SendShortRequest_RegisterSuccess_ReceivesUid() {
            FakeServer server = StartFakeServer();

            var request = new RegisterRequest {
                type = (int)LoginRequestType.REGISTER
            };

            bool successCalled = false;
            RegisterResponse successPayload = null;

            m_networkManager.SendShortRequestWithSuccess<RegisterRequest, RegisterResponse>(
                server.Port,
                request,
                response => {
                    successCalled = true;
                    successPayload = response;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            byte[] clientRaw = null;
            yield return WaitUntil(() => server.TryDequeueReceived(out clientRaw), 2f, "Register request not received.");

            var serializer = new JsonMessageSerializer();
            var sentRequest = serializer.Deserialize(clientRaw, typeof(RegisterRequest)) as RegisterRequest;
            Assert.NotNull(sentRequest);
            Assert.AreEqual((int)LoginRequestType.REGISTER, sentRequest.type);

            server.SendShortSuccess(new RegisterResponse { uid = "uid-register-001" });

            yield return WaitUntil(() => successCalled, 2f, "Register success callback not called.");
            Assert.NotNull(successPayload);
            Assert.AreEqual("uid-register-001", successPayload.uid);
        }

        [UnityTest]
        public IEnumerator SendShortRequest_CreateRoomSuccess_ReceivesRoomInfo() {
            FakeServer server = StartFakeServer();

            var request = new CreateRoomRequest {
                type = (int)HomeRequestType.CREATE_ROOM,
                uid = "owner-001",
                maximumPeople = 4
            };

            bool successCalled = false;
            CreateRoomResponse responsePayload = null;

            m_networkManager.SendShortRequestWithSuccess<CreateRoomRequest, CreateRoomResponse>(
                server.Port,
                request,
                response => {
                    successCalled = true;
                    responsePayload = response;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            byte[] clientRaw = null;
            yield return WaitUntil(() => server.TryDequeueReceived(out clientRaw), 2f, "CreateRoom request not received.");

            var serializer = new JsonMessageSerializer();
            var sentRequest = serializer.Deserialize(clientRaw, typeof(CreateRoomRequest)) as CreateRoomRequest;
            Assert.NotNull(sentRequest);
            Assert.AreEqual(request.type, sentRequest.type);
            Assert.AreEqual(request.uid, sentRequest.uid);
            Assert.AreEqual(request.maximumPeople, sentRequest.maximumPeople);

            server.SendShortSuccess(new CreateRoomResponse {
                roomInfo = BuildRoomInfo(roomId: 1001, maximumPeople: 4, "owner-001")
            });

            yield return WaitUntil(() => successCalled, 2f, "CreateRoom success callback not called.");
            Assert.NotNull(responsePayload);
            Assert.NotNull(responsePayload.roomInfo);
            Assert.AreEqual(1001, responsePayload.roomInfo.roomId);
            Assert.AreEqual(4, responsePayload.roomInfo.maximumPeople);
            Assert.That(responsePayload.roomInfo.readyUids, Contains.Item("owner-001"));
        }

        [UnityTest]
        public IEnumerator SendShortRequest_JoinRoomSuccess_ReceivesRoomInfo() {
            FakeServer server = StartFakeServer();

            var request = new JoinRoomRequest {
                type = (int)HomeRequestType.JOIN_ROOM,
                uid = "joiner-002",
                roomId = 1001
            };

            bool successCalled = false;
            JoinRoomResponse responsePayload = null;

            m_networkManager.SendShortRequest<JoinRoomRequest, JoinRoomResponse, ServerNetworkFailMessage>(
                server.Port,
                request,
                onSuccess: response => {
                    successCalled = true;
                    responsePayload = response;
                },
                onFailure: _ => { },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            byte[] clientRaw = null;
            yield return WaitUntil(() => server.TryDequeueReceived(out clientRaw), 2f, "JoinRoom request not received.");

            var serializer = new JsonMessageSerializer();
            var sentRequest = serializer.Deserialize(clientRaw, typeof(JoinRoomRequest)) as JoinRoomRequest;
            Assert.NotNull(sentRequest);
            Assert.AreEqual(request.type, sentRequest.type);
            Assert.AreEqual(request.uid, sentRequest.uid);
            Assert.AreEqual(request.roomId, sentRequest.roomId);

            server.SendShortSuccess(new JoinRoomResponse {
                roomInfo = BuildRoomInfo(roomId: 1001, maximumPeople: 4)
            });

            yield return WaitUntil(() => successCalled, 2f, "JoinRoom success callback not called.");
            Assert.NotNull(responsePayload);
            Assert.NotNull(responsePayload.roomInfo);
            Assert.AreEqual(1001, responsePayload.roomInfo.roomId);
        }

        [UnityTest]
        public IEnumerator SendShortRequest_RefreshRoomListSuccess_ReceivesRooms() {
            FakeServer server = StartFakeServer();

            var request = new RefreshRoomRequest {
                type = (int)HomeRequestType.LIST_ROOMS
            };

            bool successCalled = false;
            RefreshRoomResponse responsePayload = null;

            m_networkManager.SendShortRequestWithSuccess<RefreshRoomRequest, RefreshRoomResponse>(
                server.Port,
                request,
                response => {
                    successCalled = true;
                    responsePayload = response;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            byte[] clientRaw = null;
            yield return WaitUntil(() => server.TryDequeueReceived(out clientRaw), 2f, "RefreshRoom request not received.");

            var serializer = new JsonMessageSerializer();
            var sentRequest = serializer.Deserialize(clientRaw, typeof(RefreshRoomRequest)) as RefreshRoomRequest;
            Assert.NotNull(sentRequest);
            Assert.AreEqual((int)HomeRequestType.LIST_ROOMS, sentRequest.type);

            server.SendShortSuccess(new RefreshRoomResponse {
                roomInfos = new List<RoomInfo> {
                    BuildRoomInfo(2001, 4, "u1"),
                    BuildRoomInfo(2002, 2)
                }
            });

            yield return WaitUntil(() => successCalled, 2f, "RefreshRoom success callback not called.");
            Assert.NotNull(responsePayload);
            Assert.NotNull(responsePayload.roomInfos);
            Assert.AreEqual(2, responsePayload.roomInfos.Count);
            Assert.AreEqual(2001, responsePayload.roomInfos[0].roomId);
            Assert.AreEqual(2002, responsePayload.roomInfos[1].roomId);
        }

        [UnityTest]
        public IEnumerator SendShortRequest_JoinRoomFailure_DispatchesFailureCallback() {
            FakeServer server = StartFakeServer();

            var request = new JoinRoomRequest {
                type = (int)HomeRequestType.JOIN_ROOM,
                uid = "joiner-fail",
                roomId = 404
            };

            bool failureCalled = false;
            bool errorCalled = false;

            m_networkManager.SendShortRequest<JoinRoomRequest, JoinRoomResponse, ServerNetworkFailMessage>(
                server.Port,
                request,
                onSuccess: _ => { },
                onFailure: _ => { failureCalled = true; },
                timeoutSeconds: 2f,
                onError: _ => { errorCalled = true; },
                blockOnConnect: true);

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "JoinRoom failure request not received.");

            server.SendShortFailure(new ServerNetworkFailMessage());

            yield return WaitUntil(() => failureCalled, 2f, "JoinRoom failure callback not called.");
            Assert.False(errorCalled, "onError should not be called for SERVICE_FAIL response.");
        }

        [UnityTest]
        public IEnumerator SendShortRequest_WhenServiceReturnsErrorCode_DispatchesOnErrorWithServerMessage() {
            FakeServer server = StartFakeServer();

            var request = new JoinRoomRequest {
                type = (int)HomeRequestType.JOIN_ROOM,
                uid = "joiner-error",
                roomId = 5001
            };

            bool successCalled = false;
            bool failureCalled = false;
            bool errorCalled = false;
            NetworkErrorMessage receivedError = null;

            m_networkManager.SendShortRequest<JoinRoomRequest, JoinRoomResponse, ServerNetworkFailMessage>(
                server.Port,
                request,
                onSuccess: _ => { successCalled = true; },
                onFailure: _ => { failureCalled = true; },
                timeoutSeconds: 2f,
                onError: err => {
                    errorCalled = true;
                    receivedError = err;
                },
                blockOnConnect: true);

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "JoinRoom request not received.");

            // Non-SERVICE_SUCCESS / Non-SERVICE_FAIL code should route to onError.
            server.SendObject(new ShortEnvelope<ServerNetworkSuccessMessage> {
                code = (int)ServerCode.ERROR,
                data = new ServerNetworkSuccessMessage(),
                message = "room server internal error"
            });

            yield return WaitUntil(() => errorCalled, 2f, "onError callback not called for ERROR code.");

            Assert.False(successCalled, "onSuccess should not be called when code is ERROR.");
            Assert.False(failureCalled, "onFailure should not be called when code is ERROR.");
            Assert.NotNull(receivedError);
            Assert.AreEqual(ServerCode.ERROR, receivedError.code);
            Assert.AreEqual("room server internal error", receivedError.message);
        }

        [UnityTest]
        public IEnumerator SendShortRequest_WhenServiceReturnsTimeoutCode_DispatchesOnError() {
            FakeServer server = StartFakeServer();

            var request = new RefreshRoomRequest {
                type = (int)HomeRequestType.LIST_ROOMS
            };

            bool successCalled = false;
            bool failureCalled = false;
            bool errorCalled = false;
            NetworkErrorMessage receivedError = null;

            m_networkManager.SendShortRequest<RefreshRoomRequest, RefreshRoomResponse, ServerNetworkFailMessage>(
                server.Port,
                request,
                onSuccess: _ => { successCalled = true; },
                onFailure: _ => { failureCalled = true; },
                timeoutSeconds: 2f,
                onError: err => {
                    errorCalled = true;
                    receivedError = err;
                },
                blockOnConnect: true);

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "RefreshRoom request not received.");

            server.SendObject(new ShortEnvelope<ServerNetworkSuccessMessage> {
                code = (int)ServerCode.TIME_OUT,
                data = new ServerNetworkSuccessMessage(),
                message = "business timeout"
            });

            yield return WaitUntil(() => errorCalled, 2f, "onError callback not called for TIME_OUT code.");

            Assert.False(successCalled, "onSuccess should not be called when code is TIME_OUT.");
            Assert.False(failureCalled, "onFailure should not be called when code is TIME_OUT.");
            Assert.NotNull(receivedError);
            Assert.AreEqual(ServerCode.TIME_OUT, receivedError.code);
            Assert.AreEqual("business timeout", receivedError.message);
        }

        [UnityTest]
        public IEnumerator SendShortRequest_WhenServiceReturnsUnknownCode_UsesFallbackMessage() {
            FakeServer server = StartFakeServer();

            var request = new LoginRequest {
                type = (int)LoginRequestType.LOGIN,
                uid = "unknown-code-user"
            };

            bool errorCalled = false;
            NetworkErrorMessage receivedError = null;

            m_networkManager.SendShortRequestWithSuccess<LoginRequest, LoginResponse>(
                server.Port,
                request,
                _ => { },
                onError: err => {
                    errorCalled = true;
                    receivedError = err;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "Login request not received.");

            const int codeNeitherSuccessNorFail = 999996;
            server.SendObject(new ShortEnvelope<ServerNetworkSuccessMessage> {
                code = codeNeitherSuccessNorFail,
                data = new ServerNetworkSuccessMessage(),
                message = null
            });

            yield return WaitUntil(() => errorCalled, 2f, "onError callback not called for unknown code.");

            Assert.NotNull(receivedError);
            Assert.AreEqual((ServerCode)codeNeitherSuccessNorFail, receivedError.code);
            Assert.AreEqual("Unknown error", receivedError.message);
        }

        [UnityTest]
        public IEnumerator CreateLongConnection_BroadcastRoomStatus_DispatchesBusinessPayload() {
            FakeServer server = StartFakeServer();

            bool broadcastCalled = false;
            BroadcastRoomStatusResponse received = null;

            var mainHandlers = new Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> {
                {
                    HomeRequestType.BROADCAST,
                    NetworkManager.CreateDispatchEntry<BroadcastRoomStatusResponse>(payload => {
                        broadcastCalled = true;
                        received = payload;
                    })
                }
            };

            var ch = m_networkManager.CreateLongConnection(
                NetworkConstants.DefaultHost,
                server.Port,
                mainHandlers,
                new Dictionary<int, Action>(),
                dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();
            yield return WaitUntil(() => server.IsClientConnected, 2f, "Long connection not established.");

            server.SendObject(new LongEnvelope<BroadcastRoomStatusResponse> {
                type = (int)HomeRequestType.BROADCAST,
                data = new BroadcastRoomStatusResponse {
                    roomInfo = BuildRoomInfo(3001, 4, "u1", "u2")
                },
                pushMessages = new List<int>()
            });

            yield return WaitUntil(() => broadcastCalled, 2f, "Broadcast handler not called.");
            Assert.NotNull(received);
            Assert.NotNull(received.roomInfo);
            Assert.AreEqual(3001, received.roomInfo.roomId);
            Assert.That(received.roomInfo.readyUids, Contains.Item("u2"));
        }

        [UnityTest]
        public IEnumerator CreateLongConnection_SendSetReadyStatusRequest_SendsExpectedPayload() {
            FakeServer server = StartFakeServer();

            var ch = m_networkManager.CreateLongConnection(
                NetworkConstants.DefaultHost,
                server.Port,
                new Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> {
                    {
                        HomeRequestType.LEAVE_ROOM,
                        NetworkManager.CreateDispatchEntry<LeaveRoomResponse>(_ => { })
                    }
                },
                new Dictionary<int, Action>(),
                dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();
            yield return WaitUntil(() => server.IsClientConnected, 2f, "Long connection not established.");

            var readyReq = new SetReadyStatusRequest {
                type = (int)HomeRequestType.SET_READY,
                uid = "ready-user",
                ready = true
            };
            ch.Send(readyReq);

            byte[] raw = null;
            yield return WaitUntil(() => server.TryDequeueReceived(out raw), 2f, "SetReadyStatus request not received.");

            var serializer = new JsonMessageSerializer();
            var decoded = serializer.Deserialize(raw, typeof(SetReadyStatusRequest)) as SetReadyStatusRequest;
            Assert.NotNull(decoded);
            Assert.AreEqual((int)HomeRequestType.SET_READY, decoded.type);
            Assert.AreEqual("ready-user", decoded.uid);
            Assert.True(decoded.ready);
        }

        [UnityTest]
        public IEnumerator CreateLongConnection_SendLeaveRoomRequest_SendsExpectedPayload() {
            FakeServer server = StartFakeServer();

            var ch = m_networkManager.CreateLongConnection(
                NetworkConstants.DefaultHost,
                server.Port,
                new Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> {
                    {
                        HomeRequestType.LEAVE_ROOM,
                        NetworkManager.CreateDispatchEntry<LeaveRoomResponse>(_ => { })
                    }
                },
                new Dictionary<int, Action>(),
                dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();
            yield return WaitUntil(() => server.IsClientConnected, 2f, "Long connection not established.");

            var leaveReq = new LeaveRoomRequest {
                type = (int)HomeRequestType.LEAVE_ROOM,
                uid = "leave-user"
            };
            ch.Send(leaveReq);

            byte[] raw = null;
            yield return WaitUntil(() => server.TryDequeueReceived(out raw), 2f, "LeaveRoom request not received.");

            var serializer = new JsonMessageSerializer();
            var decoded = serializer.Deserialize(raw, typeof(LeaveRoomRequest)) as LeaveRoomRequest;
            Assert.NotNull(decoded);
            Assert.AreEqual((int)HomeRequestType.LEAVE_ROOM, decoded.type);
            Assert.AreEqual("leave-user", decoded.uid);
        }

        #region Protocol boundary cases

        [UnityTest]
        public IEnumerator SendShortRequest_WhenResponseFrameEmpty_DispatchesDeserializeError() {
            FakeServer server = StartFakeServer();

            bool successCalled = false;
            bool errorCalled = false;
            NetworkErrorMessage receivedError = null;

            m_networkManager.SendShortRequestWithSuccess<LoginRequest, LoginResponse>(
                server.Port,
                new LoginRequest { type = (int)LoginRequestType.LOGIN, uid = "edge-empty" },
                _ => { successCalled = true; },
                onError: err => {
                    errorCalled = true;
                    receivedError = err;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "Request not received.");

            server.SendFramedPayload(Array.Empty<byte>());

            yield return WaitUntil(() => errorCalled, 2f, "onError not called for empty framed response.");

            Assert.False(successCalled);
            Assert.NotNull(receivedError);
            Assert.AreEqual(ServerCode.DESERIALIZE_ERROR, receivedError.code);
            Assert.AreEqual("Failed to deserialize server envelope", receivedError.message);
        }

        /// <summary>
        /// Documents current wiring: garbage UTF-8 makes <see cref="JsonMessageSerializer.Deserialize"/> throw while probing
        /// <see cref="ShortEnvelope{ServerNetworkSuccessMessage}"/>; the exception escapes the short-request handler into
        /// <see cref="TcpConnectionChannel.DispatchPendingMessages"/>, which catches it and logs a Warning only—
        /// <c>onError</c> is not invoked (unlike the <c>envelopeProbe == null</c> branch).
        /// </summary>
        [UnityTest]
        public IEnumerator SendShortRequest_WhenResponseNotJson_ChannelLogsWarning_OnErrorNotInvoked() {
            FakeServer server = StartFakeServer();

            bool errorCalled = false;
            NetworkErrorMessage receivedError = null;

            m_networkManager.SendShortRequestWithSuccess<LoginRequest, LoginResponse>(
                server.Port,
                new LoginRequest { type = (int)LoginRequestType.LOGIN, uid = "edge-badjson" },
                _ => { },
                onError: err => {
                    errorCalled = true;
                    receivedError = err;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "Request not received.");

            LogAssert.Expect(LogType.Warning, new Regex(@"\[TcpConnectionChannel\] Deserialize/Dispatch failed:"));
            server.SendFramedUtf8("not-json-at-all");

            yield return null;
            yield return null;

            Assert.False(errorCalled, "Probe deserialize throws; channel swallows callback exception so onError is skipped.");
            Assert.Null(receivedError);
        }

        [UnityTest]
        public IEnumerator SendShortRequest_WhenSuccessPayloadNestedShapeInvalid_JsonUtilityMayStillInvokeOnSuccess() {
            FakeServer server = StartFakeServer();

            bool successCalled = false;
            bool errorCalled = false;
            NetworkErrorMessage receivedError = null;
            LoginResponse receivedPayload = null;

            m_networkManager.SendShortRequestWithSuccess<LoginRequest, LoginResponse>(
                server.Port,
                new LoginRequest { type = (int)LoginRequestType.LOGIN, uid = "edge-baddata" },
                r => {
                    successCalled = true;
                    receivedPayload = r;
                },
                onError: err => {
                    errorCalled = true;
                    receivedError = err;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "Request not received.");

            server.SendFramedUtf8("{\"code\":1,\"data\":{\"playerData\":{\"basicInfo\":1}},\"message\":\"\"}");

            yield return WaitUntil(() => successCalled, 2f, "onSuccess not called after malformed nested success payload.");

            Assert.False(errorCalled);
            Assert.Null(receivedError);
            Assert.NotNull(receivedPayload);
        }

        [UnityTest]
        public IEnumerator SendShortRequest_WhenCodeCombinesSuccessAndFailBits_DispatchesSuccessPath() {
            FakeServer server = StartFakeServer();

            bool successCalled = false;
            LoginResponse received = null;

            m_networkManager.SendShortRequestWithSuccess<LoginRequest, LoginResponse>(
                server.Port,
                new LoginRequest { type = (int)LoginRequestType.LOGIN, uid = "edge-bitmask" },
                r => {
                    successCalled = true;
                    received = r;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "Request not received.");

            server.SendObject(new ShortEnvelope<LoginResponse> {
                code = (int)(ServerCode.SUCCESS | ServerCode.FAIL),
                data = new LoginResponse {
                    playerData = new PlayerData {
                        basicInfo = new PlayerData.PlayerBasicInfo("edge-bitmask", "n", 1)
                    }
                },
                message = ""
            });

            yield return WaitUntil(() => successCalled, 2f, "Success path should win when both SUCCESS and FAIL bits are set.");

            Assert.NotNull(received);
            Assert.AreEqual("edge-bitmask", received.playerData.basicInfo.uid);
        }

        [UnityTest]
        public IEnumerator SendShortRequest_WhenConnectFails_DispatchesConnectionError() {
            int closedPort = TakeReleasedEphemeralLoopbackPort();

            bool successCalled = false;
            bool errorCalled = false;
            NetworkErrorMessage receivedError = null;

            LogAssert.Expect(LogType.Error, new Regex(@"\[TcpConnectionChannel\] Connect failed:"));
            m_networkManager.SendShortRequestWithSuccess<LoginRequest, LoginResponse>(
                closedPort,
                new LoginRequest { type = (int)LoginRequestType.LOGIN, uid = "no-server" },
                _ => { successCalled = true; },
                onError: err => {
                    errorCalled = true;
                    receivedError = err;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return null;

            Assert.False(successCalled);
            Assert.True(errorCalled, "onError should fire when TCP connect is refused.");
            Assert.NotNull(receivedError);
            Assert.AreEqual(ServerCode.CONNECTION_ERROR, receivedError.code);
            Assert.AreEqual("Failed to connect", receivedError.message);
        }

        [UnityTest]
        public IEnumerator SendShortRequest_WhenSuccessOmitsDataField_InvokesOnSuccessWithDefaultLoginResponse() {
            FakeServer server = StartFakeServer();

            bool successCalled = false;
            LoginResponse received = null;

            m_networkManager.SendShortRequestWithSuccess<LoginRequest, LoginResponse>(
                server.Port,
                new LoginRequest { type = (int)LoginRequestType.LOGIN, uid = "edge-nodata" },
                r => {
                    successCalled = true;
                    received = r;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "Request not received.");

            server.SendFramedUtf8("{\"code\":1,\"message\":\"ok\"}");

            yield return WaitUntil(() => successCalled, 2f, "onSuccess not called for SERVICE_SUCCESS without data field.");

            Assert.NotNull(received);
            Assert.IsNull(received.playerData);
        }

        [UnityTest]
        public IEnumerator SendShortRequest_RefreshRoomSuccess_EmptyRoomInfos_IsDelivered() {
            FakeServer server = StartFakeServer();

            bool successCalled = false;
            RefreshRoomResponse payload = null;

            m_networkManager.SendShortRequestWithSuccess<RefreshRoomRequest, RefreshRoomResponse>(
                server.Port,
                new RefreshRoomRequest { type = (int)HomeRequestType.LIST_ROOMS },
                r => {
                    successCalled = true;
                    payload = r;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => server.TryDequeueReceived(out _), 2f, "Refresh request not received.");

            server.SendShortSuccess(new RefreshRoomResponse { roomInfos = new List<RoomInfo>() });

            yield return WaitUntil(() => successCalled, 2f, "Success callback not called.");

            Assert.NotNull(payload);
            Assert.NotNull(payload.roomInfos);
            Assert.AreEqual(0, payload.roomInfos.Count);
        }

        [UnityTest]
        public IEnumerator CreateLongConnection_KnownMainType_NullPushMessages_DispatchesMainOnly() {
            FakeServer server = StartFakeServer();

            bool mainCalled = false;
            bool pushCalled = false;

            var ch = m_networkManager.CreateLongConnection(
                NetworkConstants.DefaultHost,
                server.Port,
                new Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> {
                    {
                        HomeRequestType.LEAVE_ROOM,
                        NetworkManager.CreateDispatchEntry<LeaveRoomResponse>(_ => { mainCalled = true; })
                    }
                },
                new Dictionary<int, Action> {
                    { 1, () => { pushCalled = true; } }
                },
                dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();
            yield return WaitUntil(() => server.IsClientConnected, 2f, "Long connection not established.");

            server.SendObject(new LongEnvelope<LeaveRoomResponse> {
                type = (int)HomeRequestType.LEAVE_ROOM,
                data = new LeaveRoomResponse(),
                pushMessages = null
            });

            yield return WaitUntil(() => mainCalled, 2f, "Main handler not called.");
            Assert.False(pushCalled, "Null pushMessages must not dispatch push handlers.");
        }

        [UnityTest]
        public IEnumerator CreateLongConnection_KnownMainType_UnknownPushMarker_WarnsAndDispatchesKnownMarkers() {
            FakeServer server = StartFakeServer();

            bool mainCalled = false;
            bool knownPushCalled = false;

            LogAssert.Expect(LogType.Warning, "[LongConnectionInboundDispatcher] Unknown push marker 777");

            var ch = m_networkManager.CreateLongConnection(
                NetworkConstants.DefaultHost,
                server.Port,
                new Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> {
                    {
                        HomeRequestType.LEAVE_ROOM,
                        NetworkManager.CreateDispatchEntry<LeaveRoomResponse>(_ => { mainCalled = true; })
                    }
                },
                new Dictionary<int, Action> {
                    { 42, () => { knownPushCalled = true; } }
                },
                dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();
            yield return WaitUntil(() => server.IsClientConnected, 2f, "Long connection not established.");

            server.SendObject(new LongEnvelope<LeaveRoomResponse> {
                type = (int)HomeRequestType.LEAVE_ROOM,
                data = new LeaveRoomResponse(),
                pushMessages = new List<int> { 777, 42 }
            });

            yield return WaitUntil(() => mainCalled && knownPushCalled, 2f, "Main or known push not dispatched.");

            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator CreateLongConnection_WhenFrameIsInvalidJson_MainHandlerNotInvoked() {
            FakeServer server = StartFakeServer();

            bool mainCalled = false;

            var ch = m_networkManager.CreateLongConnection(
                NetworkConstants.DefaultHost,
                server.Port,
                new Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> {
                    {
                        HomeRequestType.LEAVE_ROOM,
                        NetworkManager.CreateDispatchEntry<LeaveRoomResponse>(_ => { mainCalled = true; })
                    }
                },
                new Dictionary<int, Action>(),
                dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();
            yield return WaitUntil(() => server.IsClientConnected, 2f, "Long connection not established.");

            server.SendFramedUtf8("{");

            yield return null;
            yield return null;
            yield return null;

            Assert.False(mainCalled);
        }

        #endregion
    }
}
