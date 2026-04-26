using Constants;
using UnityEngine;
using Network.Messages;

namespace Battle {
    public interface IEntity {
        int EntityId { get; } 
        BattleEntityType EntityType { get; }
        void InitIfNot(BattleEntity data);
        void ReceiveData(object data);
        void RemoveSelf();
    }
    
    /// <summary>
    /// 战斗场景中所有可同步实体的抽象基类。
    /// Init / OnSnapshot 均为 public abstract，派生类按实体类型自行强转后处理。
    /// InitBase 提供公共字段（EntityId、位置）的初始化，供派生类 override 时调用。
    /// </summary>
    public abstract class Entity<TEntityData> : MonoBehaviour, IEntity where TEntityData : BattleEntity {
        public int EntityId { get; protected set; }
        public BattleEntityType EntityType { get; private set; }

        private bool m_isInit;

        #region Interface Implementation
        public void InitIfNot(BattleEntity data) {
            if (m_isInit) {
                return;
            }

            if (data is not TEntityData entityData) {
                return;
            }

            Init(entityData);
            m_isInit = true;
        }
        public void ReceiveData(object data) {
            if (data is not TEntityData entityData) {
                return;
            }
            
            OnReceiveData(entityData);
        }
        public virtual void RemoveSelf() {
            Destroy(gameObject);
        }
        #endregion
        
        #region Unity Lifecycle & Forwarding
        protected virtual void TickBehaviour() {}
        protected virtual void FixedTickBehaviour() {}

        private void Update() {
            if (!m_isInit) {
                return;
            }

            TickBehaviour();
        }

        private void FixedUpdate() {
            if (!m_isInit) {
                return;
            }

            FixedTickBehaviour();
        }
        #endregion

        /// <summary>
        /// 实体首次生成时由 BattleSession 调用。
        /// 派生类负责将 BattleEntity 强转为具体子类型（如 BattlePlayerEntity）后执行初始化。
        /// </summary>
        protected virtual void Init(TEntityData entityData) {
            EntityId = entityData.entityId;
            EntityType = entityData.entityType;
            if (entityData.position != null)
                transform.position = new Vector3(entityData.position.x, entityData.position.y, 0f);
        }

        /// <summary>
        /// 每帧由 BattleSession 推入服务端快照。
        /// 派生类负责将裸 object 强转为具体子类型后执行同步逻辑。
        /// </summary>
        public abstract void OnReceiveData(TEntityData entityData);
    }
}