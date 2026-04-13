using System;
using System.Collections.Generic;
using UnityEngine;

namespace Constants {
    public static class NetworkConstants {
        public const string DefaultHost = "127.0.0.1";
        public const int LoginPort = 22222;
        public const int HomePort = 22223;
        public const int ShopPort = 22224;
        public const int MapPort = 22225;
        public const int BattlePort = 22226;
    }

    [Flags]
    public enum ServerCode {
        // service success
        SUCCESS = 1,

        // service fail
        FAIL = 1 << 1,

        // system error
        ERROR = 1 << 2,
        TIME_OUT = 1 << 3,
        DESERIALIZE_ERROR = 1 << 4,
        CONNECTION_ERROR = 1 << 5,

        SERVICE_SUCCESS = SUCCESS,
        SERVICE_FAIL = FAIL,
        SYSTEM_ERROR = ERROR | TIME_OUT | DESERIALIZE_ERROR | CONNECTION_ERROR,
    }

    #region Client Message Types

    public enum SampleState2MessageType {
        SUCCESS = 0,
        FAIL = 1,
    }

    #endregion

    #region PlayerData Service

    public class PlayerAvatarColor {
        public string uid;
        public AvatarColorID color;
    }

    public static class AvatarColorConfig {
        private static readonly Dictionary<AvatarColorID, Color> s_colors = new() {
                { AvatarColorID.WHITE, Color.white },
                { AvatarColorID.RED, Color.red },
                { AvatarColorID.GREEN, Color.green },
                { AvatarColorID.BLUE, Color.blue },
                { AvatarColorID.YELLOW, Color.yellow },
                { AvatarColorID.MAGENTA, Color.magenta },
                { AvatarColorID.CYAN, Color.cyan }
        };

        public static Color GetColor(AvatarColorID id) {
            return s_colors.TryGetValue(id, out var color)
                    ? color
                    : Color.white;
        }
    }


    #endregion

    #region Login Service

    public enum LoginRequestType {
        LOGIN = 0,
        REGISTER = 1,
        LOGOUT = 2,
    }

    #endregion
    #region Avatar Color

    [Serializable]
    public enum AvatarColorID {
        WHITE = 0,
        RED = 1,
        GREEN = 2,
        BLUE = 3,
        YELLOW = 4,
        MAGENTA = 5,
        CYAN = 6
    }

    #endregion

    #region Home Service

    public enum HomeRequestType {
        CREATE_ROOM = 0,
        JOIN_ROOM = 1,
        LEAVE_ROOM = 2,
        LIST_ROOMS = 3,
        SEND_MESSAGE = 4,
        HEARTBEAT = 5,
        EDIT_PROFILE = 6,
        SET_READY = 7,
        BROADCAST = 8
    }

    #endregion
    #region Map Service

    public enum MapRequestType {
        MAP_INIT = 0,
        MAP_MOVE = 1,
    };

    public enum MapResponseType {
        MAP_INIT = 0,
        MAP_SYNC = 1,
    };

    public enum NodeType {
        NORMAL = 0,
        ELITE = 1,
        EVENT = 2,
        BOSS = 3
    }



    #endregion
    #region Shop Service

    public enum ShopRequestType {
        SHOP_INIT = 0,
        SHOP_MOVE_CURSOR = 1,
        SHOP_BUY_ITEM = 2,
    };

    public enum ShopResponseType {
        SHOP_SYNC = 0,
    };

    #endregion

    #region Room Service
    public enum RoomPushMessage {
        ALL_PLAYERS_READY = 0,
    }
    #endregion
    
    #region Battle Service
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
    #endregion
}
