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
                            var go = new GameObject("[NetworkManager]");
                            s_instance = go.AddComponent<NetworkManager>();
                            DontDestroyOnLoad(go);
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

        private readonly List<INetworkChannel> m_channels = new();
        private readonly object m_channelsLock = new();
        private readonly List<INetworkChannel> m_pumpScratch = new();

        private readonly List<(INetworkChannel channel, float deadline, Action onTimeout)> m_shortRequestPending =
                new();

        private readonly object m_shortRequestLock = new();
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
            PumpAll();
            PruneShortRequestTimeouts();
        }
        #endregion

        /// <summary>
        /// Common connection factory method.
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
        /// <summary>
        /// Send a single message to the specified host/port using a short-lived connection.
        /// </summary>
        public void SendShort<T>(
                string host,
                int port,
                T payload,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        ) where T : class {
            var f = framer ?? m_defaultFramer ?? new LineFramer();
            var s = serializer ?? m_defaultSerializer ?? new JsonMessageSerializer();
            ThreadPool.QueueUserWorkItem(_ => {
                try {
                    var ch = new TcpConnectionChannel(host, port, f, s);
                    ch.Connect();
                    if (ch.IsConnected) {
                        ch.Send(payload);
                    }

                    ch.Disconnect();
                }
                catch (Exception ex) {
                    Debug.LogWarning("[NetworkManager] SendShort failed: " + ex.Message);
                }
            });
        }

        /// <summary>
        /// Send a request and wait for a single response, using a short-lived connection.
        /// Deserializes the server envelope (<see cref="ServerEnvelope"/>) first;
        /// on success, the inner <c>data</c> JSON is deserialized as <typeparamref name="TResponse"/>;
        /// on failure, <paramref name="onError"/> is called with the populated <see cref="ErrorResponse"/>.
        /// </summary>
        /// <typeparam name="TRequest">request type</typeparam>
        /// <typeparam name="TResponse">business payload type, carried in <c>ServerEnvelope.data</c></typeparam>
        public void SendShortRequest<TRequest, TResponse>(
                string host,
                int port,
                TRequest request,
                Action<TResponse> onResponse,
                Action<ErrorResponse> onError = null,
                float timeoutSeconds = 5f,
                Action onTimeout = null,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        )
                where TRequest : class
                where TResponse : class {
            var f = framer ?? m_defaultFramer ?? new LineFramer();
            var s = serializer ?? m_defaultSerializer ?? new JsonMessageSerializer();
            var ch = new TcpConnectionChannel(host, port, f, s);
            onTimeout ??= DefaultOnTimeoutAction;

            ch.RegisterHandler<ServerEnvelope>(envelope => {
                try {
                    lock (m_shortRequestLock) {
                        RemoveShortRequestPending(ch);
                    }

                    if (envelope.status) {
                        try {
                            if (string.IsNullOrEmpty(envelope.data)) {
                                envelope.data = "{}";
                            }

                            byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(envelope.data);
                            var resp = s.Deserialize(dataBytes, typeof(TResponse)) as TResponse;
                            onResponse?.Invoke(resp);
                        }
                        catch (Exception ex) {
                            onError?.Invoke(new ErrorResponse {
                                    status = false,
                                    errorCode = (int)NetworkConstants.ErrorCode.CLIENT_DESERIALIZE_ERROR,
                                    message = ex.Message
                            });
                        }
                    }
                    else {
                        onError?.Invoke(new ErrorResponse {
                                status = false,
                                errorCode = envelope.errorCode,
                                message = envelope.message
                        });
                    }
                }
                finally {
                    RemoveConnection(ch);
                }
            });

            lock (m_channelsLock) {
                m_channels.Add(ch);
            }

            lock (m_shortRequestLock) {
                m_shortRequestPending.Add((ch, Time.time + timeoutSeconds, onTimeout));
            }

            ch.Connect();
            if (ch.IsConnected)
                ch.Send(request);
            else {
                RemoveConnection(ch);
                lock (m_shortRequestLock) {
                    RemoveShortRequestPending(ch);
                }

                onTimeout.Invoke();
            }
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

        private void PruneShortRequestTimeouts() {
            float now = Time.time;
            List<(INetworkChannel ch, Action onTimeout)> toRemove = null;
            lock (m_shortRequestLock) {
                for (int i = m_shortRequestPending.Count - 1; i >= 0; i--) {
                    if (now < m_shortRequestPending[i].deadline) continue;
                    if (toRemove == null) toRemove = new List<(INetworkChannel, Action)>();
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
            }
            finally {
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

        private void DefaultOnTimeoutAction() {
            Debug.Log("[NetworkManager] Default Action On Timeout");
        }
    }
}