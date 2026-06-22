# Software Client

基于 Unity 的联机游戏客户端，采用 C# 开发。核心能力包括：栈式 UI 状态机、MVVM 数据绑定、模态对话框系统、TCP 短/长连接网络层，以及与服务端帧同步的战斗表现层。

---

## 目录

- [环境要求](#环境要求)
- [项目结构](#项目结构)
- [程序集划分](#程序集划分)
- [架构概览](#架构概览)
  - [场景与页面层](#场景与页面层)
  - [状态机引擎](#状态机引擎)
  - [MVVM 与对话框](#mvvm-与对话框)
  - [网络层](#网络层)
  - [战斗层](#战斗层)
- [场景与业务流程](#场景与业务流程)
- [测试](#测试)
- [文档](#文档)
- [协议协作](#协议协作)
- [代码规范](#代码规范)

---

## 环境要求

| 项 | 版本 |
|----|------|
| Unity Editor | **2022.3.28f1** |
| 主要依赖 | TextMeshPro、Unity Test Framework、2D Feature、Rider IDE 集成 |

使用 Unity Hub 打开项目根目录即可。首次打开后由 Unity 自动生成 `Library/`、`Temp/` 等本地目录，无需提交版本库。

---

## 项目结构

```
Assets/
├── Scenes/                  # LoginScene、HomeScene、BattleScene、SampleScene
├── Prefabs/                 # 页面、状态、对话框、战斗实体、UI 组件
├── Resources/               # 运行时资源（音频、本地化等）
├── ScriptableObjects/       # DialogRegistry 等配置资产
├── Scripts/
│   ├── Constants/           # 场景、网络、战斗等共享枚举与常量
│   ├── Data/                # 跨模块共享数据模型（PlayerData 等）
│   ├── Network/             # TCP 连接、分帧、序列化、消息分发
│   │   └── NetModels/       # 协议消息 struct 与网络数据结构
│   ├── Singletons/          # 跨场景单例（GameSceneManager）
│   ├── UI/
│   │   ├── StateEngine/     # 状态栈引擎、PageController、DialogMgr
│   │   ├── States/          # Login / Home / Battle 各业务状态
│   │   ├── Pages/           # BattlePage、CommonPage
│   │   ├── Views/           # ViewBase 子类（按功能分子目录）
│   │   ├── ViewModels/      # ViewModelBase 子类
│   │   ├── Dialogs/         # DialogBase 子类（按页面分组）
│   │   ├── Models/          # UI 层业务模型
│   │   └── MVVMFactory.cs
│   ├── Battle/              # 战斗会话、实体、帧同步、敌人/子弹注册表
│   └── Utils/               # Toast、通用 UI 工具
├── Server/                  # 本地简易权威服务端脚本（开发调试用）
├── Tests/
│   ├── EditMode/            # 单元测试（StateEngine、Network、Algorithms）
│   └── PlayMode/            # 集成测试（Network、Business 业务流程）
├── Plugins/ParrelSync/      # 多实例联机调试
└── Logs/                    # 运行时日志输出（已加入 .gitignore）

config/                      # 飞书接口文档与协议对齐配置（wiki-registry 等）
.docs/                       # 框架与战斗设计文档（State / Page / Dialog / Battle）
docs/                        # 客户端-服务端整体架构 PlantUML
.cursor/                     # Agent 协议协作规则与 skill
```

---

## 程序集划分

脚本通过 Assembly Definition 按依赖分层，避免循环引用：

| 程序集 | 路径 | 说明 |
|--------|------|------|
| `Constants` | `Scripts/Constants/` | 最底层，无项目内依赖 |
| `Data` | `Scripts/Data/` | 共享数据模型 |
| `Network` | `Scripts/Network/` | 依赖 Constants、Data |
| `UI.StateEngine` | `Scripts/UI/StateEngine/` | 状态机核心，独立于业务 UI |
| `Singletons` | `Scripts/Singletons/` | 跨场景管理器 |
| `Utils` | `Scripts/Utils/` | 通用工具 |
| `UI` | `Scripts/UI/` | 依赖 Network、StateEngine 等，承载全部业务 UI |
| `Battle` | `Scripts/Battle/` | 默认程序集，战斗逻辑与 UI/Network 协作 |

测试程序集：`StateEngineTests.EditMode`、`NetworkTests.EditMode`、`AlgorithmsTests.EditMode`、`NetworkTests.PlayMode`、`BusinessTests.PlayMode`。

---

## 架构概览

整体分层见 [`docs/architecture-client-server.puml`](docs/architecture-client-server.puml)：表现与交互（Scene / StateEngine / MVVM）→ 业务流程（登录、大厅、商店、地图、战斗）→ 网络接入（短连接 + 长连接）→ 与服务端共享的 JSON 协议契约。

### 场景与页面层

`GameSceneManager` 是跨场景单例（`DontDestroyOnLoad`），负责在顶层场景间切换，支持淡入淡出：

| 场景 | SceneType |
|------|-----------|
| `LoginScene` | `LOGIN` |
| `HomeScene` | `HOME` |
| `BattleScene` | `BATTLE` |

场景内由 `PageController` 管理 **Page**——每个 Page 是独立 `StateEngine` 的 UI 容器，同一时刻仅一个 Page 处于激活状态。`HomePage` 在切走时 deactivate 而非销毁，返回时复用。

```
PageController
  └── StateEnginePage（当前激活页）
        ├── Canvas / GraphicRaycaster / CanvasScaler
        └── StateEngine
              ├── State A
              └── State B
```

详见 [`.docs/Page.md`](.docs/Page.md)。

### 状态机引擎

`StateEngine` 维护 `StateBase` 的 **LIFO 栈**，生命周期钩子：

| 钩子 | 调用时机 |
|------|----------|
| `OnEnter` | 每次入栈 |
| `OnPause` | 被新状态覆盖时 |
| `OnResume` | 重新成为栈顶时 |
| `OnExit` | 出栈后 |

状态间可通过 `SendMessage<TFrom, TTo>(message)` 传递数据，目标状态在下次成为栈顶时通过 `ReceiveMessage` 一次性消费。

```csharp
engine.AddTop<T>();           // 入栈
engine.TryRemoveTop();        // 出栈
engine.ReplaceTop<T>();       // 替换栈顶
engine.TryRemoveTo<T>();      // 弹出至指定状态
engine.Clear();               // 清空栈
engine.SendMessage<TFrom, TTo>(message);
```

详见 [`.docs/State.md`](.docs/State.md)。

### MVVM 与对话框

**MVVM**：`ViewBase<TVm>` 通过 `MVVMFactory.Bind(view, vm)` 绑定 `ViewModelBase<TVm>`，绑定时立即渲染并在 `PropertyChanged` 时更新；`OnDestroy` 自动取消订阅。

**对话框**：`DialogMgr` 在状态内管理模态对话框栈，打开时实例化、关闭时销毁。状态 `OnExit` 时必须调用 `_dialogMgr.Dispose()` 防止泄漏。预制体根节点需挂 `DialogBase`，类型在 `DialogRegistry` ScriptableObject 中注册。

详见 [`.docs/MiniDialog.md`](.docs/MiniDialog.md)。

### 网络层

`NetworkManager` 是线程安全的跨场景单例，管理一个或多个 `INetworkChannel`。网络回调经队列投递，在 `NetworkManager.Update()` 中于 **Unity 主线程** 消费。

| 类 | 职责 |
|----|------|
| `NetworkManager` | 注册通道、每帧泵送消息、短连接超时 |
| `NetworkChannel` | TCP 收发，入队原始字节 |
| `MessageFramer` | 长度前缀分帧 |
| `MessageSerializer` | JSON 序列化/反序列化 |
| `LongConnectionInboundDispatcher` | 长连接入站消息路由 |
| `LongConnectionDispatchTables` | 命令字到处理器的映射表 |

协议消息定义于 `Assets/Scripts/Network/NetModels/`，常量与命令字见 `NetworkConstants.cs`、`BattleConstants.cs`。

### 战斗层

战斗采用 **服务端 Tick 权威 + 客户端表现** 模型。开战前的商店（`ShopState`）与地图选点（`MapState`）仍走 StateEngine；点击地图节点重载 `BattleScene` 后，由 `BattleSession` 接管实时逻辑。

```
OnlineRoomState ──(全员就绪)──► BattlePage
ShopState ──► MapState ──(选点切场景)──► BattleSession
                                              │
                    ┌─────────────────────────┼─────────────────────────┐
                    ▼                         ▼                         ▼
            BattleFrameQueue          BattleSyncUploader         实体注册表
         (消费 BATTLE_FRAME)      (PositionSyncRequest)     Enemy / Bullet Registry
                    │
                    ▼
         LocalPlayer / RemotePlayer / Enemy / Bullet
```

| 组件 | 职责 |
|------|------|
| `BattleSession` | 长连接（BattlePort 22226）、就绪循环、帧/event 分发、战斗结束 |
| `BattleFrameQueue` | 线程安全入队，主线程按序消费 `BattleFrameResponse` |
| `BattleSyncUploader` | 后台线程定频上报玩家 + 敌人位置（默认 20 Hz） |
| `BattleSyncSnapshot` | 主线程写入、后台线程读取的位置快照 |
| `LocalPlayerCharacter` | 本地输入、射击请求、快照发布 |
| `RemotePlayerCharacter` | 远端玩家 Lerp 跟随服务端位置 |
| `EnemyCharacter` / `BulletEntity` | 事件驱动生成，帧/event 驱动更新与销毁 |

协议消息见 `ServerNetworkMessages.cs`，数据结构见 `NetworkStructures.cs`，事件枚举见 `BattleConstants.cs`。

详见 [`.docs/Battle.md`](.docs/Battle.md)。

---

## 场景与业务流程

| 场景 | 主要状态 / 功能 |
|------|-----------------|
| `LoginScene` | `LoginState` — 登录、注册 |
| `HomeScene` | `HomeState` → `OnlineLobbyState`（房间列表）→ `OnlineRoomState`（房间准备） |
| `BattleScene` | `ShopState`（ShopPort）→ `MapState`（MapPort）→ 选点切场景 → `BattleSession`（BattlePort 帧同步） |
| `SampleScene` | 状态机与 UI 开发沙盒 |

---

## 测试

在 Unity Test Runner 中运行：

| 类型 | 路径 | 覆盖范围 |
|------|------|----------|
| EditMode | `Assets/Tests/EditMode/` | StateEngine 生命周期与消息、MessageFramer/Serializer、LongConnection 分发、地图生成算法 |
| PlayMode | `Assets/Tests/PlayMode/` | NetworkManager 与 FakeServer、登录/大厅/房间业务流程；战斗 Sample 协议（`BattleRealtimeSampleBusinessPlayModeTests`） |

PlayMode 业务测试基类为 `BusinessPlayModeTestBase`，网络测试基类为 `NetworkTestBase`。

---

## 文档

| 文档 | 说明 |
|------|------|
| [`.docs/State.md`](.docs/State.md) | State / StateEngine 规范 |
| [`.docs/Page.md`](.docs/Page.md) | Page / PageController 规范 |
| [`.docs/MiniDialog.md`](.docs/MiniDialog.md) | 对话框系统设计 |
| [`.docs/Battle.md`](.docs/Battle.md) | 战斗帧同步、实体体系、网络协议与调试 |
| [`docs/architecture-client-server.puml`](docs/architecture-client-server.puml) | 客户端-服务端整体架构图 |

---

## 协议协作

飞书 Wiki 为接口文档权威源。模块与代码路径映射见 [`config/wiki-registry.yaml`](config/wiki-registry.yaml)。

在 Cursor 中可使用 skill `/game-api-sync` 或参考 [`.cursor/skills/game-api-sync/`](.cursor/skills/game-api-sync/) 完成：刷新 ECS 文档缓存、对比飞书与代码差异、对齐协议代码、生成飞书更新草稿。

协议源文件集中在 `Assets/Scripts/Network/NetModels/` 与 `Assets/Scripts/Constants/`，**禁止**新建 `Generated/` 或平行协议目录。

---

## 代码规范

遵循 [`.editorconfig`](.editorconfig)：

| 符号类型 | 命名 |
|----------|------|
| 私有/保护实例字段 | `m_` 前缀（如 `m_health`） |
| `[SerializeField]` 私有/保护字段 | `_` 前缀（如 `_speed`） |
| 私有/保护静态字段 | `s_` 前缀（如 `s_instance`） |
| 公开成员 | PascalCase，无前缀 |

类内成员顺序：`const` / `static readonly` → `[SerializeField]` → 私有字段 → 属性 → Unity 生命周期 → 公开方法 → 私有方法。
