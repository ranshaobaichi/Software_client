#!/usr/bin/env python3
"""Score one model run against golden + scenario expectations."""

from __future__ import annotations

import argparse
import json
import os
import re
import sys
from pathlib import Path
from typing import Any

try:
    import yaml
except ImportError:
    yaml = None  # type: ignore


def _client_root() -> Path:
    return Path(__file__).resolve().parents[2]


def _load_yaml(path: Path) -> dict[str, Any]:
    if yaml is None:
        raise RuntimeError("pyyaml required: pip install pyyaml")
    raw = path.read_bytes()
    for enc in ("utf-8-sig", "utf-8", "gbk"):
        try:
            return yaml.safe_load(raw.decode(enc)) or {}
        except UnicodeDecodeError:
            continue
    return yaml.safe_load(raw.decode("utf-8", errors="replace")) or {}


def _load_json(path: Path) -> dict[str, Any]:
    text = path.read_text(encoding="utf-8-sig").strip()
    if not text:
        return {"ok": False, "error": "empty_file"}
    return json.loads(text)


def _load_text(path: Path) -> str:
    if not path.is_file():
        return ""
    return path.read_text(encoding="utf-8-sig", errors="replace")


def _norm_draft(s: str) -> str:
    return re.sub(r"\s+", " ", (s or "").strip())


def _draft_for_target(data: dict[str, Any], target: str) -> str:
    for d in data.get("drafts") or []:
        if d.get("target") == target and not d.get("skipped"):
            return d.get("docx_draft") or ""
    return ""


def _classification_of(data: dict[str, Any], target: str) -> str | None:
    for d in data.get("drafts") or []:
        if d.get("target") == target:
            cls = d.get("classification") or {}
            return cls.get("classification")
    return None


def _glob_field(data: dict[str, Any], key: str) -> Any:
    g = data.get("glob") or {}
    return g.get(key)


def _score_symbols(text: str, include: list[str], exclude: list[str]) -> dict[str, Any]:
    ok_inc = all(sym in text for sym in include)
    bad_exc = [sym for sym in exclude if sym in text]
    return {
        "include_ok": ok_inc,
        "missing_include": [s for s in include if s not in text],
        "exclude_violations": bad_exc,
        "ok": ok_inc and not bad_exc,
    }


def _orchestration_score(run_dir: Path, checklist: dict[str, Any]) -> dict[str, Any]:
    orch_path = run_dir / "orchestration.yaml"
    if not orch_path.is_file():
        return {
            "filled": False,
            "score": 0,
            "max": sum(i.get("weight", 1) for i in checklist.get("items") or []),
            "details": {},
        }
    orch = _load_yaml(orch_path)
    details: dict[str, bool] = {}
    earned = 0
    total = 0
    for item in checklist.get("items") or []:
        iid = item["id"]
        w = int(item.get("weight", 1))
        total += w
        val = bool(orch.get(iid, False))
        details[iid] = val
        if val:
            earned += w
    return {
        "filled": True,
        "score": earned,
        "max": total,
        "pct": round(100 * earned / total, 1) if total else 0,
        "details": details,
    }


def _compare_to_golden(
    run_data: dict[str, Any],
    golden_data: dict[str, Any],
    module_cfg: dict[str, Any],
) -> dict[str, Any]:
    targets = module_cfg.get("expected_targets") or []
    sym_cfg = module_cfg.get("_symbols") or {}
    issues: list[str] = []
    draft_checks: list[dict[str, Any]] = []

    for tgt in targets:
        run_draft = _draft_for_target(run_data, tgt)
        gold_draft = _draft_for_target(golden_data, tgt)
        if not run_draft and not any(
            d.get("target") == tgt and d.get("skipped") for d in run_data.get("drafts") or []
        ):
            issues.append(f"no draft for target {tgt}")
        if gold_draft and _norm_draft(run_draft) != _norm_draft(gold_draft):
            sym = sym_cfg.get(tgt) or {}
            sc = _score_symbols(
                run_draft,
                sym.get("include") or [],
                sym.get("exclude") or [],
            )
            draft_checks.append({"target": tgt, "exact_match": False, "symbols": sc})
            if not sc["ok"]:
                issues.append(f"draft symbol mismatch for {tgt}")
        else:
            draft_checks.append({"target": tgt, "exact_match": True, "symbols": {"ok": True}})

    run_cls = _classification_of(run_data, targets[0]) if targets else None
    gold_cls = _classification_of(golden_data, targets[0]) if targets else None
    if run_cls != gold_cls and gold_cls is not None:
        issues.append(f"classification {run_cls} != golden {gold_cls}")

    return {
        "draft_checks": draft_checks,
        "issues": issues,
        "ok": not issues,
    }


def score_s1(
    *,
    client_root: Path,
    scenario: dict[str, Any],
    scenario_id: str,
    model_id: str,
    run_dir: Path,
    golden_dir: Path,
    orch: dict[str, Any],
) -> dict[str, Any]:
    test_file = scenario.get("test_file", "")
    run_files = scenario.get("run_files") or {
        "商店": "shop.json",
        "网络相关": "network.json",
    }
    golden_files = scenario.get("golden_files") or {
        "商店": "shop.api_docs.json",
        "网络相关": "network.type_constraints.json",
    }
    expected_symbols = scenario.get("expected_symbols") or {}
    modules_out: dict[str, Any] = {}
    all_ok = True

    for mod in scenario.get("modules") or []:
        name = mod["name"]
        run_file = run_dir / run_files.get(name, f"{name}.json")
        if not run_file.is_file():
            modules_out[name] = {"ok": False, "error": "missing_run_json"}
            all_ok = False
            continue

        run_data = _load_json(run_file)
        golden_file = golden_dir / golden_files.get(name, f"{name}.json")
        golden_data = _load_json(golden_file) if golden_file.is_file() else {}

        in_glob = test_file.replace("\\", "/") in (_glob_field(run_data, "in_glob") or [])
        missing = test_file.replace("\\", "/") in (_glob_field(run_data, "missing_from_glob") or [])
        glob_ok = in_glob == mod.get("expected_in_glob", True)
        if mod.get("expected_in_glob") is False and not missing:
            glob_ok = False

        mod_symbols = expected_symbols.get(name) or {}
        mod_cfg = {**mod, "_symbols": mod_symbols}
        cmp = _compare_to_golden(run_data, golden_data, mod_cfg)
        mod_ok = glob_ok and cmp["ok"]
        if not mod_ok:
            all_ok = False

        modules_out[name] = {
            "glob": {
                "in_glob": in_glob,
                "missing_from_glob": missing,
                "needs_registry_update": _glob_field(run_data, "needs_registry_update"),
                "expected_in_glob": mod.get("expected_in_glob"),
                "ok": glob_ok,
            },
            "compare": cmp,
            "ok": mod_ok,
        }

    return {
        "model": model_id,
        "scenario": scenario_id,
        "orchestration": orch,
        "modules": modules_out,
        "overall": "pass" if all_ok and orch.get("pct", 0) >= 60 else "fail",
        "all_results_ok": all_ok,
    }


def score_s2(
    *,
    client_root: Path,
    scenario: dict[str, Any],
    scenario_id: str,
    model_id: str,
    run_dir: Path,
    golden_dir: Path,
    orch: dict[str, Any],
) -> dict[str, Any]:
    artifacts = scenario.get("run_artifacts") or {}
    golden_files = scenario.get("golden_files") or {}
    steps: dict[str, Any] = {}
    all_ok = True

    refresh_path = run_dir / artifacts.get("refresh", "refresh_battle.json")
    if refresh_path.is_file():
        refresh_data = _load_json(refresh_path)
        refresh_ok = bool(refresh_data.get("ok"))
        steps["refresh"] = {"ok": refresh_ok, "file": refresh_path.name}
        if not refresh_ok:
            all_ok = False
    else:
        steps["refresh"] = {"ok": False, "error": "missing refresh_battle.json"}
        all_ok = False

    compare_json = run_dir / artifacts.get("compare", "compare_battle.json")
    compare_report = run_dir / artifacts.get("compare_report", "compare_report.md")
    report_text = _load_text(compare_report)
    symbols = scenario.get("compare_report_symbols") or []
    sym_check = _score_symbols(report_text, symbols, [])
    compare_ok = compare_json.is_file() and compare_report.is_file() and sym_check["ok"]
    steps["compare"] = {
        "ok": compare_ok,
        "report_symbols": sym_check,
        "files": [compare_json.name, compare_report.name],
    }
    if not compare_ok:
        all_ok = False

    align_rel = scenario.get("align_file", "")
    align_path = client_root / align_rel.replace("/", os.sep) if align_rel else None
    align_text = _load_text(align_path) if align_path else ""
    align_cfg = scenario.get("align_symbols") or {}
    align_sym = _score_symbols(
        align_text,
        align_cfg.get("include") or [],
        align_cfg.get("exclude") or [],
    )
    align_ok = bool(align_rel) and align_path is not None and align_path.is_file() and align_sym["ok"]
    steps["align"] = {"ok": align_ok, "path": align_rel, "symbols": align_sym}
    if not align_ok:
        all_ok = False

    doc_file = scenario.get("doc_draft_file", "")
    expected_symbols = scenario.get("expected_symbols") or {}
    modules_out: dict[str, Any] = {}
    for mod in scenario.get("doc_draft_modules") or []:
        name = mod["name"]
        run_key = mod.get("run_file_key", "")
        gold_key = mod.get("golden_key", run_key)
        run_file = run_dir / artifacts.get(run_key, f"{name}.json")
        if not run_file.is_file():
            modules_out[name] = {"ok": False, "error": f"missing {run_file.name}"}
            all_ok = False
            continue

        run_data = _load_json(run_file)
        golden_file = golden_dir / golden_files.get(gold_key, run_file.name)
        golden_data = _load_json(golden_file) if golden_file.is_file() else {}

        in_glob = doc_file.replace("\\", "/") in (_glob_field(run_data, "in_glob") or [])
        missing = doc_file.replace("\\", "/") in (_glob_field(run_data, "missing_from_glob") or [])
        glob_ok = in_glob == mod.get("expected_in_glob", True)
        if mod.get("expected_in_glob") is False and not missing:
            glob_ok = False

        mod_symbols = expected_symbols.get(name) or {}
        mod_cfg = {**mod, "_symbols": mod_symbols}
        cmp = _compare_to_golden(run_data, golden_data, mod_cfg)
        mod_ok = glob_ok and cmp["ok"]
        if not mod_ok:
            all_ok = False
        modules_out[name] = {
            "glob": {
                "in_glob": in_glob,
                "missing_from_glob": missing,
                "needs_registry_update": _glob_field(run_data, "needs_registry_update"),
                "expected_in_glob": mod.get("expected_in_glob"),
                "ok": glob_ok,
            },
            "compare": cmp,
            "ok": mod_ok,
        }

    steps["doc_draft"] = {
        "modules": modules_out,
        "ok": all(m.get("ok") for m in modules_out.values()) if modules_out else False,
    }

    return {
        "model": model_id,
        "scenario": scenario_id,
        "orchestration": orch,
        "steps": steps,
        "modules": modules_out,
        "overall": "pass" if all_ok and orch.get("pct", 0) >= 60 else "fail",
        "all_results_ok": all_ok,
    }


def score_run(
    *,
    client_root: Path,
    scenario_id: str,
    model_id: str,
) -> dict[str, Any]:
    scenario = _load_yaml(client_root / "eval/scenarios" / scenario_id / "scenario.yaml")
    checklist = _load_yaml(client_root / "eval/scenarios" / scenario_id / "checklist.yaml")
    run_dir = client_root / "eval/runs" / model_id / scenario_id
    golden_dir = client_root / "eval/golden" / scenario_id

    if not run_dir.is_dir():
        raise FileNotFoundError(f"run dir not found: {run_dir}")

    orch = _orchestration_score(run_dir, checklist)

    if scenario.get("run_artifacts"):
        return score_s2(
            client_root=client_root,
            scenario=scenario,
            scenario_id=scenario_id,
            model_id=model_id,
            run_dir=run_dir,
            golden_dir=golden_dir,
            orch=orch,
        )

    return score_s1(
        client_root=client_root,
        scenario=scenario,
        scenario_id=scenario_id,
        model_id=model_id,
        run_dir=run_dir,
        golden_dir=golden_dir,
        orch=orch,
    )


def main() -> None:
    parser = argparse.ArgumentParser(description="Score eval run vs golden")
    parser.add_argument("--model", required=True, help="model id e.g. Composer2.5")
    parser.add_argument("--scenario", default="s2-full-workflow-test")
    parser.add_argument("--out", default="", help="write score.json path")
    args = parser.parse_args()

    client_root = _client_root()
    result = score_run(
        client_root=client_root,
        scenario_id=args.scenario,
        model_id=args.model,
    )

    run_dir = client_root / "eval/runs" / args.model / args.scenario
    out_path = Path(args.out) if args.out else run_dir / "score.json"
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(json.dumps(result, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps(result, ensure_ascii=False, indent=2))
    sys.exit(0 if result["overall"] == "pass" else 1)


if __name__ == "__main__":
    main()
