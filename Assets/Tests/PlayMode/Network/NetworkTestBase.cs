using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Network;

namespace Tests.PlayMode.Network {
    /// <summary>
    /// Shared setup/teardown and utility helpers for network PlayMode tests.
    /// </summary>
    public abstract class NetworkPlayModeTestBase {
        protected NetworkManager m_networkManager;
        protected FakeServer m_fakeServer;

        [UnitySetUp]
        public virtual IEnumerator SetUp() {
            m_networkManager = UnityEngine.Object.FindObjectOfType<NetworkManager>();
            if (m_networkManager == null)
                m_networkManager = global::Network.NetworkManager.SInstance;

            yield return null;
        }

        [UnityTearDown]
        public virtual IEnumerator TearDown() {
            m_fakeServer?.Dispose();
            m_fakeServer = null;

            if (m_networkManager != null) {
                UnityEngine.Object.Destroy(m_networkManager.gameObject);
                m_networkManager = null;
            }

            yield return null;
        }

        /// <summary>
        /// Binds an ephemeral TCP port on loopback, then releases it. A subsequent connect to the returned port
        /// should be refused when nothing else has bound it since release (stronger than probing with a temporary listener).
        /// </summary>
        protected static int TakeReleasedEphemeralLoopbackPort() {
            var server = new FakeServer(0);
            server.Start();
            try {
                return server.Port;
            }
            finally {
                server.Dispose();
            }
        }

        protected FakeServer StartFakeServer(int? port = null) {
            m_fakeServer?.Dispose();
            int listenPort = port ?? 0;
            m_fakeServer = new FakeServer(listenPort);
            m_fakeServer.Start();
            return m_fakeServer;
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
