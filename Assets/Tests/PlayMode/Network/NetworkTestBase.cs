using System;
using System.Collections;
using System.Net;
using System.Net.Sockets;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Network;

namespace Tests.PlayMode.Network {
    /// <summary>
    /// Shared setup/teardown and utility helpers for network PlayMode tests.
    /// </summary>
    public abstract class NetworkPlayModeTestBase {
        protected NetworkManager NetworkManager;
        protected FakeServer FakeServer;

        [UnitySetUp]
        public virtual IEnumerator SetUp() {
            NetworkManager = UnityEngine.Object.FindObjectOfType<NetworkManager>();
            if (NetworkManager == null)
                NetworkManager = NetworkManager.SInstance;

            yield return null;
        }

        [UnityTearDown]
        public virtual IEnumerator TearDown() {
            FakeServer?.Dispose();
            FakeServer = null;

            if (NetworkManager != null) {
                UnityEngine.Object.Destroy(NetworkManager.gameObject);
                NetworkManager = null;
            }

            yield return null;
        }

        protected int ReserveLocalPort() {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        protected FakeServer StartFakeServer(int? port = null) {
            int targetPort = port ?? ReserveLocalPort();
            FakeServer = new FakeServer(targetPort);
            FakeServer.Start();
            return FakeServer;
        }

        protected IEnumerator WaitUntil(Func<bool> predicate, float timeoutSeconds = 2f, string timeoutMessage = null) {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (Time.realtimeSinceStartup < deadline) {
                if (predicate())
                    yield break;
                yield return null;
            }

            Assert.Fail(timeoutMessage ?? "Condition not met before timeout.");
        }
    }
}
