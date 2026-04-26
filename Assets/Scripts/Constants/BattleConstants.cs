namespace Constants {
    public enum BattleEventType {
        ENEMY_SPAWN = 0,
        BULLET_SPAWN = 1,

        BULLET_HIT_ENEMY = 10,
        BULLET_HIT_PLAYER = 11,
        BULLET_HIT_WALL = 12,

        ENTITY_DAMAGE = 20,
        ENTITY_DESTROY = 21,

        ENEMY_INTENT_CHANGE = 30,
    }

    public enum BattleEntityDestroyReason {
        NONE = 0,

        BULLET_HIT_ENTITY = 1,
        BULLET_HIT_WALL = 2,

        ENTITY_DEAD = 10,
    }

    public enum BattleEntityType {
        PLAYER = 0,
        ENEMY = 1,

        PLAYER_BULLET = 10,
        ENEMY_BULLET = 11,

        WALL = 20,

        NONE = 999,
    }

    public enum BattleEnemyType {
        BUBBLE_FISH = 0,
    }
}