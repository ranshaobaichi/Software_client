using System;
using Network.Messages;

namespace Automation.Protocol {
    [Serializable]
    public class BattleVector2 {
        public float x;
        public float y;

        public BattleVector2() { }

        public BattleVector2(float x, float y) {
            this.x = x;
            this.y = y;
        }
    }

    [Serializable]
    public class BattlePlayerReadyRequest : ClientNetworkMessage {
        public string uid;
    }

    [Serializable]
    public class BattlePositionSyncRequest : ClientNetworkMessage {
        public string uid;
        public BattleVector2 playerPosition;
        public BattleVector2 playerDirection;
        public BattleEnemyPositionEntry[] enemyPositions;
    }

    [Serializable]
    public class BattleEnemyPositionEntry {
        public int entityId;
        public BattleVector2 position;
        public BattleVector2 direction;
    }

    [Serializable]
    public class BattlePlayerShootRequest : ClientNetworkMessage {
        public string uid;
        public BattleVector2 direction;
    }

    [Serializable]
    public class BattleWaitResponseData {
        public int gameFrame;
        public int readyCount;
        public int totalCount;
    }

    [Serializable]
    public class BattleFrameResponseData {
        public int serverTick;
    }
}
