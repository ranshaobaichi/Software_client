using UnityEngine;
using Constants;
using Network.Messages;

namespace Battle {
    [RequireComponent(typeof(Rigidbody2D))]
    public class EnemyCharacter : Entity<BattleEnemyEntity> {
        private string m_targetPlayerUid;
        private float m_moveSpeed = 2f;
        private int m_currentHp;
        private int m_maxHp;
        private Rigidbody2D m_rigidbody2D;

        public string TargetPlayerUid => m_targetPlayerUid;

        protected override void Init(BattleEnemyEntity entityData) {
            base.Init(entityData);
            m_rigidbody2D = GetComponent<Rigidbody2D>();
            if (entityData.attribute != null && entityData.attribute.maxHP > 0) {
                m_currentHp = entityData.attribute.currentHP;
                m_maxHp = entityData.attribute.maxHP;
            } else {
                BattleEnemyConfig.GetDefaultHp(entityData.enemyType, out m_currentHp, out m_maxHp);
            }

            m_targetPlayerUid = entityData.targetPlayerUid;
            m_moveSpeed = BattleEnemyConfig.GetMoveSpeed(entityData.enemyType);
            BattleSession.SInstance?.EnemyRegistry?.Register(this);
        }

        public override void OnReceiveData(BattleEnemyEntity entityData) {
            if (entityData == null) {
                return;
            }

            if (!string.IsNullOrEmpty(entityData.targetPlayerUid)) {
                m_targetPlayerUid = entityData.targetPlayerUid;
            }
        }

        public void SetTargetPlayerUid(string uid) {
            m_targetPlayerUid = uid;
        }

        public void ApplyDamage(int damage, int currentHpFromEvent) {
            if (currentHpFromEvent >= 0) {
                m_currentHp = currentHpFromEvent;
            } else {
                m_currentHp = Mathf.Max(0, m_currentHp - damage);
            }
        }

        public BattleVector2 GetReportPosition() {
            var p = transform.position;
            return new BattleVector2 { x = p.x, y = p.y };
        }

        protected override void FixedTickBehaviour() {
            if (string.IsNullOrEmpty(m_targetPlayerUid)) {
                return;
            }

            var session = BattleSession.SInstance;
            if (session == null || !session.TryGetPlayerWorldPosition(m_targetPlayerUid, out var targetPos)) {
                return;
            }

            var current = m_rigidbody2D != null ? m_rigidbody2D.position : (Vector2)transform.position;
            var next = Vector2.MoveTowards(current, targetPos, m_moveSpeed * Time.fixedDeltaTime);
            if (m_rigidbody2D != null) {
                m_rigidbody2D.MovePosition(next);
            } else {
                transform.position = new Vector3(next.x, next.y, 0f);
            }
        }

        public override void RemoveSelf() {
            BattleSession.SInstance?.EnemyRegistry?.Unregister(EntityId);
            Destroy(gameObject);
        }
    }
}
