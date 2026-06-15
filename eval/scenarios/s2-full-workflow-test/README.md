# s2-full-workflow-test 准备说明

## 与 s1 的区别

| 步骤 | 能力 | s1 | s2 |
|------|------|----|----|
| 1 | 刷新 ECS 缓存 | CLI 内嵌 | 显式 refresh 战斗 |
| 2 | 对比文档与代码 | CLI 内嵌 | 显式 api-compare + report_md |
| 3 | 文档 → 代码 | ✗ | 战斗 Eval 协议 → BattleProtocolApiSync_Test.cs |
| 4 | 代码 → 文档草稿 | 商店+网络 CLI | 同左（Shop 测试文件） |

## 一次性准备（主仓）

1. 将 `feishu-battle-doc-insert.md` 内容粘贴到飞书 **接口文档 · 战斗** 页。  
2. 设置 ECS 环境变量（真实 token）。  
3. 确认 `ShopProtocolApiSync_Test.cs` 存在于主仓（步骤 4 用）。  
4. **不要**提前创建 `BattleProtocolApiSync_Test.cs`（留给 Agent 步骤 3）。  
5. 生成 golden：

```powershell
python eval/tools/run_golden.py --scenario s2-full-workflow-test
```

golden 不含 align 源文件（标准答案为 `expected_battle_align.cs`）；align 由 Agent 运行时生成。

## 并行评测

与 s1 相同：每模型一个 worktree，替换 `prompt.md` 中 `{{MODEL_ID}}` 后发给 Agent。

## 打分

```powershell
python eval/tools/score_run.py --model Composer2.5 --scenario s2-full-workflow-test
python eval/tools/make_report.py --scenario s2-full-workflow-test --rescore
```
