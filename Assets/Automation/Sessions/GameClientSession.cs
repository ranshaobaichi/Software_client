using System;
using System.Collections;
using Automation.Config;
using Automation.Util;

namespace Automation.Sessions {
    /// <summary>
    /// Aggregates Automation sessions for headless / test-harness flows.
    /// </summary>
    public class GameClientSession {
        public NetworkEndpointConfig Config { get; }
        public AuthSession Auth { get; }
        public LobbySession Lobby { get; }
        public ShopSession Shop { get; }
        public MapSession Map { get; }
        public BattleSession Battle { get; }

        public GameClientSession(NetworkEndpointConfig config = null) {
            Config = config ?? new NetworkEndpointConfig();
            Auth = new AuthSession(Config);
            Lobby = new LobbySession(Config);
            Shop = new ShopSession(Config);
            Map = new MapSession(Config);
            Battle = new BattleSession(Config);
        }

        public IEnumerator RegisterAndLogin(Action<string> onUid = null) {
            yield return Auth.RegisterAndLogin(onUid);
        }

        public IEnumerator LogoutSafely() {
            if (Lobby.IsInRoom)
                yield return Lobby.LeaveRoom();

            Shop.Disconnect();
            Map.Disconnect();
            Battle.Disconnect();

            yield return Auth.Logout();
        }

        /// <summary>
        /// Single-player minimal flow: lobby ready → shop skip → map default route → battle idle until end.
        /// </summary>
        public IEnumerator RunSinglePlayerFlow(int maximumPeople = 4, bool buyFirstShopItem = false) {
            string uid = null;
            yield return RegisterAndLogin(value => uid = value);

            yield return Lobby.CreateRoom(maximumPeople);
            yield return Lobby.SetReady(true);
            yield return Lobby.WaitForAllReady();

            if (buyFirstShopItem)
                yield return Shop.AutoBuyFirst();
            else
                yield return Shop.Skip();

            int roomId = Lobby.RoomId;
            yield return Map.InitMap(roomId);
            yield return Map.SelectDefaultRoute();
            yield return Map.WaitForMapCommit();

            yield return Battle.Connect();
            yield return Battle.PlayerReady();
            yield return Battle.WaitForBattleStart();
            yield return Battle.IdleUntilEnd();

            yield return LogoutSafely();
        }

        public void DisconnectAll() {
            Lobby.DisconnectLongConnection();
            Shop.Disconnect();
            Map.Disconnect();
            Battle.Disconnect();
        }
    }
}
