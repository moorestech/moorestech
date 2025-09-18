#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
Unity Test Runner の XML 出力(artifacts 下など)を解析し、
失敗テストの一覧とメッセージ/スタックトレースを標準出力に出力。
さらに GitHub の repository_dispatch を発行して、
リポジトリに設定済みの Webhooks へ GitHub から通知を配送する。

- NUnit3 形式（Unity Test Framework 既定）:
  <test-case result="Failed"> <failure><message>..</message><stack-trace>..</stack-trace></failure> </test-case>
- JUnit 形式:
  <testcase ...> <failure message="..">stacktrace...</failure> </testcase>

出力:
- 標準出力: 失敗テストの詳細（テスト名、メッセージ、スタックトレース）
- artifacts/failed_tests.json: 失敗テストを配列で保存
- repository_dispatch: event_type=unity-tests-failed（既定）、client_payload に失敗詳細（サマリ）を格納

使い方例:
python scripts/report_unity_failures.py --artifacts-path artifacts --limit 50 --dispatch-event-type unity-tests-failed
"""

import argparse
import json
import os
import sys
import textwrap
import traceback
import urllib.request
import urllib.error
from pathlib import Path
import xml.etree.ElementTree as ET


def _read_env(name: str, default: str = "") -> str:
    return os.environ.get(name, default)


def find_xml_files(root_dir: Path):
    if not root_dir.exists():
        return []
    return list(root_dir.rglob("*.xml"))


def extract_text(elem, child_tag):
    """子要素(child_tag)を探してテキストを返す。なければ空文字。"""
    if elem is None:
        return ""
    # タグ名が 'stack-trace' のようなハイフン付きもあるため、末尾一致で探す
    for ch in elem:
        tag = ch.tag.split("}")[-1]  # 名前空間対策
        if tag == child_tag:
            return (ch.text or "").strip()
    return ""


def parse_nunit_failures(root: ET.Element, file_name: str):
    """NUnit3 形式の失敗を抽出。"""
    failures = []
    for elem in root.iter():
        tag = elem.tag.split("}")[-1]
        if tag == "test-case":
            result = (elem.attrib.get("result") or "").lower()
            if result == "failed":
                name = elem.attrib.get("fullname") or elem.attrib.get("name") or ""
                failure = None
                for ch in elem:
                    if ch.tag.split("}")[-1] == "failure":
                        failure = ch
                        break
                message = extract_text(failure, "message")
                stack = extract_text(failure, "stack-trace")
                failures.append({
                    "framework": "nunit",
                    "file": file_name,
                    "test_name": name.strip(),
                    "message": (message or "").strip(),
                    "stack_trace": (stack or "").strip(),
                })
    return failures


def parse_junit_failures(root: ET.Element, file_name: str):
    """JUnit 形式の失敗を抽出。"""
    failures = []
    for elem in root.iter():
        tag = elem.tag.split("}")[-1]
        if tag == "testcase":
            # 子に <failure> があるか
            failure = None
            for ch in elem:
                if ch.tag.split("}")[-1] == "failure":
                    failure = ch
                    break
            if failure is not None:
                classname = elem.attrib.get("classname") or ""
                name = elem.attrib.get("name") or ""
                test_name = f"{classname}.{name}".strip(".")
                # message 属性 or 本文にスタックトレースが入っていることがある
                msg_attr = failure.attrib.get("message") or ""
                inner = (failure.text or "").strip()
                # できる範囲で message と stack を分離（単純化）
                message = msg_attr or inner.splitlines()[0] if inner else ""
                stack = inner if inner and inner != message else ""
                failures.append({
                    "framework": "junit",
                    "file": file_name,
                    "test_name": test_name.strip(),
                    "message": (message or "").strip(),
                    "stack_trace": (stack or "").strip(),
                })
    return failures


def parse_failures_from_xml(xml_path: Path):
    try:
        root = ET.parse(str(xml_path)).getroot()
    except Exception:
        # 解析不能なXMLはスキップ（警告だけ出す）
        print(f"[warn] Failed to parse XML: {xml_path}", file=sys.stderr)
        return []

    # まず NUnit で探し、ゼロなら JUnit を試す
    fails = parse_nunit_failures(root, xml_path.name)
    if not fails:
        fails = parse_junit_failures(root, xml_path.name)
    return fails


def shorten(s: str, limit: int):
    if limit <= 0 or len(s) <= limit:
        return s
    return s[: max(0, limit - 3)] + "..."


def build_run_context():
    server = _read_env("GITHUB_SERVER_URL", "https://github.com")
    repo = _read_env("GITHUB_REPOSITORY", "")
    run_id = _read_env("GITHUB_RUN_ID", "")
    ref = _read_env("GITHUB_REF", "")
    sha = _read_env("GITHUB_SHA", "")
    run_url = f"{server}/{repo}/actions/runs/{run_id}" if repo and run_id else ""
    return {
        "repository": repo,
        "ref": ref,
        "sha": sha,
        "run_url": run_url,
    }


def build_run_context_text():
    ctx = build_run_context()
    lines = []
    if ctx.get("repository"):
        lines.append(f"Repo : {ctx['repository']}")
    if ctx.get("ref"):
        lines.append(f"Ref  : {ctx['ref']}")
    if ctx.get("sha"):
        lines.append(f"SHA  : {ctx['sha']}")
    if ctx.get("run_url"):
        lines.append(f"Run  : {ctx['run_url']}")
    return "\n".join(lines)


def format_console_output(failures, show_stack=True):
    parts = []
    parts.append("===== Unity Test Failures =====")
    parts.append(build_run_context_text())
    parts.append(f"Failed: {len(failures)}")
    for f in failures:
        parts.append("")
        parts.append(f"- {f['test_name']}")
        if f.get("message"):
            parts.append(f"  Message: {f['message']}")
        if show_stack and f.get("stack_trace"):
            parts.append("  StackTrace:")
            st = "\n".join(("    " + ln) for ln in f["stack_trace"].splitlines())
            parts.append(st)
        parts.append(f"  (file: {f['file']}, framework: {f['framework']})")
    return "\n".join(parts)


def post_repository_dispatch(event_type: str, client_payload: dict) -> tuple[bool, str]:
    """
    GitHub REST API: POST /repos/{owner}/{repo}/dispatches
    - Authorization: token <GITHUB_TOKEN or GH_TOKEN>
    - Accept: application/vnd.github+json
    """
    repo = _read_env("GITHUB_REPOSITORY", "")
    if not repo:
        return False, "GITHUB_REPOSITORY not set"

    api_base = _read_env("GITHUB_API_URL", "https://api.github.com")
    url = f"{api_base}/repos/{repo}/dispatches"

    token = _read_env("GH_TOKEN") or _read_env("GITHUB_TOKEN")
    if not token:
        return False, "No token (GH_TOKEN or GITHUB_TOKEN) available"

    body = {
        "event_type": event_type,
        "client_payload": client_payload,
    }
    data = json.dumps(body).encode("utf-8")

    req = urllib.request.Request(
        url,
        data=data,
        headers={
            "Authorization": f"token {token}",
            "Accept": "application/vnd.github+json",
            "Content-Type": "application/json",
            "User-Agent": "unity-test-reporter",
        },
    )
    try:
        with urllib.request.urlopen(req, timeout=20) as resp:
            _ = resp.read()
        return True, ""
    except urllib.error.HTTPError as e:
        try:
            err_body = e.read().decode("utf-8", "ignore")
        except Exception:
            err_body = "<no body>"
        return False, f"HTTPError {e.code}: {err_body}"
    except Exception as e:
        return False, f"Exception: {e!r}"


def build_payload_summary(failures, limit: int, per_message_limit=1000, per_stack_limit=3000):
    """
    Webhook配送向けにサイズを抑えたサマリを返す。
    """
    take = failures[: max(0, limit)]
    items = []
    for f in take:
        items.append({
            "test_name": f.get("test_name", "")[:2000],
            "message": shorten(f.get("message", ""), per_message_limit),
            "stack_trace": shorten(f.get("stack_trace", ""), per_stack_limit),
            "framework": f.get("framework", ""),
            "file": f.get("file", ""),
        })
    payload = {
        "run": build_run_context(),
        "failed_count": len(failures),
        "failures": items,
        "truncated": len(failures) > len(take),
    }
    return payload


def main():
    p = argparse.ArgumentParser(description="Parse Unity Test Runner XML and report failures")
    p.add_argument("--artifacts-path", required=True, help="Artifacts directory (e.g., artifacts)")
    p.add_argument("--limit", type=int, default=50, help="Max failures to include in dispatch payload")
    p.add_argument("--no-stack", action="store_true", help="Do not print stack traces to console")
    p.add_argument("--dispatch-event-type", default="unity-tests-failed", help="repository_dispatch event_type")
    args = p.parse_args()

    root_dir = Path(args.artifacts_path)
    xml_files = find_xml_files(root_dir)

    if not xml_files:
        print(f"[info] No XML files found under: {root_dir}")
