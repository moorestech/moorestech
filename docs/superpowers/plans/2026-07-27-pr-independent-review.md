---
spec: docs/superpowers/specs/2026-07-27-pr-independent-review-design.md
---

> **注記（2026-07-27追記）: 本planは歴史的文書であり、実行してはいけない。**
> 実装済みスキルの契約は `.claude/skills/pr-independent-review/SKILL.md` が唯一の正である。
> 本planに書かれた「exit 0契約」「base参照を `origin/<baseRefName>` に固定する手順」「旧シャドー台帳の列構成」は
> いずれも実装過程で改定された（マージ済みPRでの沈黙故障・測定器メタデータの欠落が実測で判明したため）。
> 齟齬があった場合はSKILL.mdを正とし、本planは当時の設計意図を辿る資料としてのみ読むこと。

# pr-independent-review スキル Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** PR URLを渡すと独立セッションがdiffレビュー（moores-code-review＋新規性ゲートL1）を実行し、コード実物入りインフォグラフィックHTMLダイジェストとシャドー台帳を出力する手動発火スキルを作る。

**Architecture:** 新設スキル `.claude/skills/pr-independent-review/` は、①決定論の新規性ゲート（Pythonスクリプト）、②ダイジェストHTMLテンプレート（create-infographic-lightのコメント機能をvendor）、③オーケストレーションSKILL.md の3部品。レビューエンジンは既存moores-code-reviewを起動側正典treeの絶対パスでreport-onlyモード起動する（本体は無改変）。

**Tech Stack:** Python3（新規性ゲート・pytest）、gh CLI、git worktree、vanilla HTML/JS（テンプレート）

## Global Constraints

- moores-code-review本体（`.claude/skills/moores-code-review/`）は一切変更しない（spec「コンポーネント」節）
- スクリプト・レンズ・統合ルールは起動側正典treeの絶対パスで参照。cwd（レビューworktree）はレビュー対象コードの読み取り専用（spec手順6）
- report-onlyモード: 本体の確定修正自動適用・uloop compile・records/eval記録生成は全停止（spec手順6）
- checkoutは `git reset --hard && git clean -fd` 後に `gh pr checkout <番号> --detach`（spec手順2）
- patchはexclude方式: `.meta` `.prefab` `.asset` `.unity` `.png` `.jpg` `.controller` `.mat` `.fbx` を除外、yml/jsonは残す（spec手順3・ADR）
- 出所ラベル正式文法: ユーザー裁定=`[ADR: <spec名>#<台帳項目>]` / それ以外=`[agent前提]`（spec手順4）
- ダイジェストHTMLは実コード抜粋・ファイル名（太字）・リポジトリ相対フルパス・行番号を必ず含む（spec出力フォーマット節・ユーザー裁定 2026-07-27）
- HTMLは絵文字不使用・日本語・file://で動作・コメント機能JSはverbatim維持（create-infographic-light規約）
- AskUserQuestion不使用。判断は全部ダイジェストへ書き出す（spec ADR）
- gh未認証・PR不存在は即座に明示エラー終了。黙って縮退しない（specエラー処理）

---

### Task 1: 新規性ゲートL1スクリプト

**Files:**
- Create: `.claude/skills/pr-independent-review/scripts/novelty_gate.py`
- Test: `.claude/skills/pr-independent-review/tests/test_novelty_gate.py`

**Interfaces:**
- Consumes: なし（git CLIのみ）
- Produces: CLI `python3 .claude/skills/pr-independent-review/scripts/novelty_gate.py <repo_root> <base_ref>`。
  stdoutにJSON: `{"new_edges": [{"file", "line", "using", "dir", "generic_origin"}], "asmdef_refs": [{"file", "ref"}], "grammar": [{"file", "line", "kind", "detail"}]}`。
  kindは `interface` / `abstract_class` / `subject` / `new_protocol_file` / `new_datastore_file` / `schema_change`。
  Task 3のSKILL.mdがこのJSONを新形フラグ判定に使う。exit codeは常に0（レポートツール）

- [ ] **Step 1: 失敗するテストを書く**

```python
# .claude/skills/pr-independent-review/tests/test_novelty_gate.py
# 一時gitリポジトリを組み立ててnovelty_gate.pyの検出を検証する
# Build a throwaway git repo and verify novelty_gate.py detections
import json
import subprocess
import sys
from pathlib import Path

import pytest

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "novelty_gate.py"


def _git(repo: Path, *args: str) -> str:
    return subprocess.run(
        ["git", "-C", str(repo), *args],
        check=True, capture_output=True, text=True,
    ).stdout


@pytest.fixture()
def repo(tmp_path: Path) -> Path:
    r = tmp_path / "repo"
    r.mkdir()
    _git(r, "init", "-b", "master")
    _git(r, "config", "user.email", "t@t")
    _git(r, "config", "user.name", "t")
    # base: 具体ドメイン側は既にElectricWireをusing済み / domain side already uses ElectricWire
    wire_dir = r / "Client.Game" / "ElectricWire"
    wire_dir.mkdir(parents=True)
    (wire_dir / "WireView.cs").write_text(
        "using Game.ElectricWire;\nnamespace Client.Game.ElectricWire { class WireView {} }\n"
    )
    common_dir = r / "Client.Game" / "BlockSystem" / "PlaceSystem" / "Common"
    common_dir.mkdir(parents=True)
    (common_dir / "CommonBlockPlaceSystem.cs").write_text(
        "using UnityEngine;\nnamespace Client.Game.BlockSystem.PlaceSystem { class CommonBlockPlaceSystem {} }\n"
    )
    _git(r, "add", "-A")
    _git(r, "commit", "-m", "base")
    # base_refはタグで固定する（masterのままだとHEADと同一でdiffが常に空になり空虚な合格を生む）
    # Pin base as a tag; using master would make base...HEAD always empty
    _git(r, "tag", "basetag")
    return r


def _run(repo: Path) -> dict:
    out = subprocess.run(
        [sys.executable, str(SCRIPT), str(repo), "basetag"],
        check=True, capture_output=True, text=True,
    ).stdout
    return json.loads(out)


def test_new_using_edge_from_generic_dir_is_flagged(repo: Path):
    # 汎用Common配下がドメインnamespaceを初めてusing → new_edge / generic Common dir gains first domain using
    f = repo / "Client.Game" / "BlockSystem" / "PlaceSystem" / "Common" / "CommonBlockPlaceSystem.cs"
    f.write_text(
        "using UnityEngine;\nusing Game.ElectricWire;\n"
        "namespace Client.Game.BlockSystem.PlaceSystem { class CommonBlockPlaceSystem {} }\n"
    )
    _git(repo, "commit", "-am", "add wire dep")
    result = _run(repo)
    edges = [e for e in result["new_edges"] if e["using"] == "Game.ElectricWire"]
    assert len(edges) == 1
    assert edges[0]["generic_origin"] is True


def test_existing_pair_is_not_flagged(repo: Path):
    # 既存ペア（ElectricWireディレクトリ内のGame.ElectricWire）は新エッジではない / pre-existing pair is not novel
    f = repo / "Client.Game" / "ElectricWire" / "WireView2.cs"
    f.write_text("using Game.ElectricWire;\nnamespace Client.Game.ElectricWire { class WireView2 {} }\n")
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "sibling file")
    result = _run(repo)
    assert result["new_edges"] == []


def test_grammar_elements_detected(repo: Path):
    # interface新設・Subject新設・スキーマyml変更を検出 / detect new interface, Subject, schema yml change
    f = repo / "Client.Game" / "ElectricWire" / "IWirePreview.cs"
    f.write_text(
        "using UniRx;\nnamespace Client.Game.ElectricWire {\n"
        "public interface IWirePreview {}\n"
        "class Impl { private readonly Subject<int> _onChanged = new(); }\n}\n"
    )
    schema = repo / "VanillaSchema" / "blocks.yml"
    schema.parent.mkdir()
    schema.write_text("key: value\n")
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "grammar")
    result = _run(repo)
    kinds = {g["kind"] for g in result["grammar"]}
    assert "interface" in kinds
    assert "subject" in kinds
    assert "schema_change" in kinds


def test_asmdef_reference_addition_detected(repo: Path):
    # 実際のasmdefは複数行配列。追加refは裸の文字列行として現れ、key行(`":`含む)は無視される
    # Real asmdefs use multi-line arrays; added refs appear as bare string lines
    asmdef = repo / "Client.Game" / "Client.Game.asmdef"
    asmdef.write_text('{\n  "name": "Client.Game",\n  "references": [\n  ]\n}\n')
    _git(repo, "add", "-A")
    _git(repo, "commit", "-m", "asmdef base")
    asmdef.write_text('{\n  "name": "Client.Game",\n  "references": [\n    "Game.ElectricWire"\n  ]\n}\n')
    _git(repo, "commit", "-am", "asmdef ref")
    result = _run(repo)
    assert {"file": "Client.Game/Client.Game.asmdef", "ref": "Game.ElectricWire"} in result["asmdef_refs"]
    # key行の値("Client.Game"等)が誤検知されていないこと / key-line values must not be false positives
    assert all(r["ref"] != "Client.Game" for r in result["asmdef_refs"])
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `uv run --with pytest python -m pytest .claude/skills/pr-independent-review/tests/test_novelty_gate.py -v`（pytestは未導入・uv経由で実行する）
Expected: FAIL（`scripts/novelty_gate.py` が存在せず全テストがエラー）

- [ ] **Step 3: スクリプトを実装する**

```python
#!/usr/bin/env python3
# .claude/skills/pr-independent-review/scripts/novelty_gate.py
# 新規性ゲートL1 — PR diffから設計新形の決定論シグナル（依存新エッジ・asmdef参照追加・文法要素新設）を検出する
# Novelty gate L1 — deterministic signals of novel design shapes in a PR diff
#
# usage: novelty_gate.py <repo_root> <base_ref>
#   base_ref...HEAD のdiffを検査し、JSONをstdoutへ出す。exit codeは常に0。
#   Inspects base_ref...HEAD and prints JSON to stdout. Always exits 0.
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


def git(repo, *args):
    return subprocess.run(["git", "-C", repo, *args], check=True, capture_output=True, text=True).stdout


def build_base_pairs(repo, base_ref):
    # base時点の「ディレクトリ→using済みnamespace集合」を1パスで構築する
    # Build dir -> set(namespace) inventory at base in one pass
    # 注意: git grepはPOSIX EREのため\sは使えない（実測で0件ヒット化する）。[[:space:]]を使う
    # git grep is POSIX ERE — \s silently matches nothing; use [[:space:]]
    pairs = defaultdict(set)
    proc = subprocess.run(
        ["git", "-C", repo, "grep", "-E", r"^[[:space:]]*using[[:space:]]+", base_ref, "--", "*.cs"],
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
    out = git(repo, "diff", f"{base_ref}...HEAD", "--unified=0")
    added, new_files, cur, line_no = [], set(), None, 0
    for raw in out.splitlines():
        if raw.startswith("+++ b/"):
            cur = raw[6:]
        elif raw.startswith("new file mode"):
            pass
        elif raw.startswith("--- /dev/null"):
            new_files.add("__pending__")
        elif raw.startswith("@@"):
            m = re.search(r"\+(\d+)", raw)
            line_no = int(m.group(1)) if m else 0
        elif raw.startswith("+") and not raw.startswith("+++"):
            if "__pending__" in new_files:
                new_files.discard("__pending__")
                new_files.add(cur)
            added.append((cur, line_no, raw[1:]))
            line_no += 1
    return added, new_files


def main():
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
            # key行（`":`を含む）はスキップし、references配列要素（裸の文字列行）だけ拾う
            # Skip key-value lines; keep only bare string lines = reference array elements
            if '":' in content:
                continue
            for ref in re.findall(r'"([A-Za-z0-9_.]+)"', content):
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
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `uv run --with pytest python -m pytest .claude/skills/pr-independent-review/tests/test_novelty_gate.py -v`（pytestは未導入・uv経由で実行する）
Expected: PASS ×4

- [ ] **Step 5: 実リポジトリでスモーク実行する**

Run: `python3 .claude/skills/pr-independent-review/scripts/novelty_gate.py "$(pwd)" origin/master`
Expected: 現ブランチ（specコミットのみ）では `{"new_edges": [], "asmdef_refs": [], "grammar": []}` に近い出力。エラーが出ないこと

- [ ] **Step 6: コミットする**

```bash
git add .claude/skills/pr-independent-review/scripts/novelty_gate.py .claude/skills/pr-independent-review/tests/test_novelty_gate.py
git commit -m "feat: pr-independent-review 新規性ゲートL1スクリプト"
```

---

### Task 2: ダイジェストHTMLテンプレート

**Files:**
- Create: `.claude/skills/pr-independent-review/assets/digest-template.html`

**Interfaces:**
- Consumes: `.claude/skills/create-infographic-light/assets/template.html`（リポジトリ同期済み・ユーザーローカル版とバイト同一を確認済み。コメント機能のvendor元）
- Produces: 裁定カード（`.verdict-card`）・コード抜粋（`.code-card`）・suppressedカード（`.suppressed-card`）のサンプル構造を含むテンプレート。Task 3のSKILL.mdが生成手順から参照する

- [ ] **Step 1: create-infographic-lightのテンプレートをvendorコピーする**

```bash
mkdir -p .claude/skills/pr-independent-review/assets
cp .claude/skills/create-infographic-light/assets/template.html \
   .claude/skills/pr-independent-review/assets/digest-template.html
```

理由（vendorする判断）: 実行時に他スキルのファイルへ依存すると参照が壊れやすい。コピー元もリポジトリ内パスを使う（ユーザーローカルパスは他マシンで壊れる）。コメント機能JS/CSSはverbatim維持のままコピーして自己完結させる。

- [ ] **Step 2: ダイジェスト固有コンポーネントのサンプルを`<main>`内に追加する**

テンプレートの`<main>`のサンプルコンテンツを以下に差し替える（CSS/コメント機能JSは無変更。追加スタイルは既存`<style>`の末尾に追記）:

```html
<!-- verdictヘッダ: 1行サマリ / verdict header -->
<section class="verdict-header" data-verdict="ruling">
  <h1>独立レビュー: PR #0000 タイトル</h1>
  <p class="verdict-line"><strong>verdict: 新形につき裁定行き</strong> — Critical 0 / 新形 1 / 設計判断 1 / suppressed 1</p>
</section>

<!-- 裁定カード: ファイル名・フルパス・実コード抜粋・主張・代替案 / ruling card -->
<div class="figure" data-label="PlaceSystem/CommonからGame.ElectricWireへの新規依存エッジの裁定カード（実コード抜粋つき）">
  <section class="verdict-card">
    <h2><span class="badge badge-new">新形</span> CommonBlockPlaceSystem.cs</h2>
    <p class="file-path"><code>moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs:33</code></p>
    <pre class="code-card"><code><span class="ln">31</span>using Client.Game.InGame.BlockSystem.PlaceSystem;
<span class="ln">32</span>using UnityEngine;
<span class="ln hl">33</span><ins>using Client.Game.InGame.ElectricWire;</ins>
<span class="ln">34</span>
<span class="ln hl">35</span><ins>private ElectricWireAutoConnectPreview _autoConnectPreview;</ins></code></pre>
    <p><strong>PR側の主張:</strong> 設置プレビュー自動接続のためのフック最小化 <code>[agent前提]</code>（免責力なし）</p>
    <p><strong>代替案:</strong> 専用ElectricWirePlaceSystem新設 / プレビュー拡張点 <code>IPlacePreviewHook</code> の導入</p>
  </section>
</div>

<!-- suppressedカード / suppressed finding card -->
<div class="figure" data-label="ユーザー裁定ADRにより免責されたWarningの明細カード（実コード抜粋つき）">
  <section class="suppressed-card">
    <h2><span class="badge badge-sup">suppressed</span> blocks.yml</h2>
    <p class="file-path"><code>VanillaSchema/blocks.yml:120</code></p>
    <pre class="code-card"><code><span class="ln hl">120</span><ins>wireParam: ...</ins></code></pre>
    <p><strong>suppressed-by:</strong> <code>[ADR: 2026-07-25-electric-wire#yaml重複はあるべき姿]</code>（ユーザー裁定 2026-07-25）</p>
  </section>
</div>
```

追加CSS（既存`<style>`末尾へ）:

```css
.verdict-header { border-left: 4px solid #2563eb; padding-left: 16px; margin-bottom: 24px; }
.file-path code { font-size: 12px; color: #555; }
.verdict-card h2, .suppressed-card h2 { font-size: 18px; }
.code-card { background: #f6f8fa; border: 1px solid #d0d7de; border-radius: 6px; padding: 12px; overflow-x: auto; font-size: 13px; line-height: 1.6; }
.code-card .ln { display: inline-block; width: 3em; color: #8b949e; user-select: none; }
.code-card .hl { background: #fff8c5; display: inline-block; width: 100%; }
.code-card ins { text-decoration: none; background: #dafbe1; }
.badge { display: inline-block; padding: 2px 8px; border-radius: 10px; font-size: 12px; margin-right: 8px; }
.badge-new { background: #ddf4ff; color: #0969da; }
.badge-sup { background: #fff1e5; color: #bc4c00; }
```

- [ ] **Step 3: 構文チェックする**

```bash
node -e "
const fs=require('fs');
const html=fs.readFileSync('.claude/skills/pr-independent-review/assets/digest-template.html','utf8');
const m=html.match(/<script>([\s\S]*)<\/script>/);
new Function(m[1]);
console.log('SYNTAX_OK');
"
```

Expected: `SYNTAX_OK`

- [ ] **Step 4: ブラウザで表示確認する**

Run: `open .claude/skills/pr-independent-review/assets/digest-template.html`
Expected: verdictヘッダ・裁定カード（行番号付きコード抜粋・33行目ハイライト）・suppressedカードが表示され、図の右上コメントボタンが機能する

- [ ] **Step 5: コミットする**

```bash
git add .claude/skills/pr-independent-review/assets/digest-template.html
git commit -m "feat: pr-independent-review ダイジェストHTMLテンプレート"
```

---

### Task 3: SKILL.md（オーケストレーション）とシャドー台帳

**Files:**
- Create: `.claude/skills/pr-independent-review/SKILL.md`
- Create: `.claude/skills/pr-independent-review/records/shadow-ledger.md`

**Interfaces:**
- Consumes: Task 1のCLI（novelty_gate.py・JSON形式）、Task 2の`assets/digest-template.html`、既存`.claude/skills/moores-code-review/`一式
- Produces: 手動発火スキル本体。発火例: freshセッションで「/pr-independent-review https://github.com/moorestech/moorestech/pull/1063」

- [ ] **Step 1: SKILL.mdを書く**

```markdown
---
name: pr-independent-review
description: |
  実装セッションと完全に独立したセッションでPRをレビューする手動発火スキル。PR URLまたは番号を受け取り、
  レビュー専用worktreeにcheckoutして moores-code-review（report-only）＋新規性ゲートL1を実行し、
  実コード抜粋入りのインフォグラフィックHTMLダイジェスト（verdict/裁定カード/suppressed）と
  シャドー台帳を出力する。実装セッションの自己申告contextは一切受け取らない。
  Use When:
  1. 「/pr-independent-review <PR URL|番号>」で起動された時
  2. 「このPRを独立レビューして」「シャドーレビューして」と言われた時
---

# pr-independent-review — 独立セッションPRレビュー（シャドー運用v1）

対応spec: `docs/superpowers/specs/2026-07-27-pr-independent-review-design.md`

**正典tree**: このSKILL.md自身が置かれているリポジトリルート（以下 `$CANON`）。
スクリプト・レンズ・統合ルールは必ず `$CANON` の絶対パスで参照する。レビューworktree側の
`.claude/` は**絶対に使わない**（PRごとに測定器が変わり見逃し率実測が壊れる・自己弱体化経路）。

## Step 1: PR取得

`gh pr view <番号> --repo moorestech/moorestech --json number,title,body,baseRefName,headRefName,additions,deletions,files`
で取得。失敗（未認証・不存在）は即エラー終了し理由を報告する。黙って縮退しない。

## Step 2: レビューworktreeへcheckout

- 場所固定: `~/moorestech-worktrees/pr-review`。無ければ `git -C ~/moorestech worktree add ~/moorestech-worktrees/pr-review origin/master --detach` で作成
- 毎回リセット: `git -C ~/moorestech-worktrees/pr-review reset --hard && git -C ~/moorestech-worktrees/pr-review clean -fd`
- checkout: `cd ~/moorestech-worktrees/pr-review && gh pr checkout <番号> --detach`
  （--detach必須: PRブランチは実装worktreeが保持していることが多くブランチロックで失敗する）
- `git fetch origin <baseRefName>` してbaseを最新化する

## Step 3: patch生成（exclude方式）

    git -C ~/moorestech-worktrees/pr-review diff origin/<baseRefName>...HEAD -- . \
      ':(exclude)*.meta' ':(exclude)*.prefab' ':(exclude)*.asset' ':(exclude)*.unity' \
      ':(exclude)*.png' ':(exclude)*.jpg' ':(exclude)*.controller' ':(exclude)*.mat' ':(exclude)*.fbx' \
      > /tmp/pr-review-<番号>-patch.diff

yml/jsonは残す（master-data系レンズの守備範囲のため）。

## Step 4: 4カテゴリcontextの独立再構成

`/tmp/pr-review-<番号>-context.md` に書く。**情報源はPR本文とリポジトリ内のspec/planの判断台帳（ADR）のみ**。
実装セッションの申告・PRコメントの合意主張は使わない。

- 出所ラベル正式文法: ユーザー裁定=`[ADR: <spec名>#<台帳項目>]`（実在するADR項目のみ）/ それ以外=`[agent前提]`
- PR本文が主張する方針・トレードオフは全部 `[agent前提]`（免責力なし）として書く

## Step 5: 新規性ゲートL1

    python3 $CANON/.claude/skills/pr-independent-review/scripts/novelty_gate.py ~/moorestech-worktrees/pr-review origin/<baseRefName>

出力JSONのうち **generic_origin=true のnew_edges・asmdef_refs・grammar全件**が新形フラグ。

## Step 6: moores-code-review本体をreport-onlyで発火

`$CANON/.claude/skills/moores-code-review/SKILL.md` の手順に従うが、以下を上書きする:

- PATCH_PATH = Step 3の生成物 / USER_PROMPT_PATH = Step 4の生成物 / cwd＝レビューworktree（コード読み取り専用）
- スクリプト実行・レンズ/reviewer/統合ルールのReadパスは全部 `$CANON` 配下の絶対パス
- **report-only**: 確定修正の自動適用・uloop compile・Step 6.5の適用後diff再生成・Step 7.3のrecords/eval記録生成は行わない。指摘は全部ダイジェストへ
- AskUserQuestionは使わない。設計判断もダイジェストの裁定カードへ

## Step 7: ダイジェストHTML生成

`$CANON/.claude/skills/pr-independent-review/assets/digest-template.html` をReadし、sonnet subagentに
`/tmp/pr-review-<番号>/index.html` を生成させて `open` する。CSS・コメント機能JSはverbatim維持。

- verdictヘッダ（verdict＋件数） → 裁定カード（新形・設計判断。各カードに: ファイル名太字・リポジトリ相対フルパス・
  行番号・当該diffハンクの実コード抜粋（前後数行・追加行`<ins>`・問題行`.hl`）・PR側の主張（出所ラベル付き）・代替案） →
  suppressedカード（全件・同形式＋suppressed-by出所） → 判断台帳（ユーザー裁定/agent前提） → 折りたたみ参考
- CONFIG固有化: `STORAGE_KEY='pr-review-<番号>-comments-v1'`、`COPY_TITLE='PR #<番号> 独立レビュー裁定'`
- 実コード抜粋はStep 3のpatchから機械的に転記する（創作・要約禁止）

## Step 8: 記録

- md版サマリを `$CANON/.claude/skills/pr-independent-review/records/pr-<番号>.md` に保存
  （verdict・裁定/suppressed/新形の各明細のテキスト縮約。grep用）
- シャドー台帳 `$CANON/.claude/skills/pr-independent-review/records/shadow-ledger.md` に1行追記:
  `| 日付 | PR番号 | verdict | 新形数 | suppressed数 | あなたの実判断（空欄） | 一致（空欄） |`
- 正典treeでの記録類のコミットはユーザーに委ねる（独立セッションは正典treeへ書き込むが勝手にcommitしない）

## verdict判定規則

- **Critical差し戻し**: 統合後Criticalが1件以上（200行超過は除外＝努力目標）
- **新形につき裁定行き**: Criticalなし、かつ新形フラグ or `設計判断: あり` が1件以上
- **自動マージ可**: 上記いずれも無し
- suppressedはverdictに影響しない（ダイジェストに全件列挙）

## エラー処理

- gh未認証・PR不存在・checkout失敗: 即エラー終了・理由報告
- codex不在などmoores-code-review内の縮退: 本体規約に従いダイジェストの参考節に明記
```

- [ ] **Step 2: シャドー台帳の初期ファイルを作る**

```markdown
# シャドー台帳 — pr-independent-review

独立レビューのverdictと人間の実マージ判断を突き合わせ、見逃し率を実測するための帳簿。
「あなたの実判断」「一致」列は人間が後で記入する。追記型・行の書き換え禁止（記入列を除く）。

| 日付 | PR | verdict | 新形 | suppressed | あなたの実判断 | 一致 |
|---|---|---|---|---|---|---|
```

- [ ] **Step 3: SKILL.mdの参照パス実在チェック**

Run: `ls .claude/skills/pr-independent-review/scripts/novelty_gate.py .claude/skills/pr-independent-review/assets/digest-template.html .claude/skills/moores-code-review/SKILL.md`
Expected: 3ファイルとも存在

- [ ] **Step 4: コミットする**

```bash
git add .claude/skills/pr-independent-review/SKILL.md .claude/skills/pr-independent-review/records/shadow-ledger.md
git commit -m "feat: pr-independent-review スキル本体とシャドー台帳"
```

---

### Task 4: 実PRでのスモークテスト（レビューエンジン以外の全配管）

**Files:**
- Modify: なし（実行検証のみ。生成物は/tmp配下）

**Interfaces:**
- Consumes: Task 1〜3の全成果物
- Produces: 実PRでの動作確認記録。対象は差分の小さいマージ済みPRを選ぶ: `gh pr list --repo moorestech/moorestech --state merged --limit 20 --json number,additions,deletions` からadditions最小級を選定（#1057は+2882と大きいため避ける。以下手順の1057は選定したPR番号に読み替え）

moores-code-reviewフル実行（5系統・高コスト）はここではやらない。初回の本番シャドーセッションが担う。
このタスクはStep 1〜5・7〜8の配管（PR取得・checkout・patch・新規性ゲート・ダイジェスト生成・台帳追記）だけを検証する。

- [ ] **Step 1: Step 1〜3の配管を実行する**

Run: SKILL.mdのStep 1〜3をPR番号1057で実行
Expected: `/tmp/pr-review-1057-patch.diff` が生成され、`grep -c '^diff' /tmp/pr-review-1057-patch.diff` が1以上

- [ ] **Step 2: 新規性ゲートを実行する**

Run: `python3 .claude/skills/pr-independent-review/scripts/novelty_gate.py ~/moorestech-worktrees/pr-review origin/master`
Expected: JSONが出力される（内容はPR実態に依存。エラーが出ないこと）

- [ ] **Step 3: スタブ所見でダイジェスト生成を検証する**

Step 6の本体レビューは実行せず、Step 2の新規性ゲート実出力＋ダミー設計判断1件を所見としてSKILL.md Step 7の手順で `/tmp/pr-review-1057/index.html` を生成・`open`。
Expected: verdictヘッダ・実コード抜粋入りカード・コメント機能が機能する（コメント追加→すべてコピーまで手動確認）

- [ ] **Step 4: 台帳追記を検証する**

SKILL.md Step 8を実行。
Expected: `records/pr-1057.md` と `shadow-ledger.md` の新規行（verdictはスタブ由来である旨をmd内に明記）

- [ ] **Step 5: 検証記録をコミットする**

```bash
git add .claude/skills/pr-independent-review/records/
git commit -m "test: pr-independent-review スモーク実行記録（PR1057・レビューエンジン除く配管）"
```

---

### Task 5: moores-code-review 全ブランチレビュー（必須クロージング）

必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。

- [ ] **Step 1: moores-code-reviewスキルを発火し、本ブランチの全変更をレビューする**
- [ ] **Step 2: 指摘の機械的修正を適用し、コミットする**

---

## 判断記録（ADR）

- specのADR: `docs/superpowers/specs/2026-07-27-pr-independent-review-design.md` の「判断記録」節を参照（台帳承認方式・手動発火・exclude方式・インフォグラフィックHTML・simulator review適用4件）
- planning中の判断:
  - **テンプレートはvendorコピー**（agent前提）: リポジトリ同期スキルからユーザーローカルskillへの実行時依存は他環境で壊れるため、コメント機能込みで自己完結させる
  - **新規性ゲートのgeneric判定はスクリプト冒頭の正規表現リスト**（agent前提）: v1はレンズpaths由来＋設置系のシード値。シャドー運用の誤検知実測で調整する前提
  - **スモークはレビューエンジン抜きの配管検証**（agent前提）: moores-code-reviewフル実行は高コストのため初回本番シャドーに委ね、Task 4はそれ以外の全経路を実PRで通す
  - **正典treeへの記録書き込みはするがcommitしない**（agent前提): 独立セッションが勝手にコミットすると実装ブランチと混線するため、記録のコミットはユーザー判断
