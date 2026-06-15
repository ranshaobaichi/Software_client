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
            "| 模型 | 编排分 | refresh | compare | align | 商店 draft | 网络 draft | 总评 |"
        )
        lines.append(
            "|------|--------|---------|---------|-------|------------|------------|------|"
        )
        for model, sc in scores:
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
            overall = sc.get("overall", "?")
            lines.append(
                f"| {model} | {orch_pct}% | {r} | {c} | {a} | {shop_d} | {net_d} | **{overall}** |"
            )
    else:
        lines.append(
            "| 模型 | 编排分 | 结果 | 商店 glob | 商店 draft | 网络 glob | 网络 draft | 总评 |"
        )
        lines.append(
            "|------|--------|------|-----------|------------|-----------|------------|------|"
        )
        for model, sc in scores:
            orch_pct = sc.get("orchestration", {}).get("pct", "-")
            mods = sc.get("modules") or {}
            shop = mods.get("商店") or {}
            net = mods.get("网络相关") or {}
            shop_g = "✓" if (shop.get("glob") or {}).get("ok") else "✗"
            net_g = "✓" if (net.get("glob") or {}).get("ok") else "✗"
            shop_d = "✓" if shop.get("ok") else "✗"
            net_d = "✓" if net.get("ok") else "✗"
            res_ok = "✓" if sc.get("all_results_ok") else "✗"
            overall = sc.get("overall", "?")
            lines.append(
                f"| {model} | {orch_pct}% | {res_ok} | {shop_g} | {shop_d} | {net_g} | {net_d} | **{overall}** |"
            )

    if not scores:
        lines.append("| _(无 runs)_ | | | | | | | |")

    lines.extend(["", "## 编排明细", ""])
    for model, sc in scores:
        details = (sc.get("orchestration") or {}).get("details") or {}
        lines.append(f"### {model}")
        if not details:
            lines.append("- _(未填写 orchestration.yaml)_")
        else:
            for k, v in details.items():
                lines.append(f"- `{k}`: {'✓' if v else '✗'}")
        lines.append("")

    lines.extend(["## 结果差异", ""])
    for model, sc in scores:
        lines.append(f"### {model}")
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
    out_path = report_dir / f"report-{scenario}-{today}.md"
    out_path.write_text("\n".join(lines), encoding="utf-8")
    print(f"Wrote {out_path}")


if __name__ == "__main__":
    main()
