using UnityEngine;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace Network {
    /// <summary>
    /// Specific TCP channel implementation, using the long connection pattern
    /// </summary>
    public class TcpConnectionChannel : INetworkChannel {
        public bool IsConnected => _running && _client?.Connected == true;
        public event Action OnConnected;
        public event Action OnDisconnected;
        
        private readonly string _host;
        private readonly int _port;
        private readonly IMessageFramer _framer;
        private readonly IMessageSerializer _serializer;

        private TcpClient _client;
        private Stream _stream;
        private Thread _receiveThread;
        private volatile bool _running;
        private readonly object _sendLock = new object();

        private readonly ConcurrentQueue<byte[]> _incoming = new ConcurrentQueue<byte[]>();
        private Type _handlerType;
        private Delegate _handlerCallback;
        private readonly object _handlerLock = new object();
        
        public TcpConnectionChannel(string host, int port, IMessageFramer framer, IMessageSerializer serializer) {
            _host = host ?? Constants.NetworkConstants.DefaultHost;
            _port = port;
            _framer = framer ?? new LineFramer();
            _serializer = serializer ?? new JsonMessageSerializer();
        }

        public void Connect() {
            if (_running) return;
            try {
                _client = new TcpClient();
                _client.Connect(_host, _port);
                _stream = _client.GetStream();
                _running = true;
                _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
                _receiveThread.Start();
                OnConnected?.Invoke();
            }
            catch (Exception ex) {
                Debug.LogError("[TcpConnectionChannel] Connect failed: " + ex.Message);
                _running = false;
            }
        }

        /// <summary>
        /// Disconnect the channel and clean up resources.
        /// After disconnection, the channel cannot be reused; create a new instance to connect again.
        /// [Remember to set the channel variable to null after disconnection to avoid accidental reuse.]
        /// </summary>
        public void Disconnect() {
            _running = false;
            try {
                _client?.Close();
            }
            catch {
                // ignored
            }

            try {
                _stream?.Close();
            }
            catch {
                // ignored
            }

            try {
                _receiveThread?.Join(500);
            }
            catch {
                // ignored
            }

            _stream = null;
            _client = null;
            OnDisconnected?.Invoke();
        }

        /// <summary>
        /// Send a message of any serializable type, using the long connection.
        /// Serialization method is determined by the implementation (e.g. JSON).
        /// </summary>
        /// <param name="payload"></param>
        /// <typeparam name="T"></typeparam>
        public void Send<T>(T payload) where T : class {
            if (_stream == null || _serializer == null) return;
            lock (_sendLock) {
                try {
                    byte[] raw = _serializer.Serialize(payload);
                    if (raw != null)
                        _framer.WriteMessage(_stream, raw);
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
            lock (_handlerLock) {
                _handlerType = typeof(T);
                _handlerCallback = callback;
            }
        }

        public void ClearHandler() {
            lock (_handlerLock) {
                _handlerType = null;
                _handlerCallback = null;
            }
        }

        public void DispatchPendingMessages() {
            Type type;
            Delegate callback;
            lock (_handlerLock) {
                type = _handlerType;
                callback = _handlerCallback;
            }

            if (type == null || callback == null) {
                while (_incoming.TryDequeue(out _)) { }

                return;
            }

            while (_incoming.TryDequeue(out byte[] data)) {
                if (data == null || data.Length == 0) continue;
                try {
                    object obj = _serializer.Deserialize(data, type);
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
                while (_running && _stream != null) {
                    if (_framer.TryReadMessage(_stream, out byte[] message) && message != null)
                        _incoming.Enqueue(message);
                    else if (!_stream.CanRead)
                        break;
                }
            }
            catch (Exception ex) {
                if (_running)
                    Debug.LogWarning("[TcpConnectionChannel] Receive error: " + ex.Message);
            }
            finally {
                _running = false;
            }
        }
    }
}