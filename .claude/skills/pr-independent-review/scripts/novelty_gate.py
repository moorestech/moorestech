#!/usr/bin/env python3
# .claude/skills/pr-independent-review/scripts/novelty_gate.py
# 新規性ゲートL1 — PR diffから設計新形の決定論シグナル（依存新エッジ・asmdef参照追加・文法要素新設）を検出する
# Novelty gate L1 — deterministic signals of novel design shapes in a PR diff
#
# usage: novelty_gate.py <repo_root> <base_ref>
#   base_ref...HEAD のdiffを検査し、JSONをstdoutへ出す。正常時はexit 0。
#   git失敗・引数不足など実行エラー時は非ゼロで落ちる（黙って縮退せず失敗を見せる）。
#   Inspects base_ref...HEAD and prints JSON to stdout; exits 0 on success.
#   Execution errors (git failure, missing args) exit non-zero instead of degrading silently.
import json
import re
import subprocess
import sys
from collections import defaultdict
from pathlib import PurePosixPath

from git_query import (
    DIFF_SAFE_FLAGS,
    GIT_SAFE_CONFIG,
    collect_asmdef_ref_additions,
    git,
)

# 汎用層とみなすディレクトリ（generic_origin判定）。レンズpaths由来＋クライアント設置系
# Directories treated as generic layer, seeded from lens paths + client place system
GENERIC_DIR_RES = [
    re.compile(r"/Common/"),
    re.compile(r"/Base/"),
    re.compile(r"/Core\.[^/]*/"),
    re.compile(r"/Template/"),
    re.compile(r"PlaceSystem/"),
    re.compile(r"/Service/"),
]
# リポジトリ内namespaceとみなす接頭辞（外部ライブラリusingはエッジ対象外）
# Namespace roots considered in-repo; external library usings are ignored
REPO_NS_RES = re.compile(r"^(Game|Core|Client|Server|Mooresmaster)\b")

USING_RE = re.compile(r"^\s*using\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;")
# DataStore新設判定はパス全体に当てる（DataStoreディレクトリ配下の新規ファイルも対象）
# New-DataStore detection matches the whole path, so files under a DataStore directory count too
DATASTORE_FILE_RE = re.compile(r"datastore", re.I)
GRAMMAR_RES = [
    ("interface", re.compile(r"\binterface\s+I[A-Z]")),
    ("abstract_class", re.compile(r"\babstract\s+class\s+")),
    ("subject", re.compile(r"\b(?:Replay)?Subject<")),
]


def build_base_pairs(repo, base_ref):
    # base時点の「ディレクトリ→using済みnamespace集合」を1パスで構築する
    # Build dir -> set(namespace) inventory at base in one pass
    # 注意: git grepはPOSIX EREのため\sは使えない（実測で0件ヒット化する）。[[:space:]]を使う
    # git grep is POSIX ERE — \s silently matches nothing; use [[:space:]]
    # color.grep=always が設定されているとANSIが混入し全行がマッチしなくなるため明示的に殺す
    # color.grep=always would inject ANSI codes and make every line fail to match; force it off
    pairs = defaultdict(set)
    proc = subprocess.run(
        ["git", "-C", repo, *GIT_SAFE_CONFIG, "-c", "color.grep=never",
         "grep", "-E", r"^[[:space:]]*using[[:space:]]+", base_ref, "--", "*.cs"],
        capture_output=True, text=True,
    )
    if proc.returncode == 1:
        return pairs  # ヒット0件のみ空扱い / only zero-hit is empty
    if proc.returncode != 0:
        raise RuntimeError(f"git grep failed (ref={base_ref}): {proc.stderr}")
    for line in proc.stdout.splitlines():
        # 形式: <ref>:<path>:<content> / format: ref:path:content
        _, path, content = line.split(":", 2)
        m = USING_RE.match(content)
        if m:
            pairs[str(PurePosixPath(path).parent)].add(m.group(1))
    return pairs


def parse_diff(repo, base_ref):
    # 追加行を (file, new_line_no, content) で列挙し、新規ファイル集合も返す
    # Yield added lines with line numbers; also return the set of new files
    out = git(repo, "diff", *DIFF_SAFE_FLAGS, f"{base_ref}...HEAD", "--unified=0")
    added, new_files, cur, line_no = [], set(), None, 0
    in_hunk, is_new = False, False
    for raw in out.splitlines():
        # ヘッダ領域とハンク内を明確に分けないと、`++ x`のような追加行がヘッダに化ける
        # Separate header region from hunk body; otherwise an added line like `++ x` looks like a header
        if raw.startswith("diff --git "):
            in_hunk, is_new, cur = False, False, None
        elif not in_hunk and raw.startswith("--- "):
            is_new = raw[4:].rstrip("\t") == "/dev/null"
        elif not in_hunk and raw.startswith("+++ "):
            cur = parse_plus_header(raw)
            if is_new and cur:
                new_files.add(cur)
        elif raw.startswith("@@"):
            # 新ファイル側の開始行が読めない場合は落とす。0で代替すると偽の行番号を全所見へ配る
            # Abort when the new-side start line is unreadable; a 0 fallback would fake every line number
            in_hunk = True
            m = re.search(r"\+(\d+)", raw)
            if not m:
                raise RuntimeError(f"unparsable hunk header: {raw!r}")
            line_no = int(m.group(1))
        elif in_hunk and raw.startswith("+"):
            # cur=Noneは削除ファイル（+++ /dev/null）側。バイナリ化け行が+で始まるケースを弾く
            # cur=None means a deleted file (+++ /dev/null); guard against binary garbage lines starting with +
            if cur is not None:
                added.append((cur, line_no, raw[1:]))
            line_no += 1
    return added, new_files


def parse_plus_header(raw):
    # `+++ b/<path>` か `+++ /dev/null` のみ許す。想定外形式は誤帰属させず即座に落とす
    # Only `+++ b/<path>` and `+++ /dev/null` are valid; anything else fails instead of misattributing
    # パスにスペースを含むとgitは末尾にTABを付ける（`+++ b/Third Party/A.cs\t`）ため必ず落とす
    # git appends a TAB when the path contains a space; strip it or the extension checks all miss
    target = raw[4:].rstrip("\t")
    if target == "/dev/null":
        return None
    if target.startswith("b/"):
        return target[2:]
    raise RuntimeError(
        f"unexpected diff header: {raw!r} — "
        "diff.noprefix / diff.mnemonicPrefix / パスのクォート等の設定が原因の可能性がある "
        "(possible cause: diff.noprefix, diff.mnemonicPrefix, or a quoted path)"
    )


def collect_file_level_grammar(added, new_files):
    # スキーマ変更・新規プロトコル/DataStoreファイルはファイル単位の所見（行番号を持たないのでline=null）
    # Schema changes and new protocol/datastore files are per-file findings, hence line=null
    findings, seen_files = [], set()
    for path, _, _ in added:
        if path.endswith((".yml", ".yaml")) and "VanillaSchema" in path and path not in seen_files:
            seen_files.add(path)
            findings.append({"file": path, "line": None, "kind": "schema_change", "detail": "スキーマyml変更"})
        if path in new_files and path.endswith(".cs") and path not in seen_files:
            if "/Protocol/" in path:
                seen_files.add(path)
                findings.append({"file": path, "line": None, "kind": "new_protocol_file", "detail": "プロトコル新設"})
            elif DATASTORE_FILE_RE.search(path):
                seen_files.add(path)
                findings.append({"file": path, "line": None, "kind": "new_datastore_file", "detail": "DataStore新設"})
    return findings


def main():
    if len(sys.argv) != 3:
        sys.exit("usage: novelty_gate.py <repo_root> <base_ref>")
    repo, base_ref = sys.argv[1], sys.argv[2]
    base_pairs = build_base_pairs(repo, base_ref)
    added, new_files = parse_diff(repo, base_ref)
    result = {
        "new_edges": [],
        "asmdef_refs": collect_asmdef_ref_additions(repo, base_ref),
        "grammar": collect_file_level_grammar(added, new_files),
    }

    # 行単位の所見: 依存新エッジと文法要素の新設 / per-line findings: new dependency edges and grammar elements
    for path, line_no, content in added:
        if not path.endswith(".cs"):
            continue
        m = USING_RE.match(content)
        if m and REPO_NS_RES.match(m.group(1)):
            ns, d = m.group(1), str(PurePosixPath(path).parent)
            if ns not in base_pairs.get(d, set()):
                result["new_edges"].append({
                    "file": path, "line": line_no, "using": ns, "dir": d,
                    "generic_origin": any(r.search("/" + path) for r in GENERIC_DIR_RES),
                    # baseにこのディレクトリのusing記録が皆無＝新設ディレクトリ。全usingが機械的に新エッジ化する
                    # No using inventory at base means a brand-new directory, where every using is trivially novel
                    "dir_is_new": not base_pairs.get(d),
                })
        for kind, rx in GRAMMAR_RES:
            if rx.search(content):
                result["grammar"].append({"file": path, "line": line_no, "kind": kind, "detail": content.strip()[:80]})

    print(json.dumps(result, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
