# レビューダイジェストのMarkdown正本化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** `pr-independent-review` のAIが所定フォーマットの `digest.md` 1枚だけを書き、決定論コンバータが `digest.html` と `findings.json` を生成する構成へ移す。

**Architecture:** `.agents/skills/pr-independent-review/scripts/digest_build.py` をCLI入口とし、`scripts/digest_md/` パッケージに parse（Markdown→文書モデル）・blocks/inline（Markdown→HTML片）・findings（文書モデル→findings.json）・render（文書モデル→HTML）を分ける。HTMLの外枠（CSS・コメント機能JS・hero・footer）は既存 `assets/digest-template.html` を**シェルとしてそのまま使い**、`<main>` の中身だけを差し替える。これにより見た目の同一性が構造的に保たれる。

**Tech Stack:** Python 3（標準ライブラリのみ・外部依存禁止）／pytest（テスト実行は `~/hermes-agent/venv/bin/pytest`）

## Requirements

- R1. AIが書く正本は `digest.md` 1枚。`digest.html` と `findings.json` はコンバータの出力であり、AIは直接書かない。受け入れ基準: SKILL.md Step 7 に「HTMLを書く」手順が1つも残っていない。
- R2. ページ骨格（セクション分け・並び順・「あなたが判断すること」インデックス・0件セクションの存置）はコンバータが導出する。受け入れ基準: `digest.md` にセクション見出しを書かなくても、golden と同じ8ゾーンが同じ順で出る。
- R3. finding は「見出し＋YAMLブロック＋自由本文」で書く。`options` はリスト順がそのまま案キー A/B/C になる。受け入れ基準: `options` を3つ書くと findings.json に key A/B/C がこの順で出る。
- R4. `options` の先頭は必ず `key: "A"` かつ `recommended: true` として出力される。受け入れ基準: `recommended` を書く欄がフォーマットに存在せず、全 non-suppressed finding でちょうど1つ true になる。
- R5. 裁定サイトは `recommended` 欠落時に黙って先頭案を採用しない。受け入れ基準: `autoPlanFor` の fallback 経路が消え、欠落があると完了（一括採用）が拒否される。
- R6. 見た目は現行とほぼ変わらない。受け入れ基準: PR #1155 の golden md からの再生成HTMLと現行 `pr-1155-r2/digest.html` を同一幅でレンダリングしたスクリーンショットが、意図した差分以外で違わない。
- R7. finding id（F01..）はコンバータが採番規則（severity降順→ファイルパス昇順→行番号昇順）で振る。本文中の相互参照は slug で書き、コンバータがアンカーへ解決する。受け入れ基準: md に `F01` という文字列を一切書かずに golden が再現できる。
- R8. コード抜粋のHTMLエスケープはコンバータが行う。受け入れ基準: `Subject<int>` を含む抜粋が `&lt;int&gt;` として表示され、消えない。
- R9. 未知の記法・必須キー欠落・予約見出し欠落はコンバータがエラーで落ちる。受け入れ基準: 異常入力それぞれで非0終了し、原因が1行で分かるメッセージが出る。
- R10. `digest.md` も `$RUNDIR` に保存される成果物に含める。受け入れ基準: SKILL.md Step 7 に保存先が明記される。

**やらないこと（スコープ境界）:**
- 過去runのdigest再生成（成果物は当時のまま保存する）
- `pr-independent-review` 以外のスキルへの展開（`digest-template.html` を使うスキルは本スキルのみ）
- 汎用Markdown処理系の実装（digestが使う記法のみ）
- 裁定サイトのUI・裁定フロー自体の変更（R5のフォールバック削除以外は触らない）

## Global Constraints

- 実装は worktree `/Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/digest-md`（ブランチ `feature/digest-markdown-converter`）で行う。
- Python は**標準ライブラリのみ**。`pyyaml` / `markdown` / `markdown-it-py` は使用禁止（このマシンの `python3` に未導入で、レビューは使い捨てworktreeで無人headless実行されるため）。YAMLは digest が使うサブセット（`key: value`、`key:` 直下の `- ` リスト、`[a, b]` のインラインリスト）だけを自前で読む。
- 1ファイル200行以下。1ディレクトリ10ファイルまで。責務での分割を守る。
- コメントは日本語1行→英語1行の2行セットで、約3〜10行ごと。日本語・英語とも1行に収める。
- テストは既存前例（`tests/test_novelty_gate.py` は pytest）に合わせ pytest で書く。実行は `~/hermes-agent/venv/bin/pytest`。
- 出力HTMLは `assets/digest-template.html` を**シェルとして再利用**する。CSS・コメント機能JS・hero・footer・コメントUIのDOMは1文字も書き換えない。
- 本planの作業タスクは bd の `moorestech-cg2`。着手時にclaimし、完了時にcloseする。

---

## digest.md フォーマット仕様（全タスク共通の契約）

実装者はこの節を仕様の正本として扱う。

### 文書ヘッダ（必須・ファイル先頭）

    # PR #1155 機械UI改修: 進捗矢印の共通化・タブ入替/未選択時レシピ表示・電力の充足率化（ADR 0010）

    ```yaml
    pr: 1155
    head: 33e39a1f0c2b4d5e6f708192a3b4c5d6e7f80912
    verdict: reject
    verdict_line: Critical 8件 / 設計判断 3件 / 新形 0件 / suppressed 0件
    date: 2026-08-18
    generated_at: 2026-08-18T02:44:00+09:00
    ```

- `verdict` は `auto` / `ruling` / `reject` / `stub` の4語のみ。`data-verdict` 属性へそのまま入る。
- `verdict` の表示文言は固定表: `auto`=自動マージ可 / `ruling`=新形につき裁定行き / `reject`=Critical差し戻し / `stub`=未測定（スタブ）。

### finding ブロック（`## ` 見出しで始まる）

    ## 歯車機械の要求トルク率に上限なしの電力倍率を流すが供給側は1でクランプする

    ```yaml
    slug: gear-torque-rate
    category: design-decision
    severity: medium
    must_read: true
    summary: 需要だけ1.5倍に膨らみ、供給も速度も増えない。
    index_label: 歯車機械に倍率を効かせるか（案A/案Bは排他）
    files: [moorestech_server/Assets/Scripts/Game.Block/Blocks/Machine/VanillaGearMachineComponent.cs:40]
    options:
      - 供給側に requestRate を通し、要求と供給を同じ式に揃える
      - Processing ? 1f : idleRate へ戻す
    ```

    ```code-card
     36|        private void UpdateTorqueRequestRate()
     37|        {
    +38|            // 表示の分母・加工速度と同じ導出をそのまま歯車網への要求へ反映する
    *+40|            _gearEnergyTransformer.SetTorqueRequestRate(...);
     41|        }
    ```

    **PR側の主張:** 歯車機械の要求トルクにもモジュール倍率を反映する `[agent前提]`

    **独立レビューの実測:** `GearConsumptionCalculator.cs:41-52` の required は rate を見ない。

YAMLキー:

| キー | 必須 | 意味 |
|---|---|---|
| `slug` | 必須 | 本文からの相互参照に使う安定キー。文書内で一意 |
| `category` | 必須 | `critical` / `design-decision` / `novelty` のいずれか |
| `severity` | 必須 | `critical` / `high` / `medium` / `low` のいずれか |
| `must_read` | `design-decision` のみ必須 | true なら「必読の設計判断」ゾーンへ入る |
| `summary` | 必須 | 一言サマリ1行（`p.summary-line` になる） |
| `index_label` | 任意 | 「あなたが判断すること」の短ラベル。省略時は `summary` |
| `files` | 必須 | `path:line` の配列。1件目が主。**id採番のキー** |
| `options` | 非suppressedで必須 | 案の要約の配列。**先頭が推奨案** |
| `suppressed` | 任意（既定 false） | true なら suppressed ゾーンへ |
| `suppress_reason` | `suppressed: true` のとき必須 | 免責の出所要約 |
| `recommendation` | 任意 | findings.json の `recommendation`。省略時は先頭optionの文言 |
| `label` | 任意 | `data-label`。省略時は `{title}のカード（実コード抜粋つき）` |

**`recommended` を書く欄は存在しない**（R4）。コンバータが先頭optionに付ける。

### コードフェンス `code-card`

各行は `[フラグ]<行番号>|<コード>`。フラグ `+` = 追加行（`<ins>`）、`*` = 問題行（`.hl`）。`*+` の併用可。
`|` の最初の1個だけが区切り。コードは**エスケープせず生のまま**書く（コンバータがエスケープする）。

### 相互参照

本文中で他の finding を指すときは `[F:gear-torque-rate]` と書く。コンバータが `<a href="#f03">F03</a>` へ解決する。未定義 slug はエラー。

### 予約見出し（`# ` 見出し・すべて必須）

- `# 注記` — 直下に `## must-read` / `## other-rulings` / `## suppressed` / `## new-shape` / `## criticals` の5つ（各ゾーンの導入段落。0件ゾーンでは「該当なし（0件）。…」を書く）
- `# 判断台帳` — 自由Markdown（`<section id="ledger">` の中身）
- `# 折りたたみ参考` — 直下の `## ` 見出しがそれぞれ `<details><summary>` になる

### 対応する記法（これ以外はエラー）

段落 / `- ` 箇条書き / `### ` 見出し（h3） / コードフェンス（`code-card` と無印） / `**強調**` / `` `コード` `` / `[F:slug]` 参照。生のHTMLタグは書かない。

---

### Task 1: Markdownパーサ（文書モデル化）

**Files:**
- Create: `.agents/skills/pr-independent-review/scripts/digest_md/__init__.py`
- Create: `.agents/skills/pr-independent-review/scripts/digest_md/parse.py`
- Test: `.agents/skills/pr-independent-review/tests/test_digest_parse.py`

**Interfaces:**
- Produces: `parse_document(text: str) -> Document`。`Document` は dataclass で `meta: dict`、`notes: dict`、`ledger_md: str`、`appendix_md: str`、`findings: list[Finding]`。`Finding` は dataclass で `slug/title/category/severity/summary/files/body_md/options/must_read/index_label/suppressed/suppress_reason/recommendation/label/id`（`id` は後段で代入・初期値 `""`）。
- Produces: `class DigestError(Exception)` — 全モジュール共通のエラー型。
- Produces: `parse_yaml_block(text: str) -> dict` — サブセットYAML（`key: value` / `key:` + `- ` リスト / `[a, b]`）。

- [x] **Step 1: 失敗するテストを書く**

```python
# .agents/skills/pr-independent-review/tests/test_digest_parse.py
# digest.md のパースと必須キー検査を検証する
# Verify digest.md parsing and required-key validation
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.parse import DigestError, parse_document, parse_yaml_block

MINIMAL = """# PR #1 テストPR

```yaml
pr: 1
head: 0123456789012345678901234567890123456789
verdict: reject
verdict_line: Critical 1件
date: 2026-08-18
generated_at: 2026-08-18T02:44:00+09:00
```

## 最初の指摘

```yaml
slug: first
category: critical
severity: critical
summary: 壊れている。
files: [a/b/C.cs:10]
options:
  - 直す
```

**PR側の主張:** なし

# 注記

## must-read

必読は0件。

## other-rulings

残りも0件。

## suppressed

該当なし（0件）。

## new-shape

該当なし（0件）。

## criticals

1件ある。

# 判断台帳

- ユーザー裁定なし

# 折りたたみ参考

## Warning全件

0件。
"""


def test_parse_yaml_block_subset():
    got = parse_yaml_block("pr: 1\nfiles: [a.cs:1, b.cs:2]\nopts:\n  - x\n  - y\n")
    assert got == {"pr": "1", "files": ["a.cs:1", "b.cs:2"], "opts": ["x", "y"]}


def test_parse_document_reads_meta_and_findings():
    doc = parse_document(MINIMAL)
    assert doc.meta["pr"] == "1"
    assert doc.meta["verdict"] == "reject"
    assert len(doc.findings) == 1
    f = doc.findings[0]
    assert f.slug == "first"
    assert f.category == "critical"
    assert f.files == ["a/b/C.cs:10"]
    assert f.options == ["直す"]
    assert "PR側の主張" in f.body_md
    assert doc.notes["criticals"] == "1件ある。"
    assert "ユーザー裁定なし" in doc.ledger_md
    assert "Warning全件" in doc.appendix_md


def test_missing_reserved_section_is_error():
    text = MINIMAL.replace("# 判断台帳\n\n- ユーザー裁定なし\n", "")
    with pytest.raises(DigestError) as e:
        parse_document(text)
    assert "判断台帳" in str(e.value)


def test_missing_required_key_is_error():
    text = MINIMAL.replace("severity: critical\n", "")
    with pytest.raises(DigestError) as e:
        parse_document(text)
    assert "severity" in str(e.value)


def test_unknown_verdict_is_error():
    text = MINIMAL.replace("verdict: reject", "verdict: maybe")
    with pytest.raises(DigestError) as e:
        parse_document(text)
    assert "verdict" in str(e.value)


def test_recommended_key_is_rejected():
    text = MINIMAL.replace("options:\n  - 直す", "recommended: true\noptions:\n  - 直す")
    with pytest.raises(DigestError) as e:
        parse_document(text)
    assert "recommended" in str(e.value)
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `cd /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/digest-md && ~/hermes-agent/venv/bin/pytest .agents/skills/pr-independent-review/tests/test_digest_parse.py -v`
Expected: FAIL with `ModuleNotFoundError: No module named 'digest_md'`

- [x] **Step 3: 最小限の実装を書く**

```python
# .agents/skills/pr-independent-review/scripts/digest_md/__init__.py
# digest.md を digest.html / findings.json へ変換するパッケージ
# Package that converts digest.md into digest.html / findings.json
```

```python
# .agents/skills/pr-independent-review/scripts/digest_md/parse.py
# digest.md を文書モデルへ落とす。未知の構造・必須キー欠落は全てDigestErrorで落とす
# Parse digest.md into the document model; unknown structure and missing keys raise DigestError
from __future__ import annotations

from dataclasses import dataclass, field

VERDICTS = {"auto", "ruling", "reject", "stub"}
CATEGORIES = {"critical", "design-decision", "novelty"}
SEVERITIES = {"critical", "high", "medium", "low"}
META_KEYS = ["pr", "head", "verdict", "verdict_line", "date", "generated_at"]
NOTE_KEYS = ["must-read", "other-rulings", "suppressed", "new-shape", "criticals"]


class DigestError(Exception):
    pass


@dataclass
class Finding:
    slug: str
    title: str
    category: str
    severity: str
    summary: str
    files: list
    body_md: str
    options: list = field(default_factory=list)
    must_read: bool = False
    index_label: str = ""
    suppressed: bool = False
    suppress_reason: str = ""
    recommendation: str = ""
    label: str = ""
    id: str = ""


@dataclass
class Document:
    meta: dict
    notes: dict
    ledger_md: str
    appendix_md: str
    findings: list


def parse_yaml_block(text: str) -> dict:
    # digestが使うサブセットだけを読む。深い構造は推測せずエラーにする
    # Only the subset digest uses; deeper structures are rejected rather than guessed
    out: dict = {}
    key = None
    for raw in text.splitlines():
        if not raw.strip():
            continue
        stripped = raw.strip()
        if stripped.startswith("- "):
            if key is None:
                raise DigestError(f"リスト項目の親キーがありません: {raw!r}")
            if not isinstance(out.get(key), list):
                raise DigestError(f"キー {key} に値とリストが混在しています")
            out[key].append(stripped[2:].strip())
            continue
        if raw.startswith(" "):
            raise DigestError(f"未対応のインデント行です: {raw!r}")
        if ":" not in raw:
            raise DigestError(f"key: value 形式ではありません: {raw!r}")
        key, value = raw.split(":", 1)
        key, value = key.strip(), value.strip()
        if value == "":
            out[key] = []
        elif value.startswith("[") and value.endswith("]"):
            inner = value[1:-1].strip()
            out[key] = [v.strip() for v in inner.split(",")] if inner else []
        else:
            out[key] = value
    return out


def _fence(lines: list, i: int) -> tuple:
    # フェンスの中身と、閉じフェンスの次の行番号を返す
    # Return the fenced body and the line index just after the closing fence
    body = []
    i += 1
    while i < len(lines) and not lines[i].startswith("```"):
        body.append(lines[i])
        i += 1
    if i >= len(lines):
        raise DigestError("閉じられていないコードフェンスがあります")
    return "\n".join(body), i + 1


def _split_blocks(text: str) -> tuple:
    # 見出しレベル1/2でブロックへ割る。フェンス内の見出しは無視する
    # Split by level-1/2 headings, ignoring headings that live inside fences
    lines = text.splitlines()
    blocks, title = [], ""
    cur_level, cur_title, buf = "", "", []
    i, in_fence = 0, False
    while i < len(lines):
        line = lines[i]
        if line.startswith("```"):
            in_fence = not in_fence
        if not in_fence and line.startswith("# ") and not title and not cur_level:
            title = line[2:].strip()
            i += 1
            continue
        if not in_fence and (line.startswith("# ") or line.startswith("## ")):
            if cur_level:
                blocks.append((cur_level, cur_title, "\n".join(buf).strip()))
            cur_level = "1" if line.startswith("# ") else "2"
            cur_title = line.lstrip("#").strip()
            buf = []
            i += 1
            continue
        buf.append(line)
        i += 1
    if cur_level:
        blocks.append((cur_level, cur_title, "\n".join(buf).strip()))
    return blocks, title


def _finding_from(title: str, body: str) -> Finding:
    lines = body.splitlines()
    j = 0
    while j < len(lines) and not lines[j].strip():
        j += 1
    if j >= len(lines) or not lines[j].startswith("```yaml"):
        raise DigestError(f"finding「{title}」の直下に ```yaml ブロックがありません")
    meta_text, after = _fence(lines, j)
    meta = parse_yaml_block(meta_text)
    rest = "\n".join(lines[after:]).strip()

    suppressed = str(meta.get("suppressed", "false")).lower() == "true"
    required = ["slug", "category", "severity", "summary", "files"]
    required += ["suppress_reason"] if suppressed else ["options"]
    for key in required:
        if not meta.get(key):
            raise DigestError(f"finding「{title}」に必須キー {key} がありません")
    if meta["category"] not in CATEGORIES:
        raise DigestError(f"finding「{title}」の category が不正です: {meta['category']}")
    if meta["severity"] not in SEVERITIES:
        raise DigestError(f"finding「{title}」の severity が不正です: {meta['severity']}")
    if "recommended" in meta:
        raise DigestError(f"finding「{title}」に recommended は書けません（先頭optionが推奨です）")

    files = meta["files"] if isinstance(meta["files"], list) else [meta["files"]]
    options = meta.get("options", [])
    if not suppressed and not isinstance(options, list):
        raise DigestError(f"finding「{title}」の options はリストで書いてください")
    return Finding(
        slug=meta["slug"], title=title, category=meta["category"], severity=meta["severity"],
        summary=meta["summary"], files=files, body_md=rest, options=list(options),
        must_read=str(meta.get("must_read", "false")).lower() == "true",
        index_label=meta.get("index_label", ""), suppressed=suppressed,
        suppress_reason=meta.get("suppress_reason", ""),
        recommendation=meta.get("recommendation", ""), label=meta.get("label", ""),
    )


def parse_document(text: str) -> Document:
    # 文書ヘッダ → finding群 → 予約見出し、の順に取り出す
    # Extract the document header, then findings, then the reserved sections
    blocks, title = _split_blocks(text)
    if not title:
        raise DigestError("先頭に `# PR #<番号> <タイトル>` の見出しがありません")

    head_lines = text.splitlines()
    k = next((n for n, ln in enumerate(head_lines) if ln.startswith("```yaml")), -1)
    if k < 0:
        raise DigestError("文書ヘッダの ```yaml ブロックがありません")
    meta = parse_yaml_block(_fence(head_lines, k)[0])
    for key in META_KEYS:
        if not meta.get(key):
            raise DigestError(f"文書ヘッダに必須キー {key} がありません")
    if meta["verdict"] not in VERDICTS:
        raise DigestError(f"verdict が不正です: {meta['verdict']}")
    meta["title"] = title

    findings, notes, ledger, appendix, zone = [], {}, "", "", ""
    for level, name, body in blocks:
        if level == "1":
            zone = name
            if name == "判断台帳":
                ledger = body
            elif name == "折りたたみ参考":
                appendix = body
            elif name not in ("注記", ""):
                raise DigestError(f"未知の予約見出しです: # {name}")
            continue
        if zone == "注記":
            if name not in NOTE_KEYS:
                raise DigestError(f"未知の注記見出しです: ## {name}")
            notes[name] = body
        elif zone == "折りたたみ参考":
            appendix += f"\n\n## {name}\n\n{body}"
        elif zone == "判断台帳":
            ledger += f"\n\n### {name}\n\n{body}"
        else:
            findings.append(_finding_from(name, body))

    for key in NOTE_KEYS:
        if key not in notes:
            raise DigestError(f"注記に ## {key} がありません")
    if not ledger.strip():
        raise DigestError("予約見出し # 判断台帳 がありません")
    if not appendix.strip():
        raise DigestError("予約見出し # 折りたたみ参考 がありません")
    slugs = [f.slug for f in findings]
    dup = {s for s in slugs if slugs.count(s) > 1}
    if dup:
        raise DigestError(f"slug が重複しています: {sorted(dup)}")
    return Document(meta=meta, notes=notes, ledger_md=ledger.strip(),
                    appendix_md=appendix.strip(), findings=findings)
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `~/hermes-agent/venv/bin/pytest .agents/skills/pr-independent-review/tests/test_digest_parse.py -v`
Expected: PASS（6件）

- [x] **Step 5: コミットする**

```bash
git add .agents/skills/pr-independent-review/scripts/digest_md/ .agents/skills/pr-independent-review/tests/test_digest_parse.py
git commit -m "feat(digest): digest.md を文書モデルへ落とすパーサを追加する"
```

---

### Task 2: インライン・ブロックのMarkdown→HTML変換

**Files:**
- Create: `.agents/skills/pr-independent-review/scripts/digest_md/inline.py`
- Create: `.agents/skills/pr-independent-review/scripts/digest_md/blocks.py`
- Test: `.agents/skills/pr-independent-review/tests/test_digest_blocks.py`

**Interfaces:**
- Consumes: `digest_md.parse.DigestError`
- Produces: `escape(text: str) -> str` — `&` `<` `>` `"` `'` をこの順で置換する。
- Produces: `inline_html(text: str, refs: dict) -> str` — `**強調**` / `` `code` `` / `[F:slug]` を変換する。`refs` は slug→`F03` の対応表。
- Produces: `blocks_html(md: str, refs: dict, indent: str) -> str` — 段落・`- `箇条書き・`### `見出し・`code-card`フェンス・無印フェンスをHTMLへ変換する。
- Produces: `code_card_html(body: str, indent: str) -> str` — `[フラグ]<行番号>|<コード>` を `pre.code-card` へ変換する。

- [x] **Step 1: 失敗するテストを書く**

```python
# .agents/skills/pr-independent-review/tests/test_digest_blocks.py
# Markdown片のHTML化とエスケープ、code-cardの行マークアップを検証する
# Verify markdown-to-HTML conversion, escaping, and code-card line markup
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.blocks import blocks_html, code_card_html
from digest_md.inline import escape, inline_html
from digest_md.parse import DigestError


def test_escape_order_keeps_ampersand_first():
    assert escape('a & b < c > d " e \' f') == "a &amp; b &lt; c &gt; d &quot; e &#39; f"


def test_inline_html_converts_strong_code_and_ref():
    got = inline_html("**主張:** `Subject<int>` は [F:gear] を壊す", {"gear": "F03"})
    assert got == '<strong>主張:</strong> <code>Subject&lt;int&gt;</code> は <a href="#f03">F03</a> を壊す'


def test_inline_html_unknown_ref_is_error():
    with pytest.raises(DigestError) as e:
        inline_html("[F:nope]", {"gear": "F03"})
    assert "nope" in str(e.value)


def test_code_card_marks_ins_and_hl():
    body = " 36|        void A()\n+38|            // add\n*+40|            B<int>();"
    got = code_card_html(body, "        ")
    assert '<pre class="code-card"><code><span class="ln">36</span>        void A()' in got
    assert '<span class="ln">38</span><ins>            // add</ins>' in got
    assert '<span class="hl"><span class="ln">40</span><ins>            B&lt;int&gt;();</ins></span>' in got


def test_blocks_html_paragraph_and_list():
    got = blocks_html("段落だ。\n\n- 一つ目\n- 二つ目", {}, "      ")
    assert "<p>段落だ。</p>" in got
    assert "<ul>" in got and "<li>一つ目</li>" in got


def test_blocks_html_rejects_unknown_syntax():
    with pytest.raises(DigestError) as e:
        blocks_html("> 引用は未対応", {}, "")
    assert "未対応" in str(e.value)


def test_blocks_html_rejects_unknown_fence():
    with pytest.raises(DigestError) as e:
        blocks_html("```mermaid\ngraph TD\n```", {}, "")
    assert "mermaid" in str(e.value)
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `~/hermes-agent/venv/bin/pytest .agents/skills/pr-independent-review/tests/test_digest_blocks.py -v`
Expected: FAIL with `ModuleNotFoundError: No module named 'digest_md.inline'`

- [x] **Step 3: 最小限の実装を書く**

```python
# .agents/skills/pr-independent-review/scripts/digest_md/inline.py
# 文字列のエスケープとインライン記法の変換。エスケープは必ずマークアップ付与の前に行う
# String escaping and inline markup; escaping always runs before markup is added
from __future__ import annotations

import re

from .parse import DigestError

_STRONG = re.compile(r"\*\*(.+?)\*\*")
_CODE = re.compile(r"`([^`]+)`")
_REF = re.compile(r"\[F:([A-Za-z0-9_-]+)\]")


def escape(text: str) -> str:
    # & を最初に置換しないと、後続で付けた実体参照まで二重エスケープされる
    # Ampersand must go first, otherwise entities added later get double-escaped
    out = text.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")
    return out.replace('"', "&quot;").replace("'", "&#39;")


def inline_html(text: str, refs: dict) -> str:
    # エスケープ済み文字列に対してのみマークアップを付ける
    # Markup is applied only on top of already-escaped text
    out = escape(text)

    def ref(m):
        slug = m.group(1)
        if slug not in refs:
            raise DigestError(f"未定義の参照です: [F:{slug}]")
        fid = refs[slug]
        return f'<a href="#{fid.lower()}">{fid}</a>'

    out = _REF.sub(ref, out)
    out = _CODE.sub(lambda m: f"<code>{m.group(1)}</code>", out)
    return _STRONG.sub(lambda m: f"<strong>{m.group(1)}</strong>", out)
```

```python
# .agents/skills/pr-independent-review/scripts/digest_md/blocks.py
# ブロック記法（段落・箇条書き・h3・コードフェンス）のHTML化。未知記法は落とす
# Block-level markdown to HTML (paragraph, list, h3, fences); unknown syntax fails loudly
from __future__ import annotations

from .inline import escape, inline_html
from .parse import DigestError

_KNOWN_FENCES = ("code-card", "")


def code_card_html(body: str, indent: str) -> str:
    # 各行は [フラグ]<行番号>|<コード>。+ は追加行、* は問題行
    # Each line is [flags]<lineno>|<code>; "+" marks an insertion, "*" marks the offending line
    rendered = []
    for raw in body.splitlines():
        if "|" not in raw:
            raise DigestError(f"code-card の行に | がありません: {raw!r}")
        head, code = raw.split("|", 1)
        head = head.strip()
        ins, hl = "+" in head, "*" in head
        num = head.replace("+", "").replace("*", "").strip()
        if not num.isdigit():
            raise DigestError(f"code-card の行番号が数字ではありません: {raw!r}")
        inner = escape(code)
        inner = f"<ins>{inner}</ins>" if ins else inner
        line = f'<span class="ln">{num}</span>{inner}'
        rendered.append(f'<span class="hl">{line}</span>' if hl else line)
    return f'{indent}<pre class="code-card"><code>' + "\n".join(rendered) + "</code></pre>"


def blocks_html(md: str, refs: dict, indent: str) -> str:
    # 空行区切りのブロックへ割ってから、種別ごとに変換する
    # Split on blank lines, then convert each block by its kind
    out = []
    lines = md.splitlines()
    i = 0
    while i < len(lines):
        line = lines[i]
        if not line.strip():
            i += 1
            continue
        if line.startswith("```"):
            lang = line[3:].strip()
            if lang not in _KNOWN_FENCES:
                raise DigestError(f"未対応のコードフェンス種別です: {lang}")
            body, i = _collect_fence(lines, i)
            if lang == "code-card":
                out.append(code_card_html(body, indent))
            else:
                out.append(f"{indent}<pre><code>{escape(body)}</code></pre>")
            continue
        if line.startswith("### "):
            out.append(f"{indent}<h3>{inline_html(line[4:].strip(), refs)}</h3>")
            i += 1
            continue
        if line.startswith("- "):
            items, i = _collect_list(lines, i)
            body = "\n".join(f"{indent}  <li>{inline_html(x, refs)}</li>" for x in items)
            out.append(f"{indent}<ul>\n{body}\n{indent}</ul>")
            continue
        if line[0] in "#>|" or (line[0] in "*+" and not line.startswith("**")):
            raise DigestError(f"未対応の記法です: {line!r}")
        para, i = _collect_paragraph(lines, i)
        out.append(f"{indent}<p>{inline_html(para, refs)}</p>")
    return "\n".join(out)


def _collect_fence(lines: list, i: int) -> tuple:
    body = []
    i += 1
    while i < len(lines) and not lines[i].startswith("```"):
        body.append(lines[i])
        i += 1
    if i >= len(lines):
        raise DigestError("閉じられていないコードフェンスがあります")
    return "\n".join(body), i + 1


def _collect_list(lines: list, i: int) -> tuple:
    items = []
    while i < len(lines) and lines[i].startswith("- "):
        items.append(lines[i][2:].strip())
        i += 1
    return items, i


def _collect_paragraph(lines: list, i: int) -> tuple:
    buf = []
    while i < len(lines) and lines[i].strip() and not lines[i].startswith(("```", "- ", "### ")):
        buf.append(lines[i].strip())
        i += 1
    return " ".join(buf), i
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `~/hermes-agent/venv/bin/pytest .agents/skills/pr-independent-review/tests/test_digest_blocks.py -v`
Expected: PASS（7件）

- [x] **Step 5: コミットする**

```bash
git add .agents/skills/pr-independent-review/scripts/digest_md/inline.py .agents/skills/pr-independent-review/scripts/digest_md/blocks.py .agents/skills/pr-independent-review/tests/test_digest_blocks.py
git commit -m "feat(digest): Markdown片のHTML化とcode-card行マークアップを追加する"
```

---

### Task 3: id採番と findings.json 生成

**Files:**
- Create: `.agents/skills/pr-independent-review/scripts/digest_md/findings.py`
- Test: `.agents/skills/pr-independent-review/tests/test_digest_findings.py`

**Interfaces:**
- Consumes: `digest_md.parse.Document` / `Finding`
- Produces: `assign_ids(doc: Document) -> dict` — severity降順→ファイルパス昇順→行番号昇順で `F01` から採番し、各 `Finding.id` を書き換えて slug→id の対応表を返す。
- Produces: `build_findings(doc: Document) -> dict` — findings.json の dict。`options` は先頭が `{"key": "A", "summary": ..., "recommended": True}`、2件目以降は `{"key": "B", "summary": ...}`。
- Produces: `sort_key(f: Finding) -> tuple` — 採番順の比較キー（render からも使う）。

- [x] **Step 1: 失敗するテストを書く**

```python
# .agents/skills/pr-independent-review/tests/test_digest_findings.py
# id採番規則と、先頭optionが必ず推奨になることを検証する
# Verify the id numbering rule and that the first option is always the recommended one
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.findings import assign_ids, build_findings
from digest_md.parse import Document, Finding


def _doc(findings):
    meta = {"pr": "1", "head": "0" * 40, "verdict": "reject", "verdict_line": "x",
            "date": "2026-08-18", "generated_at": "2026-08-18T00:00:00+09:00", "title": "t"}
    return Document(meta=meta, notes={}, ledger_md="", appendix_md="", findings=findings)


def _f(slug, severity, path, options=("直す",), category="critical"):
    return Finding(slug=slug, title=slug, category=category, severity=severity,
                   summary="s", files=[path], body_md="", options=list(options))


def test_assign_ids_orders_by_severity_then_path_then_line():
    doc = _doc([
        _f("b", "medium", "z/A.cs:5"),
        _f("a", "critical", "b/B.cs:20"),
        _f("c", "critical", "b/B.cs:3"),
        _f("d", "critical", "a/C.cs:1"),
    ])
    refs = assign_ids(doc)
    assert refs == {"d": "F01", "c": "F02", "a": "F03", "b": "F04"}


def test_build_findings_makes_first_option_recommended():
    doc = _doc([_f("a", "critical", "a/A.cs:1", options=("直す", "戻す", "消す"))])
    assign_ids(doc)
    out = build_findings(doc)
    opts = out["findings"][0]["options"]
    assert [o["key"] for o in opts] == ["A", "B", "C"]
    assert opts[0]["recommended"] is True
    assert all("recommended" not in o for o in opts[1:])


def test_build_findings_every_non_suppressed_has_exactly_one_recommended():
    doc = _doc([_f("a", "critical", "a/A.cs:1"), _f("b", "low", "b/B.cs:2", options=("x", "y"))])
    assign_ids(doc)
    out = build_findings(doc)
    for f in out["findings"]:
        assert len([o for o in f["options"] if o.get("recommended")]) == 1


def test_build_findings_suppressed_has_no_options():
    f = _f("s", "high", "a/A.cs:1", options=())
    f.suppressed = True
    f.suppress_reason = "ADRで免責"
    doc = _doc([f])
    assign_ids(doc)
    out = build_findings(doc)
    assert out["findings"][0]["options"] == []
    assert out["findings"][0]["suppress_reason"] == "ADRで免責"
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `~/hermes-agent/venv/bin/pytest .agents/skills/pr-independent-review/tests/test_digest_findings.py -v`
Expected: FAIL with `ModuleNotFoundError: No module named 'digest_md.findings'`

- [x] **Step 3: 最小限の実装を書く**

```python
# .agents/skills/pr-independent-review/scripts/digest_md/findings.py
# id採番と findings.json 生成。推奨案は「optionsの先頭」で機械的に決まる
# Id assignment and findings.json generation; the recommended option is always options[0]
from __future__ import annotations

from .parse import DigestError, Document, Finding

SEVERITY_ORDER = {"critical": 0, "high": 1, "medium": 2, "low": 3}
OPTION_KEYS = "ABCDEF"


def sort_key(f: Finding) -> tuple:
    # severity降順→ファイルパス昇順→行番号昇順で安定させる
    # Stable ordering: severity desc, then file path asc, then line number asc
    path, _, line = f.files[0].partition(":")
    return (SEVERITY_ORDER[f.severity], path, int(line) if line.isdigit() else 0)


def assign_ids(doc: Document) -> dict:
    refs = {}
    for n, f in enumerate(sorted(doc.findings, key=sort_key), start=1):
        f.id = f"F{n:02d}"
        refs[f.slug] = f.id
    return refs


def build_findings(doc: Document) -> dict:
    out = []
    for f in sorted(doc.findings, key=sort_key):
        options = []
        for n, summary in enumerate(f.options):
            if n >= len(OPTION_KEYS):
                raise DigestError(f"{f.id}: 案が{len(OPTION_KEYS)}件を超えています")
            option = {"key": OPTION_KEYS[n], "summary": summary}
            # 先頭が推奨。フラグを書く欄が無いので欠落しようがない
            # The first option is the recommended one; there is no field to forget
            if n == 0:
                option["recommended"] = True
            options.append(option)
        out.append({
            "id": f.id, "title": f.title, "severity": f.severity, "category": f.category,
            "files": f.files, "excerpt": _excerpt(f.body_md),
            "recommendation": f.recommendation or (f.options[0] if f.options else ""),
            "options": options, "suppressed": f.suppressed, "suppress_reason": f.suppress_reason,
        })
    return {"pr": int(doc.meta["pr"]), "head": doc.meta["head"], "verdict": doc.meta["verdict"],
            "generated_at": doc.meta["generated_at"], "findings": out}


def _excerpt(body_md: str) -> str:
    # code-cardの中身を行番号を落として抜粋にする（HTMLエスケープはしない契約）
    # Take the code-card body as the excerpt, dropping line numbers; no HTML escaping by contract
    lines = body_md.splitlines()
    for n, line in enumerate(lines):
        if line.startswith("```code-card"):
            body = []
            for rest in lines[n + 1:]:
                if rest.startswith("```"):
                    break
                body.append(rest.split("|", 1)[1] if "|" in rest else rest)
            return "\n".join(body)
    return ""
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `~/hermes-agent/venv/bin/pytest .agents/skills/pr-independent-review/tests/test_digest_findings.py -v`
Expected: PASS（4件）

- [x] **Step 5: コミットする**

```bash
git add .agents/skills/pr-independent-review/scripts/digest_md/findings.py .agents/skills/pr-independent-review/tests/test_digest_findings.py
git commit -m "feat(digest): id採番と先頭option推奨のfindings.json生成を追加する"
```

---

### Task 4: HTMLレンダラ（カード・ゾーン骨格・テンプレシェル）

**Files:**
- Create: `.agents/skills/pr-independent-review/scripts/digest_md/render.py`
- Test: `.agents/skills/pr-independent-review/tests/test_digest_render.py`

**Interfaces:**
- Consumes: `digest_md.parse.Document`、`digest_md.findings.sort_key`、`digest_md.blocks.blocks_html`、`digest_md.inline.escape/inline_html`
- Produces: `render_html(doc: Document, template: str, refs: dict) -> str` — テンプレの `<main>…</main>` を差し替え、`{{TITLE}}`/`{{DATE}}`/`{{SUBTITLE}}` と `STORAGE_KEY`/`COPY_TITLE` を置換し、冒頭の使い方コメントを削除した完全なHTMLを返す。

**ゾーン定義（順序・見出し・所属条件）:**

| id | h2見出し | 所属条件 |
|---|---|---|
| `you-decide` | あなたが判断すること | （自動生成のインデックス） |
| `must-read` | 必読の設計判断 | `category==design-decision` かつ `must_read` |
| `other-rulings` | 残りの設計判断（推奨案どおりで良ければ一言で足りる） | `category==design-decision` かつ not `must_read` |
| `suppressed` | suppressed（判断台帳で免責された指摘） | `suppressed` |
| `new-shape` | 新形（このリポジトリに前例のない形） | `category==novelty` |
| `criticals` | Critical要点（裁定不要の修正リスト） | `category==critical` |
| `ledger` | 判断台帳 | `# 判断台帳` の中身 |
| `appendix` | 折りたたみ参考 | `# 折りたたみ参考` の `## ` ごとに `<details>` |

- [x] **Step 1: 失敗するテストを書く**

```python
# .agents/skills/pr-independent-review/tests/test_digest_render.py
# ゾーン骨格の自動生成と、カードのHTML形状（data-finding-idの付与先を含む）を検証する
# Verify auto-generated zone skeleton and card HTML shape, including where data-finding-id lands
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.findings import assign_ids
from digest_md.parse import Document, Finding
from digest_md.render import render_html

TEMPLATE = (Path(__file__).resolve().parent.parent / "assets" / "digest-template.html").read_text(encoding="utf-8")


def _doc():
    meta = {"pr": "1155", "head": "0" * 40, "verdict": "reject", "verdict_line": "Critical 1件",
            "date": "2026-08-18", "generated_at": "2026-08-18T00:00:00+09:00", "title": "テスト"}
    ruling = Finding(slug="gear", title="歯車の要求トルク率", category="design-decision",
                     severity="medium", summary="需要だけ膨らむ。", files=["a/Gear.cs:40"],
                     body_md="**PR側の主張:** 一致させる", options=["供給側に通す", "戻す"], must_read=True)
    crit = Finding(slug="latch", title="再ラッチ漏れ", category="critical", severity="critical",
                   summary="分母がズレる。", files=["b/Latch.cs:10"],
                   body_md="[F:gear] と同根。", options=["直す"])
    notes = {k: "該当なし（0件）。" for k in
             ["must-read", "other-rulings", "suppressed", "new-shape", "criticals"]}
    return Document(meta=meta, notes=notes, ledger_md="- 台帳の中身",
                    appendix_md="## Warning全件\n\n- なし", findings=[ruling, crit])


def test_render_places_zones_in_fixed_order():
    doc = _doc()
    html = render_html(doc, TEMPLATE, assign_ids(doc))
    ids = re.findall(r'<section id="([a-z-]+)"', html)
    assert ids == ["you-decide", "must-read", "other-rulings", "suppressed",
                   "new-shape", "criticals", "ledger", "appendix"]


def test_render_puts_finding_id_on_the_card_element():
    doc = _doc()
    html = render_html(doc, TEMPLATE, assign_ids(doc))
    # 裁定サイトはこの属性でボタン注入位置を決める。figureラッパではなくカードに付ける
    # The adjudication site injects buttons at this attribute; it must sit on the card, not the wrapper
    assert '<section class="verdict-card critical" id="f01" data-finding-id="F01">' in html
    assert '<section class="verdict-card ruling" id="f02" data-finding-id="F02">' in html


def test_render_sets_verdict_and_placeholders():
    doc = _doc()
    html = render_html(doc, TEMPLATE, assign_ids(doc))
    assert '<section class="verdict-header" data-verdict="reject">' in html
    assert "{{TITLE}}" not in html and "{{DATE}}" not in html and "{{SUBTITLE}}" not in html
    assert "pr-review-1155-comments-v1" in html


def test_render_keeps_template_shell_untouched():
    doc = _doc()
    html = render_html(doc, TEMPLATE, assign_ids(doc))
    assert html.count("<script") == 1
    assert "使い方:" not in html
    assert '<div id="comment-ui-root" data-comment-ui>' in html


def test_render_resolves_cross_reference_to_anchor():
    doc = _doc()
    html = render_html(doc, TEMPLATE, assign_ids(doc))
    assert '<a href="#f02">F02</a> と同根。' in html


def test_render_index_lists_must_read_findings():
    doc = _doc()
    html = render_html(doc, TEMPLATE, assign_ids(doc))
    index = html.split('<section id="you-decide">')[1].split("</section>")[0]
    assert 'href="#f02"' in index and "必読の設計判断 1件" in index
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `~/hermes-agent/venv/bin/pytest .agents/skills/pr-independent-review/tests/test_digest_render.py -v`
Expected: FAIL with `ModuleNotFoundError: No module named 'digest_md.render'`

- [x] **Step 3: 最小限の実装を書く**

```python
# .agents/skills/pr-independent-review/scripts/digest_md/render.py
# 文書モデルからHTMLを組む。外枠はテンプレをそのまま使い <main> の中身だけ差し替える
# Build HTML from the document model; reuse the template shell and replace only <main>
from __future__ import annotations

import re

from .blocks import blocks_html
from .findings import sort_key
from .inline import escape, inline_html
from .parse import DigestError, Document, Finding

VERDICT_TEXT = {"auto": "自動マージ可", "ruling": "新形につき裁定行き",
                "reject": "Critical差し戻し", "stub": "未測定（スタブ）"}
BADGE = {"design-decision": ("badge-new", "設計判断", "verdict-card ruling"),
         "critical": ("badge-sup", "Critical", "verdict-card critical"),
         "novelty": ("badge-new", "新形", "verdict-card")}
ZONES = [("must-read", "必読の設計判断"),
         ("other-rulings", "残りの設計判断（推奨案どおりで良ければ一言で足りる）"),
         ("suppressed", "suppressed（判断台帳で免責された指摘）"),
         ("new-shape", "新形（このリポジトリに前例のない形）"),
         ("criticals", "Critical要点（裁定不要の修正リスト）")]


def _zone_of(f: Finding) -> str:
    if f.suppressed:
        return "suppressed"
    if f.category == "novelty":
        return "new-shape"
    if f.category == "critical":
        return "criticals"
    return "must-read" if f.must_read else "other-rulings"


def _card_html(f: Finding, refs: dict) -> str:
    # data-finding-id はカード要素そのものに付ける（裁定サイトの注入位置の正）
    # data-finding-id sits on the card element itself: the anchor the adjudication site injects at
    if f.suppressed:
        badge_class, badge_text, card_class = "badge-sup", "suppressed", "suppressed-card"
    else:
        badge_class, badge_text, card_class = BADGE[f.category]
    names = " / ".join(f"<strong>{escape(p.split(':')[0].split('/')[-1])}</strong>" for p in f.files)
    head, *rest = f.files
    paths = f"<code>{escape(head)}</code>"
    if rest:
        paths += "（＋ " + ", ".join(f"<code>{escape(p)}</code>" for p in rest) + "）"
    label = f.label or f"{f.title}のカード（実コード抜粋つき）"
    body = blocks_html(f.body_md, refs, "        ")
    extra = ""
    if f.suppressed:
        extra = f'\n        <p><strong>suppressed-by:</strong> {inline_html(f.suppress_reason, refs)}</p>'
    return f"""    <div class="figure" data-label="{escape(label)}">
      <button class="figure-comment-btn" data-comment-ui>コメント</button>
      <section class="{card_class}" id="{f.id.lower()}" data-finding-id="{f.id}">
        <h2><span class="badge {badge_class}">{badge_text}</span> {names}</h2>
        <p class="file-path">{paths}</p>
        <p class="summary-line">{inline_html(f.summary, refs)}</p>
{body}{extra}
      </section>
    </div>"""


def _index_html(doc: Document, refs: dict) -> str:
    # 「あなたが判断すること」はカードから機械的に導出する
    # The "what you decide" index is derived mechanically from the cards
    ordered = sorted(doc.findings, key=sort_key)
    by_zone = {z: [f for f in ordered if _zone_of(f) == z] for z, _ in ZONES}
    rows = []
    must = by_zone["must-read"]
    links = " ／ ".join(f'<a href="#{f.id.lower()}">{f.id} {escape(f.index_label or f.summary)}</a>'
                       for f in must)
    rows.append(f"<strong>必読の設計判断 {len(must)}件</strong>" + (f" — {links}" if links else ""))
    rows.append(f'<strong>suppressed {len(by_zone["suppressed"])}件</strong> — '
                f'<a href="#suppressed">suppressedセクション</a>')
    rows.append(f'<strong>新形の入国審査 {len(by_zone["new-shape"])}件</strong> — '
                f'<a href="#new-shape">新形セクション</a>')
    crit = "・".join(f'<a href="#{f.id.lower()}">{f.id}</a>' for f in by_zone["criticals"])
    rows.append('<strong>裁定不要</strong>: <a href="#criticals">Critical要点</a>'
                + (f"（{crit}）" if crit else "（0件）"))
    items = "\n".join(
        f'      <li class="lead-item"><span class="badge">{n}</span><div>{row}</div></li>'
        for n, row in enumerate(rows, start=1))
    return f"""  <section id="you-decide">
    <h2>あなたが判断すること</h2>
    <ul class="lead-list">
{items}
    </ul>
  </section>"""


def _appendix_html(md: str, refs: dict) -> str:
    # ## 見出しごとに details へ畳む
    # Fold each "## " heading into its own details block
    out = []
    for part in re.split(r"^## ", md, flags=re.M):
        if not part.strip():
            continue
        title, _, body = part.partition("\n")
        out.append(f"    <details>\n      <summary>{inline_html(title.strip(), refs)}</summary>\n"
                   f"{blocks_html(body.strip(), refs, '      ')}\n    </details>")
    return "\n".join(out)


def render_html(doc: Document, template: str, refs: dict) -> str:
    if "<main>" not in template:
        raise DigestError("テンプレートに <main> がありません")
    meta = doc.meta
    verdict = meta["verdict"]
    text = escape(VERDICT_TEXT[verdict])
    parts = [f"""  <section class="verdict-header" data-verdict="{verdict}">
    <h2>verdict: {text}</h2>
    <p class="verdict-line"><strong>verdict: {text}</strong> — {escape(meta['verdict_line'])}</p>
  </section>""", _index_html(doc, refs)]

    ordered = sorted(doc.findings, key=sort_key)
    for zone_id, heading in ZONES:
        cards = [_card_html(f, refs) for f in ordered if _zone_of(f) == zone_id]
        note = blocks_html(doc.notes[zone_id], refs, "    ")
        body = note + ("\n" + "\n".join(cards) if cards else "")
        parts.append(f'  <section id="{zone_id}">\n    <h2>{escape(heading)}</h2>\n{body}\n  </section>')

    parts.append('  <section id="ledger">\n    <h2>判断台帳</h2>\n'
                 f'{blocks_html(doc.ledger_md, refs, "    ")}\n  </section>')
    parts.append('  <section id="appendix">\n    <h2>折りたたみ参考</h2>\n'
                 f'{_appendix_html(doc.appendix_md, refs)}\n  </section>')

    main = "<main>\n\n" + "\n\n".join(parts) + "\n\n</main>"
    out = re.sub(r"<main>.*</main>", lambda _: main, template, flags=re.S)
    title = f"独立レビュー: PR #{meta['pr']} {meta['title']}"
    out = out.replace("{{TITLE}}", escape(title)).replace("{{DATE}}", escape(meta["date"]))
    out = out.replace("{{SUBTITLE}}", escape(f"verdict: {VERDICT_TEXT[verdict]}"))
    out = out.replace("REPLACE_WITH_UNIQUE_STORAGE_KEY", f"pr-review-{meta['pr']}-comments-v1")
    out = out.replace("REPLACE_WITH_COPY_HEADING", f"PR #{meta['pr']} 独立レビュー裁定")
    return re.sub(r"<!--\n  使い方:.*?-->\n", "", out, flags=re.S)
```

- [x] **Step 4: テストを実行して通ることを確認する**

Run: `~/hermes-agent/venv/bin/pytest .agents/skills/pr-independent-review/tests/test_digest_render.py -v`
Expected: PASS（6件）。落ちる場合はテンプレの実文字列を `grep -n "REPLACE_WITH\|{{" .agents/skills/pr-independent-review/assets/digest-template.html` で確認し、置換対象名を実値に合わせる

- [x] **Step 5: コミットする**

```bash
git add .agents/skills/pr-independent-review/scripts/digest_md/render.py .agents/skills/pr-independent-review/tests/test_digest_render.py
git commit -m "feat(digest): ゾーン骨格自動生成とカードHTMLのレンダラを追加する"
```

---

### Task 5: CLI入口と生成後検査

**Files:**
- Create: `.agents/skills/pr-independent-review/scripts/digest_build.py`
- Test: `.agents/skills/pr-independent-review/tests/test_digest_build.py`

**Interfaces:**
- Consumes: `digest_md.parse.parse_document`、`digest_md.findings.assign_ids/build_findings`、`digest_md.render.render_html`
- Produces: CLI `python3 scripts/digest_build.py <RUNDIR>` — `<RUNDIR>/digest.md` を読み、`<RUNDIR>/digest.html` と `<RUNDIR>/findings.json` を書く。エラー時は stderr に出して終了コード1、出力は書かない。
- Produces: `verify(html: str, findings: dict) -> list` — 生成後検査（未置換プレースホルダ0件 / `<script>` 1個 / `data-finding-id` 件数一致 / 全 non-suppressed に recommended ちょうど1件）。違反メッセージの配列を返す。

- [x] **Step 1: 失敗するテストを書く**

```python
# .agents/skills/pr-independent-review/tests/test_digest_build.py
# CLIの入出力と生成後検査を検証する
# Verify the CLI end-to-end and the post-generation checks
import json
import subprocess
import sys
from pathlib import Path

SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "digest_build.py"
GOLDEN_MD = Path(__file__).resolve().parent / "golden" / "pr-1155-digest.md"


def test_cli_writes_html_and_findings(tmp_path):
    (tmp_path / "digest.md").write_text(GOLDEN_MD.read_text(encoding="utf-8"), encoding="utf-8")
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    html = (tmp_path / "digest.html").read_text(encoding="utf-8")
    findings = json.loads((tmp_path / "findings.json").read_text(encoding="utf-8"))
    assert html.count('data-finding-id="') == len(findings["findings"])
    assert findings["pr"] == 1155


def test_cli_fails_loudly_on_broken_markdown(tmp_path):
    (tmp_path / "digest.md").write_text("# PR #1 x\n\n本文だけ\n", encoding="utf-8")
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode == 1
    assert r.stderr.strip()
    assert not (tmp_path / "digest.html").exists()
```

- [x] **Step 2: テストを実行して失敗を確認する**

Run: `~/hermes-agent/venv/bin/pytest .agents/skills/pr-independent-review/tests/test_digest_build.py -v`
Expected: 2件ともFAIL（`digest_build.py` 不在。golden md も Task 6 で作るため1件目はそこまで赤のままでよい）

- [x] **Step 3: 最小限の実装を書く**

```python
#!/usr/bin/env python3
# .agents/skills/pr-independent-review/scripts/digest_build.py
# digest.md から digest.html と findings.json を生成するCLI入口
# CLI entry point that builds digest.html and findings.json from digest.md
from __future__ import annotations

import json
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))

from digest_md.findings import assign_ids, build_findings
from digest_md.parse import DigestError, parse_document
from digest_md.render import render_html

TEMPLATE = Path(__file__).resolve().parent.parent / "assets" / "digest-template.html"


def verify(html: str, findings: dict) -> list:
    # 出荷前の機械検査。人の目視に頼っていた検査をここへ集約する
    # Pre-ship machine checks; the checks that used to rely on human inspection live here
    problems = []
    if "{{" in html:
        problems.append("未置換のプレースホルダが残っています")
    if html.count("<script") != 1:
        problems.append(f"<script> が {html.count('<script')} 個あります（1個であるべき）")
    ids = [f["id"] for f in findings["findings"]]
    for fid in ids:
        if f'data-finding-id="{fid}"' not in html:
            problems.append(f"{fid} のカードがHTMLにありません")
    if html.count('data-finding-id="') != len(ids):
        problems.append("data-finding-id の件数がfindings件数と一致しません")
    for f in findings["findings"]:
        if f["suppressed"]:
            continue
        n = len([o for o in f["options"] if o.get("recommended")])
        if n != 1:
            problems.append(f"{f['id']}: recommended が {n} 件（1件であるべき）")
    return problems


def main() -> int:
    if len(sys.argv) != 2:
        print("usage: digest_build.py <RUNDIR>", file=sys.stderr)
        return 1
    rundir = Path(sys.argv[1])
    md_path = rundir / "digest.md"
    if not md_path.is_file():
        print(f"digest.md がありません: {md_path}", file=sys.stderr)
        return 1

    # 外部入力（AI生成のMarkdown）の隔離のためここだけ例外を捕える
    # This is the external-input boundary (AI-authored markdown), so the exception is caught here
    try:
        doc = parse_document(md_path.read_text(encoding="utf-8"))
        refs = assign_ids(doc)
        findings = build_findings(doc)
        html = render_html(doc, TEMPLATE.read_text(encoding="utf-8"), refs)
    except DigestError as e:
        print(f"digest.md の形式エラー: {e}", file=sys.stderr)
        return 1

    problems = verify(html, findings)
    if problems:
        for p in problems:
            print(f"生成後検査に失敗: {p}", file=sys.stderr)
        return 1

    (rundir / "digest.html").write_text(html, encoding="utf-8")
    with (rundir / "findings.json").open("w", encoding="utf-8") as fp:
        json.dump(findings, fp, ensure_ascii=False, indent=2)
        fp.write("\n")
    print(f"generated: {rundir/'digest.html'} / {rundir/'findings.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [x] **Step 4: テストを実行して2件目が通ることを確認する**

Run: `~/hermes-agent/venv/bin/pytest .agents/skills/pr-independent-review/tests/test_digest_build.py::test_cli_fails_loudly_on_broken_markdown -v`
Expected: PASS

- [x] **Step 5: コミットする**

```bash
git add .agents/skills/pr-independent-review/scripts/digest_build.py .agents/skills/pr-independent-review/tests/test_digest_build.py
git commit -m "feat(digest): digest.md からHTMLとfindings.jsonを生成するCLIを追加する"
```

---

### Task 6: PR #1155 ゴールデンと見た目の突き合わせ

**Files:**
- Create: `.agents/skills/pr-independent-review/tests/golden/pr-1155-digest.md`
- Create: `.agents/skills/pr-independent-review/tests/golden/pr-1155-digest.expected.html`
- Test: `.agents/skills/pr-independent-review/tests/test_digest_golden.py`

**Interfaces:**
- Consumes: `scripts/digest_build.py`
- Produces: `tests/golden/pr-1155-digest.md` — 現行 `pr-1155-r2/digest.html`（11 finding）を本フォーマットへ書き戻したもの。以後の見た目回帰の基準。

- [x] **Step 1: 現行digestを参照用にコピーする**

```bash
mkdir -p .agents/skills/pr-independent-review/tests/golden
cp /Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/pr-independent-review/runs/pr-1155-r2/digest.html /tmp/pr1155-current.html
```

- [x] **Step 2: golden md を書き起こす**

`/tmp/pr1155-current.html` の `<main>` を読み、11件の finding（現行の F01〜F11）と5つの注記・判断台帳・折りたたみ参考を、本plan「digest.md フォーマット仕様」に従って `tests/golden/pr-1155-digest.md` へ書き起こす。**写経であって創作ではない**（本文・コード抜粋・行番号は現行HTMLからそのまま移す）。

- `slug` は内容から付ける（例: F03=`gear-torque-rate` / F08=`power-rate-visibility` / F04=`effective-rate-dedup` / F02=`change-selection-latch` / F01=`unused-public-rate` / F06=`qa-capture-timeout` / F07=`initial-tab-stuck` / F05=`mutation-survives` / F09=`multiplier-snapshot` / F10=`arrow-token-name` / F11=`arrow-duplication`）
- 現行HTMLの `<a href="#f03">` 等の相互参照は `[F:gear-torque-rate]` 形式へ置き換える
- `must_read: true` を付けるのは現行の `#must-read` にある3件（F03/F08/F04）
- 現行で推奨が案Bだった F09・F11 は、**推奨案を `options` の先頭へ移す**（これは意図した差分）

- [x] **Step 3: 再生成して生成後検査を通す**

```bash
mkdir -p /tmp/goldenrun && cp .agents/skills/pr-independent-review/tests/golden/pr-1155-digest.md /tmp/goldenrun/digest.md
python3 .agents/skills/pr-independent-review/scripts/digest_build.py /tmp/goldenrun
```
Expected: `generated: /tmp/goldenrun/digest.html / /tmp/goldenrun/findings.json`。非0終了なら digest.md を直す（**コンバータを緩めて通すのは禁止**）

- [x] **Step 4: findings.json が現行と同値であることを確認する**

```bash
python3 - <<'PY'
import json
a = json.load(open('/tmp/goldenrun/findings.json'))
b = json.load(open('/Users/sakastudio/hermes-agent/data/repos/moorestech_logs/harness/pr-independent-review/runs/pr-1155-r2/findings.json'))
ka = {f['id']: (f['severity'], f['category'], f['files'], [o['summary'] for o in f['options']]) for f in a['findings']}
kb = {f['id']: (f['severity'], f['category'], f['files'], [o['summary'] for o in f['options']]) for f in b['findings']}
print('同一' if ka == kb else '差分あり')
for k in sorted(set(ka) | set(kb)):
    if ka.get(k) != kb.get(k):
        print(k, '\n  new:', ka.get(k), '\n  old:', kb.get(k))
PY
```
Expected: 差分が出るのは F09・F11 の案の並び替えだけ（推奨を先頭へ移したため）。それ以外の差分が出たら golden md の写経ミスなので直す

- [x] **Step 5: スクリーンショットで見た目を突き合わせる**

```bash
cat > /Users/sakastudio/hermes-agent/data/repos/moorestech/moorestech_web/webui/shot.mjs <<'JS'
import { chromium } from 'playwright';
const b = await chromium.launch();
for (const [name, file] of [["current", "/tmp/pr1155-current.html"], ["rebuilt", "/tmp/goldenrun/digest.html"]]) {
  const p = await b.newPage({ viewport: { width: 1000, height: 1400 } });
  await p.goto("file://" + file);
  await p.screenshot({ path: `/tmp/digest-${name}.png`, fullPage: true });
  await p.close();
}
await b.close();
JS
cd /Users/sakastudio/hermes-agent/data/repos/moorestech/moorestech_web/webui && node shot.mjs && rm -f shot.mjs
```
Expected: `/tmp/digest-current.png` と `/tmp/digest-rebuilt.png` が生成される。**両方をReadツールで開いて目視で突き合わせる**。差分が「案の並び替え」「インデックスの文言」以外に無いことを確認する。余白・フォント・枠線・バッジ・コード抜粋の見え方が変わっていたらレンダラを直す（**テンプレのCSSは変えない**）

- [x] **Step 6: 期待HTMLをゴールデンとして固定する**

```bash
cp /tmp/goldenrun/digest.html .agents/skills/pr-independent-review/tests/golden/pr-1155-digest.expected.html
```

```python
# .agents/skills/pr-independent-review/tests/test_digest_golden.py
# goldenのmdから生成したHTMLが固定版と一致することを検証する（見た目の回帰検知）
# Verify the HTML built from the golden md matches the frozen copy (visual regression guard)
import subprocess
import sys
from pathlib import Path

GOLDEN = Path(__file__).resolve().parent / "golden"
SCRIPT = Path(__file__).resolve().parent.parent / "scripts" / "digest_build.py"


def test_golden_html_is_reproduced(tmp_path):
    (tmp_path / "digest.md").write_text((GOLDEN / "pr-1155-digest.md").read_text(encoding="utf-8"), encoding="utf-8")
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode == 0, r.stderr
    got = (tmp_path / "digest.html").read_text(encoding="utf-8")
    want = (GOLDEN / "pr-1155-digest.expected.html").read_text(encoding="utf-8")
    assert got == want
```

- [x] **Step 7: 全テストを実行して通ることを確認する**

Run: `~/hermes-agent/venv/bin/pytest .agents/skills/pr-independent-review/tests/ -v`
Expected: PASS（全件。`test_cli_writes_html_and_findings` も golden ができたことで通る）

- [x] **Step 8: 裁定サイトで実際にボタンが正しい位置に出ることを確認する**

```bash
mkdir -p /tmp/adjcheck/pr-9999 && cp /tmp/goldenrun/digest.html /tmp/goldenrun/findings.json /tmp/adjcheck/pr-9999/
PR_REVIEW_PORT=8941 PR_REVIEW_DATA_ROOT=/tmp/adjcheck python3 /Users/sakastudio/hermes-agent/data/services/pr-review/site/app.py &
sleep 2 && curl -s http://127.0.0.1:8941/pr/9999 | grep -c 'data-finding-id'
```
Expected: 11以上（カード分）。ブラウザまたはplaywrightで開き、**11件すべての案ボタンが各カードの直下に出る**ことを確認する（今回の不具合の再発検知）。確認後 `kill %1`

- [x] **Step 9: コミットする**

```bash
git add .agents/skills/pr-independent-review/tests/golden .agents/skills/pr-independent-review/tests/test_digest_golden.py
git commit -m "test(digest): PR#1155をゴールデンに据えて見た目の回帰を固定する"
```

---

### Task 7: SKILL.md の Step 7 / 7.5 改訂

**Files:**
- Modify: `.agents/skills/pr-independent-review/SKILL.md`（Step 7 全体・約 L358-451）
- Modify: `.agents/skills/pr-independent-review/SKILL.md`（Step 7.5 全体・約 L452-520）
- Modify: `.agents/skills/pr-independent-review/SKILL.md`（成果物一覧・約 L49 に `digest.md` を追加）
- Create: `.agents/skills/pr-independent-review/README-digest-format.md`

**Interfaces:**
- Consumes: Task 5 の CLI（`python3 <スキル>/scripts/digest_build.py <$RUNDIR>`）
- Produces: なし（文書のみ）

- [x] **Step 1: フォーマット仕様書を切り出す**

`.agents/skills/pr-independent-review/README-digest-format.md` を新規作成し、本planの「digest.md フォーマット仕様（全タスク共通の契約）」節を丸ごと転記する（生成subagentの参照先を1つにするため）。

- [x] **Step 2: Step 7 を差し替える**

見出しを `## Step 7: ダイジェスト生成（digest.md → コンバータ）` に変え、本文を次の内容にする:

- sonnet subagent に `<$RUNDIRの実値>/digest.md` を**Markdownで**生成させる。フォーマットの正本は `$CANON/.claude/skills/pr-independent-review/README-digest-format.md` を読ませる
- 生成後に `python3 $CANON/.claude/skills/pr-independent-review/scripts/digest_build.py <$RUNDIRの実値>` を実行する。非0終了なら **digest.md を直して再実行**する（HTMLを手で直すのは禁止）
- 成功したら `open <$RUNDIRの実値>/digest.html`
- **残す規約**: カードのトリアージ基準（`must_read: true` を付ける条件 (a)指摘系統の一致数が多い (b)裁定がCriticalの直し方を左右する (c)ゲームプレイ・アーキテクチャの方向を変える）／一言サマリの書式（欠陥・裁定対象そのものを主語にした短文1つ・目安20字前後・メタ情報禁止）／コード抜粋は全カード必須でpatchから機械転記／折りたたみ参考に必ず入れる5項目／推奨案は `options` の先頭に書く
- **削除する規約**（すべてコンバータの責務へ移った）: HTMLエスケープ契約 / `<h1>` は1個 / 絵文字不使用 / プレースホルダ置換 / `data-verdict` の手設定 / `data-finding-id` の付与先 / テンプレ冒頭コメントの削除 / STORAGE_KEY・COPY_TITLE の置換 / カード間の視覚分離指示 / 生成後検査4点 / 並び順とセクション構成の規定
- **保存**: `digest.md` / `digest.html` / `findings.json` はいずれも `$RUNDIR` 直下。`/tmp` へ書かない

- [x] **Step 3: Step 7.5 を縮約する**

見出しを `## Step 7.5: findings.json（コンバータ出力の確認）` に変え、本文を次の内容にする:

- `findings.json` は Step 7 のコンバータが生成する。**手で書かない・手で直さない**
- スキーマ表は「コンバータ出力の読み方」として残す（裁定サイトと `pr-adjudicated-apply` の入力契約であるため）
- `recommended` は `options` の先頭に必ず付く。**推奨したい案を digest.md の `options` 先頭に書く**のが唯一の指定方法。`recommended` を digest.md に書くとコンバータがエラーで落ちる
- 旧「recommended検査」の python ワンライナーは削除する（`digest_build.py` の生成後検査が同じ検査を内蔵している）
- id採番規則の記述は「コンバータが severity降順→ファイルパス昇順→行番号昇順で振る。digest.md には `F01` と書かず、参照は `[F:slug]` で書く」へ置き換える

- [x] **Step 4: 整合を確認する**

```bash
sed -n '/^## Step 7:/,/^## Step 8:/p' .agents/skills/pr-independent-review/SKILL.md | grep -n "エスケープ\|data-finding-id\|生成後検査4点\|{{TITLE}}\|絵文字"
```
Expected: 出力が空（Step 7〜7.5 の範囲にこれらの記述が残っていない）

- [x] **Step 5: コミットする**

```bash
git add .agents/skills/pr-independent-review/SKILL.md .agents/skills/pr-independent-review/README-digest-format.md
git commit -m "docs(pr-independent-review): digest生成をMarkdown正本＋コンバータへ差し替える"
```

---

### Task 8: 裁定サイトの推奨フォールバック削除

**Files:**
- Modify: `/Users/sakastudio/hermes-agent/data/services/pr-review/site/inject.py`（`autoPlanFor`・約 L145-160）
- Modify: `/Users/sakastudio/hermes-agent/data/services/pr-review/site/inject.py`（`openCompleteModal`・約 L312-350）
- Modify: `/Users/sakastudio/hermes-agent/data/services/pr-review/site/adj_style.py`（`ADJ_CSS` 末尾）

**Interfaces:**
- Consumes: `findings.json` の `options[].recommended`
- Produces: なし（サイト内部）

**注意:** この箱は**git管理外**（`~/hermes-agent/data/services/pr-review` はリポジトリではない）。コミットは発生しないので、差分の記録はこのplanとADRが担う。変更後は `pkill -f "pr-review/site/app.py"` で supervisor に再起動させる（KeepAliveで自動復帰する）。

**既存データへの影響（着手前に把握しておくこと）:** 過去runの `findings.json` のうち `pr-1127-r2`（42件中30件）・`pr-1137-r2`（16件中8件）・`pr-1138`（6件中6件）・`pr-1140-r2`（43件中38件）は `recommended` 欠落findingを含む。うち 1127/1137/1140 は裁定完了済み（`completed: true`）で影響なし。`pr-1138` は未裁定のまま残っており、この変更後は「完了」による一括採用が拒否される（明示クリックでの裁定は従来どおり可能）。**これは意図した挙動**である。

- [x] **Step 1: `autoPlanFor` からフォールバックを外す**

`inject.py` の `ADJ_SCRIPT` 内、`autoPlanFor` を次へ差し替える:

```javascript
  // 完了時に無裁定へ充てる推奨案。recommendedフラグだけを正とし、無ければ採用しない
  // The recommended option used at completion: only the flag counts; absence blocks adoption
  function autoPlanFor(f){
    const opts = f.options || [];
    const flagged = opts.find(o => o.recommended === true);
    if (!flagged) return null;
    return { key: flagged.key, label: flagged.summary || `案${flagged.key}` };
  }
```

- [x] **Step 2: モーダルで欠落を拒否する**

`openCompleteModal` の集計ループを次へ差し替える:

```javascript
    const auto = [], blocked = [], missing = [];
    for (const f of state.findings){
      if (isDecided(f)) continue;
      // 「それ以外」を選んだ指摘は意図が本人にしか無いので推奨で上書きしない
      // A finding set to "other" holds intent only the author knows; never overwrite it
      if (decisionOf(f) === "other"){ blocked.push(f); continue; }
      const plan = autoPlanFor(f);
      // 推奨が無い指摘を先頭案で埋めない。人が明示的に選ぶまで完了させない
      // Never fill a finding that has no recommendation; completion waits for an explicit choice
      if (!plan){ missing.push(f); continue; }
      auto.push({ f, plan });
    }
```

- [x] **Step 3: 欠落の警告を出し、完了ボタンを止める**

`openCompleteModal` のリスト生成部から次の1行を**削除**する（`source` は廃止したため）:

```javascript
        if (plan.source === "fallback") li.appendChild(el("em", "推奨マーク無し→先頭案"));
```

`<ul class="adj-modal-list">` を `modal` へ足した直後に次を挿入する:

```javascript
    if (missing.length){
      const warn = document.createElement("p");
      warn.className = "adj-modal-error";
      warn.textContent = "推奨案が無い指摘が " + missing.length + " 件あります（"
        + missing.map(f => f.id).join("・")
        + "）。これらは明示的に裁定してから完了してください。";
      modal.appendChild(warn);
    }
```

モーダルの実行ボタン（完了を確定する `button`）を生成している箇所の直後に、次の1行を足す:

```javascript
    confirmBtn.disabled = missing.length > 0;
```

（実際の変数名は `openCompleteModal` 内の該当ボタン変数に合わせる。`grep -n "adj-modal" inject.py` で確認する）

- [x] **Step 4: 警告のCSSを足す**

`adj_style.py` の `ADJ_CSS` 末尾へ追記する:

```css
.adj-modal-error{color:#DC2626;font-size:13px;font-weight:600;margin:8px 0 0;}
```

- [x] **Step 5: 再起動して両系統を確認する**

```bash
pkill -f "pr-review/site/app.py"; sleep 8
curl -s -o /dev/null -w "1155:%{http_code}\n" http://127.0.0.1:8931/pr/1155
curl -s -o /dev/null -w "1138:%{http_code}\n" http://127.0.0.1:8931/pr/1138
```
Expected: どちらも `200`。ブラウザで `http://127.0.0.1:8931/pr/1138`（recommended 全欠落）を開いて完了ボタンを押すと「推奨案が無い指摘が 6 件あります」の警告が出て確定できないこと、`http://127.0.0.1:8931/pr/1154`（recommended 完備・裁定途中）では従来どおり完了モーダルが機能することを確認する

---

### Task 9: 最終ブランチレビュー（省略不可）

**Files:**
- レビュー対象: 本ブランチの master からの全差分

- [x] **Step 1: moores-code-review スキルで全ブランチレビューを実行する**

`moores-code-review` スキルを起動し、`feature/digest-markdown-converter` の master からの全差分をレビューする。**ゴール文言による省略は不可**。

- [x] **Step 2: 指摘へ対応し、再度テストを通す**

Run: `~/hermes-agent/venv/bin/pytest .agents/skills/pr-independent-review/tests/ -v`
Expected: PASS（全件）

- [x] **Step 3: コミットする**

```bash
git add -A
git commit -m "fix(digest): レビュー指摘へ対応する"
```

---

## 機能パリティ死活表（同じ機構にぶら下がる操作の生死）

digest.html というひとつの成果物にぶら下がる操作を全部並べ、この計画の後も生きるかを1行の根拠つきで示す。

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 本文選択コメント・図コメント・左下パネル・すべてコピー | 生きる | テンプレのコメント機能JSとDOMを1文字も変えず、`<main>` の中身だけ差し替えるため（Task 4）。`<script>` が1個であることを生成後検査が担保する（Task 5） |
| localStorage のコメント永続化 | 生きる | `STORAGE_KEY` の置換はコンバータが行い、PR番号ごとに固有値になる（Task 4）。テストで確認する |
| 裁定サイトの案ボタン注入 | 生きる（改善） | `data-finding-id` がカード要素に付くようになり、章末へまとまって落ちる不具合が消える。Task 6 Step 8 で実機確認する |
| 裁定サイトの「完了」一括採用 | 条件付きで生きる | `recommended` を持つ findings では従来どおり。欠落データ（過去4run）では拒否される。**ユーザー裁定済みの意図した挙動** |
| 明示クリックによる裁定・途中保存 | 生きる | Task 8 は `autoPlanFor` と完了モーダルのみを触り、ボタン・保存経路には触れない |
| 過去runのdigestをブラウザで開く | 生きる | 再生成しない。裁定サイトの `findings.json` 読み取りとボタン注入の両対応（`closest("section.verdict-card") || byId`）は残す |
| `pr-adjudicated-apply` の findings.json 読み取り | 生きる | スキーマを変えない（キー・型・`recommended` の意味とも同じ）。Task 6 Step 4 で現行との同値を確認する |
| poller のフェーズ遷移（findings.json 検出で裁定待ちへ） | 生きる | 出力先ファイル名・場所を変えない（`$RUNDIR/findings.json`） |

死ぬ操作・退化する操作は「完了ボタンによる一括採用（recommended欠落データに限る）」のみで、これは本計画の目的そのものであり、2026-08-18のユーザー裁定で確定済み。

## 判断記録（ADR）

設計セッションのADR: [docs/adr/0015-review-digest-from-markdown-via-deterministic-converter.md](../../adr/0015-review-digest-from-markdown-via-deterministic-converter.md)

裁定の蒸留:
- `.decisions/2026-08-18-レビューダイジェストはMarkdown正本から決定論生成する.md`
- `.decisions/2026-08-18-裁定サイトの推奨案は欠落を構造的に不可能にする.md`
- `.decisions/2026-08-18-digestコンバータは標準ライブラリのみの限定サブセットで書く.md`
- `.decisions/2026-08-18-digestの見た目はPR1155をゴールデンに据えて担保する.md`

planning中に新たに生じた判断:

- **id採番はコンバータが行い、本文の相互参照は `[F:slug]` で書く**（出所: agent前提）。ADRは「id採番はコンバータの責務」までしか決めていなかったが、goldenの本文には `F03` 等への相互参照が多数あり、AIが採番を知らずに参照を書く手段が必要だった。slug参照はid採番ズレという不具合クラスを本文側からも消す。**前例のない新規パターンなのでレビュー注目点**
- **`code-card` フェンスの行記法 `[フラグ]<行番号>|<コード>`**（出所: agent前提）。行番号・追加行・問題行の3情報を1行で表し、コードを生のまま書かせてエスケープをコンバータへ寄せるための最小の記法。**前例のない新規パターンなのでレビュー注目点**
- **HTMLの外枠はテンプレをシェルとして再利用し `<main>` だけ差し替える**（出所: agent前提。ユーザー指示「今までと見た目はほぼ変わらないことを強く重視」から導出）。CSS・コメント機能JSを一切書き換えないことで、見た目の同一性をレンダラの正しさに依存させない
- **`recommendation` フィールドは残す**（出所: agent前提）。`pr-adjudicated-apply` が読む契約で、`options` 先頭とは別に自由文の推奨対応を持てるため。省略時は先頭optionの文言で埋める
- **裁定サイトの変更はgit管理外の箱で行うためコミットが残らない**（出所: agent前提）。差分の記録はADRとこのplanが担う
- **`pr-1138`（未裁定・recommended全欠落）は Task 8 後に一括採用が拒否される**（出所: agent前提）。ユーザー裁定「欠けを構造的に不可能にする」の直接の帰結であり、明示クリックでの裁定は可能なので閉塞しない

## Execution Handoff

planが完成し `docs/superpowers/plans/2026-08-18-digest-markdown-converter.md` に保存されました。新規セッションを開き、以下を貼り付けて実装を開始してください:

```
subagent-driven-development スキルを使って、以下の実装planを実行してください。

- plan: docs/superpowers/plans/2026-08-18-digest-markdown-converter.md
- 作業場所: feature/digest-markdown-converter（worktree: /Users/sakastudio/hermes-agent/data/repos/moorestech-worktrees/digest-md）
- まずplan全文を読み、`## Requirements`・`## Global Constraints`・`## 判断記録（ADR）`を全タスク共通の制約として扱ってください
- 進捗はplanのチェックボックス更新で管理してください
- planの最終タスク（moores-code-review による全ブランチレビュー）は省略不可です
```
