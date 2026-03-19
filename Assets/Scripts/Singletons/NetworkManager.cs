using UnityEngine;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Network {
    /// <summary>
    /// 全局网络单例：承担全部网络职责，支持多连接，每连接独立接收线程，主线程统一派发。
    /// 不假定帧格式与序列化方式，通过可注入的 Framer/Serializer 扩展；客户端自行处理各连接时序。
    /// </summary>
    public class NetworkManager : MonoBehaviour {
        #region Singleton
        private static NetworkManager s_instance;
        private static readonly object s_instanceLock = new object();

        public static NetworkManager SInstance {
            get {
                if (s_instance == null) {
                    lock (s_instanceLock) {
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
        private IMessageFramer _defaultFramer;
        private IMessageSerializer _defaultSerializer;

        private readonly List<INetworkChannel> _channels = new List<INetworkChannel>();
        private readonly object _channelsLock = new object();

        private readonly List<(INetworkChannel channel, float deadline, Action onTimeout)> _shortRequestPending =
                new List<(INetworkChannel, float, Action)>();

        private readonly object _shortRequestLock = new object();
        #endregion

        #region Unity Lifecycle
        private void Awake() {
            if (s_instance != null && s_instance != this) {
                Destroy(gameObject);
                return;
            }

            s_instance = this;
            DontDestroyOnLoad(gameObject);
            _defaultFramer ??= new LineFramer();
            _defaultSerializer ??= new JsonMessageSerializer();
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
            var framerToUse = framer ?? _defaultFramer ?? new LineFramer();
            var serializerToUse = serializer ?? _defaultSerializer ?? new JsonMessageSerializer();
            var channel = new TcpConnectionChannel(host, port, framerToUse, serializerToUse);
            lock (_channelsLock) {
                _channels.Add(channel);
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
            lock (_channelsLock) {
                _channels.Remove(channel);
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
            var f = framer ?? _defaultFramer ?? new LineFramer();
            var s = serializer ?? _defaultSerializer ?? new JsonMessageSerializer();
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
        /// </summary>
        /// <typeparam name="TRequest">request type</typeparam>
        /// <typeparam name="TResponse">response type</typeparam>
        public void SendShortRequest<TRequest, TResponse>(
                string host,
                int port,
                TRequest request,
                Action<TResponse> onResponse,
                float timeoutSeconds = 5f,
                Action onTimeout = null,
                IMessageFramer framer = null,
                IMessageSerializer serializer = null
        )
                where TRequest : class
                where TResponse : class {
            var f = framer ?? _defaultFramer ?? new LineFramer();
            var s = serializer ?? _defaultSerializer ?? new JsonMessageSerializer();
            var ch = new TcpConnectionChannel(host, port, f, s);

            ch.RegisterHandler<TResponse>(resp => {
                lock (_shortRequestLock) {
                    RemoveShortRequestPending(ch);
                }

                onResponse?.Invoke(resp);
                RemoveConnection(ch);
            });

            lock (_channelsLock) {
                _channels.Add(ch);
            }

            lock (_shortRequestLock) {
                _shortRequestPending.Add((ch, Time.time + timeoutSeconds, onTimeout));
            }

            ch.Connect();
            if (ch.IsConnected)
                ch.Send(request);
            else {
                RemoveConnection(ch);
                lock (_shortRequestLock) {
                    RemoveShortRequestPending(ch);
                }

                onTimeout?.Invoke();
            }
        }
        #endregion

        private void RemoveShortRequestPending(INetworkChannel ch) {
            for (int i = _shortRequestPending.Count - 1; i >= 0; i--) {
                if (_shortRequestPending[i].channel == ch) {
                    _shortRequestPending.RemoveAt(i);
                    return;
                }
            }
        }

        private void PruneShortRequestTimeouts() {
            float now = Time.time;
            List<(INetworkChannel ch, Action onTimeout)> toRemove = null;
            lock (_shortRequestLock) {
                for (int i = _shortRequestPending.Count - 1; i >= 0; i--) {
                    if (now < _shortRequestPending[i].deadline) continue;
                    if (toRemove == null) toRemove = new List<(INetworkChannel, Action)>();
                    toRemove.Add((_shortRequestPending[i].channel, _shortRequestPending[i].onTimeout));
                    _shortRequestPending.RemoveAt(i);
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
            List<INetworkChannel> copy;
            lock (_channelsLock) {
                copy = new List<INetworkChannel>(_channels);
            }

            foreach (var ch in copy)
                ch.DispatchPendingMessages();
        }

        private void DisconnectAll() {
            lock (_channelsLock) {
                foreach (var ch in _channels)
                    ch.Disconnect();
                _channels.Clear();
            }
        }
    }
}