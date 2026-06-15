# Golden 标准答案

由 `eval/tools/run_golden.ps1` 生成（真实 ECS 链路，**无 `--sync`**）。

| 文件 | 模块 | 期望 target |
|------|------|-------------|
| `shop.api_docs.json` | 商店 | api_docs |
| `network.type_constraints.json` | 网络相关 | type_constraints |

若飞书 snapshot 变更导致 golden 漂移，在同一时段对所有 worktree 重跑 `run_golden.ps1` 后再评测。
