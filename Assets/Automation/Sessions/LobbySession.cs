using System;
using System.Collections;
using System.Collections.Generic;
using Automation.Config;
using Automation.Protocol;
using Automation.Util;
using Constants;
using Network;
using Network.Messages;
using UnityEngine;

namespace Automation.Sessions {
    public class LobbySession {
        readonly NetworkEndpointConfig m_config;

        INetworkChannel m_channel;
        int m_roomId = -1;
        readonly SignalAwaiter m_allReadySignal = new SignalAwaiter();
        readonly SignalAwaiter m_leaveSignal = new SignalAwaiter();
        BroadcastRoomStatusResponse m_lastBroadcast;

        public int RoomId => m_roomId;
        public BroadcastRoomStatusResponse LastBroadcast => m_lastBroadcast;
        public GetStateStatusResponse LastStateStatus { get; private set; }
        public bool IsInRoom => m_roomId >= 0 && m_channel != null;

        public LobbySession(NetworkEndpointConfig config) {
            m_config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public IEnumerator ListRooms(Action<List<RoomInfo>> onRooms = null) {
            EnsureLoggedIn();
            bool done = false;
            RefreshRoomResponse payload = null;
            NetworkErrorMessage error = null;

            NetworkManager.SInstance.SendShortRequestWithSuccess<RefreshRoomRequest, RefreshRoomResponse>(
                    m_config.HomePort,
                    new RefreshRoomRequest { type = (int)HomeRequestType.LIST_ROOMS },
                    response => {
                        payload = response;
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
                    "ListRooms timed out");

            if (error != null)
                throw new AutomationException(error.message ?? "ListRooms failed", error);

            onRooms?.Invoke(payload?.roomInfos);
        }

        public IEnumerator CreateRoom(int maximumPeople) {
            EnsureLoggedIn();
            bool done = false;
            RoomInfo roomInfo = null;
            NetworkErrorMessage error = null;
            bool failed = false;
            string uid = PlayerData.SInstance.basicInfo.uid;

            NetworkManager.SInstance.SendShortRequest<CreateRoomRequest, CreateRoomResponse, ServerNetworkFailMessage>(
                    m_config.HomePort,
                    new CreateRoomRequest {
                            type = (int)HomeRequestType.CREATE_ROOM,
                            uid = uid,
                            maximumPeople = maximumPeople
                    },
                    response => {
                        roomInfo = response?.roomInfo;
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
                    "CreateRoom timed out");

            if (error != null)
                throw new AutomationException(error.message ?? "CreateRoom failed", error);
            if (failed)
                throw new AutomationException("CreateRoom service failure");
            if (roomInfo == null)
                throw new AutomationException("CreateRoom returned empty roomInfo");

            m_roomId = roomInfo.roomId;
            yield return EnsureLongConnection();
        }

        public IEnumerator JoinRoom(int roomId) {
            EnsureLoggedIn();
            bool done = false;
            RoomInfo roomInfo = null;
            NetworkErrorMessage error = null;
            bool failed = false;
            string uid = PlayerData.SInstance.basicInfo.uid;

            NetworkManager.SInstance.SendShortRequest<JoinRoomRequest, JoinRoomResponse, ServerNetworkFailMessage>(
                    m_config.HomePort,
                    new JoinRoomRequest {
                            type = (int)HomeRequestType.JOIN_ROOM,
                            uid = uid,
                            roomId = roomId
                    },
                    response => {
                        roomInfo = response?.roomInfo;
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
                    "JoinRoom timed out");

            if (error != null)
                throw new AutomationException(error.message ?? "JoinRoom failed", error);
            if (failed)
                throw new AutomationException("JoinRoom service failure");
            if (roomInfo == null)
                throw new AutomationException("JoinRoom returned empty roomInfo");

            m_roomId = roomInfo.roomId;
            yield return EnsureLongConnection();
        }

        public IEnumerator SetReady(bool ready) {
            EnsureLongConnectionReady();
            m_allReadySignal.Reset();

            m_channel.Send(new SetReadyStatusRequest {
                    type = (int)HomeRequestType.SET_READY,
                    uid = PlayerData.SInstance.basicInfo.uid,
                    ready = ready
            });

            yield return AutomationAwaiter.WaitForNextFrame();
        }

        public IEnumerator WaitForAllReady() {
            if (m_allReadySignal.IsSignaled)
                yield break;

            float deadline = Time.realtimeSinceStartup + m_config.DefaultTimeoutSeconds;
            while (Time.realtimeSinceStartup < deadline) {
                if (m_allReadySignal.IsSignaled)
                    yield break;

                GetStateStatusResponse status = null;
                yield return GetStateStatus(value => status = value);
                if (status != null && status.roomPhase >= (int)RoomPhase.SHOP)
                    yield break;

                yield return AutomationAwaiter.WaitForNextFrame();
            }

            throw new AutomationException(
                    "Timed out waiting for ALL_READY (pushMessages contains 0 or roomPhase>=SHOP)");
        }

        /// <summary>
        /// Poll server-side room/player snapshot (GET_STATE_STATUS, type=9 on Home port).
        /// </summary>
        public IEnumerator GetStateStatus(Action<GetStateStatusResponse> onStatus = null) {
            EnsureLoggedIn();
            bool done = false;
            GetStateStatusResponse payload = null;
            NetworkErrorMessage error = null;

            NetworkManager.SInstance.SendShortRequestWithSuccess<GetStateStatusRequest, GetStateStatusResponse>(
                    m_config.HomePort,
                    new GetStateStatusRequest {
                            type = (int)LobbyRequestType.GET_STATE_STATUS,
                            uid = PlayerData.SInstance.basicInfo.uid
                    },
                    response => {
                        payload = response;
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
                    "GetStateStatus timed out");

            if (error != null)
                throw new AutomationException(error.message ?? "GetStateStatus failed", error);

            LastStateStatus = payload;
            onStatus?.Invoke(payload);
        }

        public IEnumerator LeaveRoom() {
            if (m_channel == null || m_roomId < 0)
                yield break;

            m_leaveSignal.Reset();
            m_channel.Send(new LeaveRoomRequest {
                    type = (int)HomeRequestType.LEAVE_ROOM,
                    uid = PlayerData.SInstance.basicInfo.uid
            });

            yield return AutomationAwaiter.WaitForSignal(m_leaveSignal, m_config.DefaultTimeoutSeconds,
                    "Timed out waiting for LeaveRoom response");

            DisconnectLongConnection();
        }

        public void DisconnectLongConnection() {
            if (m_channel == null)
                return;

            NetworkManager.SInstance.RemoveConnection(m_channel);
            m_channel = null;
            m_roomId = -1;
            m_lastBroadcast = null;
            m_allReadySignal.Reset();
            m_leaveSignal.Reset();
        }

        public IEnumerator EnsureLongConnection() {
            if (m_channel != null) {
                if (!m_channel.IsConnected)
                    m_channel.Connect();
                yield return AutomationAwaiter.WaitUntil(() => m_channel.IsConnected, m_config.DefaultTimeoutSeconds,
                        "Lobby long connection failed");
                yield break;
            }

            var mainHandlers = new Dictionary<HomeRequestType, LongConnectionMainDispatchEntry> {
                    {
                            HomeRequestType.BROADCAST,
                            NetworkManager.CreateDispatchEntry<BroadcastRoomStatusResponse>(OnBroadcast)
                    },
                    {
                            HomeRequestType.LEAVE_ROOM,
                            NetworkManager.CreateDispatchEntry<LeaveRoomResponse>(_ => OnLeaveRoom())
                    }
            };

            var pushHandlers = new Dictionary<int, Action> {
                    { 0, () => m_allReadySignal.Signal() }
            };

            m_channel = NetworkManager.SInstance.CreateLongConnection(
                    m_config.Host,
                    m_config.HomePort,
                    mainHandlers,
                    pushHandlers,
                    dispatchPushWhenMainTypeUnknown: true);

            m_channel.Connect();
            yield return AutomationAwaiter.WaitUntil(() => m_channel.IsConnected, m_config.DefaultTimeoutSeconds,
                    "Lobby long connection failed");
        }

        void OnBroadcast(BroadcastRoomStatusResponse response) {
            if (response != null)
                m_lastBroadcast = response;
        }

        void OnLeaveRoom() {
            m_leaveSignal.Signal();
        }

        void EnsureLoggedIn() {
            if (!PlayerData.IsInit())
                throw new AutomationException("Player must be logged in before lobby operations");
        }

        void EnsureLongConnectionReady() {
            EnsureLoggedIn();
            if (m_channel == null)
                throw new AutomationException("Lobby long connection is not open");
        }
    }
}
