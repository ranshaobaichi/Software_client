#!/usr/bin/env python3
"""Generate eval golden JSON via ECS + agent_doc_draft (real ECS, no --sync)."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from pathlib import Path

try:
    import yaml
except ImportError:
    yaml = None  # type: ignore

SCRIPT_DIR = Path(__file__).resolve().parent
if str(SCRIPT_DIR) not in sys.path:
    sys.path.insert(0, str(SCRIPT_DIR))

from ecs_helpers import api_compare, refresh_module  # noqa: E402


def client_root() -> Path:
    return Path(__file__).resolve().parents[2]


def load_scenario(scenario_id: str) -> dict:
    if yaml is None:
        raise RuntimeError("pip install pyyaml")
    path = client_root() / "eval/scenarios" / scenario_id / "scenario.yaml"
    return yaml.safe_load(path.read_text(encoding="utf-8")) or {}


def run_agent_doc_draft(
    *,
    central: Path,
    root: Path,
    module: str,
    repo: str,
    paths: list[str],
    env: dict[str, str],
) -> str:
    script = central / "scripts" / "agent_doc_draft.py"
    if not script.is_file():
        raise FileNotFoundError(f"central repo script not found: {script}")
    proc = subprocess.run(
        [
            sys.executable,
            str(script),
            "--module",
            module,
            "--repo",
            repo,
            "--paths",
            *paths,
        ],
        cwd=root,
        capture_output=True,
        env=env,
    )
    stdout = (proc.stdout or b"").decode("utf-8", errors="replace")
    stderr = (proc.stderr or b"").decode("utf-8", errors="replace")
    if proc.returncode != 0:
        raise RuntimeError(stderr or stdout or f"agent_doc_draft failed: {module}")
    return stdout


def golden_s1(root: Path, scenario: dict, scenario_id: str, central: Path, golden_dir: Path) -> None:
    test_file = scenario.get("test_file", "")
    env = os.environ.copy()
    env["PYTHONIOENCODING"] = "utf-8"
    golden_map = scenario.get("golden_files") or {
        "商店": "shop.api_docs.json",
        "网络相关": "network.type_constraints.json",
    }
    for mod in scenario.get("modules") or []:
        name = mod["name"]
        out_name = golden_map.get(name, f"{name}.json")
        out_path = golden_dir / out_name
        print(f"Golden: {name} -> {out_path}")
        stdout = run_agent_doc_draft(
            central=central,
            root=root,
            module=name,
            repo=scenario.get("repo", "client"),
            paths=[test_file],
            env=env,
        )
        out_path.write_text(stdout, encoding="utf-8")


def golden_s2(root: Path, scenario: dict, scenario_id: str, central: Path, golden_dir: Path) -> None:
    env = os.environ.copy()
    env["PYTHONIOENCODING"] = "utf-8"
    battle = scenario.get("battle_module", "战斗")
    artifacts = scenario.get("run_artifacts") or {}
    golden_files = scenario.get("golden_files") or {}

    # 1) refresh
    print(f"Golden: refresh {battle} -> {artifacts.get('refresh', 'refresh_battle.json')}")
    refresh_out = refresh_module(battle, force=True)
    refresh_path = golden_dir / artifacts.get("refresh", "refresh_battle.json")
    refresh_path.write_text(json.dumps(refresh_out, ensure_ascii=False, indent=2), encoding="utf-8")

    # 2) compare
    compare_paths = scenario.get("compare_files") or []
    print(f"Golden: compare {battle} -> {artifacts.get('compare', 'compare_battle.json')}")
    compare_out = api_compare(
        module=battle,
        repo=scenario.get("repo", "client"),
        repo_root=root,
        paths=compare_paths,
    )
    compare_path = golden_dir / golden_files.get("compare", artifacts.get("compare", "compare_battle.json"))
    compare_path.write_text(json.dumps(compare_out, ensure_ascii=False, indent=2), encoding="utf-8")
    report_md = compare_out.get("report_md") or ""
    report_path = golden_dir / artifacts.get("compare_report", "compare_report.md")
    report_path.write_text(report_md, encoding="utf-8")

    # 3) align golden = expected cs copy
    align_golden_name = scenario.get("align_golden", "expected_battle_align.cs")
    src_align = root / "eval/scenarios" / scenario_id / align_golden_name
    if src_align.is_file():
        dst = golden_dir / align_golden_name
        dst.write_text(src_align.read_text(encoding="utf-8"), encoding="utf-8")
        print(f"Golden: align reference -> {dst}")

    # 4) doc drafts
    doc_file = scenario.get("doc_draft_file", "")
    if not doc_file:
        raise ValueError("doc_draft_file required for s2")
    doc_path = root / doc_file.replace("/", os.sep)
    if not doc_path.is_file():
        raise FileNotFoundError(f"doc draft test file missing: {doc_path}")

    draft_modules = [
        ("商店", golden_files.get("shop_draft", "shop_doc_draft.json")),
        ("网络相关", golden_files.get("network_draft", "network_doc_draft.json")),
    ]
    for mod_name, out_name in draft_modules:
        out_path = golden_dir / out_name
        print(f"Golden: doc draft {mod_name} -> {out_path}")
        stdout = run_agent_doc_draft(
            central=central,
            root=root,
            module=mod_name,
            repo=scenario.get("repo", "client"),
            paths=[doc_file.replace("\\", "/")],
            env=env,
        )
        out_path.write_text(stdout, encoding="utf-8")


def main() -> None:
    parser = argparse.ArgumentParser(description="Generate eval golden JSON")
    parser.add_argument("--scenario", default="s2-full-workflow-test")
    parser.add_argument("--central-repo", default="")
    args = parser.parse_args()

    root = client_root()
    scenario = load_scenario(args.scenario)
    central = Path(args.central_repo) if args.central_repo else root.parent / "game-api-sync"
    central = central.resolve()

    golden_dir = root / "eval/golden" / args.scenario
    golden_dir.mkdir(parents=True, exist_ok=True)

    if scenario.get("run_artifacts"):
        golden_s2(root, scenario, args.scenario, central, golden_dir)
    else:
        golden_s1(root, scenario, args.scenario, central, golden_dir)

    print(f"Done. Review {golden_dir}")


if __name__ == "__main__":
    main()
