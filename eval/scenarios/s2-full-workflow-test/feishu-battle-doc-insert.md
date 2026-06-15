# 飞书「接口文档 · 战斗」待插入内容（Eval 用）

> **用途**：测试 game-api-sync **「根据文档更新代码」**（步骤 3）。  
> 请将下文按说明粘贴到飞书 Wiki **[接口文档 → 战斗](https://my.feishu.cn/wiki/)** 对应叶子页（obj: `ZxhjdUHTnoceWpxEk5ScAzvgn07`）。  
> **粘贴完成后**再运行 `run_golden.py --scenario s2-full-workflow-test` 与各模型 eval。

---

## 插入位置（模式 A · 仅 client 仓评测）

你已添加的飞书页面（示例）：

- 类型约束 / 枚举：<https://my.feishu.cn/wiki/G0eowbDini7wrlkIclkcegzNnce>
- 接口文档 / 客户端请求：<https://my.feishu.cn/wiki/ID0twsHU3iTwPyk80blcadzCnJc>

战斗接口文档页内 **`h1 客户端`** 分区末尾新增 **`h2 Eval战斗测试协议`**。

---

## h1 客户端 分区内新增

### h2 标题

```text
Eval战斗测试协议
```

### 代码块 1 — 枚举（无 caption 或 caption 留空）

语言：`TypeScript`

```typescript
enum BattleTestActionKind {
  move = 0;
  skill = 1;
  defend = 2;
}
```

### 代码块 2 — 客户端请求（caption = 客户端）

语言：`TypeScript`

```typescript
BattleTestActionRequest: {
  uid: string;           // 玩家 uid
  turnIndex: number;     // 当前回合序号，从 1 开始
  actionKind: BattleTestActionKind;
  targetId: string;      // 目标实体 id，无目标时传空字符串
};
```

---

## h1 服务端 分区内新增

> **本次 client-only eval 可跳过**；步骤 3 对齐只需 enum + `BattleTestActionRequest`。

<!--

### h2 标题

```text
Eval战斗测试协议
```

### 代码块 1 — 回合开始通知（caption = 服务端）

语言：`TypeScript`

```typescript
BattleTestTurnStartNotify: {
  turnIndex: number;
  activePlayerId: string;  // 本回合行动玩家 uid
};
```

### 代码块 2 — 行动结果响应（caption = 服务端）

语言：`TypeScript`

```typescript
BattleTestActionResponse: {
  accepted: boolean;
  reason: string;          // rejected 时说明原因，accepted 时可空字符串
};
```

-->

---

## 插入后 Agent 应对齐的 C# 目标

对齐完成后，代码应出现在（由 Agent 新建）：

`Assets/Scripts/Network/NetModels/BattleProtocolApiSync_Test.cs`

字段与类型名须与上文一致（**enum + BattleTestActionRequest**）。标准答案见同目录 `expected_battle_align.cs`。

---

## 注意事项

1. 字段名、类型名 **不要改**，否则 golden / 打分符号会对不上。  
2. 插入后请对 **战斗** 模块执行 **强制刷新 ECS 缓存**（`{"module":"战斗","force":true}`），再开始 eval。  
3. 本段内容 **仅用于评测**，审阅后可从飞书删除或保留在独立 h2 下。
