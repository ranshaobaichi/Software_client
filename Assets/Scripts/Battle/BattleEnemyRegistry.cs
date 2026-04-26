using System.Collections.Generic;
using Network.Messages;

namespace Battle {
    public sealed class BattleEnemyRegistry {
        private readonly Dictionary<int, EnemyCharacter> m_enemies = new Dictionary<int, EnemyCharacter>();

        public void Register(EnemyCharacter enemy) {
            if (enemy == null) {
                return;
            }

            m_enemies[enemy.EntityId] = enemy;
        }

        public void Unregister(int entityId) {
            m_enemies.Remove(entityId);
        }

        public bool TryGet(int entityId, out EnemyCharacter enemy) {
            return m_enemies.TryGetValue(entityId, out enemy);
        }

        public EnemyPositionEntry[] GetAllReportPositions() {
            if (m_enemies.Count == 0) {
                return System.Array.Empty<EnemyPositionEntry>();
            }

            var list = new List<EnemyPositionEntry>(m_enemies.Count);
            foreach (var (_, enemy) in m_enemies) {
                if (enemy == null) {
                    continue;
                }

                list.Add(new EnemyPositionEntry {
                        entityId = enemy.EntityId,
                        position = enemy.GetReportPosition()
                });
            }

            return list.ToArray();
        }

        public void ApplyIntentChange(BattleEventIntentParameter intent) {
            if (intent == null) {
                return;
            }

            if (m_enemies.TryGetValue(intent.enemyEntityId, out var enemy) && enemy != null) {
                enemy.SetTargetPlayerUid(intent.targetPlayerUid);
            }
        }

        public void Clear() {
            var enemies = new List<EnemyCharacter>(m_enemies.Values);

            m_enemies.Clear();

            foreach (var enemy in enemies) {
                enemy?.RemoveSelf();
            }
        }
    }
}
