# Eval 四流程 — 固定 Prompt

**评测标签（MODEL_ID）**：`{{MODEL_ID}}`

---

请按顺序执行 **game-api-sync 完整 eval 流程**（真实 ECS；**禁止 sync 飞书**）。

## 硬性约束

1. 回复**第一句**：`评测标签：{{MODEL_ID}}`
2. 在 PowerShell **自行**设置 ECS 环境变量（勿让用户手动）
3. **禁止** `--sync`、`POST /jobs/api-doc-sync`
4. **禁止**手写 DocxXML 代替 CLI
5. 步骤 1/2/4 **必须**用本仓 `eval/tools/write_eval_artifacts.py` 落盘（UTF-8，勿用 `Out-File` / `ConvertTo-Json` 写 JSON 或 compare 报告）
6. 步骤 3 对齐代码：新建 `Assets/Scripts/Network/NetModels/BattleProtocolApiSync_Test.cs`（勿改其他业务文件）
7. `orchestration.yaml` **必须**复制 `orchestration.template.yaml` 结构，只改 `true`/`false`

## 前置（负责人已完成）

飞书「接口文档 · 战斗」已插入 `eval/scenarios/s2-full-workflow-test/feishu-battle-doc-insert.md` 中的 Eval 协议内容。

---

## 步骤 0 — 设置 ECS 环境变量

```powershell
$env:API_SYNC_BASE = "http://120.27.249.20"
$env:API_SYNC_TOKEN = "ed7484c01552b1d3c271870a4c128bc7e1c0e5b92c732d33"
```

---

## 步骤 1 & 2 — 刷新缓存 + 对比文档与代码（战斗）

**一条命令**完成 refresh-cache 与 api-compare，并写入 UTF-8 产物：

```powershell
python eval/tools/write_eval_artifacts.py ecs --model {{MODEL_ID}} --scenario s2-full-workflow-test
```

产物路径：

- `eval/runs/{{MODEL_ID}}/s2-full-workflow-test/refresh_battle.json`
- `eval/runs/{{MODEL_ID}}/s2-full-workflow-test/compare_battle.json`
- `eval/runs/{{MODEL_ID}}/s2-full-workflow-test/compare_report.md`

向用户展示 `compare_report.md` 摘要（须含 Eval 协议在代码侧 missing_in_code）。

---

## 步骤 3 — 根据文档更新代码（战斗）

阅读对比报告与飞书 Eval 章节，**新建**：

`Assets/Scripts/Network/NetModels/BattleProtocolApiSync_Test.cs`

须包含 **`BattleTestActionKind`（enum）** 与 **`BattleTestActionRequest`（客户端消息）**；字段与飞书 Eval 章节一致（本次仅 client 仓评测，**不需要**服务端 Notify/Response）。参考 `eval/scenarios/s2-full-workflow-test/expected_battle_align.cs`。

**不要** `--apply-glob` 改 wiki-registry（本文件不在战斗 glob，对齐后仅用于评测）。

---

## 步骤 4 — 根据代码生成本地文档草稿（Shop 测试文件）

```powershell
python eval/tools/write_eval_artifacts.py doc-draft --model {{MODEL_ID}} --scenario s2-full-workflow-test
```

产物：

- `eval/runs/{{MODEL_ID}}/s2-full-workflow-test/shop_doc_draft.json`
- `eval/runs/{{MODEL_ID}}/s2-full-workflow-test/network_doc_draft.json`

Shop 文件 **不在** 商店 glob — 提醒用户核对 registry，**不要** 擅自 `--apply-glob`。

---

## 落盘清单

```
eval/runs/{{MODEL_ID}}/s2-full-workflow-test/
├── refresh_battle.json
├── compare_battle.json
├── compare_report.md
├── shop_doc_draft.json
├── network_doc_draft.json
├── orchestration.yaml          # 扁平 true/false
└── notes.md                    # 可选
```

以及新建：`Assets/Scripts/Network/NetModels/BattleProtocolApiSync_Test.cs`

---

## 回复内容

1. 评测标签  
2. refresh 是否成功（战斗是否在 modules 中）  
3. compare 报告要点（Eval 协议在代码侧缺失项）  
4. 对齐文件路径与主要类型列表  
5. 商店 / 网络相关 doc draft 的 classification、glob、needs_registry_update  
6. **不要** sync 飞书
