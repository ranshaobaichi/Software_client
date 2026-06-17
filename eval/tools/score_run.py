#!/usr/bin/env python3
"""Score one model run against golden + scenario expectations (multi-dimensional)."""

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

DIMENSION_WEIGHTS = {
    "accuracy": 0.50,
    "completeness": 0.30,
    "conformance": 0.20,
}


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


def _round_score(value: float) -> float:
    return round(max(0.0, min(100.0, value)), 1)


def _weighted_total(dimensions: dict[str, float]) -> float:
    total = sum(dimensions.get(k, 0) * w for k, w in DIMENSION_WEIGHTS.items())
    return _round_score(total)


def _score_symbols(text: str, include: list[str], exclude: list[str]) -> dict[str, Any]:
    ok_inc = all(sym in text for sym in include)
    bad_exc = [sym for sym in exclude if sym in text]
    inc_found = sum(1 for s in include if s in text)
    inc_total = len(include) or 1
    exc_penalty = len(bad_exc)
    ratio = max(0.0, (inc_found / inc_total) - 0.25 * exc_penalty)
    return {
        "include_ok": ok_inc,
        "missing_include": [s for s in include if s not in text],
        "exclude_violations": bad_exc,
        "ok": ok_inc and not bad_exc,
        "ratio": min(1.0, ratio),
    }


def _orchestration_score(run_dir: Path, checklist: dict[str, Any]) -> dict[str, Any]:
    orch_path = run_dir / "orchestration.yaml"
    if not orch_path.is_file():
        return {
            "filled": False,
            "score": 0,
            "max": sum(i.get("weight", 1) for i in checklist.get("items") or []),
            "pct": 0,
            "details": {},
            "flat_format": False,
        }
    raw = orch_path.read_text(encoding="utf-8-sig", errors="replace")
    flat_format = "items:" not in raw and "done:" not in raw and "result:" not in raw
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
        "flat_format": flat_format,
    }


def _repair_compare_encoding(
    run_dir: Path,
    artifacts: dict[str, str],
    required_symbols: list[str],
) -> dict[str, Any]:
    compare_json_path = run_dir / artifacts.get("compare", "compare_battle.json")
    compare_report_path = run_dir / artifacts.get("compare_report", "compare_report.md")
    info: dict[str, Any] = {
        "raw_report_ok": False,
        "json_report_md_ok": False,
        "repaired": False,
        "repair_source": None,
    }
    if not compare_json_path.is_file():
        return info

    raw_report = _load_text(compare_report_path)
    info["raw_report_ok"] = bool(raw_report) and all(s in raw_report for s in required_symbols)
    if info["raw_report_ok"]:
        return info

    try:
        compare_data = _load_json(compare_json_path)
    except json.JSONDecodeError:
        return info

    report_md = compare_data.get("report_md") or ""
    info["json_report_md_ok"] = bool(report_md) and all(s in report_md for s in required_symbols)
    if not info["json_report_md_ok"]:
        return info

    compare_report_path.write_text(report_md, encoding="utf-8")
    if report_md not in _load_text(compare_json_path):
        compare_data["report_md"] = report_md
        compare_json_path.write_text(
            json.dumps(compare_data, ensure_ascii=False, indent=2),
            encoding="utf-8",
        )
    info["repaired"] = True
    info["repair_source"] = "compare_battle.json.report_md"
    return info


def _json_utf8_ok(path: Path) -> bool:
    if not path.is_file():
        return False
    try:
        _load_json(path)
        return True
    except (json.JSONDecodeError, UnicodeError):
        return False


def _compare_to_golden(
    run_data: dict[str, Any],
    golden_data: dict[str, Any],
    module_cfg: dict[str, Any],
) -> dict[str, Any]:
    targets = module_cfg.get("expected_targets") or []
    sym_cfg = module_cfg.get("_symbols") or {}
    issues: list[str] = []
    draft_checks: list[dict[str, Any]] = []
    ratios: list[float] = []

    for tgt in targets:
        run_draft = _draft_for_target(run_data, tgt)
        gold_draft = _draft_for_target(golden_data, tgt)
        if not run_draft and not any(
            d.get("target") == tgt and d.get("skipped") for d in run_data.get("drafts") or []
        ):
            issues.append(f"no draft for target {tgt}")
            ratios.append(0.0)
            draft_checks.append({"target": tgt, "exact_match": False, "symbols": {"ok": False, "ratio": 0.0}})
            continue
        if gold_draft and _norm_draft(run_draft) != _norm_draft(gold_draft):
            sym = sym_cfg.get(tgt) or {}
            sc = _score_symbols(
                run_draft,
                sym.get("include") or [],
                sym.get("exclude") or [],
            )
            draft_checks.append({"target": tgt, "exact_match": False, "symbols": sc})
            ratios.append(sc["ratio"])
            if not sc["ok"]:
                issues.append(f"draft symbol mismatch for {tgt}")
        else:
            draft_checks.append({"target": tgt, "exact_match": True, "symbols": {"ok": True, "ratio": 1.0}})
            ratios.append(1.0)

    run_cls = _classification_of(run_data, targets[0]) if targets else None
    gold_cls = _classification_of(golden_data, targets[0]) if targets else None
    if run_cls != gold_cls and gold_cls is not None:
        issues.append(f"classification {run_cls} != golden {gold_cls}")
        ratios.append(0.5)
    elif targets:
        ratios.append(1.0)

    accuracy_ratio = sum(ratios) / len(ratios) if ratios else 0.0
    return {
        "draft_checks": draft_checks,
        "issues": issues,
        "ok": not issues,
        "accuracy_ratio": accuracy_ratio,
    }


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


def _build_dimensions(
    *,
    accuracy_parts: list[float],
    completeness_parts: list[float],
    conformance_parts: list[float],
) -> dict[str, Any]:
    accuracy = _round_score(100 * (sum(accuracy_parts) / len(accuracy_parts) if accuracy_parts else 0))
    completeness = _round_score(100 * (sum(completeness_parts) / len(completeness_parts) if completeness_parts else 0))
    conformance = _round_score(100 * (sum(conformance_parts) / len(conformance_parts) if conformance_parts else 0))
    dims = {
        "accuracy": accuracy,
        "completeness": completeness,
        "conformance": conformance,
    }
    dims["total"] = _weighted_total(dims)
    dims["weights"] = DIMENSION_WEIGHTS
    dims["grade"] = (
        "优秀" if dims["total"] >= 90 else "良好" if dims["total"] >= 75 else "合格" if dims["total"] >= 60 else "待改进"
    )
    return dims


def _score_doc_module(
    *,
    run_dir: Path,
    golden_dir: Path,
    mod: dict[str, Any],
    artifacts: dict[str, str],
    golden_files: dict[str, str],
    doc_file: str,
    expected_symbols: dict[str, Any],
) -> dict[str, Any]:
    name = mod["name"]
    run_key = mod.get("run_file_key", "")
    gold_key = mod.get("golden_key", run_key)
    run_file = run_dir / artifacts.get(run_key, f"{name}.json")
    if not run_file.is_file():
        return {"ok": False, "error": f"missing {run_file.name}", "accuracy_ratio": 0.0}

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
    return {
        "glob": {
            "in_glob": in_glob,
            "missing_from_glob": missing,
            "needs_registry_update": _glob_field(run_data, "needs_registry_update"),
            "expected_in_glob": mod.get("expected_in_glob"),
            "ok": glob_ok,
        },
        "compare": cmp,
        "ok": mod_ok,
        "accuracy_ratio": cmp["accuracy_ratio"] if glob_ok else max(0.0, cmp["accuracy_ratio"] * 0.5),
        "json_utf8_ok": _json_utf8_ok(run_file),
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
    accuracy_parts: list[float] = []
    completeness_parts: list[float] = []
    conformance_parts: list[float] = []

    for mod in scenario.get("modules") or []:
        name = mod["name"]
        run_file = run_dir / run_files.get(name, f"{name}.json")
        if not run_file.is_file():
            modules_out[name] = {"ok": False, "error": "missing_run_json", "accuracy_ratio": 0.0}
            accuracy_parts.append(0.0)
            completeness_parts.append(0.0)
            conformance_parts.append(0.0)
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
            "accuracy_ratio": cmp["accuracy_ratio"] if glob_ok else max(0.0, cmp["accuracy_ratio"] * 0.5),
            "json_utf8_ok": _json_utf8_ok(run_file),
        }
        accuracy_parts.append(modules_out[name]["accuracy_ratio"])
        completeness_parts.append(1.0)
        conformance_parts.append(1.0 if modules_out[name]["json_utf8_ok"] else 0.0)

    orch_path = run_dir / "orchestration.yaml"
    notes_path = run_dir / "notes.md"
    for fname in (run_files.get("商店", "shop.json"), run_files.get("网络相关", "network.json")):
        completeness_parts.append(1.0 if (run_dir / fname).is_file() else 0.0)
    completeness_parts.append(1.0 if orch_path.is_file() else 0.0)
    completeness_parts.append(orch.get("pct", 0) / 100.0)
    conformance_parts.append(1.0 if orch.get("flat_format", False) else 0.4)
    conformance_parts.append(1.0 if orch_path.is_file() else 0.0)
    conformance_parts.append(1.0 if notes_path.is_file() else 0.7)

    dimensions = _build_dimensions(
        accuracy_parts=accuracy_parts,
        completeness_parts=completeness_parts,
        conformance_parts=conformance_parts,
    )

    return {
        "model": model_id,
        "scenario": scenario_id,
        "orchestration": orch,
        "modules": modules_out,
        "dimensions": dimensions,
        "overall": dimensions["grade"],
        "total_score": dimensions["total"],
        "all_results_ok": all(m.get("ok") for m in modules_out.values()) if modules_out else False,
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
    accuracy_parts: list[float] = []
    completeness_parts: list[float] = []
    conformance_parts: list[float] = []

    required_symbols = scenario.get("compare_report_symbols") or []
    encoding_info = _repair_compare_encoding(run_dir, artifacts, required_symbols)
    steps["encoding"] = encoding_info

    refresh_path = run_dir / artifacts.get("refresh", "refresh_battle.json")
    if refresh_path.is_file():
        refresh_data = _load_json(refresh_path)
        refresh_ok = bool(refresh_data.get("ok"))
        steps["refresh"] = {"ok": refresh_ok, "file": refresh_path.name}
        accuracy_parts.append(1.0 if refresh_ok else 0.0)
        completeness_parts.append(1.0)
        conformance_parts.append(1.0 if _json_utf8_ok(refresh_path) else 0.0)
    else:
        steps["refresh"] = {"ok": False, "error": "missing refresh_battle.json"}
        accuracy_parts.append(0.0)
        completeness_parts.append(0.0)
        conformance_parts.append(0.0)

    compare_json = run_dir / artifacts.get("compare", "compare_battle.json")
    compare_report = run_dir / artifacts.get("compare_report", "compare_report.md")
    report_text = _load_text(compare_report)
    sym_check = _score_symbols(report_text, required_symbols, [])
    compare_ok = compare_json.is_file() and compare_report.is_file() and sym_check["ok"]
    steps["compare"] = {
        "ok": compare_ok,
        "report_symbols": sym_check,
        "files": [compare_json.name, compare_report.name],
    }
    accuracy_parts.append(sym_check["ratio"])
    completeness_parts.append(1.0 if compare_json.is_file() and compare_report.is_file() else 0.0)
    if encoding_info.get("raw_report_ok"):
        conformance_parts.append(1.0)
    elif encoding_info.get("repaired"):
        conformance_parts.append(0.5)
    else:
        conformance_parts.append(0.0 if compare_report.is_file() else 0.0)
    conformance_parts.append(1.0 if _json_utf8_ok(compare_json) else 0.0)

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
    accuracy_parts.append(align_sym["ratio"])
    completeness_parts.append(1.0 if align_path and align_path.is_file() else 0.0)
    conformance_parts.append(1.0 if align_path and align_path.is_file() else 0.0)

    doc_file = scenario.get("doc_draft_file", "")
    expected_symbols = scenario.get("expected_symbols") or {}
    modules_out: dict[str, Any] = {}
    for mod in scenario.get("doc_draft_modules") or []:
        mod_out = _score_doc_module(
            run_dir=run_dir,
            golden_dir=golden_dir,
            mod=mod,
            artifacts=artifacts,
            golden_files=golden_files,
            doc_file=doc_file,
            expected_symbols=expected_symbols,
        )
        name = mod["name"]
        modules_out[name] = mod_out
        accuracy_parts.append(mod_out.get("accuracy_ratio", 0.0))
        run_key = mod.get("run_file_key", "")
        run_file = run_dir / artifacts.get(run_key, f"{name}.json")
        completeness_parts.append(1.0 if run_file.is_file() else 0.0)
        conformance_parts.append(1.0 if mod_out.get("json_utf8_ok") else 0.0)

    steps["doc_draft"] = {
        "modules": modules_out,
        "ok": all(m.get("ok") for m in modules_out.values()) if modules_out else False,
    }

    for key in ("shop_draft", "network_draft"):
        fname = artifacts.get(key)
        if fname:
            completeness_parts.append(1.0 if (run_dir / fname).is_file() else 0.0)

    completeness_parts.append(1.0 if (run_dir / "orchestration.yaml").is_file() else 0.0)
    completeness_parts.append(orch.get("pct", 0) / 100.0)
    conformance_parts.append(1.0 if orch.get("flat_format", False) else 0.4)
    conformance_parts.append(1.0 if (run_dir / "orchestration.yaml").is_file() else 0.0)
    if orch.get("details", {}).get("glob_awareness"):
        conformance_parts.append(1.0)
    elif orch.get("filled"):
        conformance_parts.append(0.0)
    else:
        conformance_parts.append(0.0)

    dimensions = _build_dimensions(
        accuracy_parts=accuracy_parts,
        completeness_parts=completeness_parts,
        conformance_parts=conformance_parts,
    )

    return {
        "model": model_id,
        "scenario": scenario_id,
        "orchestration": orch,
        "steps": steps,
        "modules": modules_out,
        "dimensions": dimensions,
        "overall": dimensions["grade"],
        "total_score": dimensions["total"],
        "all_results_ok": all(
            [
                (steps.get("refresh") or {}).get("ok"),
                (steps.get("compare") or {}).get("ok"),
                (steps.get("align") or {}).get("ok"),
                (steps.get("doc_draft") or {}).get("ok"),
            ]
        ),
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
    dims = result.get("dimensions") or {}
    print(json.dumps(result, ensure_ascii=False, indent=2))
    sys.exit(0 if dims.get("total", 0) >= 60 else 1)


if __name__ == "__main__":
    main()
