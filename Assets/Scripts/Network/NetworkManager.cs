using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;
using Constants;
using Network.Messages;

namespace Network {
    /// <summary>
    /// Singleton manager for network connections.
    /// </summary>
    public class NetworkManager : MonoBehaviour {
        #region Singleton

        private static volatile NetworkManager s_instance;
        private static readonly object InstanceLock = new object();

        public static NetworkManager SInstance {
            get {
                if (s_instance == null) {
                    lock (InstanceLock) {
                        if (s_instance == null) {
                            s_instance = FindObjectOfType<NetworkManager>();
                            if (s_instance == null) {
                                var go = new GameObject("[NetworkManager]");
                                s_instance = go.AddComponent<NetworkManager>();
                                DontDestroyOnLoad(go);
                            }
                        }
                    }
                }

                return s_instance;
            }
        }

        #endregion

        #region Fields

        private IMessageFramer m_defaultFramer;
        private IMessageSerializer m_defaultSerializer;

        private readonly List<INetworkChannel> m_channels = new List<INetworkChannel>();
        private readonly object m_channelsLock = new object();
        private readonly List<INetworkChannel> m_pumpScratch = new List<INetworkChannel>();

        private readonly List<(INetworkChannel channel, float deadline, Action onTimeout)> m_shortRequestPending =
                new List<(INetworkChannel channel, float deadline, Action onTimeout)>();

        private readonly object m_shortRequestLock = new object();
        private readonly Queue<Action> m_mainThreadActions = new Queue<Action>();
        private readonly object m_mainThreadActionsLock = new object();

        #endregion

        #region Unity Lifecycle

        private void Awake() {
            if (s_instance != null && s_instance != this) {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            m_defaultFramer ??= new LineFramer();
            m_defaultSerializer ??= new JsonMessageSerializer();
        }

        private void OnDestroy() {
            if (s_instance == this)
                s_instance = null;

            DisconnectAll();
        }

        private void Update() {
            FlushMainThreadActions();
            PumpAll();
            PruneShortRequestTimeouts();
        }

        private void OnApplicationQuit() {
            NetworkManager.SInstance.SendShortRequest(NetworkConstants.LoginPort,
                    new LogoutRequest {
                            type = (int)LoginRequestType.LOGOUT,
                            uid = PlayerData.SInstance.basicInfo.uid
                    },
                    blockOnConnect: true);
        }

        #endregion

        #region Static Methods

        private static void DefaultOnServiceSuccessAction(ServerNetworkSuccessMessage successMsg) {
            // Debug.Log(successMsg);
        }

        private static void DefaultOnServiceFailAction(ServerNetworkFailMessage failMsg) {
            // Debug.LogWarning("[NetworkManager] Service Failure: " + (failMsg != null ? failMsg.ToString() : "null"));
        }

        private static void DefaultOnTimeoutAction() {
            Debug.LogError("[NetworkManager] Timeout");
        }

        private static void DefaultOnErrorAction(NetworkErrorMessage errorMsg) {
            Debug.LogError($"[NetworkManager] Error: {errorMsg.code} - {errorMsg.message}");
        }

        /// <summary>
        /// Creates a dispatch entry from a strongly typed handler, wrapping type conversion internally.
        /// </summary>
        public static LongConnectionMainDispatchEntry CreateDispatchEntry<TData>(Action<TData> handler)
                where TData : class {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            return new LongConnectionMainDispatchEntry(typeof(TData), payload => {
                if (payload is TData typed) {
                    handler(typed);
                    return;
                }

                if (payload == null) {
                    handler(null);
                    return;
                }

                throw new InvalidCastException(
                        $"Payload type mismatch. Expected {typeof(TData).FullName}, got {payload.GetType().FullName}.");
            });
        }

        #endregion

        /// <summary>
        /// Creates a TCP channel tracked by this manager, using a single <see cref="INetworkChannel.RegisterHandler{T}"/> path
        /// (no <see cref="LongConnectionDispatchTables"/>). Use <see cref="CreateLongConnection"/> when inbound frames use <see cref="Messages.LongEnvelope{T}"/> dispatch.
        /// </summary>
        /// <param name="framer">[Can Be Null]</param>
        /// <param name="serializer">[Can Be Null]</param>
        public INetworkChannel CreateConnection(string host,
                int port,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        ) {
            var framerToUse = framer ?? m_defaultFramer ?? new LineFramer();
            var serializerToUse = serializer ?? m_defaultSerializer ?? new JsonMessageSerializer();
            var channel = new TcpConnectionChannel(host, port, framerToUse, serializerToUse);
            lock (m_channelsLock) {
                m_channels.Add(channel);
            }

            return channel;
        }

        /// <summary>
        /// Creates a long-lived TCP channel with inbound dispatch: main outer <c>type</c> maps to a typed <see cref="Messages.LongEnvelope{T}"/> <c>data</c> handler;
        /// <see cref="Messages.LongEnvelope{T}.pushMessages"/> ints map to lightweight push handlers.
        /// </summary>
        /// <typeparam name="TMainOpcode">Business enum for wire <c>type</c> (underlying int keys are used internally).</typeparam>
        public INetworkChannel CreateLongConnection<TMainOpcode>(
                string host,
                int port,
                IReadOnlyDictionary<TMainOpcode, LongConnectionMainDispatchEntry> mainHandlers,
                IReadOnlyDictionary<int, Action> pushHandlers,
                bool dispatchPushWhenMainTypeUnknown = true,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        )
                where TMainOpcode : struct {
            if (!typeof(TMainOpcode).IsEnum)
                throw new ArgumentException("TMainOpcode must be an enum", nameof(TMainOpcode));
            var mainInt = new Dictionary<int, LongConnectionMainDispatchEntry>(mainHandlers.Count);
            foreach (var kv in mainHandlers)
                mainInt[Convert.ToInt32(kv.Key)] = kv.Value;

            return CreateLongConnection(host, port, mainInt, pushHandlers, dispatchPushWhenMainTypeUnknown, framer,
                    serializer);
        }

        /// <summary>
        /// Creates a long-lived TCP channel with inbound dispatch keyed by outer wire <c>type</c> as <see cref="int"/>.
        /// </summary>
        public INetworkChannel CreateLongConnection(
                string host,
                int port,
                IReadOnlyDictionary<int, LongConnectionMainDispatchEntry> mainHandlers,
                IReadOnlyDictionary<int, Action> pushHandlers,
                bool dispatchPushWhenMainTypeUnknown = true,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        ) {
            if (mainHandlers == null) throw new ArgumentNullException(nameof(mainHandlers));
            if (pushHandlers == null) throw new ArgumentNullException(nameof(pushHandlers));
            var tables = new LongConnectionDispatchTables(mainHandlers, pushHandlers, dispatchPushWhenMainTypeUnknown);
            var framerToUse = framer ?? m_defaultFramer ?? new LineFramer();
            var serializerToUse = serializer ?? m_defaultSerializer ?? new JsonMessageSerializer();
            var channel = new TcpConnectionChannel(host, port, framerToUse, serializerToUse, tables);
            lock (m_channelsLock) {
                m_channels.Add(channel);
            }

            return channel;
        }

        /// <summary>
        /// Remove a specified connection from the manager, disconnecting it if still active.
        /// Safe to call multiple times or with null.
        /// </summary>
        /// <param name="channel">the channel to remove</param>
        public void RemoveConnection(INetworkChannel channel) {
            if (channel == null) return;
            channel.Disconnect();
            lock (m_channelsLock) {
                m_channels.Remove(channel);
            }
        }

        #region Short Connection Utilities

        public void SendShortRequest<TRequest>(
                int port,
                TRequest request,
                string host = NetworkConstants.DefaultHost,
                Action<NetworkErrorMessage> onError = null,
                float timeoutSeconds = 3f,
                bool blockOnConnect = false,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        )
                where TRequest : ClientNetworkMessage {
            SendShortRequest<TRequest, ServerNetworkSuccessMessage, ServerNetworkFailMessage>
            (port, request, DefaultOnServiceSuccessAction, DefaultOnServiceFailAction, host,
                    timeoutSeconds, onError, blockOnConnect, framer, serializer);
        }

        public void SendShortRequestWithSuccess<TRequest, TSuccessResponse>(
                int port,
                TRequest request,
                Action<TSuccessResponse> onSuccess,
                string host = NetworkConstants.DefaultHost,
                Action<NetworkErrorMessage> onError = null,
                float timeoutSeconds = 3f,
                bool blockOnConnect = false,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        )
                where TRequest : ClientNetworkMessage
                where TSuccessResponse : ServerNetworkSuccessMessage {
            SendShortRequest<TRequest, TSuccessResponse, ServerNetworkFailMessage>
            (port, request, onSuccess, DefaultOnServiceFailAction, host,
                    timeoutSeconds, onError, blockOnConnect, framer, serializer);
        }

        public void SendShortRequestWithFailure<TRequest, TFailureResponse>(
                int port,
                TRequest request,
                Action<TFailureResponse> onFailure,
                string host = NetworkConstants.DefaultHost,
                Action<NetworkErrorMessage> onError = null,
                float timeoutSeconds = 3f,
                bool blockOnConnect = false,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        )
                where TRequest : ClientNetworkMessage
                where TFailureResponse : ServerNetworkFailMessage {
            SendShortRequest<TRequest, ServerNetworkSuccessMessage, TFailureResponse>
            (port, request, DefaultOnServiceSuccessAction, onFailure, host,
                    timeoutSeconds, onError, blockOnConnect, framer, serializer);
        }

        /// <summary>
        /// Send a request and wait for a single response, using a short-lived connection.
        /// Deserializes the server envelope directly as <see cref="ShortEnvelope{T}"/>,
        /// where <c>T</c> is the concrete success/failure response type.
        /// </summary>
        public void SendShortRequest<TRequest, TSuccessResponse, TFailureResponse>(
                int port,
                TRequest request,
                Action<TSuccessResponse> onSuccess,
                Action<TFailureResponse> onFailure,
                string host = NetworkConstants.DefaultHost,
                float timeoutSeconds = 3f,
                Action<NetworkErrorMessage> onError = null,
                bool blockOnConnect = false,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        )
                where TRequest : ClientNetworkMessage
                where TSuccessResponse : ServerNetworkSuccessMessage
                where TFailureResponse : ServerNetworkFailMessage {
            var f = framer ?? m_defaultFramer ?? new LineFramer();
            var s = serializer ?? m_defaultSerializer ?? new JsonMessageSerializer();
            onError ??= DefaultOnErrorAction;

            var ch = new TcpConnectionChannel(host, port, f, s);
            ch.RegisterHandler<byte[]>(rawBytes => {
                try {
                    lock (m_shortRequestLock) {
                        RemoveShortRequestPending(ch);
                    }

                    if (rawBytes == null || rawBytes.Length == 0) {
                        onError?.Invoke(new NetworkErrorMessage {
                                code = ServerCode.DESERIALIZE_ERROR,
                                message = "Empty response payload"
                        });

                        return;
                    }

                    var envelopeProbe = s.Deserialize(rawBytes, typeof(ShortEnvelope<ServerNetworkSuccessMessage>))
                            as ShortEnvelope<ServerNetworkSuccessMessage>;
                    if (envelopeProbe == null) {
                        onError?.Invoke(new NetworkErrorMessage {
                                code = ServerCode.DESERIALIZE_ERROR,
                                message = "Failed to deserialize server envelope"
                        });

                        return;
                    }

                    var code = (ServerCode)envelopeProbe.code;
                    if ((code & ServerCode.SERVICE_SUCCESS) != 0) {
                        try {
                            var typedEnvelope = s.Deserialize(rawBytes, typeof(ShortEnvelope<TSuccessResponse>))
                                    as ShortEnvelope<TSuccessResponse>;
                            onSuccess?.Invoke(typedEnvelope?.data);
                        } catch (Exception ex) {
                            onError?.Invoke(new NetworkErrorMessage {
                                    code = ServerCode.DESERIALIZE_ERROR,
                                    message = ex.Message
                            });
                        }
                    } else if ((code & ServerCode.SERVICE_FAIL) != 0) {
                        try {
                            var typedEnvelope = s.Deserialize(rawBytes, typeof(ShortEnvelope<TFailureResponse>))
                                    as ShortEnvelope<TFailureResponse>;
                            onFailure?.Invoke(typedEnvelope?.data);
                        } catch (Exception ex) {
                            onError?.Invoke(new NetworkErrorMessage {
                                    code = ServerCode.DESERIALIZE_ERROR,
                                    message = ex.Message
                            });
                        }
                    } else {
                        onError?.Invoke(new NetworkErrorMessage {
                                code = code,
                                message = string.IsNullOrWhiteSpace(envelopeProbe.message) ? "Unknown error" : envelopeProbe.message
                        });
                    }
                } finally {
                    RemoveConnection(ch);
                }
            });

            lock (m_channelsLock) {
                m_channels.Add(ch);
            }

            lock (m_shortRequestLock) {
                m_shortRequestPending.Add((ch, Time.time + timeoutSeconds, DefaultOnTimeoutAction));
            }

            if (blockOnConnect) {
                ch.Connect();
                if (ch.IsConnected) {
                    ch.Send(request);
                    return;
                }

                RemoveConnection(ch);
                lock (m_shortRequestLock) {
                    RemoveShortRequestPending(ch);
                }

                onError(new NetworkErrorMessage {
                        code = ServerCode.CONNECTION_ERROR,
                        message = "Failed to connect"
                });
                return;
            }

            // Run connect in a worker thread so the main thread is never blocked by OS-level TCP connect timeout.
            ThreadPool.QueueUserWorkItem(_ => {
                ch.Connect();
                EnqueueMainThreadAction(() => {
                    // The request may already be timed out/cancelled before async connect finishes.
                    if (!IsShortRequestPending(ch)) {
                        if (ch.IsConnected)
                            RemoveConnection(ch);

                        return;
                    }

                    if (ch.IsConnected) {
                        ch.Send(request);
                        return;
                    }

                    RemoveConnection(ch);
                    lock (m_shortRequestLock) {
                        RemoveShortRequestPending(ch);
                    }

                    onError(new NetworkErrorMessage {
                            code = ServerCode.CONNECTION_ERROR,
                            message = "Failed to connect"
                    });
                });
            });
        }

        #endregion

        private void RemoveShortRequestPending(INetworkChannel ch) {
            for (int i = m_shortRequestPending.Count - 1; i >= 0; i--) {
                if (m_shortRequestPending[i].channel == ch) {
                    m_shortRequestPending.RemoveAt(i);
                    return;
                }
            }
        }

        private bool IsShortRequestPending(INetworkChannel ch) {
            lock (m_shortRequestLock) {
                for (int i = m_shortRequestPending.Count - 1; i >= 0; i--) {
                    if (m_shortRequestPending[i].channel == ch)
                        return true;
                }
            }

            return false;
        }

        private void EnqueueMainThreadAction(Action action) {
            if (action == null) return;
            lock (m_mainThreadActionsLock) {
                m_mainThreadActions.Enqueue(action);
            }
        }

        private void FlushMainThreadActions() {
            while (true) {
                Action action = null;
                lock (m_mainThreadActionsLock) {
                    if (m_mainThreadActions.Count > 0)
                        action = m_mainThreadActions.Dequeue();
                }

                if (action == null)
                    break;

                try {
                    action.Invoke();
                } catch (Exception ex) {
                    Debug.LogWarning("[NetworkManager] Main-thread action failed: " + ex.Message);
                }
            }
        }

        private void PruneShortRequestTimeouts() {
            float now = Time.time;
            List<(INetworkChannel ch, Action onTimeout)> toRemove = null;
            lock (m_shortRequestLock) {
                for (int i = m_shortRequestPending.Count - 1; i >= 0; i--) {
                    if (now < m_shortRequestPending[i].deadline) continue;
                    toRemove ??= new List<(INetworkChannel, Action)>();
                    toRemove.Add((m_shortRequestPending[i].channel, m_shortRequestPending[i].onTimeout));
                    m_shortRequestPending.RemoveAt(i);
                }
            }

            if (toRemove == null) return;
            foreach (var item in toRemove) {
                item.Item2?.Invoke();
                RemoveConnection(item.Item1);
            }
        }

        /// <summary>
        /// Called by Update() on the main thread, dispatches pending messages for all channels.
        /// Ensures that message handlers are invoked in a single-threaded context.
        /// </summary>
        private void PumpAll() {
            lock (m_channelsLock) {
                m_pumpScratch.AddRange(m_channels);
            }

            try {
                foreach (var ch in m_pumpScratch)
                    ch.DispatchPendingMessages();
            } finally {
                m_pumpScratch.Clear();
            }
        }

        private void DisconnectAll() {
            lock (m_channelsLock) {
                foreach (var ch in m_channels)
                    ch.Disconnect();
                m_channels.Clear();
            }
        }
    }
}