using System.Collections.Generic;
using Network.Messages;

namespace Battle {
    public sealed class BattleBulletRegistry {
        private readonly Dictionary<int, BulletEntity> m_bullets = new Dictionary<int, BulletEntity>();

        public void Register(BulletEntity bullet) {
            if (bullet == null) {
                return;
            }

            m_bullets[bullet.EntityId] = bullet;
        }

        public void Unregister(int entityId) {
            m_bullets.Remove(entityId);
        }

        public bool TryGet(int entityId, out BulletEntity bullet) {
            return m_bullets.TryGetValue(entityId, out bullet);
        }

        public void SyncFromFrame(List<BattleBulletEntity> bulletEntities) {
            if (bulletEntities == null || bulletEntities.Count == 0) {
                var toRemove = new List<int>(m_bullets.Keys);
                foreach (var id in toRemove) {
                    if (m_bullets.TryGetValue(id, out var bullet)) {
                        bullet?.RemoveSelf();
                    }
                }

                m_bullets.Clear();
                return;
            }

            var frameIds = new HashSet<int>();
            foreach (var data in bulletEntities) {
                if (data == null) {
                    continue;
                }

                frameIds.Add(data.entityId);
                if (m_bullets.TryGetValue(data.entityId, out var existing)) {
                    existing.ReceiveData(data);
                }
            }

            var removeList = new List<int>();
            foreach (var id in m_bullets.Keys) {
                if (!frameIds.Contains(id)) {
                    removeList.Add(id);
                }
            }

            foreach (var id in removeList) {
                if (m_bullets.TryGetValue(id, out var bullet)) {
                    bullet?.RemoveSelf();
                }

                m_bullets.Remove(id);
            }
        }

        public void Clear() {
            var bullets = new List<BulletEntity>(m_bullets.Values);

            m_bullets.Clear();

            foreach (var bullet in bullets) {
                bullet?.RemoveSelf();
            }
        }
    }
}
