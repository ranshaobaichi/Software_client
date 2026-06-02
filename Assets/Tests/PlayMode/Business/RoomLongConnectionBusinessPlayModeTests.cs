using System;
using System.Collections;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Constants;
using Network;
using Network.Messages;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Business {
    /// <summary>
    /// Business-level PlayMode tests for in-room TCP behavior aligned with
    /// <see cref="UI.States.OnlineRoomState"/> (same host/port, dispatch table shape, and payloads).
    /// </summary>
    public class RoomLongConnectionBusinessPlayModeTests : BusinessPlayModeTestBase {
        static RoomInfo BuildRoom(int roomId, int maximumPeople, params string[] readyUids) {
            return new RoomInfo {
                roomId = roomId,
                maximumPeople = maximumPeople,
                basicInfos = new List<PlayerData.PlayerBasicInfo> {
                    new PlayerData.PlayerBasicInfo("u1", "p1", 1),
                    new PlayerData.PlayerBasicInfo("u2", "p2", 2)
                },
                readyUids = new List<string>(readyUids ?? Array.Empty<string>())
            };
        }

        static Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> CreateOnlineRoomStyleMainHandlers(
                Action<BroadcastRoomStatusResponse> onBroadcast,
                Action<LeaveRoomResponse> onLeave) {
            return new Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> {
                {
                    HomeRequestType.BROADCAST,
                    NetworkManager.CreateDispatchEntry<BroadcastRoomStatusResponse>(onBroadcast)
                },
                {
                    HomeRequestType.LEAVE_ROOM,
                    NetworkManager.CreateDispatchEntry<LeaveRoomResponse>(onLeave)
                }
            };
        }

        [UnityTest]
        public IEnumerator RoomLong_BroadcastAndReadyLeave_NormalFlow() {
            StartHomeFakeServer();
            SeedLoggedInPlayer("room-user-1");

            int lastRoomId = -1;
            int leaveSignals = 0;

            var main = CreateOnlineRoomStyleMainHandlers(
                r => {
                    if (r?.roomInfo != null)
                        lastRoomId = r.roomInfo.roomId;
                },
                _ => { leaveSignals++; });

            var ch = m_networkManager.CreateLongConnection(
                    NetworkConstants.DefaultHost,
                    NetworkConstants.HomePort,
                    main,
                    new Dictionary<int, Action>(),
                    dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();
            yield return WaitUntil(() => m_homeFakeServer.IsClientConnected, 2f, "Client did not connect.");

            m_homeFakeServer.SendObject(new LongEnvelope<BroadcastRoomStatusResponse> {
                type = (int)HomeRequestType.BROADCAST,
                data = new BroadcastRoomStatusResponse { roomInfo = BuildRoom(777, 4, "room-user-1") },
                pushMessages = new List<int>()
            });

            yield return WaitUntil(() => lastRoomId == 777, 2f, "Broadcast did not apply room snapshot.");

            ch.Send(new SetReadyStatusRequest {
                type = (int)HomeRequestType.SET_READY,
                uid = "room-user-1",
                ready = true
            });

            byte[] readyRaw = null;
            yield return WaitUntil(() => m_homeFakeServer.TryDequeueReceived(out readyRaw), 2f, "SET_READY not received.");
            var ser = new JsonMessageSerializer();
            var readyDecoded = ser.Deserialize(readyRaw, typeof(SetReadyStatusRequest)) as SetReadyStatusRequest;
            Assert.NotNull(readyDecoded);
            Assert.True(readyDecoded.ready);
            Assert.AreEqual("room-user-1", readyDecoded.uid);

            ch.Send(new LeaveRoomRequest {
                type = (int)HomeRequestType.LEAVE_ROOM,
                uid = "room-user-1"
            });

            byte[] leaveReqRaw = null;
            yield return WaitUntil(() => m_homeFakeServer.TryDequeueReceived(out leaveReqRaw), 2f, "LEAVE_ROOM not received.");
            var leaveDecoded = ser.Deserialize(leaveReqRaw, typeof(LeaveRoomRequest)) as LeaveRoomRequest;
            Assert.NotNull(leaveDecoded);
            Assert.AreEqual("room-user-1", leaveDecoded.uid);

            m_homeFakeServer.SendObject(new LongEnvelope<LeaveRoomResponse> {
                type = (int)HomeRequestType.LEAVE_ROOM,
                data = new LeaveRoomResponse(),
                pushMessages = new List<int>()
            });

            yield return WaitUntil(() => leaveSignals == 1, 2f, "LeaveRoomResponse handler not invoked.");

            m_networkManager.RemoveConnection(ch);
        }

        [UnityTest]
        public IEnumerator RoomLong_BroadcastNullRoomInfo_DoesNotMutateSnapshot_Boundary() {
            StartHomeFakeServer();
            SeedLoggedInPlayer("edge-null-broadcast");

            int applied = 0;
            var main = CreateOnlineRoomStyleMainHandlers(
                r => {
                    if (r?.roomInfo != null)
                        applied++;
                },
                _ => { });

            var ch = m_networkManager.CreateLongConnection(
                    NetworkConstants.DefaultHost,
                    NetworkConstants.HomePort,
                    main,
                    new Dictionary<int, Action>(),
                    dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();
            yield return WaitUntil(() => m_homeFakeServer.IsClientConnected, 2f, "Client did not connect.");

            m_homeFakeServer.SendObject(new LongEnvelope<BroadcastRoomStatusResponse> {
                type = (int)HomeRequestType.BROADCAST,
                data = new BroadcastRoomStatusResponse { roomInfo = null },
                pushMessages = new List<int>()
            });

            yield return new WaitForSeconds(0.35f);
            Assert.AreEqual(0, applied, "OnlineRoomState ignores null roomInfo; applies count must stay 0.");

            m_networkManager.RemoveConnection(ch);
        }

        [UnityTest]
        public IEnumerator RoomLong_UnknownMainTypeWithPushSuppressed_NoHandlers_Boundary() {
            StartHomeFakeServer();

            bool broadcastHit = false;
            bool pushHit = false;

            var main = CreateOnlineRoomStyleMainHandlers(
                _ => { broadcastHit = true; },
                _ => { });

            var push = new Dictionary<int, Action> {
                { 501, () => { pushHit = true; } }
            };

            var ch = m_networkManager.CreateLongConnection(
                    NetworkConstants.DefaultHost,
                    NetworkConstants.HomePort,
                    main,
                    push,
                    dispatchPushWhenMainTypeUnknown: false);

            ch.Connect();
            yield return WaitUntil(() => m_homeFakeServer.IsClientConnected, 2f, "Client did not connect.");

            LogAssert.Expect(LogType.Warning, new Regex(@"\[LongConnectionInboundDispatcher\] Unknown main type 999111"));
            m_homeFakeServer.SendObject(new LongEnvelope<LeaveRoomResponse> {
                type = 999111,
                data = new LeaveRoomResponse(),
                pushMessages = new List<int> { 501 }
            });

            yield return new WaitForSeconds(0.35f);
            Assert.False(broadcastHit);
            Assert.False(pushHit);

            m_networkManager.RemoveConnection(ch);
        }

        [UnityTest]
        public IEnumerator RoomLong_BroadcastCarriesPushMarker_InvokesPush_Boundary() {
            StartHomeFakeServer();

            bool pushHit = false;
            var main = CreateOnlineRoomStyleMainHandlers(_ => { }, _ => { });
            var push = new Dictionary<int, Action> {
                { 909, () => { pushHit = true; } }
            };

            var ch = m_networkManager.CreateLongConnection(
                    NetworkConstants.DefaultHost,
                    NetworkConstants.HomePort,
                    main,
                    push,
                    dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();
            yield return WaitUntil(() => m_homeFakeServer.IsClientConnected, 2f, "Client did not connect.");

            m_homeFakeServer.SendObject(new LongEnvelope<BroadcastRoomStatusResponse> {
                type = (int)HomeRequestType.BROADCAST,
                data = new BroadcastRoomStatusResponse {
                    roomInfo = BuildRoom(1, 2)
                },
                pushMessages = new List<int> { 909 }
            });

            yield return WaitUntil(() => pushHit, 2f, "Push marker should fire alongside known BROADCAST.");

            m_networkManager.RemoveConnection(ch);
        }

        [UnityTest]
        public IEnumerator RoomLong_TwoLeaveResponses_BothDispatch_Boundary() {
            StartHomeFakeServer();

            int leaveCount = 0;
            var main = CreateOnlineRoomStyleMainHandlers(
                _ => { },
                _ => { leaveCount++; });

            var ch = m_networkManager.CreateLongConnection(
                    NetworkConstants.DefaultHost,
                    NetworkConstants.HomePort,
                    main,
                    new Dictionary<int, Action>(),
                    dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();
            yield return WaitUntil(() => m_homeFakeServer.IsClientConnected, 2f, "Client did not connect.");

            m_homeFakeServer.SendObject(new LongEnvelope<LeaveRoomResponse> {
                type = (int)HomeRequestType.LEAVE_ROOM,
                data = new LeaveRoomResponse(),
                pushMessages = new List<int>()
            });
            m_homeFakeServer.SendObject(new LongEnvelope<LeaveRoomResponse> {
                type = (int)HomeRequestType.LEAVE_ROOM,
                data = new LeaveRoomResponse(),
                pushMessages = new List<int>()
            });

            yield return WaitUntil(() => leaveCount == 2, 2f, "Each LeaveRoomResponse frame should dispatch.");

            m_networkManager.RemoveConnection(ch);
        }

        [UnityTest]
        public IEnumerator RoomLong_ReadyToggleTwice_ServerReceivesBoth_Boundary() {
            StartHomeFakeServer();
            SeedLoggedInPlayer("toggle-ready");

            var main = CreateOnlineRoomStyleMainHandlers(_ => { }, _ => { });
            var ch = m_networkManager.CreateLongConnection(
                    NetworkConstants.DefaultHost,
                    NetworkConstants.HomePort,
                    main,
                    new Dictionary<int, Action>(),
                    dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();
            yield return WaitUntil(() => m_homeFakeServer.IsClientConnected, 2f, "Client did not connect.");

            ch.Send(new SetReadyStatusRequest {
                type = (int)HomeRequestType.SET_READY,
                uid = "toggle-ready",
                ready = true
            });
            ch.Send(new SetReadyStatusRequest {
                type = (int)HomeRequestType.SET_READY,
                uid = "toggle-ready",
                ready = false
            });

            byte[] a = null;
            byte[] b = null;
            yield return WaitUntil(() => m_homeFakeServer.TryDequeueReceived(out a), 2f, "First SET_READY missing.");
            yield return WaitUntil(() => m_homeFakeServer.TryDequeueReceived(out b), 2f, "Second SET_READY missing.");

            var ser = new JsonMessageSerializer();
            var first = ser.Deserialize(a, typeof(SetReadyStatusRequest)) as SetReadyStatusRequest;
            var second = ser.Deserialize(b, typeof(SetReadyStatusRequest)) as SetReadyStatusRequest;
            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.True(first.ready);
            Assert.False(second.ready);

            m_networkManager.RemoveConnection(ch);
        }

        [UnityTest]
        public IEnumerator RoomLong_InvalidJsonFrame_DoesNotCrashHandlers_ErrorTolerance() {
            StartHomeFakeServer();

            bool broadcastHit = false;
            var main = CreateOnlineRoomStyleMainHandlers(
                _ => { broadcastHit = true; },
                _ => { });

            var ch = m_networkManager.CreateLongConnection(
                    NetworkConstants.DefaultHost,
                    NetworkConstants.HomePort,
                    main,
                    new Dictionary<int, Action>(),
                    dispatchPushWhenMainTypeUnknown: true);

            ch.Connect();
            yield return WaitUntil(() => m_homeFakeServer.IsClientConnected, 2f, "Client did not connect.");

            LogAssert.Expect(LogType.Warning, new Regex(@"\[LongConnectionInboundDispatcher\] Probe deserialize failed:"));
            m_homeFakeServer.SendFramedUtf8("{");

            yield return new WaitForSeconds(0.35f);
            Assert.False(broadcastHit);

            m_homeFakeServer.SendObject(new LongEnvelope<BroadcastRoomStatusResponse> {
                type = (int)HomeRequestType.BROADCAST,
                data = new BroadcastRoomStatusResponse { roomInfo = BuildRoom(44, 2) },
                pushMessages = new List<int>()
            });

            yield return WaitUntil(() => broadcastHit, 2f, "Channel should recover after a bad frame.");

            m_networkManager.RemoveConnection(ch);
        }

        [UnityTest]
        public IEnumerator RoomLong_ConnectToClosedPort_IsNotConnected_ErrorPath() {
            int deadPort = TakeReleasedEphemeralLoopbackPort();

            var main = CreateOnlineRoomStyleMainHandlers(_ => { }, _ => { });
            var ch = m_networkManager.CreateLongConnection(
                    NetworkConstants.DefaultHost,
                    deadPort,
                    main,
                    new Dictionary<int, Action>(),
                    dispatchPushWhenMainTypeUnknown: true);

            LogAssert.Expect(LogType.Error, new Regex(@"\[TcpConnectionChannel\] Connect failed:"));
            ch.Connect();
            yield return null;

            Assert.False(ch.IsConnected, "TCP connect should fail when nothing listens.");

            m_networkManager.RemoveConnection(ch);
        }
    }
}
