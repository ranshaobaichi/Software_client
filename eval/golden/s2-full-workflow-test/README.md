# golden — s2-full-workflow-test

在飞书插入 `feishu-battle-doc-insert.md` 后，于主仓运行：

```powershell
python eval/tools/run_golden.py --scenario s2-full-workflow-test
```

生成：`refresh_battle.json`、`compare_battle.json`、`compare_report.md`、`shop_doc_draft.json`、`network_doc_draft.json`、`expected_battle_align.cs`。
