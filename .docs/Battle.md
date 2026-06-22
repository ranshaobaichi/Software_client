# 战斗系统设计说明

## 1. 目标与非目标

**目标**

- 在 **BattleScene** 内实现与服务端 Tick 驱动的帧同步战斗表现。
- 客户端负责：本地输入采集、位置上报、远端实体插值、事件驱动的实体生成/销毁/伤害表现。
- 通过 `BattleSession` 统一管理长连接、帧队列、同步上报与实体生命周期。
- 战斗前的 **商店 / 地图** 阶段仍走 StateEngine（`ShopState`、`MapState`），与实时战斗逻辑解耦。

**非目标**

- 客户端不做碰撞、伤害、胜负等权威判定（由服务端 Tick 计算并通过 `BattleFrameResponse.events` 下发）。
- 不做客户端预测与回滚（本地玩家位置以客户端输入为准，远端玩家以服务端快照为准）。
- 战斗 UI 状态（商店、地图）的 MVVM 细节见各自 State / View 实现，本文不展开。

---

## 2. 与服务端的关系

```
┌─────────────────────────────────────────────────────────────┐
│  服务端（权威）                                               │
│  Tick 循环 → 碰撞 / 伤害 / 生成 / 销毁 → BattleFrameResponse │
└───────────────────────────┬─────────────────────────────────┘
                            │ 长连接 BattlePort (22226)
                            ▼
┌─────────────────────────────────────────────────────────────┐
│  客户端（表现）                                               │
│  BattleSession → BattleFrameQueue → 事件 / 实体同步           │
│  LocalPlayerCharacter → 输入 / 射击请求 / 位置快照             │
│  BattleSyncUploader → PositionSyncRequest（后台线程）         │
└─────────────────────────────────────────────────────────────┘
```

Room 阶段状态机（服务端）：`LOBBY → SHOP → MAP → BATTLE → END`。客户端通过不同端口的长连接跟随各阶段，详见 [`docs/architecture-client-server.puml`](../docs/architecture-client-server.puml)。

---

## 3. 场景与流程

### 3.1 从大厅到战斗

| 步骤 | 位置 | 行为 |
|------|------|------|
| 1 | `HomeScene` / `OnlineRoomState` | 收到 `ALL_PLAYERS_READY` 推送后，`PageController` 切换到 `BattlePage` |
| 2 | `BattleScene` / `ShopState` | 连接 ShopPort，同步商店数据 |
| 3 | `BattleScene` / `MapState` | 连接 MapPort，展示地图、选点 |
| 4 | `MapView` 点击节点 | `GameSceneManager.SwitchScene(BATTLE)` 重载 BattleScene |
| 5 | `BattleScene` / `BattleSession` | 连接 BattlePort，进入就绪 → 开战 → 帧同步循环 |

### 3.2 BattleInfo 传递

进入战斗前可通过静态方法写入上下文：

```csharp
BattleSession.SetPendingBattleInfo(new BattleInfo {
    mapNodeId = "...",
    roomInfo = ...,
    playerInfo = ...
});
```

`BattleSession.Awake` 时读取并清空 `s_pendingBattleInfo`，目前主要消费 `mapNodeId`。调用方（如地图选点逻辑）在切场景前应设置此项。

### 3.3 BattleSession 生命周期

| 阶段 | 触发 | 行为 |
|------|------|------|
| **Awake** | 场景加载 | 单例注册、应用 BattleInfo、初始化 LosePanel、创建 Channel / SyncUploader |
| **OnEnable** | 对象激活 | 连接 Channel、启动 `_ReadyLoop`（每秒发送 `PLAYER_READY`） |
| **就绪中** | 收到 `BATTLE_WAIT` | 更新 `readyCount/totalCount`、记录 `gameFrame` 为 `ServerFrameRate` |
| **开战** | Push `BATTLE_START` | 隐藏等待 UI、启动 `BattleSyncUploader` |
| **战斗中** | 每帧 `Update` | 排空 `BattleFrameQueue`、刷新包率 UI |
| **战斗中** | 每帧 `LateUpdate` | 本地玩家写入 `BattleSyncSnapshot`、收集敌人位置 |
| **结束** | Push `BATTLE_END` | 显示失败面板、停止同步、清理实体、断开连接、切回 Home |
| **OnDisable / OnDestroy** | 离开场景 | 停止 Ready 协程、断开 Channel、释放 SyncUploader |

---

## 4. 网络协议

连接：`NetworkConstants.DefaultHost` + `NetworkConstants.BattlePort`（22226），长连接。

### 4.1 客户端请求（BattleRequestType）

| 类型 | 类 | 发送方 | 说明 |
|------|-----|--------|------|
| `PLAYER_READY = 0` | `PlayerReadyRequest` | `_ReadyLoop` | 开战前每秒上报就绪 |
| `POSITION_SYNC = 1` | `PositionSyncRequest` | `BattleSyncUploader` | 含玩家位置 + 敌人位置数组 |
| `PLAYER_SHOOT = 2` | `PlayerShootRequest` | `LocalPlayerCharacter` | 含方向、玩家位置、敌人位置 |

### 4.2 服务端响应（BattleResponseType）

| 类型 | 类 | 处理 |
|------|-----|------|
| `BATTLE_WAIT = 0` | `BattleWaitResponse` | 更新等待 UI（`readyCount`、`totalCount`、`gameFrame`） |
| `BATTLE_FRAME = 1` | `BattleFrameResponse` | 入队 `BattleFrameQueue`（仅 `m_isGameStart == true` 时） |

### 4.3 服务端推送（BattlePushMessageType）

| 类型 | 处理 |
|------|------|
| `BATTLE_START = 0` | 标记开战、启动位置同步 |
| `BATTLE_END = 1` | 触发战斗结束流程 |

### 4.4 BattleFrameResponse 结构

```csharp
public class BattleFrameResponse {
    public int serverTick;
    public List<BattlePlayerEntity> playerEntities;
    public List<BattleEnemyEntity> enemyEntities;
    public List<BattleBulletEntity> bulletEntities;
    public List<BattleEventDTO> events;
}
```

每帧处理顺序（`_ApplyBattleFrame`）：

1. 遍历 `events`，按 `eventType` 分发
2. `_BootstrapEnemiesFromFrame`：补全帧内已有但未实例化的敌人
3. `_SyncPlayerEntities`：增删改玩家实体

---

## 5. 帧事件（BattleEventType）

| eventType | 参数 | 客户端行为 |
|-----------|------|------------|
| `ENEMY_SPAWN` | `spawnParameter` | `BattleEnemySpawner.SpawnFromEvent` |
| `BULLET_SPAWN` | `spawnParameter` | 实例化 `BulletEntity`（防重复 entityId） |
| `ENEMY_INTENT_CHANGE` | `intentParameter` | 更新敌人追击目标 UID |
| `ENTITY_DAMAGE` | `damageParameter` | 敌人调用 `ApplyDamage` |
| `ENTITY_DESTROY` | `destroyParameter` | 按 entityId 销毁玩家/敌人/子弹 |
| `BULLET_HIT_*` | `hitParameter` | 移除来源子弹实体 |

事件 DTO 定义见 `Network.Messages.BattleEventDTO`，枚举见 `Constants.BattleEventType`。

---

## 6. 实体体系

### 6.1 类层次

```
IEntity
  └── Entity<TEntityData>          # 通用 Init / ReceiveData / Tick
        └── PlayerCharacter<T>     # 持有 Uid
              ├── LocalPlayerCharacter   # 本地：输入驱动 + 射击 + 快照上报
              └── RemotePlayerCharacter  # 远端：Lerp 跟随服务端位置
        ├── EnemyCharacter         # 追击目标玩家、注册 EnemyRegistry
        └── BulletEntity           # 按方向匀速移动、注册 BulletRegistry
```

### 6.2 本地 vs 远端玩家

| | LocalPlayerCharacter | RemotePlayerCharacter |
|--|---------------------|----------------------|
| 位置来源 | 本地 `Rigidbody2D` + WASD 输入 | 服务端 `BattlePlayerEntity.position` |
| 同步方式 | `PublishSyncSnapshot` → `PositionSyncRequest` | `OnReceiveData` → Lerp 到目标点 |
| 射击 | `Fire1` → `PlayerShootRequest` | 无（子弹由服务端 `BULLET_SPAWN` 事件生成） |
| 帧快照 | 收到帧数据时更新属性，**不覆盖本地位置** | 每帧应用服务端位置 |

### 6.3 注册表

| 类 | 职责 |
|----|------|
| `BattleEnemyRegistry` | entityId → `EnemyCharacter`；提供 `GetAllReportPositions` 供射击/同步 |
| `BattleBulletRegistry` | entityId → `BulletEntity`；支持 `SyncFromFrame`（当前主流程以事件驱动为主） |

敌人预制体：`BattleSession` Inspector 配置 `_enemyEntityPrefab` 与 `_enemyPrefabsByType[]`（按 `BattleEnemyType` 映射）。客户端 HP / 移速兜底见 `BattleEnemyConfig`。

---

## 7. 位置同步

### 7.1 数据流

```
LateUpdate (主线程)
  LocalPlayerCharacter.PublishSyncSnapshot(snapshot)
  snapshot.SetEnemyPositions(EnemyRegistry.GetAllReportPositions())
        │
        ▼
BattleSyncUploader (后台线程, 默认 20Hz)
  snapshot.TryCopy → PositionSyncRequest → Channel.Send
```

### 7.2 线程安全

| 组件 | 说明 |
|------|------|
| `BattleSyncSnapshot` | `lock` 保护，`TryCopy` 供后台线程读取 |
| `BattleFrameQueue` | `ConcurrentQueue`，网络线程入队、主线程 `DrainAll` |
| `NetworkManager.Update` | 所有 Channel 回调最终在主线程执行 |

`BattleSyncUploader` 在 `BATTLE_START` 后 `Start()`，战斗结束或 `OnDestroy` 时 `Stop()` / `Dispose()`。

---

## 8. 核心类职责

| 类 | 路径 | 职责 |
|----|------|------|
| `BattleSession` | `Scripts/Battle/` | 战斗入口单例：连接、就绪、帧消费、实体管理、结束流程 |
| `BattleFrameQueue` | 同上 | 线程安全帧队列 |
| `BattleSyncSnapshot` | 同上 | 主线程收集 → 后台线程读取的位置快照 |
| `BattleSyncUploader` | 同上 | 定频发送 `PositionSyncRequest` |
| `BattleEnemySpawner` | 同上 | 从事件或帧数据生成敌人 |
| `BattleEnemyRegistry` | 同上 | 敌人索引与意图变更 |
| `BattleBulletRegistry` | 同上 | 子弹索引 |
| `BattleEnemyConfig` | 同上 | 按 `BattleEnemyType` 的客户端兜底数值 |

协议 struct / enum：

- 消息类：`Assets/Scripts/Network/NetModels/ServerNetworkMessages.cs`
- 数据结构：`Assets/Scripts/Network/NetModels/NetworkStructures.cs`
- 命令字：`Assets/Scripts/Constants/NetworkConstants.cs`、`BattleConstants.cs`

---

## 9. 调试

定义 Scripting Define Symbol **`BATTLE_NET_LOG`** 后启用战斗网络 trace：

| 输出 | 路径 / 方式 |
|------|-------------|
| Console | `[BattleNet]` 前缀 |
| Wire 日志 | `Assets/Logs/battle_net_wire_{timestamp}.log` |
| 序列化日志 | `Assets/Logs/battle_net_serialized_{timestamp}.log` |

相关类：`Network.BattleNetTrace`、`Network.NetworkPacketLog`、`Battle.BattleNetLog`。

`Assets/Logs/` 已加入 `.gitignore`，不会提交版本库。

---

## 10. 扩展指南

### 10.1 新增敌人类型

1. 在 `BattleEnemyType` 枚举中添加类型
2. 在 `BattleEnemyConfig` 中补充 HP / 移速
3. 在 `BattleSession._enemyPrefabsByType` 配置对应预制体
4. 预制体根节点挂 `EnemyCharacter`（或子类）

### 10.2 新增帧事件类型

1. 服务端与 `BattleEventType` 对齐
2. 在 `NetworkStructures.cs` 添加 parameter 类（若需要）
3. 在 `BattleSession._ApplyEvent` 的 `switch` 中增加分支

### 10.3 调整同步频率

修改 `BattleSession` Inspector 的 `m_positionSyncRateHz`（默认 20 Hz），开战时传入 `BattleSyncUploader.SetSendRateHz`。

---

## 11. 错误与边界

| 场景 | 行为 |
|------|------|
| Channel 未连接 | Ready 循环跳过发送；SyncUploader 线程 sleep 后继续 |
| 重复 `ENEMY_SPAWN` / `BULLET_SPAWN` entityId | 忽略实例化，仅应用快照或跳过 |
| 帧中玩家列表为空 | `_DestroyAllPlayers` |
| 帧中某 player entityId 消失 | 销毁对应实体 |
| 战斗结束清理 | 先停 SyncUploader → 清队列 → 分步销毁实体（避免 foreach 中 Clear） |
| `BattleSession` 重复实例 | `Awake` 中 Destroy 多余实例 |

---

## 12. 测试要点

1. **EditMode**：Network 层 MessageFramer / Serializer / LongConnection 分发（见 `Assets/Tests/EditMode/Network/`）。
2. **PlayMode**：`BattleRealtimeSampleBusinessPlayModeTests` 覆盖简易 line protocol 样本（与正式 Battle 长连接协议独立）。
3. **联机调试**：可使用 `Plugins/ParrelSync` 多开 Editor 实例；正式流程需配合 C++ 服务端各端口服务。

战斗协议对齐见飞书「战斗」模块文档，代码路径映射见 [`config/wiki-registry.yaml`](../config/wiki-registry.yaml)。
