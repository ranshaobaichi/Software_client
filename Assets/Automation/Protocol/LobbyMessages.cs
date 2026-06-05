using System;
using Network.Messages;

namespace Automation.Protocol {
    [Serializable]
    public class GetStateStatusRequest : ClientNetworkMessage {
        public string uid;
    }

    /// <summary>
    /// GET_STATE_STATUS (type=9) success payload on Home port (8766).
    /// See server docs/TESTING.md.
    /// </summary>
    [Serializable]
    public class GetStateStatusResponse : ServerNetworkSuccessMessage {
        public bool online;
        public int roomId = -1;
        public int roomPhase;
        public int roomMemberCount;
        public bool allLobbyReady;
        public int mapNodeId = -1;
        public int battleTick;
    }
}
