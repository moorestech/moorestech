#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
ブランチの変更を解析して data.json を吐く(レビュー cockpit のデータ層)。
C# (.cs) と TypeScript/React (.ts/.tsx) を 1 つの cockpit に混載できる。

各ファイルごとに:
- 変更ステータス(A/M) / +/- 行数 (git diff base...branch)
- 構造の簡易パース: .cs は usings/型/メソッド・#region、.ts(x) は imports/宣言/関数の fold 範囲
- ファイル間依存: C# は宣言型名の参照、TS は import 解決(相対 + `@/`→src)。逆依存も算出
- group(列キー): C# は最寄りの *.asmdef、TS は最寄りの package.json のディレクトリ名

使い方:
  # 変更 .cs/.ts/.tsx を自動抽出(既定。テスト・dotディレクトリ除外)
  python3 extract.py --repo /path/to/repo --base master --branch HEAD --out public/data.json
  # 明示ファイル一覧(1行1パス, repo相対)で再現性を固定
  python3 extract.py --repo /path/to/repo --base master --branch HEAD --files-from files.txt --out public/data.json
"""
import argparse, json, os, re, subprocess, sys


def run(args, repo):
    return subprocess.check_output(args, cwd=repo, text=True)


def is_ts(rel):
    return rel.endswith(".ts") or rel.endswith(".tsx")


# ---- group(Dep-Map の列キー) ----
# C#: 最寄りの *.asmdef 名(ドット区切り)。
# TS: `pkg/スライス`(スライス = pkg の src 直下を先頭2セグメントで畳む。features は1段深く割れる)。
#     group に "/" を含む=TS スライスとフロント側(asm.ts)が判別するため、必ず "/" を含める。
_group_cache = {}
def group_of(rel, repo):
    if is_ts(rel):
        return ts_group(rel, repo)
    d = os.path.dirname(rel)
    ck = ("cs", d)
    if ck in _group_cache:
        return _group_cache[ck]
    cur = d
    while cur and cur not in (".", "/"):
        full = os.path.join(repo, cur)
        if os.path.isdir(full):
            asms = sorted(f for f in os.listdir(full) if f.endswith(".asmdef"))
            if asms:
                name = os.path.splitext(asms[0])[0]
                _group_cache[ck] = name
                return name
        cur = os.path.dirname(cur)
    _group_cache[ck] = "other"
    return "other"


def ts_group(rel, repo):
    pkgdir = pkg_dir_of(rel, repo)
    if pkgdir:
        pkg = os.path.basename(pkgdir)
        sub = rel[len(pkgdir) + 1:]
    else:
        pkg = rel.split("/")[0] if "/" in rel else "root"
        sub = rel.split("/", 1)[1] if "/" in rel else rel
    parts = sub.split("/")
    if parts and parts[0] == "src":
        parts = parts[1:]            # src/ は剥がす
    dirsegs = parts[:-1]             # ファイル名を除いたディレクトリ部
    slice_ = "/".join(dirsegs[:2]) if dirsegs else "root"
    return f"{pkg}/{slice_}"


# ---- TS の `@/` エイリアス解決に使う、最寄りの package.json ディレクトリ(repo相対) ----
_pkgdir_cache = {}
def pkg_dir_of(rel, repo):
    d = os.path.dirname(rel)
    if d in _pkgdir_cache:
        return _pkgdir_cache[d]
    cur = d
    while cur and cur not in (".", "/"):
        if os.path.isfile(os.path.join(repo, cur, "package.json")):
            _pkgdir_cache[d] = cur
            return cur
        cur = os.path.dirname(cur)
    _pkgdir_cache[d] = None
    return None


# ============================ C# パーサ ============================

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


# ============================ TS/TSX パーサ ============================

# 制御構文など「(の前の語が関数名ではない」もの。これらは fold 対象外。
KW_BEFORE_PAREN_TS = {"if", "for", "while", "switch", "catch", "return", "do", "with", "else", "await", "typeof", "function"}
# トップレベル宣言(members 一覧用)。const/let/var の関数は fold 側で拾うのでここでは型・関数・クラスのみ。
TS_TOPDECL_RE = re.compile(r"^(?:export\s+)?(?:default\s+)?(?:declare\s+)?(?:abstract\s+)?(function|class|interface|type|enum)\s+([A-Za-z_$][\w$]*)")


def ts_scan_src(text):
    # import 抽出用に「ブロックコメント除去 + 行頭 // 行の除去」したテキスト。
    # 行中の // は残す(URL の // を壊さない)。コメントアウトされた import 行は落とす。
    text = re.sub(r"/\*.*?\*/", " ", text, flags=re.S)
    return "\n".join(ln for ln in text.split("\n") if not ln.lstrip().startswith("//"))


def ts_imports(text):
    src = ts_scan_src(text)
    specs = []
    # import ... from 'mod' / export ... from 'mod'(複数行 named import も対応)
    for m in re.finditer(r"\b(?:import|export)\b[^;]*?\bfrom\s*['\"]([^'\"]+)['\"]", src, re.S):
        specs.append(m.group(1))
    # 副作用 import 'mod'
    for m in re.finditer(r"\bimport\s*['\"]([^'\"]+)['\"]", src):
        specs.append(m.group(1))
    # 動的 import('mod')
    for m in re.finditer(r"\bimport\s*\(\s*['\"]([^'\"]+)['\"]\s*\)", src):
        specs.append(m.group(1))
    return sorted(set(specs))


def ts_sig_name(sig):
    s = re.sub(r"\s+", " ", sig).strip().rstrip("{").strip()
    s = re.sub(r"\s*=>\s*$", "", s).strip()   # 末尾の => を落とす
    return s


def ts_is_func(header, sig):
    h = sig.strip()
    if header.strip() == "":
        # { が行頭(JSX 式コンテナ・ブロック開始)→ 関数定義ではない
        return False
    if h.rstrip().endswith("=>"):
        return True                            # アロー関数
    if "(" in h and ")" in h:
        before = h[:h.index("(")]
        names = re.findall(r"[A-Za-z_$][\w$]*", before)
        kw = names[-1] if names else ""
        if not kw or kw in KW_BEFORE_PAREN_TS:
            return False
        return True                            # function 宣言 / メソッド / 関数式
    return False


def parse_ts(text):
    lines = text.split("\n")
    n = len(lines)
    usings = ts_imports(text)

    decl_types = []
    for i, ln in enumerate(lines):
        m = TS_TOPDECL_RE.match(ln)
        if m:
            decl_types.append({"name": m.group(2), "kind": m.group(1), "line": i + 1})

    regions = []   # TS に #region は無い
    folds = []
    i = 0
    while i < n:
        line = lines[i]
        s = line.strip()
        if "{" in line and not s.startswith("//") and not s.startswith("*"):
            brace_col = line.index("{")
            header = line[:brace_col]

            def sig_complete(seg):
                return "(" in seg and ")" in seg and seg.count("(") >= seg.count(")")
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
            is_func = ts_is_func(header, sig)

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
            if is_func and end > i:
                start_line = up + 2 if ")" not in header else i + 1
                folds.append({"kind": "method", "name": ts_sig_name(sig), "sigStart": start_line, "start": i + 1, "end": end + 1})
                i = end + 1
            else:
                i += 1
        else:
            i += 1
    return {"usings": usings, "declTypes": decl_types, "regions": regions, "folds": folds, "lineCount": n}


# ============================ 共通 ============================

def strip_comments(s):
    s = re.sub(r"/\*.*?\*/", " ", s, flags=re.S)
    s = re.sub(r"//[^\n]*", " ", s)
    return s


def resolve_ts_import(spec, base_dir, pkgdir, ts_paths):
    # 相対 / `@/`(=src) のみ解決。bare(react 等)は外部としてスキップ。
    if spec.startswith("."):
        cand = os.path.normpath(os.path.join(base_dir, spec))
    elif spec.startswith("@/"):
        if not pkgdir:
            return None
        cand = os.path.normpath(os.path.join(pkgdir, "src", spec[2:]))
    else:
        return None
    cand = cand.replace("\\", "/")
    for c in (cand, cand + ".ts", cand + ".tsx", cand + ".d.ts", cand + "/index.ts", cand + "/index.tsx"):
        if c in ts_paths:
            return c
    return None


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


def is_excluded(path, include_tests):
    # dotディレクトリ(.claude/.agents/.codex/.git 等)・依存/生成物は常に除外。
    segs = path.split("/")
    if any(seg.startswith(".") for seg in segs[:-1]):
        return True
    if any(seg in ("node_modules", "dist", "build", "Library") for seg in segs):
        return True
    if include_tests:
        return False
    base = os.path.basename(path)
    if path.endswith(".cs"):
        return base.endswith("Test.cs") or "/Tests/" in path or "/Editor/Tests/" in path
    # TS テスト
    return ".test." in base or ".spec." in base or "/e2e/" in path or "/__tests__/" in path


def main():
    ap = argparse.ArgumentParser(description="C#/TS branch → review cockpit data.json")
    ap.add_argument("--repo", required=True, help="対象リポジトリの絶対パス")
    ap.add_argument("--base", default="master", help="比較元(既定 master)")
    ap.add_argument("--branch", default="HEAD", help="比較先(既定 HEAD)")
    ap.add_argument("--out", required=True, help="出力 data.json パス")
    ap.add_argument("--files-from", help="対象ファイル一覧(1行1パス, repo相対)。未指定なら変更.cs/.ts/.tsxを自動抽出")
    ap.add_argument("--include-tests", action="store_true", help="自動抽出にテストも含める")
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
        for line in run(["git", "diff", "--name-status", f"{base}...{branch}", "--", "*.cs", "*.ts", "*.tsx"], repo).splitlines():
            parts = line.split("\t")
            if len(parts) < 2:
                continue
            st, path = parts[0][0], parts[-1]
            if st == "D":
                continue
            if is_excluded(path, args.include_tests):
                continue
            files.append(path)

    records = []
    for rel in files:
        full = os.path.join(repo, rel)
        if not os.path.exists(full):
            print("MISSING", rel, file=sys.stderr)
            continue
        text = open(full, encoding="utf-8").read()
        parsed = parse_ts(text) if is_ts(rel) else parse_csharp(text)
        st = status_map.get(rel, "A")
        if st not in ("A", "M", "D"):   # R(rename)/C(copy) は M 扱い
            st = "M"
        add, dele = numstat.get(rel, (str(parsed["lineCount"]), "0"))
        rec = {
            "path": rel, "name": os.path.basename(rel), "asmdef": group_of(rel, repo),
            "status": st, "add": int(add) if add.isdigit() else 0, "del": int(dele) if dele.isdigit() else 0,
            "text": text, "parsed": parsed,
            "addedLines": sorted(added_lines(rel, repo, base, branch)) if st == "M" else [],
        }
        records.append(rec)

    # 依存(言語別): C# は宣言型名の参照、TS は import 解決。グラフは言語内で閉じる。
    cs_records = [r for r in records if r["path"].endswith(".cs")]
    ts_records = [r for r in records if is_ts(r["path"])]

    type_owner = {}
    for rec in cs_records:
        for t in rec["parsed"]["declTypes"]:
            type_owner.setdefault(t["name"], rec["path"])
    for rec in cs_records:
        body = strip_comments(rec["text"])
        own = {t["name"] for t in rec["parsed"]["declTypes"]}
        deps = set()
        for tname, owner in type_owner.items():
            if owner == rec["path"] or tname in own:
                continue
            if re.search(r"\b" + re.escape(tname) + r"\b", body):
                deps.add(owner)
        rec["depsOut"] = sorted(deps)

    ts_paths = {r["path"] for r in ts_records}
    for rec in ts_records:
        base_dir = os.path.dirname(rec["path"])
        pkgdir = pkg_dir_of(rec["path"], repo)
        deps = set()
        for spec in rec["parsed"]["usings"]:
            tgt = resolve_ts_import(spec, base_dir, pkgdir, ts_paths)
            if tgt and tgt != rec["path"]:
                deps.add(tgt)
        rec["depsOut"] = sorted(deps)

    rev = {r["path"]: set() for r in records}
    for rec in records:
        for d in rec["depsOut"]:
            if d in rev:
                rev[d].add(rec["path"])
    for rec in records:
        rec["depsIn"] = sorted(rev[rec["path"]])

    # sourceRoot = 対象ファイルの共通ディレクトリ(表示時に剥がす接頭辞)。混載で共通が無ければ空。
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
    print(f"wrote {args.out}: {len(records)} files "
          f"({len(cs_records)} cs / {len(ts_records)} ts), "
          f"{sum(len(r['parsed']['folds']) for r in records)} folds, "
          f"{sum(len(r['depsOut']) for r in records)} edges, sourceRoot='{source_root}'")


if __name__ == "__main__":
    main()
