using System.Collections;
using Constants;
using Network;
using Tests.PlayMode.Network;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Business {
    /// <summary>
    /// PlayMode business tests: fixed login/home ports, dual fake servers, PlayerData reset, Toast harness.
    /// </summary>
    public abstract class BusinessPlayModeTestBase : NetworkPlayModeTestBase {
        protected FakeServer m_homeFakeServer;

        protected FakeServer StartHomeFakeServer() {
            m_homeFakeServer?.Dispose();
            m_homeFakeServer = new FakeServer(NetworkConstants.HomePort);
            m_homeFakeServer.Start();
            return m_homeFakeServer;
        }

        /// <summary>
        /// Mimics post-login <see cref="PlayerData"/> used by room UI requests.
        /// </summary>
        protected static void SeedLoggedInPlayer(string uid, string displayName = "LobbyTester", int color = 3) {
            PlayerData.Init(new PlayerData {
                basicInfo = new PlayerData.PlayerBasicInfo(uid, displayName ,(AvatarColorID)color)
            });
        }

        [UnitySetUp]
        public override IEnumerator SetUp() {
            PlayerData.Clear();
            ToastTestHarness.EnsureInstalled();
            yield return base.SetUp();
        }

        [UnityTearDown]
        public override IEnumerator TearDown() {
            m_homeFakeServer?.Dispose();
            m_homeFakeServer = null;
            PlayerData.Clear();

            var sceneManagers = Object.FindObjectsOfType<GameSceneManager>();
            for (var i = 0; i < sceneManagers.Length; i++)
                Object.Destroy(sceneManagers[i].gameObject);

            IEnumerator parent = base.TearDown();
            while (parent.MoveNext())
                yield return parent.Current;
        }
    }
}
