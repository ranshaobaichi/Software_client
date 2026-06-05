namespace Automation.Protocol {
    /// <summary>
    /// Home/Lobby opcodes not yet in game <see cref="Constants.HomeRequestType"/>.
    /// </summary>
    public enum LobbyRequestType {
        GET_STATE_STATUS = 9,
    }

    /// <summary>
    /// Room phase values returned by GET_STATE_STATUS (aligns with server Room::Phase).
    /// </summary>
    public enum RoomPhase {
        LOBBY = 0,
        SHOP = 1,
        MAP = 2,
        BATTLE = 3,
        END = 4,
    }
}
