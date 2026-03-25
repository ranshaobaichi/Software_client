# State / StateEngine 设计说明

## 1. 目标

- 用**栈**管理多层 UI / 逻辑层：同一时间只有**栈顶**一层处于「前台」。
- 用**四个生命周期**表达层的状态流转。
- 提供与「压栈 / 弹栈 / 换顶 / 弹到某层」对应的引擎 API，调用顺序固定。
- 支持**跨 State 的单次消息传递**：发送方在切栈前写入，接收方在成为栈顶后读取并消耗。

---

## 2. 核心概念


| 概念        | 说明                                               |
| --------- | ------------------------------------------------ |
| **State** | 一层 UI / 逻辑的抽象，对应一个挂载在 Prefab 上的 `StateBase` 派生类。 |
| **栈**     | `StateBase` 实例的 LIFO 序列；**栈顶**即当前前台。             |
| **引擎**    | 唯一入口：修改栈并按约定顺序调用生命周期。                            |


约定：**同一 State 实例在栈中至多出现一次**。

---

## 3. 生命周期

### 3.1 四个钩子


| 钩子           | 调用时机                  | 典型职责           |
| ------------ | --------------------- | -------------- |
| **OnEnter**  | 每次该实例被压入栈顶时           | 初始化、建子对象、注册事件  |
| **OnPause**  | 当前为栈顶，即将被另一层覆盖（仍留在栈中） | 停输入、停计时        |
| **OnResume** | 刚刚成为栈顶                | 恢复输入、刷新展示、读取消息 |
| **OnExit**   | 从栈中移除之后               | 清理、退订事件        |


> **OnEnter 与 OnResume 的关系**：每次压栈必然同时触发 OnEnter → OnResume。OnResume 也会在被压盖的层重新露出时单独触发（此时不触发 OnEnter，因为该实例未经 OnExit）。

### 3.2 ReceiveMessage

`ReceiveMessage` 在 `OnResume` 之后、由引擎调用，传入当前积累的消息字典（`null` 表示无消息）。详见第 6 节。

---

## 4. 操作 → 生命周期调用表

### 4.1 空栈 → AddTopT

1. 新：`OnEnter`
2. 新：`OnResume`
3. 新：`ReceiveMessage`

### 4.2 非空栈 → AddTopT

1. 旧顶：`OnPause`
2. 新顶：`OnEnter`
3. 新顶：`OnResume`
4. 新顶：`ReceiveMessage`

### 4.3 TryRemoveTop，弹后栈非空

1. 旧顶：`OnPause`
2. 旧顶：`OnExit`（清除旧顶的待接收消息）
3. 新顶：`OnResume`
4. 新顶：`ReceiveMessage`

### 4.4 ReplaceTopT

1. 旧顶：`OnPause`
2. 旧顶：`OnExit`（清除旧顶的待接收消息）
3. 新顶：`OnEnter`
4. 新顶：`OnResume`
5. 新顶：`ReceiveMessage`

空栈时退化为 AddTop（OnEnter → OnResume → ReceiveMessage）。

### 4.5 TryRemoveToT，目标在栈中且非顶

对从顶到目标之上的**每一层**（从顶向下）依次：

1. `OnPause`
2. `OnExit`（清除该层的待接收消息）

最后目标成为栈顶：
3. 目标：`OnResume`
4. 目标：`ReceiveMessage`

目标不在栈中 → 返回 false，栈不变。目标已是栈顶 → 返回 true，无操作。

### 4.6 Clear

从顶到底依次：`OnPause` → `OnExit`。结束后清空所有待消息。

---

## 5. IStateEngine 接口

```csharp
// ========== 查询 ==========
bool IsEmpty { get; }
int Count { get; }
StateBase Peek();                    // 栈顶，空栈返回 null
bool Contains<T>() where T : StateBase;

// ========== 修改栈 ==========
void AddTop<T>() where T : StateBase;
void AddTop(Type stateType);         // 运行时类型版，如 AddTop(_initState.state.GetType())
bool TryRemoveTop();
void ReplaceTop<T>() where T : StateBase;
bool TryRemoveTo<T>() where T : StateBase;
void Clear();

// ========== 消息 ==========
void SendMessage<TFrom, TTo>(object message)
    where TFrom : StateBase where TTo : StateBase;
```

**统一约束**

- 所有修改栈的方法含重入保护（`m_busy`）；若已在操作中则抛 `InvalidOperationException`。
- `AddTop`：若实例已在栈中 → 失败（记录错误，栈不变）。

---

## 6. 消息传递

### 6.1 结构

```
m_statesMessages:
  Dictionary< Type(toType),
    Dictionary< Type(fromType), object(message) >
  >
```

每个 State 类型对应一个「收件箱」字典，收件箱内按发送方类型索引消息。

### 6.2 API

```csharp
// 发送：在切栈前调用，写入目标 State 的收件箱
engine.SendMessage<StateA, StateB>(myData);

// 接收：引擎在 OnResume 后自动调用，由 StateBase 子类重写
public virtual void ReceiveMessage(Dictionary<Type, object> messages) { }
```

### 6.3 生命周期规则


| 事件               | 消息行为                                                        |
| ---------------- | ----------------------------------------------------------- |
| State 成为栈顶（任意方式） | 引擎调用 `ReceiveMessage`，传入其收件箱（无消息则传 `null`），**消息被消耗**（从字典移除） |
| State 被 Exit     | 其收件箱**立即清空**，未被读取的消息丢弃                                      |
| `Clear()`        | 所有收件箱全部清空                                                   |


### 6.4 约束

- 每对 `(fromType → toType)` 同时只能有一条消息；重复发送会覆盖并记录警告。
- 消息为 `object`，接收方自行类型断言。
- 消息**一次性消耗**：`ReceiveMessage` 返回后收件箱条目已移除。

---

## 7. StateEngine 实现说明

### 7.1 Inspector 配置

```
StateEngine (MonoBehaviour)
├── _initState : StateRegistration   ← 初始界面，单独配置
└── _registrations : List<StateRegistration>  ← 其余所有 State
```

```csharp
[Serializable]
public class StateRegistration {
    public RectTransform parent;  // 实例化时的父节点
    public StateBase state;       // Prefab 根节点上的 StateBase 组件
}
```

### 7.2 Start() 预热流程

1. 合并 `_registrations` 与 `_initState`，对所有 Prefab 执行 `Instantiate`，缓存进 `Dictionary<Type, StateBase>`，默认 `SetActive(false)`。
2. 若 `_initState` 有效，立即 `AddTop(_initState.state.GetType())` 推入初始界面。

### 7.3 运行时动态注册

```csharp
engine.RegisterState(stateInstance); // 绕过 Inspector，直接注入缓存（用于测试或热注册）
```

### 7.4 线程模型

单线程（Unity 主线程）。重入时抛 `InvalidOperationException`。

---

## 8. StateBase 抽象类

```csharp
public abstract class StateBase : MonoBehaviour
{
    protected IStateEngine m_StateEngine; // 引擎引用，由引擎在实例化后注入

    // 引擎回调（internal）
    internal void DoEnter()  => OnEnter();
    internal void DoPause()  => OnPause();
    internal void DoResume() => OnResume();
    internal void DoExit()   => OnExit();

    // 子类重写
    protected virtual void OnEnter()  { }
    protected virtual void OnPause()  { }
    protected virtual void OnResume() { }
    protected virtual void OnExit()   { }
    protected virtual void ReceiveMessage(Dictionary<Type, object> messages) { }
}
```

---

## 9. 错误与边界


| 场景                           | 行为                            |
| ---------------------------- | ----------------------------- |
| 空栈 `TryRemoveTop`            | 返回 false                      |
| `AddTop` 已在栈中的实例             | 记录错误，栈不变                      |
| `ReplaceTop` 于空栈             | 退化为首次 AddTop                  |
| `TryRemoveTo` 目标不在栈          | false，栈不变                     |
| `TryRemoveTo` 目标已是栈顶         | true，无操作                      |
| 引擎 Busy 时重入                  | 抛 `InvalidOperationException` |
| 重复 SendMessage 同一对 (from→to) | 覆盖，记录警告                       |
| State Exit 时有未读消息            | 消息丢弃                          |


---

## 10. 测试要点

1. 空栈 `AddTop<A>`：A.Enter → A.Resume → A.ReceiveMessage(null)。
2. `AddTop<B>`：A.Pause → B.Enter → B.Resume → B.ReceiveMessage(null)。
3. `TryRemoveTop`：B.Pause → B.Exit → A.Resume → A.ReceiveMessage(null)。
4. `ReplaceTop<C>`：B.Pause → B.Exit → C.Enter → C.Resume → C.ReceiveMessage(null)。
5. 栈 `[A,B,C]`，`TryRemoveTo<A>`：C/B 依次 Pause+Exit，A.Resume → A.ReceiveMessage(null)。
6. 重复 `AddTop<A>` 失败，栈不变。
7. Busy 时重入抛异常。
8. `SendMessage<A,B>(msg)` 后 `AddTop<B>`：B.ReceiveMessage 收到含 msg 的字典。
9. `SendMessage<A,B>(msg)` 后 B 被 Exit 再 AddTop：消息已丢弃，ReceiveMessage 收到 null。

