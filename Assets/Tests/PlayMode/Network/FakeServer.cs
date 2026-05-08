using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using Constants;
using Network;
using Network.Messages;

namespace Tests.PlayMode.Network {
    /// <summary>
    /// Lightweight TCP fake server for PlayMode network tests.
    /// Uses the same framer/serializer as production code to reduce protocol drift in tests.
    /// </summary>
    public sealed class FakeServer : IDisposable {
        private readonly IMessageFramer m_framer;
        private readonly IMessageSerializer m_serializer;
        private readonly ConcurrentQueue<byte[]> m_receivedMessages = new ConcurrentQueue<byte[]>();
        private readonly AutoResetEvent m_receivedEvent = new AutoResetEvent(false);
        private readonly ManualResetEventSlim m_clientConnectedEvent = new ManualResetEventSlim(false);
        private readonly object m_sendLock = new object();
        private readonly int m_listenPort;

        private TcpListener m_listener;
        private TcpClient m_client;
        private Stream m_clientStream;
        private Thread m_acceptThread;
        private Thread m_receiveThread;
        private volatile bool m_running;
        private bool m_disposed;

        /// <summary>Bound port after <see cref="Start"/>; for <c>port == 0</c> this is the OS-assigned ephemeral port.</summary>
        public int Port { get; private set; }

        public bool IsClientConnected => m_client?.Connected == true;

        public FakeServer(int port = 0, IMessageFramer framer = null, IMessageSerializer serializer = null) {
            m_listenPort = port;
            m_framer = framer ?? new LineFramer();
            m_serializer = serializer ?? new JsonMessageSerializer();
        }

        public void Start() {
            if (m_running) return;

            m_listener = new TcpListener(IPAddress.Loopback, m_listenPort);
            m_listener.Start();
            Port = ((IPEndPoint)m_listener.LocalEndpoint).Port;
            m_running = true;

            m_acceptThread = new Thread(AcceptLoop) { IsBackground = true };
            m_acceptThread.Start();
        }

        public bool WaitForClientConnected(int timeoutMs = 2000) {
            return m_clientConnectedEvent.Wait(timeoutMs);
        }

        public bool TryDequeueReceived(out byte[] payload) {
            return m_receivedMessages.TryDequeue(out payload);
        }

        public bool WaitForReceived(out byte[] payload, int timeoutMs = 2000) {
            if (m_receivedMessages.TryDequeue(out payload)) return true;
            if (!m_receivedEvent.WaitOne(timeoutMs)) {
                payload = null;
                return false;
            }

            return m_receivedMessages.TryDequeue(out payload);
        }

        public void SendRaw(byte[] payload) {
            if (payload == null || payload.Length == 0) return;
            if (!IsClientConnected || m_clientStream == null) return;

            lock (m_sendLock) {
                m_framer.WriteMessage(m_clientStream, payload);
            }
        }

        /// <summary>
        /// Sends one framed message. Unlike <see cref="SendRaw"/>, <paramref name="payload"/> may be empty
        /// (delimiter-only frame) for protocol edge-case tests.
        /// </summary>
        public void SendFramedPayload(byte[] payload) {
            if (!IsClientConnected || m_clientStream == null) return;

            lock (m_sendLock) {
                m_framer.WriteMessage(m_clientStream, payload ?? Array.Empty<byte>());
            }
        }

        /// <summary>
        /// UTF-8 text as the framed payload (no automatic newline — framing adds the delimiter).
        /// </summary>
        public void SendFramedUtf8(string text) {
            SendFramedPayload(text == null ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(text));
        }

        public void SendObject<T>(T payload) where T : class {
            byte[] raw = m_serializer.Serialize(payload);
            SendRaw(raw);
        }

        public void SendShortSuccess<TSuccess>(TSuccess data, string message = null)
                where TSuccess : class {
            SendObject(new ShortEnvelope<TSuccess> {
                    code = (int)ServerCode.SERVICE_SUCCESS,
                    data = data,
                    message = message
            });
        }

        public void SendShortFailure<TFail>(TFail data, string message = null)
                where TFail : class {
            SendObject(new ShortEnvelope<TFail> {
                    code = (int)ServerCode.SERVICE_FAIL,
                    data = data,
                    message = message
            });
        }

        private void AcceptLoop() {
            try {
                while (m_running) {
                    TcpClient accepted = m_listener.AcceptTcpClient();
                    if (!m_running) {
                        try {
                            accepted?.Close();
                        }
                        catch {
                            // ignored
                        }

                        break;
                    }

                    ReplaceClient(accepted);
                }
            }
            catch (SocketException) {
                // Listener closed on Dispose.
            }
            catch (ObjectDisposedException) {
                // Listener closed on Dispose.
            }
        }

        private void ReplaceClient(TcpClient newClient) {
            if (!m_running) {
                try {
                    newClient?.Close();
                }
                catch {
                    // ignored
                }

                return;
            }

            CloseClient();
            m_client = newClient;
            m_clientStream = m_client.GetStream();
            if (m_running)
                m_clientConnectedEvent.Set();

            m_receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            m_receiveThread.Start();
        }

        private void ReceiveLoop() {
            try {
                while (m_running && m_clientStream != null) {
                    if (m_framer.TryReadMessage(m_clientStream, out byte[] message) && message != null) {
                        m_receivedMessages.Enqueue(message);
                        if (m_running)
                            m_receivedEvent.Set();
                    }
                    else if (!m_clientStream.CanRead) {
                        break;
                    }
                }
            }
            catch (IOException) {
                // Client disconnected.
            }
            catch (ObjectDisposedException) {
                // Stream closed on Dispose.
            }
        }

        private void CloseClient() {
            try {
                m_clientStream?.Close();
            }
            catch {
                // ignored
            }

            try {
                m_client?.Close();
            }
            catch {
                // ignored
            }

            m_clientStream = null;
            m_client = null;
        }

        public void Dispose() {
            if (m_disposed)
                return;
            m_disposed = true;

            m_running = false;

            try {
                m_listener?.Stop();
            }
            catch {
                // ignored
            }

            TryJoin(m_acceptThread);
            m_acceptThread = null;

            CloseClient();

            TryJoin(m_receiveThread);
            m_receiveThread = null;

            m_listener = null;
            m_receivedEvent.Dispose();
            m_clientConnectedEvent.Dispose();
        }

        private static void TryJoin(Thread thread) {
            if (thread == null || !thread.IsAlive)
                return;
            thread.Join(millisecondsTimeout: 10000);
        }
    }
}
