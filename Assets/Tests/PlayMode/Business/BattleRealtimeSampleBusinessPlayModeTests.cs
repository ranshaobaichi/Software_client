using System.Collections;
using System.Text.RegularExpressions;
using Constants;
using Network;
using Network.Messages;
using NUnit.Framework;
using Tests.PlayMode.Network;
using UnityEngine;
using UnityEngine.TestTools;

namespace Tests.PlayMode.Business {
    /// <summary>
    /// PlayMode tests for the authoritative-style line protocol used by <see cref="SimpleNetworkClient2D"/>
    /// (<see cref="GameMessage"/> welcome/snapshot + <see cref="MoveMsg"/>), on an ephemeral port.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="UI.States.ShopState"/>, <see cref="UI.States.MapState"/>, and <see cref="Battle.BattleTEST_Singleton"/>
    /// currently contain only UI stack / scene navigation hooks—no <see cref="NetworkManager"/> traffic—so there are
    /// no separate “商店 / 地图” network business tests until those flows call the server.
    /// </para>
    /// </remarks>
    public class BattleRealtimeSampleBusinessPlayModeTests : BusinessPlayModeTestBase {
        [UnityTest]
        public IEnumerator BattleSample_WelcomeThenSnapshot_NormalFlow() {
            var server = new FakeServer(0);
            server.Start();
            try {
                string welcomeId = null;
                int snapshotVersions = 0;

                var ch = m_networkManager.CreateConnection(NetworkConstants.DefaultHost, server.Port);
                ch.RegisterHandler<GameMessage>(msg => {
                    if (msg == null || string.IsNullOrEmpty(msg.type))
                        return;
                    if (msg.type == "welcome")
                        welcomeId = msg.id;
                    if (msg.type == "snapshot" && msg.players != null)
                        snapshotVersions++;
                });

                ch.Connect();
                yield return WaitUntil(() => server.IsClientConnected, 2f, "Battle client did not connect.");

                server.SendObject(new GameMessage { type = "welcome", id = "peer-9" });
                yield return WaitUntil(() => welcomeId == "peer-9", 2f, "Welcome not processed.");

                server.SendObject(new GameMessage {
                    type = "snapshot",
                    players = new[] {
                        new NetPlayer { id = "peer-9", x = 0f, y = 0f },
                        new NetPlayer { id = "remote-a", x = 3f, y = 4f }
                    }
                });

                yield return WaitUntil(() => snapshotVersions == 1, 2f, "Snapshot not processed.");

                m_networkManager.RemoveConnection(ch);
            }
            finally {
                server.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator BattleSample_MovePayload_NormalFlow() {
            var server = new FakeServer(0);
            server.Start();
            try {
                var ch = m_networkManager.CreateConnection(NetworkConstants.DefaultHost, server.Port);
                ch.RegisterHandler<GameMessage>(_ => { });
                ch.Connect();
                yield return WaitUntil(() => server.IsClientConnected, 2f, "Battle client did not connect.");

                ch.Send(new MoveMsg { x = 12.5f, y = -3f });

                byte[] raw = null;
                yield return WaitUntil(() => server.TryDequeueReceived(out raw), 2f, "Move message not received.");

                var decoded = new JsonMessageSerializer().Deserialize(raw, typeof(MoveMsg)) as MoveMsg;
                Assert.NotNull(decoded);
                Assert.AreEqual("move", decoded.type);
                Assert.AreEqual(12.5f, decoded.x, 0.001f);
                Assert.AreEqual(-3f, decoded.y, 0.001f);

                m_networkManager.RemoveConnection(ch);
            }
            finally {
                server.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator BattleSample_SnapshotEmptyPlayers_Boundary() {
            var server = new FakeServer(0);
            server.Start();
            try {
                int snapshotCount = 0;
                var ch = m_networkManager.CreateConnection(NetworkConstants.DefaultHost, server.Port);
                ch.RegisterHandler<GameMessage>(msg => {
                    if (msg?.type == "snapshot" && msg.players != null)
                        snapshotCount++;
                });
                ch.Connect();
                yield return WaitUntil(() => server.IsClientConnected, 2f, "Battle client did not connect.");

                server.SendObject(new GameMessage {
                    type = "snapshot",
                    players = System.Array.Empty<NetPlayer>()
                });

                yield return WaitUntil(() => snapshotCount == 1, 2f, "Empty snapshot array should still dispatch.");

                m_networkManager.RemoveConnection(ch);
            }
            finally {
                server.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator BattleSample_SnapshotMissingPlayersField_NoSnapshotBranch_Boundary() {
            var server = new FakeServer(0);
            server.Start();
            try {
                int snapshotCount = 0;
                var ch = m_networkManager.CreateConnection(NetworkConstants.DefaultHost, server.Port);
                ch.RegisterHandler<GameMessage>(msg => {
                    if (msg?.type == "snapshot" && msg.players != null)
                        snapshotCount++;
                });
                ch.Connect();
                yield return WaitUntil(() => server.IsClientConnected, 2f, "Battle client did not connect.");

                server.SendFramedUtf8("{\"type\":\"snapshot\"}");

                yield return new WaitForSeconds(0.35f);
                Assert.AreEqual(0, snapshotCount,
                        "SimpleNetworkClient2D ignores snapshot frames without a players array.");

                m_networkManager.RemoveConnection(ch);
            }
            finally {
                server.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator BattleSample_MalformedJsonThenValidWelcome_Recovers_ErrorTolerance() {
            var server = new FakeServer(0);
            server.Start();
            try {
                string welcomeId = null;
                var ch = m_networkManager.CreateConnection(NetworkConstants.DefaultHost, server.Port);
                ch.RegisterHandler<GameMessage>(msg => {
                    if (msg?.type == "welcome")
                        welcomeId = msg.id;
                });
                ch.Connect();
                yield return WaitUntil(() => server.IsClientConnected, 2f, "Battle client did not connect.");

                LogAssert.Expect(LogType.Warning, new Regex(@"\[TcpConnectionChannel\] Deserialize/Dispatch failed:"));
                server.SendFramedUtf8("not-json");
                yield return new WaitForSeconds(0.2f);

                server.SendObject(new GameMessage { type = "welcome", id = "recover-1" });
                yield return WaitUntil(() => welcomeId == "recover-1", 2f, "Channel should accept frames after garbage.");

                m_networkManager.RemoveConnection(ch);
            }
            finally {
                server.Dispose();
            }
        }

        [UnityTest]
        public IEnumerator BattleSample_ConnectRefused_ErrorPath() {
            int deadPort = TakeReleasedEphemeralLoopbackPort();
            var ch = m_networkManager.CreateConnection(NetworkConstants.DefaultHost, deadPort);
            ch.RegisterHandler<GameMessage>(_ => { });
            LogAssert.Expect(LogType.Error, new Regex(@"\[TcpConnectionChannel\] Connect failed:"));
            ch.Connect();
            yield return null;
            Assert.False(ch.IsConnected);
            m_networkManager.RemoveConnection(ch);
        }
    }
}
