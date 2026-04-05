# 最小 Dialog 管理系统设计说明

## 1. 目标与非目标

**目标**

- 在 State 内管理**弹窗层**：打开时 Instantiate，关闭时 Destroy，无全量预加载。
- 用**栈**管理多层重叠 Dialog：同一时间**栈顶**接收输入，关闭后下层可恢复。
- 提供**强类型数据传入 / 结构化结果传出**的标准管道。
- 可选地在 Dialog 最底层渲染**摄像机截屏 + 高斯模糊**遮罩。

**非目标**

- 打开/关闭过渡动画（v1 不含；可后续扩展 `IDialogTransition`）。
- 异步资源加载（Prefab 由 ScriptableObject 直接引用，同步 Instantiate）。

---

## 2. 核心概念

| 概念 | 说明 |
|------|------|
| **Dialog** | 一个弹窗的抽象；对应一个 Prefab + 一个 `DialogBase` 子类组件 |
| **栈** | `Dialog` 实例的 LIFO 序列；**栈顶**即当前最上层弹窗 |
| **DialogMgr** | 唯一入口：管理栈、执行实例化/销毁、分发回调 |
| **Registry** | `ScriptableObject`，静态持有全部 Dialog Prefab 引用，按 `DialogType` 查表 |
| **DialogId** | 每个 Dialog 实例的唯一标识，使用 `GameObject.GetInstanceID()` |

约定：同一 `DialogType` 的 Dialog **允许重复打开**，每次 `OpenDialog` 均 Instantiate 新实例并压栈。

---

## 3. 生命周期定义

### 3.1 四个钩子（由 `DialogBase` 基类提供）

| 钩子 | 调用时机 | 典型职责 |
|------|----------|----------|
| **OnInit(object input, Action\<DialogResult\> close)** | Instantiate + 模糊截屏之后，一生仅一次 | 缓存组件引用、绑定按钮事件、处理输入数据并渲染 UI |
| **OnRender(TInput)** | 泛型子类 `DialogBase<TInput>` 内部由 `OnInit` 的 sealed 实现自动调用 | 业务刷新 UI 显示（仅泛型子类使用） |
| **OnDialogResume** | 上层 Dialog 关闭后，本 Dialog 重新成为栈顶 | 恢复交互、刷新数据 |
| **OnDialogDestroy** | `Destroy(gameObject)` 之前 | 释放业务资源、退订事件 |

### 3.2 关闭机制：双回调管道

Dialog 的关闭涉及**两条回调路径**，各司其职：

| 回调 | 持有方 | 用途 |
|------|--------|------|
| `Action<DialogResult> close` | Dialog 实例（OnInit 时由框架传入） | Dialog **主动触发**自身关闭，并携带结果数据 |
| `IDialogCallback` | DialogMgr（OpenDialog 时由调用方传入） | 调用方**被动接收**关闭通知和结果数据 |

流程：Dialog 调用 `close(result)` → DialogMgr 收到关闭请求 → 执行销毁 → 调用 `IDialogCallback.OnDialogClosed(dialogId, result)` 通知调用方。

### 3.3 与 Unity 生命周期的关系

- `Awake` / `Start`：**不使用**。框架通过 `OnInit` 替代，保证调用时序可控。
- `OnDestroy`：框架在 `OnDialogDestroy` 之后才 `Destroy(gameObject)`。子类若在 `OnDestroy` 中做清理亦可，但推荐统一使用 `OnDialogDestroy`。

---

## 4. 操作 → 生命周期调用表

### 4.1 打开 Dialog（`OpenDialog`）

1. Registry 按 `DialogType` 查表 → 获取 Prefab
2. `Instantiate(prefab, container)`
3. 获取 `DialogBase` 组件 → 类型校验
4. 分配 `dialogId = instance.GetInstanceID()`
5. 若 `instance.GetBlurTarget() != null` → 执行模糊截屏 → 设置到该 Image
6. `instance.Framework_Init(dialogId, mgr, input, closeAction)` → 触发 **OnInit**（泛型子类内部自动调用 **OnRender**）
7. 压栈：`_stack.Add(entry)`
8. 返回 `dialogId`

同一 `DialogType` 重复打开时，每次均执行上述完整流程，产生独立实例和 dialogId。

### 4.2 关闭 Dialog — 栈顶

由 Dialog 内部调用 `close(result)` 触发，或由外部调用 `CloseTopDialog()` 触发：

1. 弹出栈顶 entry，标记 `isClosing = true`
2. 触发外部回调：`entry.callback?.OnDialogClosed(dialogId, result)`
3. `instance.Framework_Destroy()` → 触发 **OnDialogDestroy**
4. `GameObject.Destroy(instance.gameObject)`
5. 若栈不为空 → 新栈顶：`Framework_Resume()` → 触发 **OnDialogResume**

### 4.3 关闭 Dialog — 非栈顶（按 dialogId）

由外部调用 `CloseDialog(dialogId, result)` 触发：

1. 在 `_stack` 中查找对应 entry，标记 `isClosing = true`，从列表中移除
2. 触发外部回调：`entry.callback?.OnDialogClosed(dialogId, result)`
3. `instance.Framework_Destroy()` → 触发 **OnDialogDestroy**
4. `GameObject.Destroy(instance.gameObject)`
5. **不**触发任何其他 Dialog 的 `OnDialogResume`（栈顶未变化）

### 4.4 关闭全部（`CloseAllDialogs`）

从栈顶到栈底依次：
1. 触发外部回调：`entry.callback?.OnDialogClosed(dialogId, MiniDialogResult.Empty)`
2. 触发 **OnDialogDestroy**
3. `Destroy(gameObject)`
4. 清空栈

### 4.5 Dispose（宿主退出时）

1. 从栈顶到栈底依次销毁全部 Dialog（**不触发** `IDialogCallback`，宿主已退出）
2. 依次触发 **OnDialogDestroy** + `Destroy(gameObject)`
3. 清空栈，置空全部内部引用
4. 标记 `_disposed = true`，此后任何操作静默忽略

---

## 5. 类型设计

### 5.1 `DialogRegistry` (ScriptableObject)

```text
[CreateAssetMenu]
class DialogRegistry : ScriptableObject
{
    [Serializable]
    struct Entry
    {
        DialogType key;
        GameObject prefab;
    }

    Entry[] _entries;

    // 运行时 lazy 构建的查找字典
    Dictionary<DialogType, GameObject> _lookup;

    GameObject GetPrefab(DialogType key);
}
```

**约定**：
- `DialogType` 为枚举，每个业务 Dialog 类型对应一个枚举值
- 同一 Prefab 只登记一次；若登记重复 key 则 Editor 下 `OnValidate` 报错
- Prefab 根节点上必须挂有 `DialogBase` 的子类组件

### 5.2 `DialogResult` (struct)

```text
struct DialogResult
{
    static readonly DialogResult Empty;

    int intVal;
    string strVal;
    object objVal;
}
```

**约定**：
- 轻量值类型，`objVal` 为可选装箱
- 提供 `bool` / `int` / `string` 的隐式转换运算符

### 5.3 `IDialogCallback` (interface)

```text
interface IDialogCallback
{
    void OnDialogClosed(int dialogId, DialogResult result);
}
```

**约定**：
- 回调在 `Destroy` **之前**同步触发
- 一个 Dialog 只绑定一个 `IDialogCallback`（`OpenDialog` 时传入），为 null 时静默跳过

### 5.4 `DialogBase` (abstract MonoBehaviour)

```text
abstract class DialogBase : MonoBehaviour
{
    // ---- 框架赋值，业务只读 ----
    int DialogId { get; }
    DialogMgr DialogMgr { get; }

    // ---- 生命周期钩子（子类 override）----
    protected abstract void OnInit(object input, Action<DialogResult> close);
    protected virtual void OnDialogResume();
    protected virtual void OnDialogDestroy();

    // ---- 可选能力 ----
    public virtual Image GetBlurTarget() => null;

    // ---- 框架内部（仅 DialogMgr 可调用）----
    internal void Framework_Init(int dialogId, DialogMgr mgr, object input, Action<DialogResult> close);
    internal void Framework_Resume();
    internal void Framework_Destroy();
}
```

**可见性约定**：
- `Framework_*` 方法标记 `internal`，仅同 assembly 的 `DialogMgr` 可调用
- `DialogId` / `DialogMgr` 为 `public get`、`private set`
- `OnInit` 为 `abstract`，子类必须实现

**`Framework_Init` 内部行为**：
1. 赋值 `DialogId`、`DialogMgr`
2. 调用 `OnInit(input, close)`

### 5.5 `DialogBase<TInput>` (abstract, generic)

```text
abstract class DialogBase<TInput> : DialogBase where TInput : class
{
    protected TInput Input { get; private set; }
    protected Action<DialogResult> Close { get; private set; }

    protected sealed override void OnInit(object input, Action<DialogResult> close)
    {
        Input = input as TInput;
        Close = close;
        OnRender(Input);
    }

    protected abstract void OnRender(TInput input);
}
```

**约定**：
- `TInput` 约束为 `class`（引用类型）
- `OnInit` 被 `sealed`，子类只需实现 `OnRender`
- 子类通过 `Close` 属性触发关闭：`Close(new DialogResult { intVal = 1 })`
- 若 input 为 null 或类型不匹配，`Input` 为 null，`OnRender` 仍被调用（子类自行处理）

### 5.6 `DialogMgr` (plain C# class)

```text
class DialogMgr
{
    // ---- 内部数据 ----
    struct DialogEntry
    {
        int dialogId;
        DialogType key;
        DialogBase instance;
        IDialogCallback callback;
        bool isClosing;
    }

    List<DialogEntry> _stack;        // 尾部 = 栈顶
    Transform _container;
    DialogRegistry _registry;
    Camera[] _screenshotCameras;     // 可选
    bool _disposed;

    // ---- 构建 ----
    struct Builder
    {
        Transform container;
        DialogRegistry registry;
        Camera[] screenshotCameras;   // 可选，为空则不支持模糊截屏
    }
    static DialogMgr Create(Builder builder);

    // ---- 打开 ----
    int OpenDialog<TDialog, TInput>(DialogType key, TInput input, IDialogCallback callback = null)
        where TDialog : DialogBase<TInput>
        where TInput : class;

    int OpenDialog(DialogType key, object input = null, IDialogCallback callback = null);

    // ---- 关闭 ----
    void CloseDialog(int dialogId, DialogResult result);
    void CloseTopDialog();
    void CloseAllDialogs();

    // ---- 查询 ----
    bool HasOpenDialog { get; }
    bool IsDialogOpen(int dialogId);
    DialogBase PeekTop();
    int DialogCount { get; }

    // ---- 生命周期 ----
    void Dispose();
}
```

**`OpenDialog` 内部构造 closeAction 的约定**：
```text
var closeAction = (DialogResult result) => this.CloseDialog(dialogId, result);
```
框架为每个 Dialog 实例生成专属的 `Action<DialogResult>`，调用时路由到 `CloseDialog`。

### 5.7 `DialogBlurHelper` (static utility)

```text
static class DialogBlurHelper
{
    static void CaptureAndBlur(Image target, Camera[] cameras);
}
```

**约定**：
- 渲染到半分辨率 `RenderTexture` 以提升性能
- 使用内置 2-pass 高斯模糊（水平 + 垂直 Blit）
- 模糊 Shader 由 `DialogMgr` 内部持有或硬编码 Shader.Find 获取
- 生成的 Sprite 生命周期与 Dialog 实例绑定，Dialog 销毁时自动释放
- `cameras` 为空则使用 `Camera.main`

---

## 6. 栈管理细节约定

### 6.1 数据结构

使用 `List<DialogEntry>` 而非 `Stack<T>`：
- 尾部（`[Count-1]`）为栈顶
- 支持按 `dialogId` 在任意位置移除（处理非栈顶 Dialog 被关闭的情况）

### 6.2 非栈顶关闭

允许关闭非栈顶的 Dialog（例如被下层业务逻辑主动关闭）：
1. 从 `_stack` 中移除该 entry
2. 触发 `IDialogCallback`
3. 触发 `OnDialogDestroy` + `Destroy`
4. **不**触发任何其他 Dialog 的 `OnDialogResume`（因为栈顶未变化）

### 6.3 重入保护

- `CloseDialog` 内部：在找到 entry 后立即标记 `isClosing = true`
- 若该 entry 已标记 `isClosing` 则静默返回（防止 callback 中再次触发关闭）
- Dialog 多次调用 `close(result)` 时仅首次生效

### 6.4 打开时序保证

`OpenDialog` 的全部步骤（§4.1）在**同一帧**内同步完成：
- Instantiate → Blur → Init(含 OnRender) → 压栈 → 返回 id
- 不涉及协程或异步等待

---

## 7. 模糊截屏约定

### 7.1 触发条件

仅在以下条件全部满足时执行截屏模糊：
1. `DialogMgr` 构建时提供了 `screenshotCameras`（非空）
2. 当前打开的 Dialog 实例 `GetBlurTarget()` 返回非 null 的 `Image`

### 7.2 执行时序

在 Instantiate 之后、`OnInit` 之前执行：
```
Instantiate → GetBlurTarget → [CaptureAndBlur] → Framework_Init → OnInit(→OnRender)
```
确保 `OnRender` 时模糊背景已就绪。

### 7.3 资源管理

- 截屏产生的 `Texture2D` 和 `Sprite` 由 `DialogBlurHelper` 在 Dialog 所在 GameObject 上挂一个轻量 `MonoBehaviour`（或手动注册到 `OnDialogDestroy`），随 Dialog 销毁自动释放。
- 不缓存、不复用截屏结果（每次打开均重新截取）。

---

## 8. 与 State 系统的集成约定

`DialogMgr` 是纯 C# 对象。State 系统（见 `State.md`）中的使用方式：

```text
State.OnEnter():
    _dialogMgr = DialogMgr.Create(builder);

State.OnExit():
    _dialogMgr.Dispose();
```

**约定**：
- 每个需要 Dialog 的 State **各自持有**一个 `DialogMgr` 实例
- State 的 `OnPause` **不**自动关闭 Dialog（Dialog 可跨 State Pause 存活，由业务决定）
- State 的 `OnExit` **必须**调用 `Dispose()`，否则 Dialog 实例泄漏
- 若 State 被盖住（`OnPause`），其 Dialog 仍然可见但不接收输入（由 UI 层级自然遮挡）

---

## 9. 错误与边界

| 场景 | 行为 |
|------|------|
| Registry 中 key 不存在 | `LogError`，返回 `-1`，不创建实例 |
| Prefab 上无 `DialogBase` 组件 | `LogError`，销毁已 Instantiate 的 GameObject，返回 `-1` |
| Prefab 上组件类型与泛型参数 `TDialog` 不匹配 | `LogError`，销毁，返回 `-1` |
| `CloseDialog` 传入不存在的 dialogId | 静默忽略 |
| `CloseDialog` 对已标记 `isClosing` 的实例 | 静默忽略 |
| `CloseTopDialog` 于空栈 | 静默忽略 |
| `CloseAllDialogs` 于空栈 | 无操作 |
| `Dispose` 后调用任何方法 | 静默忽略 + `LogWarning`（仅首次） |
| `OpenDialog` 时 container 已被销毁 | `LogError`，返回 `-1` |

---

## 10. 文件结构

```
Assets/Torappu/Scripts/UI/Dialog/
├── DialogMgr.cs             管理器（含 Builder、DialogEntry）
├── DialogBase.cs            基类 DialogBase + 泛型 DialogBase<TInput>
├── DialogRegistry.cs        ScriptableObject 注册表（含 DialogType 枚举）
├── DialogResult.cs          关闭结果 struct
├── IDialogCallback.cs       回调接口
└── DialogBlurHelper.cs      模糊截屏静态工具
```

共 **6 个文件**。全部位于同一命名空间（建议 `Torappu.UI.Dialog` 或直接 `Torappu.UI`，由实现时确定）。

---

## 11. 验收清单

1. 空栈 `OpenDialog(TypeA, input)` → Instantiate → 模糊截屏(若需) → `OnInit` → `OnRender` → 栈 `[A]`，返回有效 dialogId。
2. 再 `OpenDialog(TypeB, input)` → 栈 `[A, B]`。
3. B 内部调 `close(result)` → `IDialogCallback` 被触发 → B 被 Destroy → A 收到 `OnDialogResume` → 栈 `[A]`。
4. 重复 `OpenDialog(TypeA, input)` → 新实例 A' 压栈 → 栈 `[A, A']`，两者 dialogId 不同。
5. `CloseAllDialogs()` → 栈中全部 Dialog 依次触发回调并 Destroy。
6. `Dispose()` → 全部 Dialog 销毁（不触发 `IDialogCallback`），之后 `OpenDialog` 无效、不崩溃。
7. 带 `GetBlurTarget()` 的 Dialog → 打开时 Image 上已有模糊截屏 Sprite。
8. Registry 中 key 不存在 → `LogError`，返回 `-1`。
9. 关闭非栈顶 Dialog（`CloseDialog(id, result)`）→ 仅该 Dialog 被移除和销毁，栈顶不触发 Resume。
10. Dialog 多次调用 `close(result)` → 仅首次生效。
