using Network.Messages;

namespace Battle {
    /// <summary>
    /// 玩家角色的抽象中间层。
    /// 持有玩家专属的 Uid，并将 Entity 的两个公共接口（Init / OnSnapshot）
    /// 各自强转为 BattlePlayerEntity 后转发给强类型的叶子接口（InitPlayer / SyncData）。
    /// </summary>
    public abstract class PlayerCharacter<TPlayerEntity> : Entity<TPlayerEntity> where TPlayerEntity : BattlePlayerEntity {
        public string Uid { get; protected set; }

        protected override void Init(TPlayerEntity entityData) {
            base.Init(entityData);
            Uid = entityData.uid;
            InitPlayer(entityData);
        }
        
        /// <summary>
        /// 叶子类实现：接收强类型玩家数据，执行首次生成时的专属初始化逻辑。
        /// 调用时 InitBase 已执行（EntityId、Uid、位置已就绪）。
        /// </summary>
        protected abstract void InitPlayer(TPlayerEntity data);
    }
}
