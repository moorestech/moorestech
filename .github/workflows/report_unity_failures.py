#!/usr/bin/env python3
# -*- coding: utf-8 -*-

"""
Unity Test Runner の XML 出力(artifacts 下など)を解析し、
失敗テストの一覧とメッセージ/スタックトレースを標準出力に出力。
オプションで Webhook(Slack/Discord 等) にも投稿する。

- NUnit3 形式（Unity Test Framework 既定）:
  <test-case result="Failed"> <failure><message>..</message><stack-trace>..</stack-trace></failure> </test-case>
- JUnit 形式:
  <testcase ...> <failure message="..">stacktrace...</failure> </testcase>

出力:
- 標準出力: 失敗テストの詳細（テスト名、メッセージ、スタックトレース）
- artifacts/failed_tests.json: 失敗テストを配列で保存
- Webhook（任意）: テキストメッセージで投稿（Slackのtext/Discordのcontent両対応）

使い方例:
python scripts/report_unity_failures.py --artifacts-path artifacts --limit 50 --webhook-url "$WEBHOOK_URL"
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


def build_run_context_text():
    server = _read_env("GITHUB_SERVER_URL", "https://github.com")
    repo = _read_env("GITHUB_REPOSITORY", "")
    run_id = _read_env("GITHUB_RUN_ID", "")
    ref = _read_env("GITHUB_REF", "")
    sha = _read_env("GITHUB_SHA", "")
    run_url = f"{server}/{repo}/actions/runs/{run_id}" if repo and run_id else ""
    lines = []
    if repo:
        lines.append(f"Repo : {repo}")
    if ref:
        lines.append(f"Ref  : {ref}")
    if sha:
        lines.append(f"SHA  : {sha}")
    if run_url:
        lines.append(f"Run  : {run_url}")
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
            # インデント付きで複数行を表示
            st = "\n".join(("    " + ln) for ln in f["stack_trace"].splitlines())
            parts.append(st)
        parts.append(f"  (file: {f['file']}, framework: {f['framework']})")
    return "\n".join(parts)


def format_webhook_text(failures, limit=50, per_stack_limit=4000):
    head = "🚨 Unity tests failed"
    ctx = build_run_context_text()
    body_lines = []
    take = failures[: max(0, limit)]
    for f in take:
        # スタックは長くなりやすいので適度に切る
        stack = f.get("stack_trace") or ""
        stack_block = f"```\n{shorten(stack, per_stack_limit)}\n```" if stack else ""
        msg = f.get("message") or ""
        body_lines.append(
            f"- *{f['test_name']}*\n  Message: {shorten(msg, 1000)}\n  {stack_block}".strip()
        )
    more = ""
    if len(failures) > len(take):
        more = f"\n...and {len(failures) - len(take)} more"
    return f"{head}\n{ctx}\n\nFailed: {len(failures)}\n\n" + "\n\n".join(body_lines) + more


def post_webhook(url: str, text: str):
    if not url:
        return False, "WEBHOOK_URL not set"
    payload = {"text": text, "content": text}  # Slack/Discord 兼用
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(url, data=data, headers={"Content-Type": "application/json"})
    try:
        with urllib.request.urlopen(req, timeout=15) as resp:
            _ = resp.read()
        return True, ""
    except urllib.error.HTTPError as e:
        return False, f"HTTPError {e.code}: {e.read()!r}"
    except Exception as e:
        return False, f"Exception: {e!r}"


def main():
    p = argparse.ArgumentParser(description="Parse Unity Test Runner XML and report failures")
    p.add_argument("--artifacts-path", required=True, help="Artifacts directory (e.g., artifacts)")
    p.add_argument("--webhook-url", default="", help="Webhook URL (optional)")
    p.add_argument("--limit", type=int, default=50, help="Max failures to include in webhook/text")
    p.add_argument("--no-stack", action="store_true", help="Do not print stack traces to console")
    args = p.parse_args()

    root_dir = Path(args.artifacts_path)
    xml_files = find_xml_files(root_dir)

    if not xml_files:
        print(f"[info] No XML files found under: {root_dir}")
        return 0

    all_failures = []
    for x in xml_files:
        try:
            all_failures.extend(parse_failures_from_xml(x))
        except Exception:
            print(f"[warn] Failed parsing: {x}\n{traceback.format_exc()}", file=sys.stderr)

    # 失敗が 0 件なら終了（非ゼロでも成功扱いにしたいので return 0）
    if not all_failures:
        print("No failed tests found.")
        return 0

    # 標準出力
    out = format_console_output(all_failures, show_stack=not args.no_stack)
    print(out)

    # JSON も artifacts に保存（既存の upload-artifact で収集される）
    try:
        root_dir.mkdir(parents=True, exist_ok=True)
        with open(root_dir / "failed_tests.json", "w", encoding="utf-8") as f:
            json.dump(all_failures, f, ensure_ascii=False, indent=2)
    except Exception:
        print(f"[warn] Failed to write JSON to {root_dir/'failed_tests.json'}", file=sys.stderr)

    # Webhook 送信（任意）
    if args.webhook_url:
        text = format_webhook_text(all_failures, limit=args.limit)
        ok, err = post_webhook(args.webhook_url, text)
        if ok:
            print("[info] Posted failed tests to webhook.")
        else:
            print(f"[warn] Webhook post failed: {err}", file=sys.stderr)

    return 0


if __name__ == "__main__":
    sys.exit(main())
