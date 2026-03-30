using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Network {
    public interface INetworkChannel {
        bool IsConnected { get; }
        void Connect();
        void Disconnect();
        void Send<T>(T payload) where T : class;
        void RegisterHandler<T>(Action<T> callback) where T : class;
        void ClearHandler();
        void DispatchPendingMessages();
    }

    /// <summary>
    /// Specific TCP channel implementation, using the long connection pattern
    /// </summary>
    public class TcpConnectionChannel : INetworkChannel {
        public bool IsConnected => Interlocked.CompareExchange(ref m_connected, 0, 0) == 1 && m_client?.Connected == true;
        public event Action OnConnected;
        public event Action OnDisconnected;

        private readonly string m_host;
        private readonly int m_port;
        private readonly IMessageFramer m_framer;
        private readonly IMessageSerializer m_serializer;

        private TcpClient m_client;
        private Stream m_stream;
        private Thread m_receiveThread;

        private volatile bool m_running;

        // 1 = connected, 0 = not connected; use Interlocked to read-and-clear atomically
        private int m_connected;
        private readonly object m_sendLock = new object();

        private readonly ConcurrentQueue<byte[]> m_incoming = new ConcurrentQueue<byte[]>();
        private Type m_handlerType;
        private Delegate m_handlerCallback;
        private readonly object m_handlerLock = new object();

        public TcpConnectionChannel(string host, int port, IMessageFramer framer, IMessageSerializer serializer) {
            m_host = host ?? Constants.NetworkConstants.DefaultHost;
            m_port = port;
            m_framer = framer ?? new LineFramer();
            m_serializer = serializer ?? new JsonMessageSerializer();
        }

        public void Connect() {
            if (m_running) return;
            try {
                m_client = new TcpClient();
                m_client.Connect(m_host, m_port);
                m_stream = m_client.GetStream();
                m_connected = 1;
                m_running = true;
                m_receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                m_receiveThread.Start();
                OnConnected?.Invoke();
            }
            catch (Exception ex) {
                Debug.LogError("[TcpConnectionChannel] Connect failed: " + ex.Message);
                m_running = false;
                m_connected = 0;
            }
        }

        /// <summary>
        /// Disconnect the channel and clean up resources.
        /// After disconnection, the channel cannot be reused; create a new instance to connect again.
        /// [Remember to set the channel variable to null after disconnection to avoid accidental reuse.]
        /// </summary>
        public void Disconnect() {
            bool wasConnected = Interlocked.Exchange(ref m_connected, 0) == 1;
            m_running = false;
            try {
                m_client?.Close();
            }
            catch {
                // ignored
            }

            try {
                m_stream?.Close();
            }
            catch {
                // ignored
            }

            try {
                m_receiveThread?.Join(500);
            }
            catch {
                // ignored
            }

            m_stream = null;
            m_client = null;
            if (wasConnected)
                OnDisconnected?.Invoke();
        }

        /// <summary>
        /// Send a message of any serializable type, using the long connection.
        /// Serialization method is determined by the implementation (e.g. JSON).
        /// </summary>
        /// <param name="payload"></param>
        /// <typeparam name="T"></typeparam>
        public void Send<T>(T payload) where T : class {
            lock (m_sendLock) {
                if (m_stream == null || m_serializer == null) return;
                try {
                    byte[] raw = m_serializer.Serialize(payload);
                    if (raw != null)
                        m_framer.WriteMessage(m_stream, raw);
                }
                catch (Exception ex) {
                    Debug.LogWarning("[TcpConnectionChannel] Send failed: " + ex.Message);
                }
            }
        }

        /// <summary>
        ///  Register a handler for incoming messages: when a data packet is received,
        /// the entire content will be deserialized as the specified type T and the callback will be invoked.
        /// Only one handler is retained; repeated registration will overwrite the previous one.
        /// </summary>
        /// <typeparam name="T">required can be serialized</typeparam>
        public void RegisterHandler<T>(Action<T> callback) where T : class {
            if (callback == null) return;
            lock (m_handlerLock) {
                m_handlerType = typeof(T);
                m_handlerCallback = callback;
            }
        }

        public void ClearHandler() {
            lock (m_handlerLock) {
                m_handlerType = null;
                m_handlerCallback = null;
            }
        }

        public void DispatchPendingMessages() {
            Type type;
            Delegate callback;
            lock (m_handlerLock) {
                type = m_handlerType;
                callback = m_handlerCallback;
            }

            if (type == null || callback == null) {
                while (m_incoming.TryDequeue(out _)) { }

                return;
            }

            while (m_incoming.TryDequeue(out byte[] data)) {
                if (data == null || data.Length == 0) continue;
                try {
                    object obj = m_serializer.Deserialize(data, type);
                    if (obj != null)
                        callback.DynamicInvoke(obj);
                }
                catch (Exception ex) {
                    Debug.LogWarning("[TcpConnectionChannel] Deserialize/Dispatch failed: " + ex.Message);
                }
            }
        }

        private void ReceiveLoop() {
            try {
                while (m_running && m_stream != null) {
                    if (m_framer.TryReadMessage(m_stream, out byte[] message) && message != null)
                        m_incoming.Enqueue(message);
                    else if (!m_stream.CanRead)
                        break;
                }
            }
            catch (Exception ex) {
                if (m_running)
                    Debug.LogWarning("[TcpConnectionChannel] Receive error: " + ex.Message);
            }
            finally {
                bool wasConnected = Interlocked.Exchange(ref m_connected, 0) == 1;
                m_running = false;
                if (wasConnected)
                    OnDisconnected?.Invoke();
            }
        }
    }
}