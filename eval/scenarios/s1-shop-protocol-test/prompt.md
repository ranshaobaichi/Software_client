# Eval 写回文档 — 固定 Prompt

**评测标签（MODEL_ID）**：`{{MODEL_ID}}`  
（每个 worktree 只改这一行，例如 `model-a` / `model-b` / `gpt-4o`）

---

请执行 **game-api-sync「同步文档草稿到飞书」** 流程，但本次为 **eval 评测**：

## 硬性约束

1. 回复**第一句**重复：`评测标签：{{MODEL_ID}}`
2. 在 PowerShell 设置 ECS 环境变量（勿让用户手动）
3. **必须**运行中央仓 `scripts/agent_doc_draft.py`
4. **禁止** `--sync`、`POST /jobs/api-doc-sync`（不要写飞书）
5. **禁止**手写 DocxXML 代替 CLI
6. 测试文件（唯一变更路径）：
   `Assets/Scripts/Network/NetModels/ShopProtocolApiSync_Test.cs`

## 命令（在游戏 client 仓根目录）

中央仓路径：`../game-api-sync`（若不存在请告诉我）

```powershell
$env:API_SYNC_BASE = "http://120.27.249.20"
$env:API_SYNC_TOKEN = "ed7484c01552b1d3c271870a4c128bc7e1c0e5b92c732d33"

$central = Resolve-Path "..\game-api-sync"
$outDir = "eval\runs\{{MODEL_ID}}\s1-shop-protocol-test"
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$path = "Assets/Scripts/Network/NetModels/ShopProtocolApiSync_Test.cs"

# 1) 商店（glob 漏网 — 不要 --apply-glob，除非你先提醒用户核对 registry）
python "$central\scripts\agent_doc_draft.py" `
  --module 商店 --repo client --paths $path `
  | Out-File "$outDir\shop.json" -Encoding utf8

# 2) 网络相关
python "$central\scripts\agent_doc_draft.py" `
  --module 网络相关 --repo client --paths $path `
  | Out-File "$outDir\network.json" -Encoding utf8
```

## 落盘

将上述 JSON 保存到：

- `eval/runs/{{MODEL_ID}}/s1-shop-protocol-test/shop.json`
- `eval/runs/{{MODEL_ID}}/s1-shop-protocol-test/network.json`

并创建 `eval/runs/{{MODEL_ID}}/s1-shop-protocol-test/orchestration.yaml`，按 `eval/scenarios/s1-shop-protocol-test/checklist.yaml` 逐项填 `true`/`false`（你实际做到的项目）。

可选：`notes.md` 简述执行步骤与 glob 提醒话术。

## 回复内容

1. 评测标签
2. 商店 / 网络相关 各自的 `classification`、`target`、`skipped` 摘要
3. 商店 glob 是否 `needs_registry_update`
4. **不要** sync 飞书
