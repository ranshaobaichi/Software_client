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
        public void SendShortRequest<TRequest>(
                int port,
                TRequest request,
                string host = NetworkConstants.DefaultHost,
                Action<NetworkErrorMessage> onError = null,
                float timeoutSeconds = 5f,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        )
                where TRequest : ClientNetworkMessage {
            SendShortRequest<TRequest, ServerNetworkSuccessMessage, ServerNetworkFailMessage>
            (port, request, DefaultOnServiceSuccessAction, DefaultOnServiceFailAction, host,
                    timeoutSeconds, onError, framer, serializer);
        }

        public void SendShortRequestWithSuccess<TRequest, TSuccessResponse>(
                int port,
                TRequest request,
                Action<TSuccessResponse> onSuccess,
                string host = NetworkConstants.DefaultHost,
                Action<NetworkErrorMessage> onError = null,
                float timeoutSeconds = 5f,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        )
                where TRequest : ClientNetworkMessage
                where TSuccessResponse : ServerNetworkSuccessMessage {
            SendShortRequest<TRequest, TSuccessResponse, ServerNetworkFailMessage>
            (port, request, onSuccess, DefaultOnServiceFailAction, host,
                    timeoutSeconds, onError, framer, serializer);
        }
        
        public void SendShortRequestWithFailure<TRequest, TFailureResponse>(
                int port,
                TRequest request,
                Action<TFailureResponse> onFailure,
                string host = NetworkConstants.DefaultHost,
                Action<NetworkErrorMessage> onError = null,
                float timeoutSeconds = 5f,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        )
                where TRequest : ClientNetworkMessage
                where TFailureResponse : ServerNetworkFailMessage {
            SendShortRequest<TRequest, ServerNetworkSuccessMessage, TFailureResponse>
            (port, request, DefaultOnServiceSuccessAction, onFailure, host,
                    timeoutSeconds, onError, framer, serializer);
        }

        /// <summary>
        /// Send a request and wait for a single response, using a short-lived connection.
        /// Deserializes the server envelope (<see cref="ServerEnvelope"/>) first;
        /// on success, the inner <c>data</c> JSON is deserialized as <typeparamref name="TSuccessResponse"/>;
        /// </summary>
        public void SendShortRequest<TRequest, TSuccessResponse, TFailureResponse>(
                int port,
                TRequest request,
                Action<TSuccessResponse> onSuccess,
                Action<TFailureResponse> onFailure,
                string host = NetworkConstants.DefaultHost,
                float timeoutSeconds = 5f,
                Action<NetworkErrorMessage> onError = null,
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
            ch.RegisterHandler<ServerEnvelope>(envelope => {
                try {
                    lock (m_shortRequestLock) {
                        RemoveShortRequestPending(ch);
                    }

                    var code = (ServerCode)envelope.code;
                    if ((code & ServerCode.SERVICE_SUCCESS) != 0) {
                        try {
                            if (string.IsNullOrEmpty(envelope.data)) {
                                envelope.data = "{}";
                            }

                            byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(envelope.data);
                            var resp = s.Deserialize(dataBytes, typeof(TSuccessResponse)) as TSuccessResponse;
                            onSuccess?.Invoke(resp);
                        }
                        catch (Exception ex) {
                            onError?.Invoke(new NetworkErrorMessage {
                                    code = ServerCode.DESERIALIZE_ERROR,
                                    message = ex.Message
                            });
                        }
                    }
                    else if ((code & ServerCode.SERVICE_FAIL) != 0) {
                        try {
                            if (string.IsNullOrEmpty(envelope.data)) {
                                envelope.data = "{}";
                            }

                            byte[] dataBytes = System.Text.Encoding.UTF8.GetBytes(envelope.data);
                            var resp = s.Deserialize(dataBytes, typeof(TFailureResponse)) as TFailureResponse;
                            onFailure?.Invoke(resp);
                        }
                        catch (Exception ex) {
                            onError?.Invoke(new NetworkErrorMessage {
                                    code = ServerCode.DESERIALIZE_ERROR,
                                    message = ex.Message
                            });
                        }
                    }
                    else {
                        onError?.Invoke(new NetworkErrorMessage {
                                code = code,
                                message = envelope.message ?? "Unknown error"
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
                m_shortRequestPending.Add((ch, Time.time + timeoutSeconds, DefaultOnTimeoutAction));
            }

            ch.Connect();
            if (ch.IsConnected)
                ch.Send(request);
            else {
                RemoveConnection(ch);
                lock (m_shortRequestLock) {
                    RemoveShortRequestPending(ch);
                }

                DefaultOnErrorAction(new NetworkErrorMessage {
                        code = ServerCode.CONNECTION_ERROR,
                        message = "Failed to connect"
                });
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
    }
}