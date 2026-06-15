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

## 并行评测

每个模型 **一个 git worktree + 一个 Cursor 窗口**：

```powershell
git worktree add E:\Desktop\eval-wt-composer -b eval/Composer2.5
# 从主仓复制 eval/ 与 ShopProtocolApiSync_Test.cs 到 worktree（若未 commit）
```

在各 worktree：

1. 替换 `eval/scenarios/s2-full-workflow-test/prompt.md` 中 `{{MODEL_ID}}`（如 `Composer2.5` / `Grok4.3` / `KimiK2.5`）。
2. Agent 模式 + `@prompt.md` 执行四流程。
3. 填写扁平格式 `orchestration.yaml`（复制 `orchestration.template.yaml`）。

## 打分与报告

```powershell
python eval/tools/score_run.py --model Composer2.5 --scenario s2-full-workflow-test
python eval/tools/make_report.py --scenario s2-full-workflow-test --rescore
```

## 注意

- `eval/runs/` 勿 commit。
- 评测 **不要** `--sync`。
- golden 生成前须已完成飞书战斗文档插入并 refresh 成功。
