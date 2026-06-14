#!/usr/bin/env python3
"""Minimal ECS HTTP helpers for eval golden generation."""

from __future__ import annotations

import json
import os
import urllib.error
import urllib.request
from pathlib import Path
from typing import Any


def api(method: str, path: str, body: dict | None = None) -> dict[str, Any]:
    base = os.environ.get("API_SYNC_BASE", "http://120.27.249.20").rstrip("/")
    token = os.environ.get("API_SYNC_TOKEN", "")
    headers = {"Content-Type": "application/json"}
    if token:
        headers["Authorization"] = f"Bearer {token}"
    data = json.dumps(body).encode("utf-8") if body is not None else None
    req = urllib.request.Request(f"{base}{path}", data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(req, timeout=300) as resp:
            return json.loads(resp.read().decode("utf-8"))
    except urllib.error.HTTPError as e:
        detail = e.read().decode("utf-8", errors="replace")
        raise RuntimeError(f"{method} {path} -> {e.code}: {detail}") from e


def refresh_module(module: str, *, force: bool = True) -> dict[str, Any]:
    body: dict[str, Any] = {"module": module}
    if force:
        body["force"] = True
    return api("POST", "/jobs/refresh-cache", body)


def collect_files(repo_root: Path, paths: list[str]) -> dict[str, str]:
    files: dict[str, str] = {}
    for p in paths:
        norm = p.replace("\\", "/")
        full = repo_root / norm
        if not full.is_file():
            raise FileNotFoundError(f"compare file not found: {full}")
        files[norm] = full.read_text(encoding="utf-8", errors="replace")
    return files


def api_compare(
    *,
    module: str,
    repo: str,
    repo_root: Path,
    paths: list[str],
    scoped: bool = True,
) -> dict[str, Any]:
    files = collect_files(repo_root, paths)
    body: dict[str, Any] = {
        "module": module,
        "repo": repo,
        "files": files,
        "scoped": scoped,
    }
    return api("POST", "/jobs/api-compare", body)
