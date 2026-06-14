#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
C# ブランチの変更を解析して data.json を吐く(レビュー cockpit のデータ層)。
- 変更ステータス(A/M) / +/- 行数 (git diff base...branch)
- C# 構造の簡易パース: usings / 型 / メソッド・#region の fold 範囲
- ファイル間依存(宣言型名の参照) と逆依存
- asmdef は最寄りの *.asmdef から推定。sourceRoot は対象ファイルの共通ディレクトリ。

使い方:
  # 変更 .cs を自動抽出(既定)
  python3 extract.py --repo /path/to/repo --base master --branch HEAD --out public/data.json
  # 明示ファイル一覧(1行1パス, repo相対)で再現性を固定
  python3 extract.py --repo /path/to/repo --base master --branch HEAD --files-from files.txt --out public/data.json
"""
import argparse, json, os, re, subprocess, sys


def run(args, repo):
    return subprocess.check_output(args, cwd=repo, text=True)


# ---- asmdef 推定: 最寄り(祖先方向)の *.asmdef ファイル名 ----
_asm_cache = {}
def asmdef_of(rel, repo):
    d = os.path.dirname(rel)
    if d in _asm_cache:
        return _asm_cache[d]
    cur = d
    while cur and cur not in ('.', '/'):
        full = os.path.join(repo, cur)
        if os.path.isdir(full):
            asms = sorted(f for f in os.listdir(full) if f.endswith('.asmdef'))
            if asms:
                name = os.path.splitext(asms[0])[0]
                _asm_cache[d] = name
                return name
        cur = os.path.dirname(cur)
    _asm_cache[d] = 'other'
    return 'other'


KW_BEFORE_PAREN = {"if", "for", "foreach", "while", "switch", "using", "lock", "fixed", "catch", "return", "do", "sizeof", "typeof", "nameof", "await"}
TYPE_DECL_RE = re.compile(r"\b(class|struct|interface|enum)\s+([A-Za-z_]\w*)")


def sig_to_name(sig):
    sig = re.sub(r"\s+", " ", sig).strip()
    return sig.rstrip("{").strip()


def parse_csharp(text):
    lines = text.split("\n")
    n = len(lines)
    usings = []
    for ln in lines:
        m = re.match(r"\s*using\s+(?:static\s+)?([A-Za-z_][\w\.]*)\s*;", ln)
        if m:
            usings.append(m.group(1))
    usings = sorted(set(usings))

    decl_types = []
    for i, ln in enumerate(lines):
        if ln.strip().startswith("//"):
            continue
        for m in TYPE_DECL_RE.finditer(ln):
            decl_types.append({"name": m.group(2), "kind": m.group(1), "line": i + 1})

    regions = []
    stack = []
    for i, ln in enumerate(lines):
        s = ln.strip()
        if s.startswith("#region"):
            stack.append((i, s[len("#region"):].strip() or "region"))
        elif s.startswith("#endregion") and stack:
            si, name = stack.pop()
            regions.append({"kind": "region", "name": name, "start": si + 1, "end": i + 1})

    folds = []
    i = 0
    while i < n:
        line = lines[i]
        if "{" in line and not line.strip().startswith("//"):
            brace_col = line.index("{")
            header = line[:brace_col]

            def sig_complete(s):
                return "(" in s and ")" in s and s.count("(") >= s.count(")")
            collected = header
            up = i - 1
            tries = 0
            while not sig_complete(collected) and up >= 0 and tries < 12:
                prev = lines[up]
                pstrip = prev.strip()
                if pstrip.endswith(";") or pstrip in ("{", "}") or pstrip.endswith("{") or pstrip.endswith("}"):
                    break
                collected = prev + "\n" + collected
                up -= 1
                tries += 1
            sig = collected.strip()

            is_method = False
            if "(" in sig and ")" in sig:
                paren = sig.index("(")
                before = sig[:paren]
                mname = re.findall(r"[A-Za-z_]\w*", before)
                fname = mname[-1] if mname else ""
                if fname and fname not in KW_BEFORE_PAREN and "=>" not in header:
                    if not TYPE_DECL_RE.search(sig):
                        is_method = True

            depth = 0
            j = i
            end = i
            started = False
            while j < n:
                for ch in lines[j]:
                    if ch == "{":
                        depth += 1
                        started = True
                    elif ch == "}":
                        depth -= 1
                if started and depth == 0:
                    end = j
                    break
                j += 1
            if is_method and end > i:
                start_line = up + 2 if ")" not in header else i + 1
                folds.append({"kind": "method", "name": sig_to_name(sig), "sigStart": start_line, "start": i + 1, "end": end + 1})
                i = end + 1
            else:
                i += 1
        else:
            i += 1
    return {"usings": usings, "declTypes": decl_types, "regions": regions, "folds": folds, "lineCount": n}


def strip_comments(s):
    s = re.sub(r"/\*.*?\*/", " ", s, flags=re.S)
    s = re.sub(r"//[^\n]*", " ", s)
    return s


def added_lines(path, repo, base, branch):
    out = set()
    try:
        diff = run(["git", "diff", f"{base}...{branch}", "--", path], repo)
    except subprocess.CalledProcessError:
        return out
    newno = 0
    for ln in diff.splitlines():
        m = re.match(r"^@@ -\d+(?:,\d+)? \+(\d+)(?:,(\d+))? @@", ln)
        if m:
            newno = int(m.group(1))
            continue
        if ln.startswith("+++") or ln.startswith("---"):
            continue
        if ln.startswith("+"):
            out.add(newno)
            newno += 1
        elif ln.startswith("-"):
            pass
        else:
            newno += 1
    return out


def main():
    ap = argparse.ArgumentParser(description="C# branch → review cockpit data.json")
    ap.add_argument("--repo", required=True, help="対象リポジトリの絶対パス")
    ap.add_argument("--base", default="master", help="比較元(既定 master)")
    ap.add_argument("--branch", default="HEAD", help="比較先(既定 HEAD)")
    ap.add_argument("--out", required=True, help="出力 data.json パス")
    ap.add_argument("--files-from", help="対象ファイル一覧(1行1パス, repo相対)。未指定なら変更.csを自動抽出")
    ap.add_argument("--include-tests", action="store_true", help="自動抽出にテスト(*Test.cs / /Tests/)も含める")
    args = ap.parse_args()
    repo, base, branch = args.repo, args.base, args.branch

    # 変更ステータス / numstat
    status_map = {}
    for line in run(["git", "diff", "--name-status", f"{base}...{branch}"], repo).splitlines():
        parts = line.split("\t")
        if len(parts) >= 2:
            status_map[parts[-1]] = parts[0][0]
    numstat = {}
    for line in run(["git", "diff", "--numstat", f"{base}...{branch}"], repo).splitlines():
        a, d, *rest = line.split("\t")
        if rest:
            numstat[rest[-1]] = (a, d)

    # 対象ファイル決定
    if args.files_from:
        with open(args.files_from, encoding="utf-8") as f:
            files = [ln.strip() for ln in f if ln.strip() and not ln.startswith("#")]
    else:
        files = []
        for line in run(["git", "diff", "--name-status", f"{base}...{branch}", "--", "*.cs"], repo).splitlines():
            parts = line.split("\t")
            if len(parts) < 2:
                continue
            st, path = parts[0][0], parts[-1]
            if st == "D":
                continue
            if not args.include_tests and (path.endswith("Test.cs") or "/Tests/" in path or "/Editor/Tests/" in path):
                continue
            files.append(path)

    records = []
    type_owner = {}
    for rel in files:
        full = os.path.join(repo, rel)
        if not os.path.exists(full):
            print("MISSING", rel, file=sys.stderr)
            continue
        text = open(full, encoding="utf-8").read()
        parsed = parse_csharp(text)
        st = status_map.get(rel, "A")
        add, dele = numstat.get(rel, (str(parsed["lineCount"]), "0"))
        rec = {
            "path": rel, "name": os.path.basename(rel), "asmdef": asmdef_of(rel, repo),
            "status": st, "add": int(add) if add.isdigit() else 0, "del": int(dele) if dele.isdigit() else 0,
            "text": text, "parsed": parsed,
            "addedLines": sorted(added_lines(rel, repo, base, branch)) if st == "M" else [],
        }
        records.append(rec)
        for t in parsed["declTypes"]:
            type_owner.setdefault(t["name"], rel)

    # 依存(コメント除去後に他ファイルの宣言型名を参照しているか)
    for rec in records:
        body = strip_comments(rec["text"])
        own = {t["name"] for t in rec["parsed"]["declTypes"]}
        deps = set()
        for tname, owner in type_owner.items():
            if owner == rec["path"] or tname in own:
                continue
            if re.search(r"\b" + re.escape(tname) + r"\b", body):
                deps.add(owner)
        rec["depsOut"] = sorted(deps)
    rev = {r["path"]: set() for r in records}
    for rec in records:
        for d in rec["depsOut"]:
            if d in rev:
                rev[d].add(rec["path"])
    for rec in records:
        rec["depsIn"] = sorted(rev[rec["path"]])

    # sourceRoot = 対象ファイルの共通ディレクトリ(表示時に剥がす接頭辞)
    dirs = [os.path.dirname(r["path"]) for r in records]
    source_root = ""
    if dirs:
        cp = os.path.commonpath(dirs) if len(dirs) > 1 else dirs[0]
        source_root = (cp + "/") if cp else ""

    out = {
        "branch": branch, "base": base, "sourceRoot": source_root,
        "files": records, "asmdefs": sorted({r["asmdef"] for r in records}),
    }
    os.makedirs(os.path.dirname(os.path.abspath(args.out)), exist_ok=True)
    with open(args.out, "w", encoding="utf-8") as f:
        json.dump(out, f, ensure_ascii=False)
    print(f"wrote {args.out}: {len(records)} files, "
          f"{sum(len(r['parsed']['folds']) for r in records)} folds, "
          f"{sum(len(r['depsOut']) for r in records)} edges, sourceRoot='{source_root}'")


if __name__ == "__main__":
    main()
