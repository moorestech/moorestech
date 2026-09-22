#!/usr/bin/env python3
# =====================================================================
# ⚠ このscripts/配下を1行でも変更・追加したら、必ず回帰テストを実行すること:
#     python3 -m unittest discover -s .claude/skills/moores-code-review/tests
#   全緑になるまで変更は完成扱いにしない。新規スクリプトはSKILL.mdへの配線と
#   tests/test_skill_wiring.py への不変条件追加まで済ませて初めて完成。
# ⚠ Run the regression suite after ANY change under scripts/; wiring into
#   SKILL.md and a wiring-test invariant are part of "done" for new scripts.
# =====================================================================
"""Build immutable inputs for independent requirement-review workers."""

import hashlib
import json
import re
import subprocess
from pathlib import Path


GOAL_HEADINGS = {"## 目指す", "## 目指す（ゴール）", "## ゴール"}
OTHER_STRUCTURED_HEADINGS = {
    "## 目指さない", "## 非目標", "## 目指さない（非目標）",
    "## 制約", "## 尊重すべき制約", "## トレードオフ", "## 許容するトレードオフ",
}
LIST_ITEM = re.compile(r"^(?:[-*+] |\d+[.)] )")
HEADING = re.compile(r"^#{1,2}\s")
FENCE = re.compile(r"^ {0,3}(`{3,}|~{3,})")


def digest(value):
    return hashlib.sha256(value.encode("utf-8")).hexdigest()


def _outside_fence(lines):
    fence = None
    for index, line in enumerate(lines):
        marker = FENCE.match(line)
        closes = (fence is not None and marker and marker.group(1)[0] == fence[0]
                  and len(marker.group(1)) >= fence[1]
                  and not line[marker.end():].strip())
        if fence is None and marker:
            token = marker.group(1)
            fence = (token[0], len(token))
            yield index, line, False
        elif closes:
            fence = None
            yield index, line, False
        else:
            yield index, line, fence is None


def units(context):
    lines = context.splitlines(keepends=True)
    headings = [(i, line.rstrip("\r\n")) for i, line, outside in _outside_fence(lines)
                if outside and line.rstrip("\r\n") in GOAL_HEADINGS | OTHER_STRUCTURED_HEADINGS]
    goals = [(i, heading) for i, heading in headings if heading in GOAL_HEADINGS]
    if len(goals) > 1:
        raise ValueError("目標見出しが複数: 担当要求の境界を決められない")
    if not goals:
        if any(heading in OTHER_STRUCTURED_HEADINGS for _, heading in headings):
            raise ValueError("構造化カテゴリがあるが目標見出しが無い")
        if not context.strip():
            raise ValueError("要求contextが空: 検査対象を取得できない")
        return [{"id": "R001", "start": 1, "end": len(lines), "text": context}]

    goal = goals[0][0]
    start = goal + 1
    end = len(lines)
    for index, line, outside in _outside_fence(lines[start:]):
        if outside and HEADING.match(line):
            end = start + index
            break
    section = lines[start:end]
    if not "".join(section).strip():
        raise ValueError("ゴール節が空: 0要求の合格にはしない")

    cuts = [0]
    for index, line, outside in _outside_fence(section):
        if outside and LIST_ITEM.match(line) and index and "".join(section[cuts[-1]:index]).strip():
            cuts.append(index)
    cuts.append(len(section))
    result = []
    for left, right in zip(cuts, cuts[1:]):
        text = "".join(section[left:right])
        if text.strip():
            result.append({"id": f"R{len(result) + 1:03}", "start": start + left + 1,
                           "end": start + right, "text": text})
    return result


def git(repo, *args):
    return subprocess.check_output(["git", "-C", str(repo), *args])


def snapshot(repo):
    repo = Path(repo)
    if not repo.is_absolute():
        raise ValueError("snapshotのrepoは絶対Pathで指定する")
    root = Path(git(repo, "rev-parse", "--show-toplevel").decode().strip()).resolve()
    if root != repo.resolve():
        raise ValueError("repo-rootはGit worktreeのルートを指定する")
    state = hashlib.sha256(git(repo, "rev-parse", "HEAD"))
    state.update(git(repo, "diff", "--binary", "HEAD", "--"))
    for name in sorted(git(repo, "ls-files", "--others", "--exclude-standard", "-z").split(b"\0")):
        if name:
            content = (repo / name.decode("utf-8")).read_bytes()
            state.update(len(name).to_bytes(8, "big"))
            state.update(name)
            state.update(len(content).to_bytes(8, "big"))
            state.update(content)
    return state.hexdigest()


def bundle(context, patch, procedure, repo, model):
    if not patch.strip() or not procedure.strip():
        raise ValueError("差分/手順が空: 入力を省略して起動しない")
    root = Path(repo).resolve()
    data = {"context": context, "patch": patch, "procedure": procedure,
            "repo": str(root), "model": model, "snapshot": snapshot(root),
            "units": units(context)}
    data["fingerprint"] = digest(json.dumps(data, ensure_ascii=False, sort_keys=True))
    return data


def prompt(data, unit, report):
    common = [f'<supplied-{name}>\n{data[name]}\n</supplied-{name}>'
              for name in ("procedure", "context", "patch")]
    common.append(f'変更後checkout: {data["repo"]}')
    assigned = f'担当要求: {unit["id"]} (context行{unit["start"]}–{unit["end"]})\n{unit["text"]}'
    destination = f'唯一の書込先: {report}\n最終返答でなくWriteで報告全文をここへ保存する。'
    return "\n\n".join(common + [assigned, destination])
