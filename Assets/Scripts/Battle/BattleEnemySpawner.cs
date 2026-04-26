using System;
using Constants;
using Network.Messages;
using UnityEngine;

namespace Battle {
    [Serializable]
    public struct BattleEnemyPrefabEntry {
        public BattleEnemyType enemyType;
        public GameObject prefab;
    }

    /// <summary>
    /// Creates enemy instances from <see cref="BattleEventType.ENEMY_SPAWN"/> or bootstrap frame data.
    /// </summary>
    public sealed class BattleEnemySpawner {
        private readonly GameObject m_defaultPrefab;
        private readonly BattleEnemyPrefabEntry[] m_prefabsByType;
        private readonly Transform m_parent;

        public BattleEnemySpawner(GameObject defaultPrefab, BattleEnemyPrefabEntry[] prefabsByType, Transform parent) {
            m_defaultPrefab = defaultPrefab;
            m_prefabsByType = prefabsByType;
            m_parent = parent;
        }

        public EnemyCharacter SpawnFromEvent(BattleEventSpawnParameter spawn, BattleEnemyRegistry registry) {
            var data = ResolveEnemyData(spawn);
            if (data == null) {
                BattleNetLog.Log("ENEMY_SPAWN ignored: spawnParameter has no enemy data.");
                return null;
            }

            return Spawn(data, registry);
        }

        public EnemyCharacter Spawn(BattleEnemyEntity data, BattleEnemyRegistry registry) {
            if (data == null) {
                return null;
            }

            if (data.entityId == 0) {
                Debug.LogWarning("[BattleEnemySpawner] ENEMY_SPAWN ignored: entityId is 0.");
                return null;
            }

            if (registry != null && registry.TryGet(data.entityId, out var existing) && existing != null) {
                existing.ReceiveData(data);
                BattleNetLog.Log($"ENEMY_SPAWN duplicate entityId={data.entityId}, applied snapshot only.");
                return existing;
            }

            var prefab = ResolvePrefab(data.enemyType);
            if (prefab == null) {
                Debug.LogError("[BattleEnemySpawner] No enemy prefab assigned on BattleSession.");
                return null;
            }

            var go = UnityEngine.Object.Instantiate(prefab, m_parent);
            go.name = $"Enemy_{data.entityId}_{data.enemyType}";
            ApplySpawnTransform(go.transform, data);

            var enemy = go.GetComponent<EnemyCharacter>() ?? go.AddComponent<EnemyCharacter>();
            enemy.InitIfNot(data);

            BattleNetLog.Log(
                    $"ENEMY_SPAWN entityId={data.entityId} type={data.enemyType} " +
                    $"pos=({data.position?.x:F2},{data.position?.y:F2}) target={data.targetPlayerUid}"
            );
            return enemy;
        }

        public static BattleEnemyEntity ResolveEnemyData(BattleEventSpawnParameter spawn) {
            if (spawn == null) {
                return null;
            }

            BattleEnemyEntity data = spawn.enemyEntity;
            if (data == null && spawn.entityType == BattleEntityType.ENEMY) {
                data = new BattleEnemyEntity {
                    entityId = spawn.entityId,
                    entityType = spawn.entityType
                };
            }

            if (data == null) {
                return null;
            }

            if (data.entityId == 0 && spawn.entityId != 0) {
                data.entityId = spawn.entityId;
            }

            if (data.entityType == BattleEntityType.NONE && spawn.entityType != BattleEntityType.NONE) {
                data.entityType = spawn.entityType;
            }

            if (data.entityType == BattleEntityType.NONE) {
                data.entityType = BattleEntityType.ENEMY;
            }

            return data;
        }

        private GameObject ResolvePrefab(BattleEnemyType enemyType) {
            if (m_prefabsByType != null) {
                foreach (var entry in m_prefabsByType) {
                    if (entry.prefab != null && entry.enemyType == enemyType) {
                        return entry.prefab;
                    }
                }
            }

            return m_defaultPrefab;
        }

        private static void ApplySpawnTransform(Transform transform, BattleEnemyEntity data) {
            if (transform == null || data?.position == null) {
                return;
            }

            transform.position = new Vector3(data.position.x, data.position.y, 0f);
        }
    }
}
