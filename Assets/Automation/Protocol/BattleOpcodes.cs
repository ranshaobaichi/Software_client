namespace Automation.Protocol {
    public enum BattleRequestType {
        PLAYER_READY = 0,
        POSITION_SYNC = 1,
        PLAYER_SHOOT = 2,
    }

    public enum BattleResponseType {
        BATTLE_WAIT = 0,
        BATTLE_FRAME = 1,
    }

    public enum BattlePushMessageType {
        BATTLE_START = 0,
        BATTLE_END = 1,
    }
}
