using Constants;

namespace Battle {
    /// <summary>
    /// Client-side enemy stats by <see cref="BattleEnemyType"/> (server syncs type only).
    /// </summary>
    public static class BattleEnemyConfig {
        public static void GetDefaultHp(BattleEnemyType enemyType, out int currentHp, out int maxHp) {
            switch (enemyType) {
                case BattleEnemyType.BUBBLE_FISH:
                default:
                    currentHp = 30;
                    maxHp = 30;
                    break;
            }
        }

        public static float GetMoveSpeed(BattleEnemyType enemyType) {
            switch (enemyType) {
                case BattleEnemyType.BUBBLE_FISH:
                default:
                    return 2f;
            }
        }
    }
}
