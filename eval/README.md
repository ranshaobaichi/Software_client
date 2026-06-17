# game-api-sync Eval（client 仓）

在 **真实 ECS 链路** 下评测 Cursor Agent 对 game-api-sync 四类工作流的执行能力。**禁止** `--sync` / 不写飞书正文。

## 当前推荐场景：s2-full-workflow-test

| 步骤 | 能力 | 说明 |
|------|------|------|
| 1 | 刷新 ECS 缓存 | `POST /jobs/refresh-cache`（战斗，force） |
| 2 | 对比文档与代码 | `POST /jobs/api-compare` → `compare_report.md` |
| 3 | 文档 → 代码 | 飞书战斗 Eval 协议 → `BattleProtocolApiSync_Test.cs` |
| 4 | 代码 → 文档草稿 | `agent_doc_draft.py`（Shop 测试文件 → 商店 + 网络相关） |

详见 `eval/scenarios/s2-full-workflow-test/README.md`。

### 测试文件

| 路径 | 用途 |
|------|------|
| `eval/scenarios/s2-full-workflow-test/feishu-battle-doc-insert.md` | **你手动粘贴到飞书** 的战斗 Eval 协议 |
| `eval/scenarios/s2-full-workflow-test/expected_battle_align.cs` | 步骤 3 标准答案参考 |
| `Assets/Scripts/Network/NetModels/ShopProtocolApiSync_Test.cs` | 步骤 4 代码→文档（故意不在商店 glob） |
| `Assets/Scripts/Network/NetModels/BattleProtocolApiSync_Test.cs` | 步骤 3 由 Agent **新建**（测试前勿创建） |

## 目录

```text
eval/
├── scenarios/s2-full-workflow-test/   # 当前场景（prompt、checklist、飞书插入稿）
├── scenarios/s1-shop-protocol-test/   # 旧场景（仅步骤 4 子集，保留兼容）
├── golden/<scenario>/                 # run_golden 生成
├── runs/<model-id>/...                # 各模型产物（git 忽略）
├── reports/
└── tools/
```

## 一次性准备（s2）

1. 将 `feishu-battle-doc-insert.md` 粘贴到飞书 **接口文档 · 战斗** 页。
2. 设置 ECS 环境变量（真实 token，勿用 `<token>` 占位符）：

```powershell
$env:API_SYNC_BASE = "http://120.27.249.20"
$env:API_SYNC_TOKEN = "<你的真实token>"
```

3. 确认 `ShopProtocolApiSync_Test.cs` 存在；**不要**提前创建 `BattleProtocolApiSync_Test.cs`。
4. 生成 golden（主仓根目录）：

```powershell
python eval/tools/run_golden.py --scenario s2-full-workflow-test
```

## 打分与报告

采用 **三维度打分**（0–100，加权总分）：

| 维度 | 权重 | 含义 |
|------|------|------|
| 准确性 | 50% | 产物与 golden / 飞书预期一致程度 |
| 完整性 | 30% | 必需产物齐全 + 编排 checklist |
| 规范性 | 20% | UTF-8 落盘、扁平 orchestration、glob 提醒 |

步骤 1/2/4 请用 `eval/tools/write_eval_artifacts.py`（避免 PowerShell 中文编码问题）。打分脚本会在 compare 乱码时从 JSON `report_md` 自动修复（准确性按修复后内容计，规范性扣分）。

```powershell
python eval/tools/score_run.py --model Composer2.5 --scenario s2-full-workflow-test
python eval/tools/make_report.py --scenario s2-full-workflow-test --rescore
```

## 并行评测（worktree）

每个模型 **一个 git worktree（桌面目录）+ 一个 Cursor 窗口**：

| 目录 | 分支 | MODEL_ID |
|------|------|----------|
| `E:\Desktop\eval-wt-composer` | `eval/Composer2.5` | `Composer2.5` |
| `E:\Desktop\eval-wt-grok` | `eval/Grok4.3` | `Grok4.3` |
| `E:\Desktop\eval-wt-kimi` | `eval/KimiK2.5` | `KimiK2.5` |

Agent 模式发送：

```text
请严格按照 @eval/scenarios/s2-full-workflow-test/prompt.md 的全部要求执行 eval 四流程。
不要省略步骤，不要 --sync，步骤 1/2/4 必须用 write_eval_artifacts.py 落盘。
```

## 注意

- `eval/runs/` 已 gitignore：**不追踪、切换分支保留本地文件**。
- 评测 **不要** `--sync`。
- golden 生成前须已完成飞书战斗文档插入并 refresh 成功。
