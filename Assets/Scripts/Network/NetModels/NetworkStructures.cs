using System;
using System.Collections.Generic;
using Constants;
using UnityEngine;

namespace Network.Messages {
    #region Battle Structures
    [Serializable]
    public class BattleInfo {
        public RoomInfo roomInfo;
        public BattlePlayerEntity playerInfo;
        public string mapNodeId;
    }

    [Serializable]
    public class BattleVector2 {
        public float x;
        public float y;

        public static implicit operator Vector2(BattleVector2 battleVector2) {
            if (battleVector2 == null) return Vector2.zero;
            return new Vector2(battleVector2.x, battleVector2.y);
        }

        public static implicit operator BattleVector2(Vector2 vector2) {
            return new BattleVector2 {
                x = vector2.x,
                y = vector2.y
            };
        }
    }

    [Serializable]
    public class BattleEntity {
        public int entityId;
        public BattleEntityType entityType;
        public BattleVector2 position;
        public BattleVector2 direction;
    }

    [Serializable]
    public class BattlePlayerAttribute {
        public int currentHP;
        public int maxHP;
        public float speed;
    }

    [Serializable]
    public class BattleBulletAttribute {
        public int speed;
        public int size;
    }
    [Serializable]
    public class BattlePlayerEntity : BattleEntity {
        public string uid;
        public BattlePlayerAttribute attribute;
        public string[] items;
    }

    [Serializable]
    public class BattleBulletEntity : BattleEntity {
        /// <summary>0 = self, 1 = other player, 2 = enemy</summary>
        public int type;
        public BattleBulletAttribute attribute;
    }

    [Serializable]
    public class BattleEnemyAttribute {
        public int currentHP;
        public int maxHP;
    }

    [Serializable]
    public class BattleEnemyEntity : BattleEntity {
        public BattleEnemyAttribute attribute;
        public BattleEnemyType enemyType;
        public string targetPlayerUid;
    }

    [Serializable]
    public class BattleEventSpawnParameter {
        public int entityId;
        public BattleEntityType entityType;

        public BattlePlayerEntity playerEntity;
        public BattleEnemyEntity enemyEntity;
        public BattleBulletEntity bulletEntity;
    }

    [Serializable]
    public class BattleEventHitParameter {
        public int sourceEntityId;
        public BattleEntityType sourceEntityType;

        public int targetEntityId;
        public BattleEntityType targetEntityType;

        public BattleVector2 hitPosition;
    }

    [Serializable]
    public class BattleEventDamageParameter {
        public int sourceEntityId = -1;
        public BattleEntityType sourceEntityType = BattleEntityType.NONE;

        public int targetEntityId;
        public BattleEntityType targetEntityType;

        public int damage;
        public int currentHP;
    }

    [Serializable]
    public class BattleEventDestroyParameter {
        public int entityId;
        public BattleEntityType entityType;

        public BattleEntityDestroyReason destroyReason;
    }

    [Serializable]
    public class BattleEventIntentParameter {
        public int enemyEntityId;
        public string targetPlayerUid;
    }

    /// <summary>
    /// 根据 eventType 只读取对应的 parameter，其余 parameter 为空。
    /// </summary>
    [Serializable]
    public class BattleEventDTO {
        public BattleEventType eventType;
        public int eventTick;

        public BattleEventSpawnParameter spawnParameter;
        public BattleEventHitParameter hitParameter;
        public BattleEventDamageParameter damageParameter;
        public BattleEventDestroyParameter destroyParameter;
        public BattleEventIntentParameter intentParameter;
    }

    #endregion
}