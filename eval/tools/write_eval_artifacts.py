#!/usr/bin/env python3
"""Write eval artifacts with UTF-8 encoding (ECS refresh/compare, doc drafts)."""

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


def run_dir(model_id: str, scenario_id: str) -> Path:
    d = client_root() / "eval/runs" / model_id / scenario_id
    d.mkdir(parents=True, exist_ok=True)
    return d


def write_json(path: Path, data: dict) -> None:
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")


def cmd_ecs(model_id: str, scenario_id: str) -> None:
    scenario = load_scenario(scenario_id)
    root = client_root()
    out = run_dir(model_id, scenario_id)
    artifacts = scenario.get("run_artifacts") or {}

    module = scenario.get("battle_module", "战斗")
    refresh = refresh_module(module, force=True)
    write_json(out / artifacts.get("refresh", "refresh_battle.json"), refresh)

    compare_paths = scenario.get("compare_files") or []
    compare = api_compare(
        module=module,
        repo=scenario.get("repo", "client"),
        repo_root=root,
        paths=compare_paths,
        scoped=True,
    )
    compare_json = out / artifacts.get("compare", "compare_battle.json")
    write_json(compare_json, compare)

    report_md = compare.get("report_md") or ""
    report_path = out / artifacts.get("compare_report", "compare_report.md")
    report_path.write_text(report_md, encoding="utf-8")

    print(f"Wrote {out / artifacts.get('refresh', 'refresh_battle.json')}")
    print(f"Wrote {compare_json}")
    print(f"Wrote {report_path}")


def cmd_doc_draft(model_id: str, scenario_id: str) -> None:
    scenario = load_scenario(scenario_id)
    root = client_root()
    out = run_dir(model_id, scenario_id)
    artifacts = scenario.get("run_artifacts") or {}
    central_rel = scenario.get("central_repo", "../game-api-sync")
    central = (root / central_rel).resolve()
    script = central / "scripts" / "agent_doc_draft.py"
    if not script.is_file():
        raise FileNotFoundError(f"central script not found: {script}")

    doc_file = scenario.get("doc_draft_file") or scenario.get("test_file", "")
    env = os.environ.copy()
    env["PYTHONIOENCODING"] = "utf-8"

    for mod in scenario.get("doc_draft_modules") or scenario.get("modules") or []:
        name = mod["name"]
        run_key = mod.get("run_file_key") or ("shop_draft" if name == "商店" else "network_draft")
        out_name = artifacts.get(run_key, f"{name}.json")
        proc = subprocess.run(
            [
                sys.executable,
                str(script),
                "--module",
                name,
                "--repo",
                scenario.get("repo", "client"),
                "--paths",
                doc_file,
            ],
            cwd=root,
            capture_output=True,
            env=env,
        )
        stdout = (proc.stdout or b"").decode("utf-8", errors="replace")
        stderr = (proc.stderr or b"").decode("utf-8", errors="replace")
        if proc.returncode != 0:
            raise RuntimeError(stderr or stdout or f"agent_doc_draft failed: {name}")
        out_path = out / out_name
        out_path.write_text(stdout.strip() + "\n", encoding="utf-8")
        print(f"Wrote {out_path}")


def main() -> None:
    parser = argparse.ArgumentParser(description="Write eval artifacts (UTF-8)")
    sub = parser.add_subparsers(dest="command", required=True)

    p_ecs = sub.add_parser("ecs", help="refresh-cache + api-compare")
    p_ecs.add_argument("--model", required=True)
    p_ecs.add_argument("--scenario", default="s2-full-workflow-test")

    p_doc = sub.add_parser("doc-draft", help="agent_doc_draft for configured modules")
    p_doc.add_argument("--model", required=True)
    p_doc.add_argument("--scenario", default="s2-full-workflow-test")

    args = parser.parse_args()
    if args.command == "ecs":
        cmd_ecs(args.model, args.scenario)
    elif args.command == "doc-draft":
        cmd_doc_draft(args.model, args.scenario)


if __name__ == "__main__":
    main()
