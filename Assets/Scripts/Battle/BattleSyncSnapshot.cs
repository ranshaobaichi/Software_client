using Network.Messages;

namespace Battle {
    /// <summary>
    /// Thread-safe copy of data collected on the main thread for <see cref="BattleSyncUploader"/>.
    /// </summary>
    public sealed class BattleSyncSnapshot {
        private readonly object m_lock = new object();
        private BattleVector2 m_playerPosition;
        private bool m_hasPlayerPosition;
        private EnemyPositionEntry[] m_enemyPositions = System.Array.Empty<EnemyPositionEntry>();

        public void SetPlayerPosition(BattleVector2 position) {
            lock (m_lock) {
                m_playerPosition = position;
                m_hasPlayerPosition = position != null;
            }
        }

        public void SetEnemyPositions(EnemyPositionEntry[] entries) {
            lock (m_lock) {
                m_enemyPositions = entries ?? System.Array.Empty<EnemyPositionEntry>();
            }
        }

        public bool TryCopy(out BattleVector2 playerPosition, out EnemyPositionEntry[] enemyPositions) {
            lock (m_lock) {
                if (!m_hasPlayerPosition || m_playerPosition == null) {
                    playerPosition = null;
                    enemyPositions = System.Array.Empty<EnemyPositionEntry>();
                    return false;
                }

                playerPosition = new BattleVector2 {
                    x = m_playerPosition.x,
                    y = m_playerPosition.y
                };
                enemyPositions = m_enemyPositions ?? System.Array.Empty<EnemyPositionEntry>();
                return true;
            }
        }
    }
}
