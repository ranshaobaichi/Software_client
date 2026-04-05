# StateEnginePage / PageController 设计说明

## 1. 目标与非目标

**目标**

- 在 StateEngine 之上增加 **Page** 层，表示业务功能的顶级划分（首页、商店、设置…）。
- Page 持有独立的 `StateEngine` 实例，管理页内的 State 栈。
- `PageController` 提供 Page 之间的切换，处理实例化、初始化与销毁。
- HomePage 特殊处理：切换走时仅 Inactive 保留实例；其余 Page 切换走时销毁。

**非目标**

- 页面切换过渡动画（v1 不含；可后续扩展 `IPageTransition`）。
- 异步资源加载（Prefab 由注册列表直接引用，同步 Instantiate）。
- Page 之间的栈管理（Page 间无栈关系，仅有「当前活跃 Page」概念）。

---

## 2. 核心概念

| 概念 | 说明 |
|------|------|
| **StateEnginePage** | 业务页面容器；MonoBehaviour，持有一个 `StateEngine` 引用，依赖 Canvas / GraphicRaycaster / CanvasScaler |
| **PageController** | 页面控制器；管理 Page 的注册、切换、实例化与销毁 |
| **HomePage** | 特殊 Page；切换走时保留实例（SetActive(false)），切换回时复用 |
| **PageType** | 枚举，标识业务页面类型，PageController 按此查表切换 |

约定：**同一时间只有一个 Page 处于活跃状态**。

---

## 3. 组件依赖

StateEnginePage 所在 GameObject 必须存在以下组件，通过 `[RequireComponent]` 强制约束：

| 组件 | 职责 |
|------|------|
| `Canvas` | 为该 Page 提供独立渲染层级 |
| `GraphicRaycaster` | UI 事件接收 |
| `CanvasScaler` | 分辨率适配 |

每个 Page Prefab 是一个自包含的 Canvas 子树。

---

## 4. 切换生命周期

### 4.1 切换离开当前 Page

| 当前 Page 类型 | 行为 |
|---------------|------|
| HomePage | `stateEngine.Clear()` → `gameObject.SetActive(false)` → 保留实例引用 |
| 非 HomePage | `stateEngine.Clear()` → `Destroy(gameObject)` → 清除引用 |

`Clear()` 保证页内所有 State 经历完整的 `OnPause → OnExit` 生命周期后再处理 Page。

### 4.2 切换进入目标 Page

| 目标 Page 场景 | 行为 |
|---------------|------|
| HomePage（已有缓存实例） | `gameObject.SetActive(true)` → `stateEngine.Initialize()` |
| HomePage（首次进入） | `Instantiate(prefab, container)` → `Start()` 自动完成预热与初始化 |
| 非 HomePage | `Instantiate(prefab, container)` → `Start()` 自动完成预热与初始化 |

### 4.3 完整切换时序（SwitchTo）

```
SwitchTo(PageType target):

  1. if target == _currentPageType → 静默返回

  ─── 离开当前 Page ───
  2. if _currentPage != null:
     a. _currentPage.StateEngine.Clear()
     b. if _currentPageType == HomePage.type:
          _currentPage.gameObject.SetActive(false)
     c. else:
          Destroy(_currentPage.gameObject)
          _currentPage = null

  ─── 进入目标 Page ───
  3. if target == HomePage.type && _homePageInstance != null:
     a. _homePageInstance.gameObject.SetActive(true)
     b. _homePageInstance.Initialize()     // 显式重新初始化
     c. _currentPage = _homePageInstance
  4. else if target == HomePage.type && _homePageInstance == null:
     a. _currentPage = Instantiate(prefab, _container)
     b. _homePageInstance = _currentPage   // 缓存
     // Start() → StateEngine 自动预热 + Initialize()
  5. else:
     a. var reg = _lookup[target]
     b. _currentPage = Instantiate(reg.prefab, _container)
     // Start() → StateEngine 自动预热 + Initialize()

  6. _currentPageType = target
```

---

## 5. StateEngine 扩展

为支持 HomePage 复用时的重新初始化，在现有 `StateEngine` 上新增一个公开方法：

```csharp
public void Initialize()
{
    if (!isEmpty) Clear();
    if (_initState?.state != null)
        AddTop(_initState.state.GetType());
}
```

同时 `Start()` 改为在预热缓存后调用 `Initialize()`，替代原有的内联 `AddTop` 调用：

```csharp
private void Start()
{
    // ... 预热缓存（不变）...
    Initialize();   // ← 替代原 AddTop(_initState.state.GetType())
}
```

此改动**向后兼容**：现有仅使用 StateEngine + StateBase 的场景行为不变。

---

## 6. 类型设计

### 6.1 `PageType` (enum)

```text
enum PageType
{
    Home,
    // 按业务扩展
}
```

### 6.2 `PageRegistration` (Serializable struct)

```text
[Serializable]
struct PageRegistration
{
    PageType type;
    StateEnginePage prefab;
}
```

与 `DialogRegistry.Entry`、`StateRegistration` 保持一致的注册模式。

### 6.3 `StateEnginePage` (MonoBehaviour)

```text
[RequireComponent(typeof(Canvas))]
[RequireComponent(typeof(GraphicRaycaster))]
[RequireComponent(typeof(CanvasScaler))]
class StateEnginePage : MonoBehaviour
{
    [SerializeField] StateEngine _stateEngine;

    // ---- 公开属性 ----
    StateEngine StateEngine { get; }        // public get

    // ---- 初始化 ----
    void Initialize()                       // 委托 _stateEngine.Initialize()
    {
        _stateEngine.Initialize();
    }
}
```

**约定**：
- `StateEngine` 为 public get / private set，外部只读
- `_stateEngine` 在 Inspector 中配置，指向同 Prefab 下的 StateEngine 组件
- 不使用 Awake / Start；生命周期完全由 PageController 驱动（首次 Instantiate 时 StateEngine 自身的 Start 完成预热）

### 6.4 `PageController` (MonoBehaviour)

```text
class PageController : MonoBehaviour
{
    // ---- Inspector 配置 ----
    [SerializeField] PageRegistration _homePage;
    [SerializeField] List<PageRegistration> _pages;
    [SerializeField] Transform _container;

    // ---- 运行时状态 ----
    Dictionary<PageType, PageRegistration> _lookup;
    StateEnginePage _currentPage;
    PageType _currentPageType;
    StateEnginePage _homePageInstance;       // 缓存的 HomePage 实例，永不销毁

    // ---- 生命周期 ----
    void Start();                           // 构建 lookup → SwitchTo(HomePage)

    // ---- 切换 API ----
    void SwitchTo(PageType pageType);

    // ---- 查询 ----
    StateEnginePage CurrentPage { get; }
    PageType CurrentPageType { get; }
    bool IsHome { get; }
}
```

**`Start()` 流程**：

1. 合并 `_homePage` 与 `_pages`，构建 `_lookup` 字典（key 为 `PageType`）
2. 校验 `_homePage` 有效性
3. `SwitchTo(_homePage.type)` — 首次进入 HomePage

---

## 7. 所有权与层级关系

```
PageController
  └── StateEnginePage  (Page)
        ├── Canvas / GraphicRaycaster / CanvasScaler
        └── StateEngine
              ├── State A (Prefab instance)
              ├── State B (Prefab instance)
              └── ...
```

- **PageController** 管理 Page 的创建 / 销毁 / 激活
- **StateEnginePage** 持有 StateEngine 引用，提供 `Initialize()` 委托
- **StateEngine** 管理 State 栈（原有行为完全不变）
- 每个 Page Prefab 是独立的 Canvas 子树，包含 StateEngine 及其注册的 State Prefab

---

## 8. 错误与边界

| 场景 | 行为 |
|------|------|
| SwitchTo 目标与当前相同 | 静默忽略，不重复初始化 |
| SwitchTo 目标 PageType 不在 `_lookup` 中 | `LogError`，不切换 |
| `_homePage` 未配置（prefab 为 null） | `Start()` 时 `LogError` |
| Prefab 上无 `StateEnginePage` 组件 | `LogError`，不实例化 |
| `_stateEngine` 引用为 null | `Initialize()` 时 `LogError` |
| `_container` 为 null | Instantiate 到场景根节点，`LogWarning` |
| 注册列表中存在重复 PageType | 构建 lookup 时 `LogWarning`，后注册项覆盖前项 |

---

## 9. 文件结构

```
Assets/Scripts/UI/Page/
├── PageType.cs              页面类型枚举
├── StateEnginePage.cs       页面组件（含 PageRegistration）
└── PageController.cs        页面控制器
```

共 **3 个文件**。命名空间 `UI.Page`。

StateEngine 扩展（修改现有文件）：

```
Assets/Scripts/UI/StateEngine/StateEngine.cs  ← 新增 public Initialize() 方法
```

---

## 10. 验收清单

1. 启动 → `PageController.Start()` → HomePage Instantiate 并激活 → StateEngine 初始化，initState 入栈。
2. `SwitchTo(Shop)` → HomePage.StateEngine.Clear() → HomePage.SetActive(false) → Shop 页 Instantiate → Shop.StateEngine 初始化。
3. `SwitchTo(Home)` → Shop.StateEngine.Clear() → Shop 被 Destroy → HomePage.SetActive(true) → HomePage.StateEngine.Initialize() → initState 重新入栈。
4. `SwitchTo` 目标与当前相同 → 无操作。
5. `SwitchTo` 未注册的 PageType → LogError，不切换。
6. 每个 Page Prefab 的 StateEnginePage 组件自动要求 Canvas + GraphicRaycaster + CanvasScaler。
7. 非 HomePage 切换走 → Destroy 后无残留 GameObject。
8. HomePage 切换走再切回 → 同一实例复用，InstanceID 不变。
