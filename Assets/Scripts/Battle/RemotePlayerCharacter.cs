using UnityEngine;
using Network.Messages;

namespace Battle {
    [RequireComponent(typeof(Rigidbody2D))]
    public class RemotePlayerCharacter : PlayerCharacter<BattlePlayerEntity> {
        [SerializeField]
        private float m_lerpSpeed = 12f;

        private Vector2 m_targetPosition;
        private bool m_hasTarget;
        private Rigidbody2D m_rigidbody2D;

        protected override void InitPlayer(BattlePlayerEntity data) {
            m_rigidbody2D = GetComponent<Rigidbody2D>();
            _ApplyRemoteSnapshot(data, applyPosition: true);
        }

        public override void OnReceiveData(BattlePlayerEntity entityData) {
            _ApplyRemoteSnapshot(entityData, applyPosition: true);
        }

        protected override void FixedTickBehaviour() {
            if (!m_hasTarget || m_rigidbody2D == null) {
                return;
            }

            var next = Vector2.Lerp(m_rigidbody2D.position, m_targetPosition, m_lerpSpeed * Time.fixedDeltaTime);
            m_rigidbody2D.MovePosition(next);
        }

        public override void RemoveSelf() {
            Destroy(gameObject);
        }

        private void _ApplyRemoteSnapshot(BattlePlayerEntity entityData, bool applyPosition) {
            if (entityData?.position == null) {
                return;
            }

            m_targetPosition = entityData.position;
            m_hasTarget = true;
            if (applyPosition && m_rigidbody2D != null) {
                m_rigidbody2D.position = m_targetPosition;
            }
        }
    }
}
