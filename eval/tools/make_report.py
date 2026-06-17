#!/usr/bin/env python3
"""Aggregate eval runs into a Markdown report."""

from __future__ import annotations

import argparse
import json
import subprocess
import sys
from datetime import date
from pathlib import Path


def _client_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _load_score(path: Path) -> dict | None:
    if not path.is_file():
        return None
    return json.loads(path.read_text(encoding="utf-8"))


def main() -> None:
    parser = argparse.ArgumentParser(description="Make eval report from all runs")
    parser.add_argument("--scenario", default="s2-full-workflow-test")
    parser.add_argument("--rescore", action="store_true", help="re-run score_run.py for each model")
    args = parser.parse_args()

    client_root = _client_root()
    runs_root = client_root / "eval/runs"
    scenario = args.scenario
    scores: list[tuple[str, dict]] = []

    if not runs_root.is_dir():
        print("No eval/runs directory", file=sys.stderr)
        sys.exit(1)

    score_script = client_root / "eval/tools/score_run.py"

    for model_dir in sorted(runs_root.iterdir()):
        if not model_dir.is_dir() or model_dir.name.startswith("."):
            continue
        run_scenario = model_dir / scenario
        if not run_scenario.is_dir():
            continue
        if args.rescore:
            subprocess.run(
                [sys.executable, str(score_script), "--model", model_dir.name, "--scenario", scenario],
                cwd=client_root,
                check=False,
            )
        score_path = run_scenario / "score.json"
        data = _load_score(score_path)
        if data:
            scores.append((model_dir.name, data))

    today = date.today().isoformat()
    is_s2 = bool(scores and scores[0][1].get("steps"))
    lines = [
        f"# Eval 报告 — {scenario}",
        "",
        f"- 日期：{today}",
        f"- 场景：{'四流程 eval（真实 ECS，无 sync）' if is_s2 else '写回文档（真实 ECS，无 sync）'}",
        "- 评分：准确性 50% + 完整性 30% + 规范性 20%",
    ]
    if is_s2:
        lines.append("- 流程：refresh → compare → align → doc draft")
    lines.extend(
        [
            f"- Shop 测试文件：`Assets/Scripts/Network/NetModels/ShopProtocolApiSync_Test.cs`",
            "",
            "## 总表",
            "",
        ]
    )

    if is_s2:
        lines.append(
            "| 模型 | 总分 | 等级 | 准确性 | 完整性 | 规范性 | 编排分 | refresh | compare | align | 商店 | 网络 |"
        )
        lines.append(
            "|------|------|------|--------|--------|--------|--------|---------|---------|-------|------|------|"
        )
        for model, sc in scores:
            dims = sc.get("dimensions") or {}
            orch_pct = sc.get("orchestration", {}).get("pct", "-")
            steps = sc.get("steps") or {}
            mods = sc.get("modules") or {}
            shop = mods.get("商店") or {}
            net = mods.get("网络相关") or {}
            r = "✓" if (steps.get("refresh") or {}).get("ok") else "✗"
            c = "✓" if (steps.get("compare") or {}).get("ok") else "✗"
            a = "✓" if (steps.get("align") or {}).get("ok") else "✗"
            shop_d = "✓" if shop.get("ok") else "✗"
            net_d = "✓" if net.get("ok") else "✗"
            enc = steps.get("encoding") or {}
            enc_note = " (编码已修复)" if enc.get("repaired") else ""
            lines.append(
                f"| {model} | **{dims.get('total', '-')}** | {sc.get('overall', '?')} | "
                f"{dims.get('accuracy', '-')} | {dims.get('completeness', '-')} | "
                f"{dims.get('conformance', '-')}{enc_note} | {orch_pct}% | {r} | {c} | {a} | {shop_d} | {net_d} |"
            )
    else:
        lines.append(
            "| 模型 | 总分 | 等级 | 准确性 | 完整性 | 规范性 | 编排分 | 商店 | 网络 |"
        )
        lines.append(
            "|------|------|------|--------|--------|--------|--------|------|------|"
        )
        for model, sc in scores:
            dims = sc.get("dimensions") or {}
            orch_pct = sc.get("orchestration", {}).get("pct", "-")
            mods = sc.get("modules") or {}
            shop = mods.get("商店") or {}
            net = mods.get("网络相关") or {}
            shop_d = "✓" if shop.get("ok") else "✗"
            net_d = "✓" if net.get("ok") else "✗"
            lines.append(
                f"| {model} | **{dims.get('total', '-')}** | {sc.get('overall', '?')} | "
                f"{dims.get('accuracy', '-')} | {dims.get('completeness', '-')} | "
                f"{dims.get('conformance', '-')} | {orch_pct}% | {shop_d} | {net_d} |"
            )

    if not scores:
        lines.append("| _(无 runs)_ | | | | | | | | | | | | |")

    lines.extend(["", "## 维度说明", ""])
    lines.extend(
        [
            "| 维度 | 权重 | 含义 |",
            "|------|------|------|",
            "| **准确性** | 50% | 产物与 golden/飞书预期的一致程度（refresh、compare 符号、align 类型、doc draft 符号与分类） |",
            "| **完整性** | 30% | 必需产物是否齐全、编排 checklist 完成度 |",
            "| **规范性** | 20% | UTF-8 落盘、orchestration 扁平格式、glob 门禁提醒等工程规范 |",
            "",
            "## 编排明细",
            "",
        ]
    )

    for model, sc in scores:
        details = (sc.get("orchestration") or {}).get("details") or {}
        lines.append(f"### {model}")
        dims = sc.get("dimensions") or {}
        lines.append(
            f"- 总分 **{dims.get('total', '-')}**（{sc.get('overall', '?')}）"
            f" — 准确性 {dims.get('accuracy', '-')}"
            f" / 完整性 {dims.get('completeness', '-')}"
            f" / 规范性 {dims.get('conformance', '-')}"
        )
        enc = (sc.get("steps") or {}).get("encoding") or {}
        if enc.get("repaired"):
            lines.append("- compare 报告经打分脚本从 JSON `report_md` 修复 UTF-8（规范性扣分）")
        if not details:
            lines.append("- _(未填写 orchestration.yaml)_")
        else:
            for k, v in details.items():
                lines.append(f"- `{k}`: {'✓' if v else '✗'}")
        lines.append("")

    lines.extend(["## 结果差异", ""])
    for model, sc in scores:
        lines.append(f"### {model}")
        steps = sc.get("steps") or {}
        for step_name in ("refresh", "compare", "align"):
            step = steps.get(step_name) or {}
            if step.get("error"):
                lines.append(f"- **{step_name}**: {step['error']}")
            elif step.get("ok") is False and step_name in steps:
                lines.append(f"- **{step_name}**: 未通过")
        for mod_name, mod in (sc.get("modules") or {}).items():
            issues = (mod.get("compare") or {}).get("issues") or []
            if mod.get("error"):
                lines.append(f"- **{mod_name}**: {mod['error']}")
            elif issues:
                lines.append(f"- **{mod_name}**: " + "; ".join(issues))
            elif mod.get("ok"):
                lines.append(f"- **{mod_name}**: OK")
            else:
                lines.append(f"- **{mod_name}**: 未通过（见 score.json）")
        lines.append("")

    report_dir = client_root / "eval/reports"
    report_dir.mkdir(parents=True, exist_ok=True)
    short_name = scenario.replace("s2-", "").replace("s1-", "")
    out_path = report_dir / f"report-{short_name}-{today}.md"
    out_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {out_path}")


if __name__ == "__main__":
    main()
