using UnityEngine;
using Constants;
using Network;
using Network.Messages;

namespace Battle {
    /// <summary>
    /// Local player: position authority on client; reports via <see cref="BattleSyncUploader"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    public class LocalPlayerCharacter : PlayerCharacter<BattlePlayerEntity> {
        [Header("Movement")]
        [SerializeField]
        private float m_defaultSpeed = 50f;

        [Header("Shooting")]
        [SerializeField]
        private float m_shootRateHz = 10f;
        [SerializeField]
        private GameObject m_bulletPrefab;

        [SerializeField]
        private float m_bulletSpeed = 10f;
        private Vector2 m_currentInput;
        private float m_moveSpeed;
        private int m_currentHp;
        private int m_maxHp;
        private Rigidbody2D m_rigidbody2D;
        private float m_nextShootTime;
        private float m_shootInterval;

        protected override void InitPlayer(BattlePlayerEntity data) {
            m_rigidbody2D = GetComponent<Rigidbody2D>();
            m_shootInterval = 1f / Mathf.Max(1f, m_shootRateHz);
            m_nextShootTime = Time.time;
            _ApplyAttributes(data);
            BattleSession.SInstance?.RegisterLocalPlayer(this);
        }

        public override void OnReceiveData(BattlePlayerEntity entityData) {
            _ApplyAttributes(entityData);
        }

        protected override void TickBehaviour() {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            m_currentInput = new Vector2(x, y).normalized;

            if (Input.GetButton("Fire1") && Time.time >= m_nextShootTime) {
                m_nextShootTime = Time.time + m_shootInterval;
                _SendShoot();
            }
        }

        protected override void FixedTickBehaviour() {
            if (m_rigidbody2D == null || m_currentInput.sqrMagnitude < 0.0001f) {
                return;
            }

            var delta = m_currentInput * (m_moveSpeed * Time.fixedDeltaTime);
            m_rigidbody2D.MovePosition(m_rigidbody2D.position + delta);
        }

        public BattleVector2 GetReportPosition() {
            var p = m_rigidbody2D != null ? m_rigidbody2D.position : (Vector2)transform.position;
            return new BattleVector2 { x = p.x, y = p.y };
        }

        public void PublishSyncSnapshot(BattleSyncSnapshot snapshot) {
            if (snapshot == null) {
                return;
            }

            snapshot.SetPlayerPosition(GetReportPosition());
        }

        public override void RemoveSelf() {
            BattleSession.SInstance?.UnregisterLocalPlayer(this);
            Destroy(gameObject);
        }

        private void _ApplyAttributes(BattlePlayerEntity entityData) {
            if (entityData?.attribute != null) {
                m_moveSpeed = entityData.attribute.speed > 0f ? entityData.attribute.speed : m_defaultSpeed;
                if (entityData.attribute.maxHP > 0) {
                    m_maxHp = entityData.attribute.maxHP;
                    m_currentHp = entityData.attribute.currentHP;
                }
            } else {
                m_moveSpeed = m_defaultSpeed;
            }
        }

        private void _SendShoot() {
            var session = BattleSession.SInstance;
            var channel = session?.Channel;

            if (channel == null || !channel.IsConnected)
                return;

            Vector2 dir = m_currentInput.sqrMagnitude > 0.0001f
                    ? m_currentInput
                    : (Vector2)transform.right;


            Vector2 playerPos = transform.position;


            var enemyArray = session.EnemyRegistry.GetAllReportPositions();
            var enemyPositions = new EnemyPositionEntry[enemyArray.Length];

            for (int i = 0; i < enemyArray.Length; i++) {
                enemyPositions[i] = new EnemyPositionEntry {
                        entityId = enemyArray[i].entityId,
                        position = enemyArray[i].position
                };
            }

   
            channel.Send(new PlayerShootRequest {
                    type = (int)BattleRequestType.PLAYER_SHOOT,
                    uid = Uid,
                    direction = new BattleVector2 { x = dir.x, y = dir.y },
                    playerPosition = new BattleVector2 { x = playerPos.x, y = playerPos.y },
                    enemyPositions = enemyPositions
            });

            session.ReportPacketSent();
        }
    }
}
