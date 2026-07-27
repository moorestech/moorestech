---
spec: docs/superpowers/specs/2026-07-26-review-exemption-laundering-fix-design.md
---

# レビュー免責ロンダリング封鎖 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development（推奨）または superpowers:executing-plans を使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** レビュー指摘が「agent自作の合意」で握り潰される経路を、出所ラベル・suppressed可視化・判断台帳・執行スクリプトの4点で封鎖する。

**Architecture:** 指示文レイヤー（スキルmd群の文言変更）とスクリプトレイヤー（deterministic_checks拡張＋ledger_gate新設＋hooks配線）の二層。担保はスクリプトが持つ。改修対象はmoorestechリポジトリ内 `.claude/skills/`（moores-code-review・writing-plans・brainstorming・user-simulator）と、別リポジトリ `~/.agents/skills/all-code-review`。

**Tech Stack:** Markdown（スキル定義）/ Python 3（チェックスクリプト）/ bash＋Claude Code frontmatter hooks（関所）

## Global Constraints

- 出所ラベルは3種のみ: `[ユーザー裁定: "発言引用" または AskUserQuestion結果 YYYY-MM-DD]` / `[ADR: <spec名>#<台帳項目>]` / `[agent前提]`
- ラベル無し・引用不能な行は自動的に `[agent前提]` 扱い。`[agent前提]` はレビュー免責力を持たない
- suppressed可視化はCritical/Warning級のみ。Info級は列挙しない。suppressed指摘はAskUserQuestionに載せない
- pathsマッチは台帳掲載判定の機械的下限（マッチした項目は級の自己判定によらず掲載必須）
- 機械条件のカバー範囲はpaths発火型レンズのみ（keywords発火型はレビュー段階の変更2で捕捉）
- hooks配線はsim-gate.sh前例踏襲: 状態ディレクトリ `${TMPDIR:-/tmp}/claude-*-gate/<session_id>`、自前ブロックカウンタ上限2、YAML内で `$` エスケープを踏まない（メモリ: skill-frontmatter-hooksの罠）
- Pythonスクリプトのテストはfixtureファイルを `/tmp` でなくscratchpadに作らずリポジトリ内 `.claude/skills/moores-code-review/eval/synthetic/` の流儀に従う必要はない（スクリプト単体はコマンド実行で検証）
- partial禁止・try-catch原則禁止はC#規約であり、Pythonスクリプトには適用しない（既存checks_*.pyの流儀に従う）

---

### Task 1: 出所ラベル検査（checks_context.py）— moores-code-review側

**Files:**
- Create: `.claude/skills/moores-code-review/scripts/checks_context.py`
- Modify: `.claude/skills/moores-code-review/scripts/deterministic_checks.py:39-57`
- Modify: `.claude/skills/moores-code-review/SKILL.md:36-38`（Step 1）, `:42-43`（Step 2のコマンド）

**Interfaces:**
- Produces: `checks_context.run(context_path: Path) -> list[dict]`（confirmed形式 `{"check": "context_source_label", "file": <context>, "line": N, "message": ...}`）。`deterministic_checks.py` は `--context <path>` 受領時のみ confirmed に加算

- [ ] **Step 1: fixtureで失敗を確認できる検査対象を作る**

/tmp/ledger-test-context.md として:

```markdown
## 目指す（ゴール）
- 電柱の自動接続プレビュー

## 目指さない（非目標）
- パフォーマンス最適化 [agent前提]

## 許容するトレードオフ
- 設置プレビューの自動接続はフック最小化方針で合意済み。
- 一時的な重複を許容 [ユーザー裁定: "まず動かす" 2026-07-26]

## 尊重すべき制約
- AGENTS.md準拠
```

期待: 「フック最小化方針で合意済み」行だけがラベル欠落として検出される（「目指す」「制約」欄は対象外）。

- [ ] **Step 2: checks_context.py を実装する**

```python
#!/usr/bin/env python3
"""4カテゴリcontextの出所ラベル検査。

「許容するトレードオフ」「目指さない（非目標）」セクションの各箇条書き行に
出所ラベル（[ユーザー裁定: ...] / [ADR: ...] / [agent前提]）を要求する。
ラベル欠落行は confirmed（context_source_label）として返す。
欠落行は免責力を持たない=自動的に [agent前提] 扱いであることをmessageに明記する。
"""
from __future__ import annotations

import re
from pathlib import Path

TARGET_SECTIONS = ("許容するトレードオフ", "目指さない")
LABEL_RE = re.compile(r"\[(ユーザー裁定:.+?|ADR:.+?|agent前提)\]")


def run(context_path: Path) -> list[dict]:
    if not context_path.is_file():
        return [{
            "check": "context_source_label",
            "file": str(context_path),
            "line": 0,
            "message": "contextファイルが存在しない（--contextで指定されたパスを確認）",
        }]
    findings: list[dict] = []
    in_target = False
    seen_sections = 0
    for lineno, raw in enumerate(
            context_path.read_text(encoding="utf-8", errors="replace").splitlines(), 1):
        line = raw.strip()
        if line.startswith("#"):
            in_target = any(s in line for s in TARGET_SECTIONS)
            seen_sections += 1 if in_target else 0
            continue
        if in_target and line.startswith("- ") and not LABEL_RE.search(line):
            findings.append({
                "check": "context_source_label",
                "file": str(context_path),
                "line": lineno,
                "message": (
                    f"出所ラベル欠落: {line[:60]} — [ユーザー裁定]/[ADR]/[agent前提] の"
                    "いずれかを付与すること。欠落行は [agent前提] 扱いで免責力を持たない"
                ),
            })
    # 対象セクション見出しゼロは沈黙故障ではなくfail-closedで検出する
    # Zero target headings must fail closed, not silently pass
    if seen_sections == 0:
        findings.append({
            "check": "context_source_label",
            "file": str(context_path),
            "line": 0,
            "message": (
                "「許容するトレードオフ」「目指さない」の見出し（#〜###）が見つからない — "
                "4カテゴリcontextは必ず `##` 見出しで書くこと（太字箇条書きは検査対象外になる）"
            ),
        })
    return findings
```

- [ ] **Step 3: deterministic_checks.py に --context を配線する**

`main()` の `repo_root` 解決の直後に追加し、`result` 構築を変更:

```python
    context_findings: list[dict] = []
    if "--context" in argv:
        import checks_context
        context_findings = checks_context.run(Path(argv[argv.index("--context") + 1]))
```

`"confirmed":` の行を次に変更:

```python
        "confirmed": checks_static.run(files, repo_root) + checks_moores.run_confirmed(files) + context_findings,
```

docstringのUsage行を `python3 deterministic_checks.py <PATCH_PATH> [--repo-root <path>] [--context <USER_PROMPT_PATH>]` に更新し、confirmed説明に `context_source_label`（出所ラベル欠落）を追記する。

- [ ] **Step 4: 実行して検証する**

Run: `python3 .claude/skills/moores-code-review/scripts/deterministic_checks.py /dev/null --context /tmp/ledger-test-context.md | python3 -c "import json,sys; r=json.load(sys.stdin); print([f['line'] for f in r['confirmed'] if f['check']=='context_source_label'])"`
Expected: `[8]`（「フック最小化方針で合意済み」の行番号のみ。ラベル付き2行と対象外セクションは検出されない）

追加確認（fail-closed）: 見出しの無いcontext（`printf -- '- 何かのトレードオフ\n' > /tmp/ledger-test-noheading.md`）で実行し、line 0 の「見出しが見つからない」confirmedが1件返ること。

追加確認: `--context` 無しの従来呼び出しで出力が従来と同一であること（`python3 ... /dev/null` がエラーなくconfirmed空で返る）。

- [ ] **Step 5: SKILL.md の Step 1・Step 2 を更新する**

Step 1の2項（`SKILL.md:36-38`）の「自分の判断は…偽装しない」行を以下に置換:

```markdown
   - **4カテゴリは必ず `##` 見出しで書く**（太字箇条書き形式は出所ラベル検査の対象外になり沈黙故障する。見出しゼロはfail-closedでconfirmedになる）。
   - **「許容するトレードオフ」「非目標」の各行に出所ラベル必須**: `[ユーザー裁定: "発言引用" または AskUserQuestion結果 YYYY-MM-DD]` / `[ADR: <spec名>#<台帳項目>]` / `[agent前提]`。ラベル無し・引用不能な行は自動的に `[agent前提]` 扱いで免責力を持たない（`references/integration-rules.md` §6）。ユーザー裁定の出所はspec/planの判断台帳（ADRセクション）から引く（台帳がSSOT）。
```

Step 2のコマンド（`SKILL.md:43`）を次に置換:

```bash
python3 .claude/skills/moores-code-review/scripts/deterministic_checks.py "<PATCH_PATH>" --repo-root "$(pwd)" --context "<USER_PROMPT_PATH>" > /tmp/moores-review-detchecks-<ts>.json
```

Step 2の説明の confirmed 列挙に `context_source_label`（出所ラベル欠落。contextを修正して再実行）を追記する。

- [ ] **Step 6: コミットする**

```bash
git add .claude/skills/moores-code-review/scripts/checks_context.py .claude/skills/moores-code-review/scripts/deterministic_checks.py .claude/skills/moores-code-review/SKILL.md
git commit -m "出所ラベル検査をdeterministic_checksに追加（変更1・moores側）"
```

---

### Task 2: 全レンズ・reviewerのガード文言をsuppressed方式へ置換 — moores-code-review側

**Files:**
- Modify: `.claude/skills/moores-code-review/lenses/*.md`（依頼動詞優先ガード節を持つ全ファイル。domain-boundary.md, master-data-defense.md, implicit-cardinality-assumption.md, redundant-member-duplication.md, datastore-access-separation.md, server-state-sync.md, set-once-dependency-injection.md, precedent-alignment.md, type-driven-structure.md）
- Modify: `.claude/skills/moores-code-review/reviewers/*.md`（同節を持つ13ファイル: core-ts_tsx-ai-recurring-mistakes, core-ts_tsx-centralization-duplication, core-any-implicit-value-meaning, core-ts_tsx-dead-code-and-scope, core-ts_tsx-single-source-of-truth, core-ts_tsx-result-state-propagation, core-any-test-mutation-effectiveness, core-cs-dead-code-and-scope, core-cs-unidirectional-flow, core-cs-redundant-cast, core-cs-centralization-duplication, core-any-file-directory-organization, core-cs-caller-orchestration-minimization。うち免責文を含まないものは無変更でよい）
- 判定注意: `lenses/hardcoded-content-enumeration.md` は依頼動詞優先ガード節を持たないがStep 1のgrepにヒットしうる — 「トレードオフ合致で指摘を抑制する」文なら置換対象、単なる過検知ガード（技術的な非該当条件）なら対象外、の基準で個別判定する

**Interfaces:**
- Produces: 各観点ファイルが `suppressed-by:` タグ付き指摘を返す契約（Task 3の統合規則が消費）

- [ ] **Step 1: 置換対象文を洗い出す**

Run: `grep -rn "合意済み\|指摘しない\|抑制する" .claude/skills/moores-code-review/lenses/ .claude/skills/moores-code-review/reviewers/`

対象は「トレードオフ合致→指摘を出さない/抑制する」を指示する文のみ。**発火条件ガード**（例: 「依頼が具体症状を含む場合のみ判定対象」）は対象外で無変更。

- [ ] **Step 2: 各対象ファイルの当該文を統一文言に置換する**

置換後の統一文言（各ファイルの文脈に合わせ「4カテゴリcontext」等の指示語は保持してよいが、次の3要素を必ず含める）:

```markdown
## 依頼動詞優先ガード
起動prompt 3行目 `User prompt` をRead。「許容するトレードオフ」「非目標」に合致する指摘は**破棄せず**、該当指摘に `suppressed-by: <トレードオフ1行, 出所ラベル>` を付けて**重大度そのまま**で返す（統合側が報告の「免責で消された指摘」節に載せる）。suppressed化できるのは出所が `[ユーザー裁定: ...]` / `[ADR: ...]` の行だけ。`[agent前提]` またはラベル無しの行は免責事由にならない（通常のCritical/Warningとして返す）。
```

precedent-alignment.md:32-34 は既に「合意の出所」注意書きを持つ — その内容（AI自身が書いた文書は合意ではない）は上記文言に包含されるため、節全体を統一文言へ置換する。

- [ ] **Step 3: 置換漏れゼロを確認する**

Run: `grep -rn "合意済みの形は指摘しない\|合意済みの乖離は指摘しない\|反する指摘は抑制する\|なら指摘しない" .claude/skills/moores-code-review/lenses/ .claude/skills/moores-code-review/reviewers/`
Expected: 0件

Run: `grep -rln "suppressed-by" .claude/skills/moores-code-review/lenses/ .claude/skills/moores-code-review/reviewers/ | wc -l`
Expected: Step 1で「免責文を持つ」と判定したファイル数と一致（ガード節13本のうち免責文の無いものは含まれない — 期待値はStep 1の実測で確定する）

- [ ] **Step 4: コミットする**

```bash
git add .claude/skills/moores-code-review/lenses/ .claude/skills/moores-code-review/reviewers/
git commit -m "免責を消音からsuppressed降格へ変更（変更2・moores側観点ファイル）"
```

---

### Task 3: 統合規則・報告のsuppressed可視化＋依存方向確認 — moores-code-review側

**Files:**
- Modify: `.claude/skills/moores-code-review/references/integration-rules.md`（§2.5の後に§2.6新設、§3に1項追加、§6を全面改訂）
- Modify: `.claude/skills/moores-code-review/SKILL.md:125-132`（Step 7の報告構成）

**Interfaces:**
- Consumes: Task 2の `suppressed-by:` タグ

- [ ] **Step 1: integration-rules.md に §2.6 を新設する**（§2.5と§3の間）

```markdown
## 2.6. suppressed指摘の統合（免責は消音でなく降格）

- 観点ファイルが `suppressed-by:` タグ付きで返した指摘は、統合結果から**削除しない**。最終報告の「免責で消された指摘」専用セクションに1件1行（指摘要約＋suppressed-by出所）で必ず列挙する。
- 列挙対象はCritical/Warning級のみ。Info級は列挙しない（ノイズ化防止）。
- suppressed指摘はAskUserQuestionに載せない（拒否権は報告セクションの1行で行使できる）。ユーザーが報告を見て否認したら通常のCritical/Warningとして再統合する。
- `suppressed-by` の出所が `[agent前提]` の指摘が返ってきたら、それは観点ファイルの契約違反 — suppressed扱いにせず通常のCritical/Warningとして統合する。
```

- [ ] **Step 2: §3 適用時の規則に依存方向確認を1項追加する**（integration-rules.md:46の後）

```markdown
- 行数・配置系指摘の修正方針が「コードの移動」を含む場合、移動が**呼び出し元の依存方向を変えるか**を適用前に確認する。依存方向が汎用層→ドメイン層のまま不変なら、行数が減っても問題は解消していない — 修正を適用せず§4の設計判断として保留する（行数圧縮で汚染を隠す抜け道の封鎖）。
```

- [ ] **Step 3: §6 を全面改訂する**

```markdown
## 6. 出所ラベルと偽装の禁止（4カテゴリcontextを書くとき）

- 「許容するトレードオフ」「目指さない」の各行に出所ラベル必須: `[ユーザー裁定: "発言引用" または AskUserQuestion結果 YYYY-MM-DD]` / `[ADR: <spec名>#<台帳項目>]` / `[agent前提]`。欠落は deterministic_checks の `context_source_label` がconfirmedとして検出する。
- ラベル無し・引用不能な行は自動的に `[agent前提]` 扱い。`[agent前提]` は免責力を持たない（観点ファイルは通常のCritical/Warningとして返す）。
- ユーザーのrevert指示を「合意」と誤読しない（revert=現状維持≠agreement）。
- 会話の含意・推論を「合意」として扱わない。合意は発言として存在するか否かの2値。
- 自分の判断を `[ユーザー裁定]` と書き換えない。spec/plan等の文書に選択の記載があること自体は合意ではない（AI自身が書いた文書は特に）— 台帳掲載＋ユーザー承認済みの項目だけが `[ADR:]` を名乗れる。
- 規約からの帰結を「合意済み」と書いて代替案を狭めない。
```

- [ ] **Step 4: SKILL.md Step 7 の報告構成に専用セクションを追加する**

Step 7の1項（統合報告）に以下を追記:

```markdown
   - **「免責で消された指摘」セクション必須**: `suppressed-by:` タグ付きのCritical/Warning級を1件1行＋出所で列挙する（0件なら「suppressed: 0件」と明記）。§2.6参照。
```

- [ ] **Step 5: コミットする**

```bash
git add .claude/skills/moores-code-review/references/integration-rules.md .claude/skills/moores-code-review/SKILL.md
git commit -m "suppressed可視化の統合規則と依存方向確認を追加（変更2・原則②）"
```

---

### Task 4: domain-boundaryレンズの穴塞ぎ

**Files:**
- Modify: `.claude/skills/moores-code-review/lenses/domain-boundary.md:1-10`（paths）, `:38-42`（過検知ガード）

- [ ] **Step 1: paths に Client 側を追加する**

frontmatterのpathsを次に変更:

```yaml
paths:
  - "Game\.Block"
  - "Game\.Gear"
  - "Game\.EnergySystem"
  - "Game\.Fluid"
  - "Client\.Game"
```

- [ ] **Step 2: 過検知ガードの既存違反項目を改訂する**

`domain-boundary.md:41` の「既存コードに元からある違反（このpatchが新規に作っていないもの）— 備考1行に留める。」を次に置換:

```markdown
- 既存コードに元からある違反のうち、**このpatchが触っていないファイル**のもの — 備考1行に留める。**このpatchが編集中のファイル内の既存違反はWarningで必ず返す**（差分外を理由に落とさない。編集機会があるのに素通りさせない）。
```

- [ ] **Step 3: 発火を検証する**

Run: `printf -- '--- a/moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs\n+++ b/moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs\n+// test\n' > /tmp/ledger-test-client.diff && python3 .claude/skills/moores-code-review/scripts/select_lenses.py /tmp/ledger-test-client.diff | grep domain-boundary`
Expected: `domain-boundary.md<TAB>opus` の1行が出る（従来は0件だった）

- [ ] **Step 4: コミットする**

```bash
git add .claude/skills/moores-code-review/lenses/domain-boundary.md
git commit -m "domain-boundaryレンズにClient.Game発火と編集中ファイル既存違反Warningを追加（変更4）"
```

---

### Task 5: ledger_gate.py 新設＋writing-plans配線（変更3の執行）

**Files:**
- Create: `.claude/skills/moores-code-review/scripts/ledger_gate.py`
- Modify: `.claude/skills/writing-plans/SKILL.md`（frontmatter hooks＋Plan Document Header＋本文）

**Interfaces:**
- Consumes: `select_lenses.py` の `parse_yaml_header`（同ディレクトリからimport）
- Produces: hooksモード `track` / `stop`（sim-gate.sh互換のstdin JSON・exit 2ブロック・自前カウンタ上限2）

- [ ] **Step 1: ledger_gate.py を実装する**

```python
#!/usr/bin/env python3
"""writing-plans の判断台帳関所（sim-gate.sh前例踏襲）。

track: plan（docs/superpowers/plans/*.md）へのWrite/Editを状態ファイルに記録
stop : 各planの frontmatter `spec:` を解決し、plan本文の Modify:/Create: 対象のうち
       lenses/*.md の paths 正規表現にマッチするファイルが、spec の判断台帳
       （## 判断記録（ADR） または ## 判断台帳）にbasenameで言及されているか検査。
       未掲載があれば exit 2 でブロック（自前カウンタ上限2で無限ブロック防止）。
"""
from __future__ import annotations

import json
import os
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from select_lenses import parse_yaml_header  # noqa: E402

LENSES_DIR = Path(__file__).resolve().parent.parent / "lenses"
LEDGER_HEADINGS = ("## 判断記録（ADR）", "## 判断台帳")
TARGET_RE = re.compile(r"^\s*-\s*(?:Modify|Create):\s*`?([^`\s:]+)", re.MULTILINE)


def lens_path_patterns() -> list[str]:
    patterns: list[str] = []
    for md in sorted(LENSES_DIR.glob("*.md")):
        patterns.extend(p for p in parse_yaml_header(md.read_text(encoding="utf-8")).get("paths", []) if p)
    return patterns


def ledger_text(spec_path: Path) -> str:
    if not spec_path.is_file():
        return ""
    text = spec_path.read_text(encoding="utf-8", errors="replace")
    for heading in LEDGER_HEADINGS:
        idx = text.find(heading)
        if idx != -1:
            return text[idx:]
    return ""


def resolve_spec(plan_path: Path, spec_ref: str) -> Path:
    # 相対specはcwdでなくplan位置由来のリポジトリルートで解決する（worktree誤読防止）
    # Resolve relative spec against the repo root derived from the plan location, not cwd
    spec = Path(spec_ref)
    if spec.is_absolute():
        return spec
    parents = plan_path.resolve().parents
    if len(parents) >= 4:  # <root>/docs/superpowers/plans/<plan>.md
        candidate = parents[3] / spec_ref
        if candidate.is_file():
            return candidate
    return Path.cwd() / spec_ref


def missing_entries(plan_path: Path) -> list[str]:
    plan_text = plan_path.read_text(encoding="utf-8", errors="replace")
    spec_match = re.search(r"^spec:\s*(\S+)", plan_text, re.MULTILINE)
    if not spec_match:
        return [f"{plan_path.name}: frontmatterに spec: が無い（判断台帳の所在を特定できない）"]
    ledger = ledger_text(resolve_spec(plan_path, spec_match.group(1)))
    if not ledger:
        return [f"{plan_path.name}: spec {spec_match.group(1)} に判断台帳セクションが無い"]
    patterns = lens_path_patterns()
    missing: list[str] = []
    for target in dict.fromkeys(TARGET_RE.findall(plan_text)):
        if any(re.search(p, target) for p in patterns) and Path(target).name not in ledger:
            missing.append(f"{Path(target).name}（{target}）")
    return missing


def main() -> int:
    mode = sys.argv[1] if len(sys.argv) > 1 else ""
    data = json.load(sys.stdin) if not sys.stdin.isatty() else {}
    sid = data.get("session_id", "")
    if not sid:
        return 0
    state_dir = Path(os.environ.get("TMPDIR", "/tmp")) / "claude-ledger-gate"
    state_dir.mkdir(parents=True, exist_ok=True)
    plans_state = state_dir / f"{sid}.plans"
    blocks_state = state_dir / f"{sid}.blocks"

    if mode == "track":
        file_path = data.get("tool_input", {}).get("file_path", "")
        if "/docs/superpowers/plans/" in file_path and file_path.endswith(".md"):
            existing = plans_state.read_text().splitlines() if plans_state.is_file() else []
            if file_path not in existing:
                plans_state.write_text("\n".join(existing + [file_path]) + "\n")
        return 0

    if mode == "stop":
        if not plans_state.is_file():
            return 0
        count = int(blocks_state.read_text()) if blocks_state.is_file() else 0
        if count >= 2:
            return 0
        problems: list[str] = []
        for plan in plans_state.read_text().splitlines():
            if plan.strip():
                problems.extend(missing_entries(Path(plan)))
        if not problems:
            return 0
        blocks_state.write_text(str(count + 1))
        print(
            "ledger-gate: planのModify/Create対象にレンズpaths該当ファイルがありますが、"
            "specの判断台帳に未掲載です: " + " / ".join(problems)
            + " — specの『## 判断記録（ADR）』へagent前提として1行追記（対象ファイル名を含める）するか、"
            "plan frontmatterの spec: パスを修正してください。掲載なき判断は免責力を持ちません。",
            file=sys.stderr,
        )
        return 2

    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: 単体で検証する（ブロック側）**

Run（planに台帳未掲載のレンズ該当ファイルがある状況を再現）:

```bash
mkdir -p /tmp/ledger-gate-fixture/docs/superpowers/{plans,specs}
printf -- '---\nspec: /tmp/ledger-gate-fixture/docs/superpowers/specs/s.md\n---\n- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BlockSystem/PlaceSystem/Common/CommonBlockPlaceSystem.cs`\n' > /tmp/ledger-gate-fixture/docs/superpowers/plans/p.md
printf -- '# spec\n## 判断記録（ADR）\n- 別の話\n' > /tmp/ledger-gate-fixture/docs/superpowers/specs/s.md
echo '{"session_id":"testledger","tool_input":{"file_path":"/tmp/ledger-gate-fixture/docs/superpowers/plans/p.md"}}' | python3 .claude/skills/moores-code-review/scripts/ledger_gate.py track
echo '{"session_id":"testledger"}' | python3 .claude/skills/moores-code-review/scripts/ledger_gate.py stop; echo "exit=$?"
```

Expected: stderr に `CommonBlockPlaceSystem.cs` を含むブロックメッセージ、`exit=2`

- [ ] **Step 3: 単体で検証する（通過側＋カウンタ上限）**

```bash
printf -- '# spec\n## 判断記録（ADR）\n- CommonBlockPlaceSystem.cs を改修する（agent前提）\n' > /tmp/ledger-gate-fixture/docs/superpowers/specs/s.md
echo '{"session_id":"testledger"}' | python3 .claude/skills/moores-code-review/scripts/ledger_gate.py stop; echo "exit=$?"
rm -rf "${TMPDIR:-/tmp}/claude-ledger-gate"
```

Expected: 出力なし・`exit=0`。さらに未掲載状態に戻して stop を3回叩くと3回目は `exit=0`（上限2）。

追加確認（相対spec解決）: fixture planの frontmatter を `spec: docs/superpowers/specs/s.md`（相対）に書き換え、cwdを `/tmp` に変えて stop を実行しても plan位置由来で s.md が解決されること（掲載済み状態で `exit=0`）。

- [ ] **Step 4: writing-plans/SKILL.md に配線と規則を追加する**

frontmatter hooks の PostToolUse と Stop に1本ずつ追加（sim-gateの下に併記。**既存の PreToolUse（sim-gate preask）は無変更で維持** — 全置換でpreask関所を消さないこと）:

```yaml
  PreToolUse:
    - matcher: "AskUserQuestion"
      hooks:
        - type: command
          command: "bash .claude/skills/user-simulator/scripts/sim-gate.sh preask"
  PostToolUse:
    - matcher: "Write|Edit"
      hooks:
        - type: command
          command: "bash .claude/skills/user-simulator/scripts/sim-gate.sh track"
        - type: command
          command: "python3 .claude/skills/moores-code-review/scripts/ledger_gate.py track"
  Stop:
    - hooks:
        - type: command
          command: "bash .claude/skills/user-simulator/scripts/sim-gate.sh stop"
        - type: command
          command: "python3 .claude/skills/moores-code-review/scripts/ledger_gate.py stop"
```

Plan Document Header の冒頭コード例に frontmatter を追加:

```markdown
すべてのplanはファイル先頭に frontmatter で対応specパスを持つ（ledger-gateが判断台帳の所在を特定する。必須）:

    ---
    spec: docs/superpowers/specs/YYYY-MM-DD-<topic>-design.md
    ---
```

Task Structure 説明の直後に追記:

```markdown
**判断台帳掲載義務（機械的下限）:** タスクの `Modify:`/`Create:` 対象が `.claude/skills/moores-code-review/lenses/*.md` の `paths` 正規表現にマッチする場合、その改修判断はspecの『## 判断記録（ADR）』への掲載が必須（級の自己判定によらない）。未掲載は ledger-gate（Stop hook）がブロックする。掲載なき判断はレビュー免責力を持たない。カバー範囲はpaths発火型レンズのみ — keywords発火型の観点はレビュー段階のsuppressed規則で捕捉される。**Files節の対象は必ずリポジトリ相対パスで書く**（裸のクラス名だけの表記はゲートの検査対象から漏れる）。
```

- [ ] **Step 5: コミットする**

```bash
git add .claude/skills/moores-code-review/scripts/ledger_gate.py .claude/skills/writing-plans/SKILL.md
git commit -m "ledger-gate関所を新設しwriting-plansへ配線（変更3の執行レイヤー）"
```

---

### Task 6: 判断台帳方式をbrainstorming/user-simulatorへ反映（変更3の指示文）

**Files:**
- Modify: `.claude/skills/brainstorming/SKILL.md`（チェックリスト8-10項・Simulator Review節・ユーザーレビューゲート節）
- Modify: `.claude/skills/user-simulator/modes/review/protocol.md`（手順4のテンプレート）
- Modify: `.claude/skills/user-simulator/modes/preanswer/protocol.md`（手順5のADR記録）

- [ ] **Step 1: brainstorming/SKILL.md のADR節を判断台帳仕様へ拡張する**

「Simulator Review + 判断記録（ADR）」節の `## 判断記録（ADR）` 説明に追記:

```markdown
ADRは**判断台帳**として扱う: (a) ユーザー裁定 — 発言引用またはAskUserQuestion結果・日付つき（全件） (b) agent前提 — 1行・拒否権注記つき。掲載対象は原則アーキテクチャ級/不可逆級だが、**Modify対象が moores-code-review レンズの paths にマッチする判断は級によらず掲載必須**（機械的下限。ledger-gateがブロックする）。台帳に無い判断はレビュー免責力を持たない。
```

- [ ] **Step 2: ユーザーレビューゲートを台帳提示方式へ変更する**

「ユーザーレビューゲート」節の提示文テンプレートを次に置換:

```markdown
**ユーザーレビューゲート（台帳承認方式）:**
specレビューのループが通ったら、**判断台帳をメッセージ本文に直接貼って**レビューを依頼する。spec本文はパスのみ示す（全文読了を承認の前提にしない）:

> 「specを `<path>` に書いてコミットしました。**あなたの承認対象は以下の判断台帳です**（spec本文は読みたい場合のみ）。
> **ユーザー裁定済み**: <各1行>
> **agent前提（1行・拒否権つき — 黙認しても免責力は持ちません）**: <各1行>
> 問題なければ先へ進みます。」

「ok」の形式的意味は**台帳項目の承認**に限定される。spec本文にのみ書かれ台帳に無い判断は、承認後もagent前提のまま残る。ユーザーの返答を待ち、拒否があれば反映してspecレビューのループを再実行する。
```

- [ ] **Step 3: レビュー用インフォグラフィック節を任意化する**

「レビュー用インフォグラフィック（必須）」の見出しと本文を「（任意）」に変更し、冒頭に1行追加:

```markdown
**レビュー用インフォグラフィック（任意）:**
台帳提示が主・図解は補助。ユーザーが図解を希望した場合、またはspecが視覚化で明らかに伝わりやすい場合のみ生成する（デフォルトでは生成しない）。
```

チェックリスト9項も「（ユーザー希望時のみ）」に合わせて更新する。

- [ ] **Step 4: user-simulatorの両protocolへ出所ラベル種別を明記する**

`modes/review/protocol.md` 手順4の後に1行追記:

```markdown
   台帳掲載時の出所表記は3種: `ユーザー裁定（発言引用/AskUserQuestion YYYY-MM-DD）` / `シミュレーター予測→ユーザー承認 YYYY-MM-DD` / `agent前提（拒否権つき）`。レビューcontextの `[ADR:]` ラベルはこの台帳項目だけを参照できる。
```

`modes/preanswer/protocol.md` 手順5は既に「シミュレーター予測→ユーザー承認」表記を規定済み — 変更なしを確認のみ。

- [ ] **Step 5: コミットする**

```bash
git add .claude/skills/brainstorming/SKILL.md .claude/skills/user-simulator/modes/review/protocol.md
git commit -m "判断台帳方式をbrainstorming/user-simulatorへ反映（変更3・原則③）"
```

---

### Task 7: all-code-review側の同期（別リポジトリ ~/.agents/skills）

**Files:**
- Create: `~/.agents/skills/all-code-review/scripts/checks_context.py`（Task 1と同一内容）
- Modify: `~/.agents/skills/all-code-review/scripts/deterministic_checks.py`（Task 1 Step 3と同じ配線。main()構造が異なる場合は「--context受領時にchecks_context.runの結果をconfirmedへ加算」という同一意味で適応）
- Modify: `~/.agents/skills/all-code-review/SKILL.md`（4カテゴリcontext項へ出所ラベル必須化＋検査コマンドに--context追加）
- Modify: `~/.agents/skills/all-code-review/references/integration-rules.md`（§2.6相当のsuppressed統合規則＋§6相当の出所ラベル改訂。Task 3と同一文言）
- Modify: `~/.agents/skills/all-code-review/reviewers/*.md`（依頼動詞優先ガード25本のうち「トレードオフ合致→指摘しない/抑制」文を持つものをTask 2の統一文言へ置換。発火条件ガードは無変更）

- [ ] **Step 1: checks_context.py をコピーし deterministic_checks.py に配線する**

```bash
cp .claude/skills/moores-code-review/scripts/checks_context.py ~/.agents/skills/all-code-review/scripts/
```

配線後の検証はTask 1 Step 4と同じfixture・同じ期待値で `~/.agents/skills/all-code-review/scripts/deterministic_checks.py` を実行する。

- [ ] **Step 2: SKILL.md・integration-rules.md を改訂する**

SKILL.md `:44` の「偽装しない」行をTask 1 Step 5と同じ出所ラベル文言に置換し、deterministic_checks呼び出し箇所に `--context "<USER_PROMPT_PATH>"` を追加。integration-rules.md にTask 3のStep 1（§2.6）とStep 3（§6改訂）を同一文言で適用。報告構成節があれば「免責で消された指摘」セクション必須を追記。

- [ ] **Step 3: reviewers 25本のガードを置換する**

Run: `grep -rln "依頼動詞優先ガード" ~/.agents/skills/all-code-review/reviewers/` で列挙し、各ファイルについてTask 2 Step 2の判定（トレードオフ免責文のみ対象・発火条件ガードは無変更）と統一文言置換を適用。

検証: `grep -rn "合意済み.*なら指摘しない\|反する指摘は抑制する" ~/.agents/skills/all-code-review/reviewers/` が0件、`grep -rln "suppressed-by" ~/.agents/skills/all-code-review/reviewers/ | wc -l` が置換対象数と一致。

- [ ] **Step 4: 別リポジトリとしてコミットする**

```bash
git -C ~/.agents/skills status --short   # 変更が all-code-review 配下のみであることを確認
git -C ~/.agents/skills add skills/all-code-review 2>/dev/null || git -C ~/.agents/skills add all-code-review
git -C ~/.agents/skills commit -m "免責ロンダリング封鎖: 出所ラベル・suppressed降格・可視化をall-code-reviewへ同期"
```

（リポジトリルートの位置は `git -C ~/.agents/skills rev-parse --show-toplevel` で確認してからaddパスを合わせる）

---

### Task 8: 最終レビュー（必須・省略不可）

- [ ] **Step 1: moores-code-reviewスキルで全ブランチレビューを実行する**

必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。今回の変更はmd/pyのみのため、コンパイル（uloop）は対象外。レビュー対象diffは本plan着手からの全コミット＋未コミット変更。

- [ ] **Step 2: 一時fixtureを掃除する**

```bash
rm -rf /tmp/ledger-test-context.md /tmp/ledger-test-client.diff /tmp/ledger-gate-fixture "${TMPDIR:-/tmp}/claude-ledger-gate/testledger"*
```

---

## 判断記録（ADR）

- specのADR: `docs/superpowers/specs/2026-07-26-review-exemption-laundering-fix-design.md` ## 判断記録（ADR）参照
- **planning中の追加判断（agent前提・拒否権つき）**:
  - ledger_gate.py はhooksのstdin JSONを直接Pythonで読む（sim-gate.shのようなbashラッパーを挟まない。python3単体で完結し依存が減る）
  - 台帳掲載の機械判定は「specの台帳セクションに対象ファイルのbasenameが出現するか」の文字列一致（構造化パースはYAGNI）
  - ガード文言置換の対象は「トレードオフ合致→指摘抑制」文のみ。発火条件ガード（症状・依頼種別による判定対象の絞り込み）は免責ロンダリングと無関係のため無変更
  - brainstormingのインフォグラフィック生成を必須→任意（ユーザー希望時のみ）へ降格（出所: ユーザー発言「結局いま長文docを読まされる立場にいる」2026-07-26 → 台帳追加宣言 → 「一旦とりあえず最後まで進めて」で黙認継続中）
