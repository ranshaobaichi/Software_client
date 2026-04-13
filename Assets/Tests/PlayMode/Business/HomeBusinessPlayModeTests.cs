using System.Collections;
using System.Collections.Generic;
using Constants;
using Network;
using Network.Messages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Business {
    /// <summary>
    /// Business-level PlayMode tests for lobby/home short-request flows (refresh, create, join)
    /// mirroring dialog behavior against <see cref="NetworkConstants.HomePort"/>.
    /// </summary>
    public class HomeBusinessPlayModeTests : BusinessPlayModeTestBase {
        static RoomInfo BuildRoom(int roomId, int maximumPeople, params string[] readyUids) {
            return new RoomInfo {
                roomId = roomId,
                maximumPeople = maximumPeople,
                basicInfos = new List<PlayerData.PlayerBasicInfo> {
                    new PlayerData.PlayerBasicInfo("u1", "p1", (AvatarColorID)1),
                    new PlayerData.PlayerBasicInfo("u2", "p2", (AvatarColorID)2)
                },
                readyUids = new List<string>(readyUids ?? System.Array.Empty<string>())
            };
        }

        [UnityTest]
        public IEnumerator Lobby_RefreshRoomList_EmptyList_Boundary() {
            StartHomeFakeServer();

            bool done = false;
            RefreshRoomResponse payload = null;

            m_networkManager.SendShortRequestWithSuccess<RefreshRoomRequest, RefreshRoomResponse>(
                NetworkConstants.HomePort,
                new RefreshRoomRequest { type = (int)HomeRequestType.LIST_ROOMS },
                r => {
                    done = true;
                    payload = r;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => m_homeFakeServer.TryDequeueReceived(out _), 2f, "LIST_ROOMS not received.");

            m_homeFakeServer.SendShortSuccess(new RefreshRoomResponse { roomInfos = new List<RoomInfo>() });

            yield return WaitUntil(() => done, 2f, "Refresh callback missing.");

            Assert.NotNull(payload.roomInfos);
            Assert.AreEqual(0, payload.roomInfos.Count);
        }

        [UnityTest]
        public IEnumerator Lobby_RefreshRoomList_MultipleRooms_NormalFlow() {
            StartHomeFakeServer();

            bool done = false;
            RefreshRoomResponse payload = null;

            m_networkManager.SendShortRequestWithSuccess<RefreshRoomRequest, RefreshRoomResponse>(
                NetworkConstants.HomePort,
                new RefreshRoomRequest { type = (int)HomeRequestType.LIST_ROOMS },
                r => {
                    done = true;
                    payload = r;
                },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => m_homeFakeServer.TryDequeueReceived(out _), 2f, "LIST_ROOMS not received.");

            m_homeFakeServer.SendShortSuccess(new RefreshRoomResponse {
                roomInfos = new List<RoomInfo> {
                    BuildRoom(101, 4, "a"),
                    BuildRoom(202, 2)
                }
            });

            yield return WaitUntil(() => done, 2f, "Refresh callback missing.");

            Assert.AreEqual(2, payload.roomInfos.Count);
            Assert.AreEqual(101, payload.roomInfos[0].roomId);
            Assert.AreEqual(202, payload.roomInfos[1].roomId);
        }

        [UnityTest]
        public IEnumerator Lobby_CreateRoom_Success_NormalFlow() {
            StartHomeFakeServer();
            SeedLoggedInPlayer("owner-biz");

            bool done = false;
            CreateRoomResponse responsePayload = null;

            m_networkManager.SendShortRequest<CreateRoomRequest, CreateRoomResponse, ServerNetworkFailMessage>(
                NetworkConstants.HomePort,
                new CreateRoomRequest {
                    type = (int)HomeRequestType.CREATE_ROOM,
                    uid = "owner-biz",
                    maximumPeople = 4
                },
                r => {
                    done = true;
                    responsePayload = r;
                },
                _ => { },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            byte[] createRaw = null;
            yield return WaitUntil(() => m_homeFakeServer.TryDequeueReceived(out createRaw), 2f, "CreateRoom not received.");
            var serializer = new JsonMessageSerializer();
            var decoded = serializer.Deserialize(createRaw, typeof(CreateRoomRequest)) as CreateRoomRequest;
            Assert.NotNull(decoded);
            Assert.AreEqual(4, decoded.maximumPeople);

            m_homeFakeServer.SendShortSuccess(new CreateRoomResponse {
                roomInfo = BuildRoom(9001, 4, "owner-biz")
            });

            yield return WaitUntil(() => done, 2f, "CreateRoom success missing.");

            Assert.NotNull(responsePayload.roomInfo);
            Assert.AreEqual(9001, responsePayload.roomInfo.roomId);
            Assert.That(responsePayload.roomInfo.readyUids, Contains.Item("owner-biz"));
        }

        [UnityTest]
        public IEnumerator Lobby_CreateRoom_MaximumPeopleMinimum_Boundary() {
            StartHomeFakeServer();
            SeedLoggedInPlayer("owner-min");

            bool done = false;
            CreateRoomResponse responsePayload = null;

            m_networkManager.SendShortRequest<CreateRoomRequest, CreateRoomResponse, ServerNetworkFailMessage>(
                NetworkConstants.HomePort,
                new CreateRoomRequest {
                    type = (int)HomeRequestType.CREATE_ROOM,
                    uid = "owner-min",
                    maximumPeople = 1
                },
                r => {
                    done = true;
                    responsePayload = r;
                },
                _ => { },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            byte[] createRaw = null;
            yield return WaitUntil(() => m_homeFakeServer.TryDequeueReceived(out createRaw), 2f, "CreateRoom not received.");
            var serializer = new JsonMessageSerializer();
            var decoded = serializer.Deserialize(createRaw, typeof(CreateRoomRequest)) as CreateRoomRequest;
            Assert.AreEqual(1, decoded.maximumPeople);

            m_homeFakeServer.SendShortSuccess(new CreateRoomResponse {
                roomInfo = BuildRoom(3003, 1)
            });

            yield return WaitUntil(() => done, 2f, "CreateRoom success missing.");

            Assert.AreEqual(1, responsePayload.roomInfo.maximumPeople);
        }

        [UnityTest]
        public IEnumerator Lobby_JoinRoom_Failure_ServiceFail_ErrorPath() {
            StartHomeFakeServer();
            SeedLoggedInPlayer("joiner-fail");

            bool failCalled = false;

            m_networkManager.SendShortRequest<JoinRoomRequest, JoinRoomResponse, ServerNetworkFailMessage>(
                NetworkConstants.HomePort,
                new JoinRoomRequest {
                    type = (int)HomeRequestType.JOIN_ROOM,
                    uid = "joiner-fail",
                    roomId = 9999
                },
                _ => { },
                _ => { failCalled = true; },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => m_homeFakeServer.TryDequeueReceived(out _), 2f, "JoinRoom not received.");

            m_homeFakeServer.SendShortFailure(new ServerNetworkFailMessage());

            yield return WaitUntil(() => failCalled, 2f, "JoinRoom failure callback missing.");
        }

        [UnityTest]
        public IEnumerator Lobby_JoinRoom_ErrorCode_ErrorPath() {
            StartHomeFakeServer();
            SeedLoggedInPlayer("joiner-err");

            bool success = false;
            bool fail = false;
            bool err = false;
            NetworkErrorMessage errMsg = null;

            m_networkManager.SendShortRequest<JoinRoomRequest, JoinRoomResponse, ServerNetworkFailMessage>(
                NetworkConstants.HomePort,
                new JoinRoomRequest {
                    type = (int)HomeRequestType.JOIN_ROOM,
                    uid = "joiner-err",
                    roomId = 111
                },
                _ => { success = true; },
                _ => { fail = true; },
                timeoutSeconds: 2f,
                onError: e => {
                    err = true;
                    errMsg = e;
                },
                blockOnConnect: true);

            yield return WaitUntil(() => m_homeFakeServer.TryDequeueReceived(out _), 2f, "JoinRoom not received.");

            m_homeFakeServer.SendObject(new ShortEnvelope<ServerNetworkSuccessMessage> {
                code = (int)ServerCode.ERROR,
                data = new ServerNetworkSuccessMessage(),
                message = "room gone"
            });

            yield return WaitUntil(() => err, 2f, "onError expected for ERROR code.");

            Assert.False(success);
            Assert.False(fail);
            Assert.AreEqual(ServerCode.ERROR, errMsg.code);
            Assert.AreEqual("room gone", errMsg.message);
        }

        [UnityTest]
        public IEnumerator Lobby_JoinRoom_SameUidTwice_SecondAttemptFails_Boundary() {
            StartHomeFakeServer();
            SeedLoggedInPlayer("repeat-joiner");

            bool firstOk = false;
            bool secondFail = false;

            m_networkManager.SendShortRequest<JoinRoomRequest, JoinRoomResponse, ServerNetworkFailMessage>(
                NetworkConstants.HomePort,
                new JoinRoomRequest {
                    type = (int)HomeRequestType.JOIN_ROOM,
                    uid = "repeat-joiner",
                    roomId = 5050
                },
                _ => { firstOk = true; },
                _ => { },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => m_homeFakeServer.TryDequeueReceived(out _), 2f, "First join not received.");
            m_homeFakeServer.SendShortSuccess(new JoinRoomResponse { roomInfo = BuildRoom(5050, 4) });
            yield return WaitUntil(() => firstOk, 2f, "First join should succeed.");

            m_networkManager.SendShortRequest<JoinRoomRequest, JoinRoomResponse, ServerNetworkFailMessage>(
                NetworkConstants.HomePort,
                new JoinRoomRequest {
                    type = (int)HomeRequestType.JOIN_ROOM,
                    uid = "repeat-joiner",
                    roomId = 5050
                },
                _ => { },
                _ => { secondFail = true; },
                timeoutSeconds: 2f,
                blockOnConnect: true);

            yield return WaitUntil(() => m_homeFakeServer.TryDequeueReceived(out _), 2f, "Second join not received.");
            m_homeFakeServer.SendShortFailure(new ServerNetworkFailMessage());
            yield return WaitUntil(() => secondFail, 2f, "Second join should hit business failure (e.g. duplicate).");
        }
    }
}
