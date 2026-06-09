# Copilot Code Review Instructions — Unity C# Repository

## Scope

- **Review only `.cs` files.**
- Skip all other file types (scenes, prefabs, shaders, assets, configs, etc.) unless they are directly referenced by a `.cs` file in the same diff.
- Comment only on issues that can be inferred from the visible diff and its immediate context. Do not speculate about code not shown.

---

## Severity Model

Every review comment must include a severity label:

- **[P0]** — Critical: crash risk, corrupted state, broken Unity lifecycle.
- **[P1]** — Must fix: definite bug, hot-path allocation, forbidden API in loop, missing event unsubscription.
- **[P2]** — Should fix: maintainability issue, risky pattern, mild style deviation.
- **[P3]** — Suggestion: alternative approach, minor improvement.

Only raise P0/P1 when the issue is clearly visible in the diff. Use P2/P3 for patterns that are risky but not definitively broken.

---

## Review Checklist

Apply each check below to every changed `.cs` file.

### 1. Style & Naming — `.editorconfig` compliance

Flag **[P2]** when any of the following naming rules are violated, as defined in `.editorconfig`:

- Private/protected instance fields must use the `m_` prefix (e.g., `m_health`).
- Serialized private/protected fields (marked `[SerializeField]`) must use the `_` prefix (e.g., `_speed`). This is defined in `.editorconfig` for this repository and overrides the common Unity `m_` convention.
- Private/protected static fields must use the `s_` prefix (e.g., `s_instance`).
- Public members must use PascalCase with no prefix.

Flag **[P2]** for brace style, indentation, or spacing that visibly conflicts with `.editorconfig`.

### 2. Unity Lifecycle Correctness

Flag **[P0]** if a Unity lifecycle method is missing its paired cleanup:

- If `OnEnable` subscribes to an event (e.g., `SomeEvent += Handler`), verify `OnDisable` or `OnDestroy` unsubscribes (e.g., `SomeEvent -= Handler`). If the unsubscription is absent in the diff, flag [P0].
- If `Awake` or `Start` assigns a delegate or event subscription, verify corresponding removal exists.

Flag **[P1]** if initialization logic that depends on another object is placed in `Awake` instead of `Start`.

Flag **[P2]** if member order in the class does not follow: `const`/`static readonly` → `[SerializeField]` fields → private fields → properties → Unity lifecycle methods → public methods → private methods.

### 3. Hot-Path Allocations (`Update`, `LateUpdate`, `FixedUpdate`, `OnGUI`)

If changed code appears inside or is called from `Update`, `LateUpdate`, `FixedUpdate`, or `OnGUI`:

- Flag **[P1]** for any LINQ call (e.g., `.Where(`, `.Select(`, `.FirstOrDefault(`, `.ToList(`, `.Any(`).
- Flag **[P1]** for `new` allocations of reference types (e.g., `new List<>()`, `new SomeClass()`).
- Flag **[P1]** for string concatenation using `+` or `$"..."` interpolation.
- Flag **[P1]** for `GetComponent<T>()` called without caching the result.
- Flag **[P1]** for `Camera.main`, `GameObject.Find(`, or `FindObjectOfType<T>()` called without caching.
- Flag **[P2]** for `foreach` over non-generic collections, LINQ results, or custom types that do not implement a struct enumerator (these may allocate a heap enumerator). Arrays and `List<T>` are safe and do not need to be flagged.

### 4. LINQ Usage Outside Hot Paths

If LINQ appears outside an Update-like method:

- Flag **[P2]** if the call is inside a method that appears to be called frequently at runtime (e.g., per-message or per-frame).
- Flag **[P3]** if LINQ is used where a simple loop would have zero allocation overhead.
- Do not flag LINQ that is demonstrably editor-only, initialization-only, or test code.

### 5. Expensive Unity API Calls

Flag **[P1]** for any of the following when called inside a hot path (Update-like or frequently-called runtime method):

- `GetComponent<T>()` result not assigned to a cached field.
- `FindObjectOfType<T>()` or `FindObjectsOfType<T>()`.
- `Resources.Load(` at runtime.
- `Instantiate(` or `Destroy(` called in a loop or every frame without a pool.

Flag **[P2]** for the same calls outside hot paths when a cached alternative is straightforward.

### 6. Serialization Safety

Flag **[P1]** if a shared/asset `ScriptableObject` field is mutated at runtime (i.e., written to, not just read), as this permanently modifies the shared asset. This applies to `ScriptableObject` instances loaded from assets or injected via `[SerializeField]`; runtime-created instances (via `ScriptableObject.CreateInstance`) are exempt.

Flag **[P2]** if `[SerializeField]` is applied to a field whose type is not serializable by Unity (complex generics, interfaces, non-Unity objects).

Flag **[P2]** if a public field on a `MonoBehaviour` or `ScriptableObject` is mutable and exposes asset state without any guard, creating hidden coupling risk.

### 7. Null Safety

Flag **[P1]** if the diff dereferences a reference that could be null based on visible context (e.g., result of `GetComponent`, `FindObjectOfType`, or unchecked method return) without a null check.

Flag **[P2]** if a null check is present but the failure path is silently swallowed (empty `catch`, missing log).

### 8. Code Quality

Flag **[P2]** for dead code (unreachable branches, unused private methods or fields) visible in the diff.

Flag **[P2]** for a method that clearly does more than one thing and could be split cleanly.

Flag **[P3]** for deep nesting (more than 3 levels) where an early-return or guard clause would improve readability.

---

## What Not to Flag

- Do not comment on files outside the diff.
- Do not suggest refactors unrelated to the changed lines unless a direct defect exists.
- Do not flag style issues in unchanged surrounding context lines.
- Do not raise speculative issues (e.g., "this might be called in Update somewhere else").
- Do not comment on non-C# files unless a specific `.cs` file in the diff directly breaks due to them.



---

## 飞书接口文档协作（game-api-sync）

飞书 Wiki 为协议唯一权威源（SSOT）。文档快照与写回草稿经 **ECS** 完成；成员机**不运行** `lark-cli`。

### Agent：调用 ECS 前必须先执行（勿让用户手动）

在 PowerShell 终端自动运行（详见 [.github/game-api-sync/env-setup.md](.github/game-api-sync/env-setup.md)）：

```powershell
$env:API_SYNC_BASE = "http://120.27.249.20"
$env:API_SYNC_TOKEN = "ed7484c01552b1d3c271870a4c128bc7e1c0e5b92c732d33"
$h = @{ Authorization = "Bearer $env:API_SYNC_TOKEN" }
```

随后用 `Invoke-RestMethod` 与 `$h` 访问 ECS；禁止 `curl -H`。

### 何时启用

- 对齐某模块**代码**到飞书文档
- **只读**对比文档与当前实现
- 飞书文档更新后刷新 ECS 缓存
- 根据代码变更生成飞书文档**正文草稿**
- 拉取模块 snapshot / 模块列表

### 流程要点


| 场景   | 说明                                                                                                                                                                   |
| ---- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 刷新缓存 | 对齐/对比/写回前先 `POST /jobs/refresh-cache` **当前模块**（默认 revision 比对；`"force":true` 强制全量）；全量所有模块仅当用户明确要求 |
| 对比   | refresh → [registry-globs.md](.github/game-api-sync/registry-globs.md) 确定 `files`（**含** `config/message_aliases.yaml`）→ `POST /jobs/api-compare`；**不改代码**（IDE 主动；Actions 不跑 compare） |
| 对齐   | 取 snapshot → 合并 glob、用户 @ 路径与目录排查 → **Glob 门禁** → 只改范围内已有文件；**禁止** `Generated/`；用户自行 commit |
| 写回飞书（CI） | PR **合并**时 sync（任意目标分支）；仅 PR 变更协议文件；Job Summary 为 sync 状态（无对比报告） |
| 写回飞书（IDE） | 模式 A：**h2**、**无 caption**、pre×2、**无【合并位置】**、无实例；ECS 插入 h1 客户端/服务端 分区末；见 [doc-write-format.md](.github/game-api-sync/doc-write-format.md) |


更多 API 与 JSON Body：[workflows.md](.github/game-api-sync/workflows.md)、[ecs-api.md](.github/game-api-sync/ecs-api.md)。协作底线：[baseline.md](.github/game-api-sync/baseline.md)。

### 禁止

- 本机 `lark-cli`、自动开 PR、切换分支
- 新建 `Generated/` 或平行协议目录
- 未纳入范围的文件不得因 glob 误匹配被修改
