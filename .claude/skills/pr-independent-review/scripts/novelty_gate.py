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

# 汎用層とみなすディレクトリ（generic_origin判定）。レンズpaths由来＋クライアント設置系
# Directories treated as generic layer, seeded from lens paths + client place system
GENERIC_DIR_RES = [
    re.compile(r"/Common/"),
    re.compile(r"/Base/"),
    re.compile(r"Core\."),
    re.compile(r"/Template/"),
    re.compile(r"PlaceSystem/"),
    re.compile(r"/Service/"),
]
# リポジトリ内namespaceとみなす接頭辞（外部ライブラリusingはエッジ対象外）
# Namespace roots considered in-repo; external library usings are ignored
REPO_NS_RES = re.compile(r"^(Game|Core|Client|Server|Mooresmaster)\b")

USING_RE = re.compile(r"^\s*using\s+([A-Za-z_][A-Za-z0-9_.]*)\s*;")
GRAMMAR_RES = [
    ("interface", re.compile(r"\binterface\s+I[A-Z]")),
    ("abstract_class", re.compile(r"\babstract\s+class\s+")),
    ("subject", re.compile(r"\b(?:Replay)?Subject<")),
]


# 非ASCIIパスのクォート（core.quotepath既定on）は`+++ b/`マッチを外し誤帰属を生むため呼び出し毎に打ち消す
# core.quotepath defaults to on and would quote non-ASCII paths, breaking `+++ b/` and misattributing lines
GIT_SAFE_CONFIG = ["-c", "core.quotepath=false"]


def git(repo, *args):
    cmd = ["git", "-C", repo, *GIT_SAFE_CONFIG, *args]
    return subprocess.run(cmd, check=True, capture_output=True, text=True).stdout


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
    # ANSI混入(color.diff=always)は全パターンを外すため--no-colorで殺す / kill forced color
    out = git(repo, "diff", "--no-color", "--no-ext-diff", f"{base_ref}...HEAD", "--unified=0")
    added, new_files, cur, line_no = [], set(), None, 0
    in_hunk, is_new = False, False
    for raw in out.splitlines():
        # ヘッダ領域とハンク内を明確に分けないと、`++ x`のような追加行がヘッダに化ける
        # Separate header region from hunk body; otherwise an added line like `++ x` looks like a header
        if raw.startswith("diff --git "):
            in_hunk, is_new, cur = False, False, None
        elif not in_hunk and raw.startswith("--- "):
            is_new = raw[4:] == "/dev/null"
        elif not in_hunk and raw.startswith("+++ "):
            cur = parse_plus_header(raw)
            if is_new and cur:
                new_files.add(cur)
        elif raw.startswith("@@"):
            in_hunk = True
            m = re.search(r"\+(\d+)", raw)
            line_no = int(m.group(1)) if m else 0
        elif in_hunk and raw.startswith("+"):
            added.append((cur, line_no, raw[1:]))
            line_no += 1
    return added, new_files


def parse_plus_header(raw):
    # `+++ b/<path>` か `+++ /dev/null` のみ許す。想定外形式は誤帰属させず即座に落とす
    # Only `+++ b/<path>` and `+++ /dev/null` are valid; anything else fails instead of misattributing
    target = raw[4:]
    if target == "/dev/null":
        return None
    if target.startswith("b/"):
        return target[2:]
    raise RuntimeError(
        f"unexpected diff header: {raw!r} — "
        "diff.noprefix / diff.mnemonicPrefix / パスのクォート等の設定が原因の可能性がある "
        "(possible cause: diff.noprefix, diff.mnemonicPrefix, or a quoted path)"
    )


def main():
    if len(sys.argv) != 3:
        sys.exit("usage: novelty_gate.py <repo_root> <base_ref>")
    repo, base_ref = sys.argv[1], sys.argv[2]
    base_pairs = build_base_pairs(repo, base_ref)
    added, new_files = parse_diff(repo, base_ref)
    result = {"new_edges": [], "asmdef_refs": [], "grammar": []}

    # スキーマ変更・新規プロトコル/DataStoreファイルはファイル単位で判定する
    # Schema changes and new protocol/datastore files are judged per file
    seen_files = set()
    for path, line_no, content in added:
        if path.endswith((".yml", ".yaml")) and "VanillaSchema" in path and path not in seen_files:
            seen_files.add(path)
            result["grammar"].append({"file": path, "line": line_no, "kind": "schema_change", "detail": "スキーマyml変更"})
        if path in new_files and path.endswith(".cs") and path not in seen_files:
            if "/Protocol/" in path:
                seen_files.add(path)
                result["grammar"].append({"file": path, "line": 1, "kind": "new_protocol_file", "detail": "プロトコル新設"})
            elif "DataStore" in path or "Datastore" in path:
                seen_files.add(path)
                result["grammar"].append({"file": path, "line": 1, "kind": "new_datastore_file", "detail": "DataStore新設"})

    for path, line_no, content in added:
        if path.endswith(".asmdef"):
            # references行は1行形式もあるため、コロン以降～配列を閉じる`]`までだけを走査対象にする
            # A references line may hold the whole array inline; scan only from its colon to the closing `]`
            scan = content
            if '"references"' in content:
                colon = content.find(":", content.index('"references"'))
                if colon == -1:
                    scan = ""
                else:
                    close = content.find("]", colon)
                    scan = content[colon + 1: close + 1] if close != -1 else content[colon + 1:]
            # 他のkey行（`":`を含む）はスキップし、references配列要素（裸の文字列行）だけ拾う
            # Skip other key-value lines; keep only bare string lines = reference array elements
            elif '":' in content:
                continue
            # GUID形式（"GUID:9a3d..."）も参照として文字列のまま報告する
            # GUID-style references are reported verbatim, without resolving them
            for ref in re.findall(r'"([A-Za-z0-9_.:]+)"', scan):
                result["asmdef_refs"].append({"file": path, "ref": ref})
            continue
        if not path.endswith(".cs"):
            continue
        m = USING_RE.match(content)
        if m and REPO_NS_RES.match(m.group(1)):
            ns, d = m.group(1), str(PurePosixPath(path).parent)
            if ns not in base_pairs.get(d, set()):
                result["new_edges"].append({
                    "file": path, "line": line_no, "using": ns, "dir": d,
                    "generic_origin": any(r.search("/" + path) for r in GENERIC_DIR_RES),
                })
        for kind, rx in GRAMMAR_RES:
            if rx.search(content):
                result["grammar"].append({"file": path, "line": line_no, "kind": kind, "detail": content.strip()[:80]})

    print(json.dumps(result, ensure_ascii=False, indent=1))


if __name__ == "__main__":
    main()
