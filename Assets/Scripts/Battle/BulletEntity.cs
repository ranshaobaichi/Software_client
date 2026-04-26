using UnityEngine;
using Network.Messages;

namespace Battle
{
    public class BulletEntity : Entity<BattleBulletEntity>
    {
        private Vector2 m_direction;
        private float m_speed;

        protected override void Init(BattleBulletEntity entityData)
        {
            base.Init(entityData);

            m_direction = entityData.direction ?? Vector2.right;
            m_speed = entityData.attribute.speed;

            if (m_direction.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(m_direction.y, m_direction.x) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }
            BattleSession.SInstance?.BulletRegistry?.Register(this);
        }

        protected override void FixedTickBehaviour()
        {
            transform.position += (Vector3)(m_direction * m_speed * Time.fixedDeltaTime);
        }

        public override void OnReceiveData(BattleBulletEntity entityData)
        {
            if (entityData?.position == null)
                return;

            transform.position = new Vector3(
                    entityData.position.x,
                    entityData.position.y,
                    0f
            );

            if (entityData.direction != null)
            {
                Vector2 dir = entityData.direction;

                if (dir.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }
        }

        public override void RemoveSelf()
        {
            BattleSession.SInstance?.BulletRegistry?.Unregister(EntityId);
            Destroy(gameObject);
        }
    }
}