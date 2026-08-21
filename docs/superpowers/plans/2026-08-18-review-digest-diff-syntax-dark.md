# 裁定サイトのコードカード差分表示・構文着色・ダークモード Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: subagent-driven-development スキルを使い、このplanをタスクごとに実装すること。ステップはチェックボックス（`- [ ]`）記法で進捗管理する。

**Goal:** `pr-independent-review` が出す `digest.html` のコードカードを「行番号＋`+`/`-`グリフ＋構文着色」のunified diff表示にし、裁定サイト全体をOS追従のダークモードへ対応させる。

**Architecture:** `digest.md` の `code-card` 記法に削除行 `-<行番号>|<コード>` を新設し、行の描画を `ln`/`sign`/`code` の3spanへ分解する。構文着色は同梱した highlight.js が `span.code` 単位でブラウザ側に当てる。削除行の書き忘れは、コンバータが同ディレクトリの `patch.diff` と照合してビルドを落とすことで防ぐ。ダークモードは CSS 変数のトークン化＋`prefers-color-scheme` で、digestテンプレート・裁定レイヤー・一覧ページの3箇所に入れる。

**Tech Stack:** Python 3（標準ライブラリのみ・pytest でテスト）／highlight.js 11.11.1（ミニファイ済みをvendoringしてインライン同梱）／素のCSS・素のJS（ビルド無し・`file://` で動作）

## Requirements

設計ADR: `docs/adr/0018-review-digest-code-card-diff-syntax-dark.md`

- R1: `code-card` に削除行 `-<行番号>|<コード>` を追加し、削除行は旧ファイルの行番号・赤系背景で描かれる。受け入れ: `-38|foo` を含む digest.md をビルドし、HTMLに `class="cl del"` の行が出て背景が赤系になる
- R2: 追加/削除/文脈の区別は背景色だけでなくグリフ列（`+` / `-` / 空白）でも示される。受け入れ: HTMLの各行に `<span class="sign">` があり、追加行は `+`、削除行は `-`、文脈行は空白1文字
- R3: `+` 行を含む code-card が、`patch.diff` 上で削除を伴うhunkに対応するのに `-` 行を書いていない場合、`digest_build.py` は非0で終了しエラー文言に finding id を含む。受け入れ: 専用のpytestで失敗ケースが `returncode != 0` になる
- R4: `patch.diff` が `RUNDIR` に無い場合、`digest_build.py` は非0で終了する。受け入れ: patch.diff無しのtmp_pathで実行し `returncode != 0`
- R5: コードカードは finding の `files` 先頭の拡張子から言語を決めて構文着色される（`cs`→csharp、`ts`/`tsx`→typescript、`css`→css、`json`→json、`yml`/`yaml`→yaml、`md`→markdown、未知拡張子は無着色）。受け入れ: `.cs` のカードに `data-lang="csharp"` が付き、未知拡張子のカードには `data-lang` 属性が無い
- R6: highlight.js は digest.html にインライン同梱され、外部ネットワークを一切参照しない。受け入れ: 生成された digest.html に `https://` で始まる script/link 参照が無く、`<script id="hljs-bundle">` の中身が10万文字以上ある
- R7: 構文着色は行ごとに適用され、行番号ガター・グリフ・注目行マークを壊さない。受け入れ: 着色後も `span.ln` / `span.sign` の個数が行数と一致する（JS実行後のDOM検査はブラウザ手動確認、静的には生成HTMLの構造で担保）
- R8: 注目行（`*`）は背景色ではなく左縁バーと文字の強調で示す。受け入れ: `.cl.hl` のCSSに `box-shadow: inset` の左バーがあり、`background` 指定を持たない
- R9: `findings.json` の `excerpt` に削除行は含まれない。受け入れ: `-` 行を含む digest.md をビルドし、`excerpt` に削除行のコードが現れない
- R10: digest.html・裁定レイヤー・一覧ページの3つすべてが `prefers-color-scheme: dark` でダーク表示になる。受け入れ: 3ファイルすべてに `@media (prefers-color-scheme: dark)` ブロックがあり、生の16進が `:root` 系トークン定義の外に残っていない（テンプレートについては後述の閾値検査）
- R11: ダークモードの切替UIは追加しない。受け入れ: テンプレートに切替ボタンの要素・localStorageキーが増えていない
- R12: `README-digest-format.md` と `SKILL.md` が新記法（`-` 行・言語自動判定・patch.diff必須）を反映している。受け入れ: 両ファイルに `-<行番号>|` の説明と patch.diff 照合の記述がある
- R14: 各カードの本文へ `options:` が案A/案B…の一覧として描かれ、先頭に推奨マークが付く。受け入れ: 非suppressedカードの数だけ `opt-recommended` が生成HTMLに出る
- R15: 案の正本は `options:` 一本で、本文の手書き代替案と `recommendation:` はコンバータが拒否する。受け入れ: 本文に `代替案` を含む digest.md と `recommendation:` を持つ digest.md が、いずれも非0終了になる
- R13: ゴールデンテスト（pr-1155）が新表示で固定され、削除行を含むケースを1件以上持つ。受け入れ: `tests/golden/pr-1155-digest.md` に `-` 行があり、`test_digest_golden.py` が通る

**やらないこと（スコープ境界）:**
- 既存 run の `digest.html` の再生成（PR #1167 を含む。ADR 背景1のとおり不可避の事故として残す）
- 旧/新2列行番号・before/after 2ブロック表示（ADR 決定1で棄却）
- ダークモードの手動トグルUI（ADR 決定6で棄却）
- `findings.json` スキーマの変更（ADR 決定7）。`recommendation` フィールドは残り、`options` 先頭から自動で埋まる（ADR 決定8）
- `pr-adjudicated-apply` 側の改修

## Global Constraints

- **標準ライブラリのみ**: `scripts/` 配下の Python に pip 依存を足さない（無人headless実行環境にパッケージが無いと黙って死ぬ）。highlight.js はブラウザ側で動く vendored ファイルであり、Python の依存ではない
- **自己完結HTML**: 生成された `digest.html` は `file://` で開いて完全に動作する。CDN・外部フォント・外部画像を参照しない
- **1ファイル200行以下・1ディレクトリ10ファイルまで**（AGENTS.md）。`scripts/digest_md/` は現時点で11ファイルあり上限に達しているため、code-card 関連の新規コードは `scripts/digest_md/code_card/` サブパッケージへ入れる
- **コメントは日英2行セット**（日本語 → English）で、各言語1行に収める（AGENTS.md）
- **try-catch は外部境界のみ**。既存 `digest_build.py` の `except DigestError` は「AI生成Markdownの隔離」という境界コメント付きで存在する。新規に増やさない
- **`partial` 相当の分割禁止・`Func<>` 禁止**は C# の規約でここでは無関係だが、「機構の逃げで設計を曖昧にしない」姿勢は同じく適用する
- **シェルコマンドは断りが無い限りリポジトリルートで実行する**（`cd .agents/...` はルートからの相対）。`~/hermes-agent/...` で始まるパスだけがこのマシンのホーム配下を指す
- highlight.js のバージョンは **11.11.1 固定**。取得元は `https://cdn.jsdelivr.net/npm/@highlightjs/cdn-assets@11.11.1/`
- 作業ブランチは `feat/review-digest-diff-syntax-dark`。**メインワークツリーでのブランチ操作は hook で物理拒否される**ため、`moores-wt new feat/review-digest-diff-syntax-dark --no-editor` で worktree を切って作業する（Unity は不要なので `--no-editor`）
- `~/hermes-agent/data/services/pr-review/` は **git 管理外**（このマシンローカル）。Task 8 の変更はコミットできないため、変更前に `cp` でバックアップを取り、変更内容を plan のチェックボックスとコミットメッセージ本文で追跡する

---

### Task 1: code-card サブパッケージの切り出しと削除行 `-` の追加

**Files:**
- Create: `.agents/skills/pr-independent-review/scripts/digest_md/code_card/__init__.py`
- Create: `.agents/skills/pr-independent-review/scripts/digest_md/code_card/lines.py`
- Modify: `.agents/skills/pr-independent-review/scripts/digest_md/blocks.py`
- Modify: `.agents/skills/pr-independent-review/scripts/digest_md/findings.py:51-64`
- Test: `.agents/skills/pr-independent-review/tests/test_digest_code_card_lines.py`

**Interfaces:**
- Produces: `digest_md.code_card.lines.code_card_lines(body: str) -> list[tuple[str, str, bool, str]]` — 各要素は `(行番号, kind, 注目行か, コード)`。`kind` は `"add"` / `"del"` / `"ctx"` のいずれか
- Produces: `digest_md.code_card.lines.iter_code_cards(body_md: str) -> list[str]` — finding 本文から `code-card` フェンスの中身を出現順に取り出す
- Consumes: `digest_md.errors.DigestError`

- [ ] **Step 1: 失敗するテストを書く**

`.agents/skills/pr-independent-review/tests/test_digest_code_card_lines.py`:

```python
# code-card行の分解（追加・削除・文脈・注目行）とフェンス抽出を検証する
# Verify code-card line parsing (add/del/ctx/highlight) and fence extraction
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.code_card.lines import code_card_lines, iter_code_cards
from digest_md.errors import DigestError


def test_kinds_are_add_del_and_ctx():
    body = " 36|void A()\n+38|added();\n-37|removed();\n*+40|hot();"
    got = code_card_lines(body)
    assert [(n, k, h) for n, k, h, _ in got] == [
        ("36", "ctx", False),
        ("38", "add", False),
        ("37", "del", False),
        ("40", "add", True),
    ]


def test_highlighted_deletion_is_allowed():
    assert code_card_lines("*-12|gone();")[0][:3] == ("12", "del", True)


def test_add_and_del_on_same_line_is_error():
    with pytest.raises(DigestError) as e:
        code_card_lines("+-12|both();")
    assert "+" in str(e.value)


def test_missing_pipe_is_error():
    with pytest.raises(DigestError):
        code_card_lines("36 void A()")


def test_non_numeric_line_number_is_error():
    with pytest.raises(DigestError):
        code_card_lines("+xx|void A()")


def test_iter_code_cards_returns_every_fence_in_order():
    body = "段落\n\n```code-card\n+1|a\n```\n\n本文\n\n```code-card\n 2|b\n```\n"
    assert iter_code_cards(body) == ["+1|a", " 2|b"]


def test_iter_code_cards_ignores_plain_fences():
    body = "```\nplain\n```\n\n```code-card\n+1|a\n```\n"
    assert iter_code_cards(body) == ["+1|a"]
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/test_digest_code_card_lines.py -v`
Expected: FAIL with `ModuleNotFoundError: No module named 'digest_md.code_card'`

- [ ] **Step 3: サブパッケージを作る**

`scripts/digest_md/code_card/__init__.py`:

```python
# code-card（差分つきコード抜粋）の解析・描画・言語判定・patch照合をまとめたサブパッケージ
# Subpackage bundling code-card parsing, rendering, language detection, and patch cross-checking
```

`scripts/digest_md/code_card/lines.py`:

```python
# code-card 1行の記法 [フラグ]<行番号>|<コード> を分解する。全ての読み手はここを経由する
# Parse the code-card line syntax [flags]<lineno>|<code>; every reader goes through this module
from __future__ import annotations

from ..errors import DigestError

# フラグ文字と行種別の対応。* は行種別と直交する注目マーク
# Flag characters mapped to line kinds; "*" is an orthogonal highlight marker
_KIND_FLAGS = {"+": "add", "-": "del"}


def code_card_lines(body: str) -> list[tuple[str, str, bool, str]]:
    # 各行を (行番号, kind, 注目行か, コード) へ分解する。kind は add / del / ctx
    # Split each line into (line number, kind, is-highlight, code); kind is add / del / ctx
    parsed = []
    for raw in body.splitlines():
        if "|" not in raw:
            raise DigestError(f"code-card の行に | がありません: {raw!r}")
        head, code = raw.split("|", 1)
        head = head.strip()
        kinds = [k for flag, k in _KIND_FLAGS.items() if flag in head]
        if len(kinds) > 1:
            raise DigestError(f"code-card の行に + と - を同時に付けられません: {raw!r}")
        hl = "*" in head
        num = head.replace("+", "").replace("-", "").replace("*", "").strip()
        if not num.isdigit():
            raise DigestError(f"code-card の行番号が数字ではありません: {raw!r}")
        parsed.append((num, kinds[0] if kinds else "ctx", hl, code))
    return parsed


def iter_code_cards(body_md: str) -> list[str]:
    # finding本文から code-card フェンスの中身だけを出現順に取り出す
    # Extract the bodies of code-card fences from a finding body, in order of appearance
    cards, lines, i = [], body_md.splitlines(), 0
    while i < len(lines):
        if lines[i].startswith("```code-card"):
            buf = []
            i += 1
            while i < len(lines) and not lines[i].startswith("```"):
                buf.append(lines[i])
                i += 1
            cards.append("\n".join(buf))
        i += 1
    return cards
```

- [ ] **Step 4: blocks.py を新モジュールへ委譲する**

`scripts/digest_md/blocks.py` の先頭 import と `code_card_lines` 定義を置き換える。定義本体（19〜32行目の関数）を削除し、import を次にする:

```python
from .code_card.lines import code_card_lines
from .inline import escape, inline_html
from .parse import DigestError
from .sectioning import read_fence
```

`code_card_html` は `ins` フラグを使っていたので、この時点では kind から復元して従来の出力を保つ（描画の変更は Task 2）:

```python
def code_card_html(body: str, indent: str) -> str:
    # 各行は [フラグ]<行番号>|<コード>。+ は追加行、* は問題行
    # Each line is [flags]<lineno>|<code>; "+" marks an insertion, "*" marks the offending line
    rendered = []
    for num, kind, hl, code in code_card_lines(body):
        inner = escape(code)
        inner = f"<ins>{inner}</ins>" if kind == "add" else inner
        inner = f"<del>{inner}</del>" if kind == "del" else inner
        line = f'<span class="ln">{num}</span>{inner}'
        rendered.append(f'<span class="hl">{line}</span>' if hl else line)
    return f'{indent}<pre class="code-card"><code>' + "\n".join(rendered) + "</code></pre>"
```

- [ ] **Step 5: findings.py の excerpt を新モジュール経由にし、削除行を除外する（R9）**

`scripts/digest_md/findings.py` の import 行 `from .blocks import code_card_lines` を次へ差し替える:

```python
from .code_card.lines import code_card_lines, iter_code_cards
```

`_excerpt` を次へ置き換える:

```python
def _excerpt(body_md: str) -> str:
    # 最初のcode-cardをPR後の現行コードとして抜き出す（削除行はpr-adjudicated-applyの誤読を招くので落とす）
    # Take the first code-card as the post-PR code; deletions are dropped so pr-adjudicated-apply never misreads them
    # HTMLエスケープはしない契約。行の読み方は code_card_lines と共有する
    # No HTML escaping by contract; line parsing is shared with code_card_lines
    cards = iter_code_cards(body_md)
    if not cards:
        return ""
    return "\n".join(code for _, kind, _, code in code_card_lines(cards[0]) if kind != "del")
```

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/ -v`
Expected: `test_digest_code_card_lines.py` の7件が PASS。既存テストもすべて PASS（`code_card_html` の出力は従来どおりのため golden も一致する）

- [ ] **Step 7: 削除行が excerpt に出ないことをテストで固定する**

`.agents/skills/pr-independent-review/tests/test_digest_findings.py` の末尾に追記:

```python
def test_excerpt_drops_deleted_lines():
    # R9: excerptはPR後の現行コードだけを持つ（pr-adjudicated-applyの入力契約）
    # R9: the excerpt carries only post-PR code, which is pr-adjudicated-apply's input contract
    from digest_md.findings import _excerpt
    body = "```code-card\n-37|old();\n+38|new();\n```"
    assert _excerpt(body) == "new();"
```

- [ ] **Step 8: テストを実行して通ることを確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/test_digest_findings.py -v`
Expected: PASS

- [ ] **Step 9: コミットする**

```bash
cd "$(git rev-parse --show-toplevel)"
git add .agents/skills/pr-independent-review/scripts/digest_md/code_card .agents/skills/pr-independent-review/scripts/digest_md/blocks.py .agents/skills/pr-independent-review/scripts/digest_md/findings.py .agents/skills/pr-independent-review/tests/
git commit -m "feat(digest): code-cardに削除行を足しexcerptから除外する"
```

---

### Task 2: 行描画を ln/sign/code の3spanへ変え、注目行を左縁バーにする

**Files:**
- Create: `.agents/skills/pr-independent-review/scripts/digest_md/code_card/html.py`
- Modify: `.agents/skills/pr-independent-review/scripts/digest_md/blocks.py`
- Modify: `.agents/skills/pr-independent-review/assets/digest-template.html:461-465`
- Modify: `.agents/skills/pr-independent-review/tests/test_digest_blocks.py:29-36`
- Test: `.agents/skills/pr-independent-review/tests/test_digest_code_card_html.py`

**Interfaces:**
- Consumes: `digest_md.code_card.lines.code_card_lines`
- Produces: `digest_md.code_card.html.code_card_html(body: str, indent: str, lang: str) -> str` — `lang` が空文字なら `data-lang` 属性を出さない

- [ ] **Step 1: 失敗するテストを書く**

`.agents/skills/pr-independent-review/tests/test_digest_code_card_html.py`:

```python
# code-cardのHTML描画（3span構造・グリフ・注目行・data-lang）を検証する
# Verify code-card HTML rendering: the 3-span structure, signs, highlight, and data-lang
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.code_card.html import code_card_html


def test_context_line_has_blank_sign():
    got = code_card_html(" 36|void A()", "  ", "csharp")
    assert ('<span class="cl ctx"><span class="ln">36</span>'
            '<span class="sign"> </span><span class="code">void A()</span></span>') in got


def test_added_and_deleted_lines_carry_their_sign():
    got = code_card_html("+38|added();\n-37|removed();", "  ", "csharp")
    assert '<span class="cl add"><span class="ln">38</span><span class="sign">+</span>' in got
    assert '<span class="cl del"><span class="ln">37</span><span class="sign">-</span>' in got


def test_highlight_is_a_class_not_a_wrapper():
    got = code_card_html("*+40|hot();", "  ", "csharp")
    assert '<span class="cl add hl">' in got
    assert "<ins>" not in got


def test_code_is_escaped():
    got = code_card_html("+1|B<int>(a && b);", "  ", "csharp")
    assert '<span class="code">B&lt;int&gt;(a &amp;&amp; b);</span>' in got


def test_lang_becomes_data_attribute():
    assert '<pre class="code-card" data-lang="csharp">' in code_card_html(" 1|x", "  ", "csharp")


def test_empty_lang_omits_data_attribute():
    assert '<pre class="code-card">' in code_card_html(" 1|x", "  ", "")
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/test_digest_code_card_html.py -v`
Expected: FAIL with `ModuleNotFoundError: No module named 'digest_md.code_card.html'`

- [ ] **Step 3: 描画モジュールを実装する**

`scripts/digest_md/code_card/html.py`:

```python
# code-cardを行番号・差分グリフ・コードの3spanで描く。構文着色はこの構造の上にJSが当てる
# Render a code-card as three spans per line (gutter, diff sign, code); the JS highlighter sits on top
from __future__ import annotations

from ..inline import escape
from .lines import code_card_lines

# 行種別ごとのグリフ。色が潰れても記号で差分が読めるようにする
# Glyph per line kind, so the diff stays readable even when colors wash out
SIGN = {"add": "+", "del": "-", "ctx": " "}


def code_card_html(body: str, indent: str, lang: str) -> str:
    # 各行は [フラグ]<行番号>|<コード>。+ は追加行、- は削除行、* は注目行
    # Each line is [flags]<lineno>|<code>; "+" adds, "-" deletes, "*" marks the offending line
    rendered = []
    for num, kind, hl, code in code_card_lines(body):
        classes = f"cl {kind} hl" if hl else f"cl {kind}"
        rendered.append(f'<span class="{classes}"><span class="ln">{num}</span>'
                        f'<span class="sign">{SIGN[kind]}</span>'
                        f'<span class="code">{escape(code)}</span></span>')
    attr = f' data-lang="{escape(lang)}"' if lang else ""
    return f'{indent}<pre class="code-card"{attr}><code>' + "\n".join(rendered) + "</code></pre>"
```

- [ ] **Step 4: blocks.py から旧 code_card_html を削除し、lang を受け取る**

`scripts/digest_md/blocks.py` の `code_card_html` 定義を削除し、import を次にする:

```python
from .code_card.html import code_card_html
from .code_card.lines import code_card_lines
from .inline import escape, inline_html
from .parse import DigestError
from .sectioning import read_fence
```

`blocks_html` のシグネチャへ `lang` を足し、code-card 分岐で渡す:

```python
def blocks_html(md: str, refs: dict, indent: str, lang: str = "") -> str:
```

```python
            if lang_fence == "code-card":
                out.append(code_card_html(body, indent, lang))
```

（既存の分岐変数 `lang` はフェンス種別を指しているため `lang_fence` へ改名し、引数の `lang` と衝突させない。`_KNOWN_FENCES` の判定も `lang_fence` を見るよう合わせる）

- [ ] **Step 5: render.py からカードの言語を渡す（この時点では空文字）**

`scripts/digest_md/render.py` の `_card_html` 内:

```python
    body = blocks_html(f.body_md, refs, "        ", "")
```

（実際の言語判定は Task 4 で入れる。ここでは呼び出し形だけ確定させ、Task 4 の差分を1行に閉じ込める）

- [ ] **Step 6: テンプレートのCSSを新構造へ合わせる**

`assets/digest-template.html` の `.code-card` 関連5行（461〜465行目）を次へ置き換える:

```css
  /* colorは必須: 素のpreが暗背景用の明色文字なので明背景では読めなくなる / color is required: bare pre uses light text for dark bg */
  .code-card { background: #f6f8fa; border: 1px solid #d0d7de; border-radius: 6px; padding: 12px 0; overflow-x: auto; font-size: 13px; line-height: 1.6; color: #24292f; }
  /* min-width:100%で横スクロール時も行の帯が途切れないようにする / min-width keeps the row band intact while scrolling horizontally */
  .code-card code { display: inline-block; min-width: 100%; }
  .code-card .cl { display: block; white-space: pre; padding-left: 12px; }
  .code-card .ln { display: inline-block; width: 3.2em; text-align: right; padding-right: .7em; color: #8b949e; user-select: none; }
  /* 記号列は色が潰れても差分を読ませる最後の砦なので必ず等幅で確保する / the sign column is the last line of defense when colors wash out */
  .code-card .sign { display: inline-block; width: 1.2em; color: #6e7781; user-select: none; }
  .code-card .cl.add { background: #dafbe1; }
  .code-card .cl.add .sign { color: #1a7f37; }
  .code-card .cl.del { background: #ffebe9; }
  .code-card .cl.del .sign { color: #cf222e; }
  /* 注目行は背景を使わない（追加/削除の背景と3層に重なるため）。左縁バーと字の太さで示す / no background here: it would stack on the add/del bands */
  .code-card .cl.hl { box-shadow: inset 4px 0 0 #F59E0B; font-weight: 600; }
```

- [ ] **Step 7: 旧テストを新構造へ書き換える**

`tests/test_digest_blocks.py` の `test_code_card_marks_ins_and_hl` を削除する（描画の検査は `test_digest_code_card_html.py` が持つ）。同ファイル冒頭の `from digest_md.blocks import blocks_html, code_card_html` を `from digest_md.blocks import blocks_html` にする。

- [ ] **Step 8: ゴールデンを再生成する**

```bash
cd .agents/skills/pr-independent-review
python3 - <<'PY'
import shutil, subprocess, sys, tempfile, pathlib
g = pathlib.Path("tests/golden")
d = pathlib.Path(tempfile.mkdtemp())
shutil.copy(g / "pr-1155-digest.md", d / "digest.md")
r = subprocess.run([sys.executable, "scripts/digest_build.py", str(d)], capture_output=True, text=True)
print(r.returncode, r.stdout, r.stderr)
shutil.copy(d / "digest.html", g / "pr-1155-digest.expected.html")
PY
python3 -m pytest tests/ -v
```

Expected: 全テスト PASS

- [ ] **Step 9: 見た目を目視で確認する**

```bash
open .agents/skills/pr-independent-review/tests/golden/pr-1155-digest.expected.html
```

Expected: コードカードの各行に行番号と `+` 記号が出て、追加行が緑帯・注目行に左のアンバー縦バーが出ている

- [ ] **Step 10: コミットする**

```bash
cd "$(git rev-parse --show-toplevel)"
git add .agents/skills/pr-independent-review/
git commit -m "feat(digest): コードカードを行番号+記号+コードの3span構造にする"
```

---

### Task 3: highlight.js の同梱と行ごと構文着色

**Files:**
- Create: `.agents/skills/pr-independent-review/assets/vendor/highlight.min.js`
- Create: `.agents/skills/pr-independent-review/assets/vendor/github.min.css`
- Create: `.agents/skills/pr-independent-review/assets/vendor/github-dark.min.css`
- Create: `.agents/skills/pr-independent-review/assets/vendor/README.md`
- Modify: `.agents/skills/pr-independent-review/assets/digest-template.html`
- Modify: `.agents/skills/pr-independent-review/scripts/digest_md/render.py`
- Modify: `.agents/skills/pr-independent-review/scripts/digest_build.py:18-53`
- Modify: `.agents/skills/pr-independent-review/tests/test_digest_render.py`
- Modify: `.agents/skills/pr-independent-review/tests/test_digest_golden.py`

**Interfaces:**
- Consumes: `digest_md.render.render_html(doc, template, refs)` の既存シグネチャ
- Produces: `digest_md.render.render_html(doc: Document, template: str, refs: dict, assets: dict) -> str` — `assets` は `{"hljs_js": <JS本文>, "hljs_css_light": <CSS本文>, "hljs_css_dark": <CSS本文>}`

- [ ] **Step 1: vendor ファイルを取得する**

```bash
cd .agents/skills/pr-independent-review/assets
mkdir -p vendor
B=https://cdn.jsdelivr.net/npm/@highlightjs/cdn-assets@11.11.1
curl -sL "$B/highlight.min.js" -o vendor/highlight.min.js
curl -sL "$B/styles/github.min.css" -o vendor/github.min.css
curl -sL "$B/styles/github-dark.min.css" -o vendor/github-dark.min.css
wc -c vendor/*
```

Expected: `highlight.min.js` が 127496 バイト、CSS 2本がそれぞれ約1300バイト

- [ ] **Step 2: vendor の出所を記録する**

`assets/vendor/README.md`:

```markdown
# vendored assets

`digest.html` は `file://` で開ける自己完結HTMLであり、CDNを参照しない（ADR 0018 決定3）。
そのため highlight.js とそのテーマをここへ固定バージョンで置き、ビルド時に digest.html へインライン展開する。

| ファイル | 取得元 |
|---|---|
| `highlight.min.js` | https://cdn.jsdelivr.net/npm/@highlightjs/cdn-assets@11.11.1/highlight.min.js |
| `github.min.css` | https://cdn.jsdelivr.net/npm/@highlightjs/cdn-assets@11.11.1/styles/github.min.css |
| `github-dark.min.css` | https://cdn.jsdelivr.net/npm/@highlightjs/cdn-assets@11.11.1/styles/github-dark.min.css |

バージョンを上げるときは3ファイルを同じバージョンで差し替え、`python3 -m pytest tests/` を通してから
`tests/golden/pr-1155-digest.expected.html` を再生成する。
```

- [ ] **Step 3: 失敗するテストを書く**

`tests/test_digest_render.py` は `TEMPLATE` を module 変数、`_doc()` をヘルパに持ち、`render_html(doc, TEMPLATE, assign_ids(doc))` を5箇所で呼んでいる。まず module 変数を1つ足す（`TEMPLATE` の定義直下）:

```python
# 描画テストでは実バンドルを読まず、置換が起きたことだけを見分けられる短い印を渡す
# Render tests pass short markers instead of the real bundle, so substitution stays visible
ASSETS = {"hljs_js": "HLJS_BODY", "hljs_css_light": "LIGHT_CSS", "hljs_css_dark": "DARK_CSS"}
```

既存の5箇所の呼び出しを `render_html(doc, TEMPLATE, assign_ids(doc), ASSETS)` へ一括で差し替える:

```bash
cd .agents/skills/pr-independent-review
python3 -c "
import pathlib
p = pathlib.Path('tests/test_digest_render.py')
t = p.read_text(encoding='utf-8')
p.write_text(t.replace('render_html(doc, TEMPLATE, assign_ids(doc))', 'render_html(doc, TEMPLATE, assign_ids(doc), ASSETS)'), encoding='utf-8')
"
grep -c "assign_ids(doc), ASSETS" tests/test_digest_render.py
```

Expected: `5`

`test_render_keeps_template_shell_untouched` の script 本数の期待値を2へ直す:

```python
    assert html.count("<script") == 2
```

末尾へ新規テストを2件追記する:

```python
def test_render_inlines_hljs_assets():
    # R6: 生成物は外部参照ゼロで、バンドルとテーマがインライン展開される
    # R6: the output has zero external references; the bundle and themes are inlined
    doc = _doc()
    out = render_html(doc, TEMPLATE, assign_ids(doc), ASSETS)
    assert '<script id="hljs-bundle">HLJS_BODY</script>' in out
    assert "LIGHT_CSS" in out and "DARK_CSS" in out
    assert "{{HLJS_JS}}" not in out
    assert "{{HLJS_CSS_LIGHT}}" not in out and "{{HLJS_CSS_DARK}}" not in out


def test_render_pulls_no_external_asset():
    # R6: script/link/img が外部ホストを指していないこと（本文中のURL引用は対象外）
    # R6: no script/link/img points at an external host (URLs quoted in prose are out of scope)
    doc = _doc()
    out = render_html(doc, TEMPLATE, assign_ids(doc), ASSETS)
    assert not re.search(r'(?:src|href)\s*=\s*"https?://', out)
```

- [ ] **Step 4: テストを実行して失敗を確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/test_digest_render.py -v`
Expected: FAIL（`render_html()` が4引数を受け取らない / トークンが未定義）

- [ ] **Step 5: テンプレートへトークンと着色ドライバを入れる**

`assets/digest-template.html` の `</style>` の直前へ追記:

```css
  /* ---- highlight.js テーマ（ライト） / highlight.js theme (light) ---- */
{{HLJS_CSS_LIGHT}}
  @media (prefers-color-scheme: dark) {
{{HLJS_CSS_DARK}}
  }
  /* テーマ側の .hljs 背景はカードの帯を潰すので無効化する / the theme's .hljs background would hide the diff bands */
  .code-card .hljs, .code-card .code { background: transparent !important; }
```

同ファイルの `<script>` の**直前**へバンドル用の script を置く:

```html
<script id="hljs-bundle">{{HLJS_JS}}</script>
```

既存 `<script>` ブロックの末尾（`})();` の直前）へ着色ドライバを追記:

```js
  // コードカードを行ごとに構文着色する（行番号・差分グリフの構造を壊さないため行単位で当てる）
  // Highlight code cards line by line so the gutter and diff signs stay intact
  function highlightCodeCards() {
    if (!window.hljs) return;
    var cards = document.querySelectorAll('pre.code-card[data-lang]');
    for (var i = 0; i < cards.length; i++) {
      var lang = cards[i].getAttribute('data-lang');
      if (!hljs.getLanguage(lang)) continue;
      var codes = cards[i].querySelectorAll('span.code');
      for (var j = 0; j < codes.length; j++) {
        codes[j].innerHTML = hljs.highlight(codes[j].textContent, { language: lang, ignoreIllegals: true }).value;
      }
    }
  }
  highlightCodeCards();
```

- [ ] **Step 6: render.py で assets を展開する**

`scripts/digest_md/render.py` の `render_html` シグネチャを変え、末尾のトークン検査へ3つを足す:

```python
def render_html(doc: Document, template: str, refs: dict, assets: dict) -> str:
```

```python
    for token in ("{{TITLE}}", "{{DATE}}", "{{SUBTITLE}}", "{{HLJS_JS}}",
                  "{{HLJS_CSS_LIGHT}}", "{{HLJS_CSS_DARK}}",
                  "REPLACE_WITH_UNIQUE_STORAGE_KEY", "REPLACE_WITH_COPY_HEADING"):
        if token not in out:
            raise DigestError(f"テンプレに {token} がありません")
```

置換を追加する（vendor資産は信頼済みのため escape しない。ここが唯一の生HTML注入点である旨をコメントで明示する）:

```python
    # vendor資産は固定バージョンの自前管理物なのでエスケープせず素通しする（唯一の生HTML注入点）
    # Vendored assets are self-managed at a pinned version, so they pass through unescaped (the only raw-HTML injection)
    out = out.replace("{{HLJS_JS}}", assets["hljs_js"])
    out = out.replace("{{HLJS_CSS_LIGHT}}", assets["hljs_css_light"])
    out = out.replace("{{HLJS_CSS_DARK}}", assets["hljs_css_dark"])
```

- [ ] **Step 7: digest_build.py で vendor を読み、script本数の検査を直す**

`scripts/digest_build.py` の TEMPLATE 定義の下へ:

```python
VENDOR = Path(__file__).resolve().parent.parent / "assets" / "vendor"


def load_assets() -> dict:
    # インライン同梱するvendor資産を読む。file://で完結させるための唯一の外部素材
    # Load the vendored assets inlined into the page; the only external material, kept self-contained for file://
    return {"hljs_js": (VENDOR / "highlight.min.js").read_text(encoding="utf-8"),
            "hljs_css_light": (VENDOR / "github.min.css").read_text(encoding="utf-8"),
            "hljs_css_dark": (VENDOR / "github-dark.min.css").read_text(encoding="utf-8")}
```

`verify()` の script 本数検査を次へ差し替える:

```python
    if html.count("<script") != 2:
        problems.append(f"<script> が {html.count('<script')} 個あります（バンドルと本体で2個であるべき）")
    if 'id="hljs-bundle"' not in html:
        problems.append("highlight.js バンドルが埋め込まれていません")
    # 本文が外部URLを引用するのは正当なので、資産を引きに行くタグ属性だけを見る
    # Quoting an external URL in prose is legitimate, so only asset-fetching attributes are checked
    if re.search(r'(?:src|href)\s*=\s*"https?://', html):
        problems.append("外部資産を参照するタグが残っています（file://で完結しなくなる）")
```

`digest_build.py` の import へ `import re` を足す。

`main()` の `render_html` 呼び出しを次へ:

```python
        html = render_html(doc, TEMPLATE.read_text(encoding="utf-8"), refs, load_assets())
```

- [ ] **Step 8: ゴールデン比較でバンドルを正規化する**

`tests/test_digest_golden.py` の先頭へ正規化を追加し、比較箇所を差し替える:

```python
# 127KBのバンドル本体はgoldenへ持たない。存在と規模だけ検査し、比較時は差し替える
# The 127KB bundle never lands in the golden file; presence and size are checked, then it is swapped out
_BUNDLE = re.compile(r'(?s)<script id="hljs-bundle">.*?</script>')


def _normalize(html: str) -> str:
    m = _BUNDLE.search(html)
    assert m, "hljsバンドルが出力にありません"
    assert len(m.group(0)) > 100000, "hljsバンドルが小さすぎます（取得失敗の疑い）"
    return _BUNDLE.sub('<script id="hljs-bundle">[BUNDLE]</script>', html)
```

```python
    got = _normalize((tmp_path / "digest.html").read_text(encoding="utf-8"))
    want = (GOLDEN / "pr-1155-digest.expected.html").read_text(encoding="utf-8")
    assert got == want
```

- [ ] **Step 9: ゴールデンを再生成してテストを通す**

Task 2 Step 8 のスクリプトを実行するが、`shutil.copy` の前に `_normalize` 相当の置換をかける。次のスクリプトを使う:

```bash
cd .agents/skills/pr-independent-review
python3 - <<'PY'
import re, shutil, subprocess, sys, tempfile, pathlib
g = pathlib.Path("tests/golden")
d = pathlib.Path(tempfile.mkdtemp())
shutil.copy(g / "pr-1155-digest.md", d / "digest.md")
r = subprocess.run([sys.executable, "scripts/digest_build.py", str(d)], capture_output=True, text=True)
print(r.returncode, r.stdout, r.stderr)
html = (d / "digest.html").read_text(encoding="utf-8")
html = re.sub(r'(?s)<script id="hljs-bundle">.*?</script>', '<script id="hljs-bundle">[BUNDLE]</script>', html)
(g / "pr-1155-digest.expected.html").write_text(html, encoding="utf-8")
PY
python3 -m pytest tests/ -v
```

Expected: 全テスト PASS

- [ ] **Step 10: コミットする**

```bash
cd "$(git rev-parse --show-toplevel)"
git add .agents/skills/pr-independent-review/
git commit -m "feat(digest): highlight.jsを同梱し行ごとに構文着色する"
```

---

### Task 4: files 先頭の拡張子から言語を決める

**Files:**
- Create: `.agents/skills/pr-independent-review/scripts/digest_md/code_card/lang.py`
- Modify: `.agents/skills/pr-independent-review/scripts/digest_md/render.py`
- Test: `.agents/skills/pr-independent-review/tests/test_digest_code_card_lang.py`

**Interfaces:**
- Produces: `digest_md.code_card.lang.language_of(files: list) -> str` — hljs の言語名。未知拡張子・拡張子なしは空文字

- [ ] **Step 1: 失敗するテストを書く**

`.agents/skills/pr-independent-review/tests/test_digest_code_card_lang.py`:

```python
# files先頭の拡張子からhljs言語名を決める写像を検証する
# Verify the mapping from the first file's extension to an hljs language name
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.code_card.lang import language_of


@pytest.mark.parametrize("path,want", [
    ("moorestech_server/Assets/Scripts/Game.Block/Foo.cs:40", "csharp"),
    ("moorestech_web/webui/src/features/detailLogic.ts:73", "typescript"),
    ("moorestech_web/webui/src/ui/MachineSection.tsx:18", "typescript"),
    ("moorestech_web/webui/src/ui/style.module.css:4", "css"),
    ("moorestech_master/server_v8/mods/blocks.json:1", "json"),
    ("VanillaSchema/blocks.yml:3", "yaml"),
    ("docs/adr/0018-foo.md:1", "markdown"),
    ("moorestech_client/Assets/Foo.asmdef:1", "json"),
    ("scripts/run.sh:2", "bash"),
])
def test_known_extensions(path, want):
    assert language_of([path]) == want


def test_unknown_extension_is_empty():
    assert language_of(["build/output.bin:1"]) == ""


def test_no_extension_is_empty():
    assert language_of(["Makefile:1"]) == ""


def test_first_file_decides():
    assert language_of(["a/b.cs:1", "c/d.ts:2"]) == "csharp"
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/test_digest_code_card_lang.py -v`
Expected: FAIL with `ModuleNotFoundError`

- [ ] **Step 3: 実装する**

`scripts/digest_md/code_card/lang.py`:

```python
# finding が指すファイルの拡張子から構文着色の言語を決める。書き忘れが起きない唯一の情報源
# Decide the highlight language from the finding's file extension: the one source that cannot be forgotten
from __future__ import annotations

# 拡張子 → highlight.js の言語名。未収録の拡張子は無着色にフォールバックする
# Extension to highlight.js language name; unlisted extensions fall back to no highlighting
LANGUAGE_BY_EXTENSION = {
    "cs": "csharp", "ts": "typescript", "tsx": "typescript", "js": "javascript",
    "jsx": "javascript", "css": "css", "scss": "scss", "json": "json",
    "asmdef": "json", "yml": "yaml", "yaml": "yaml", "md": "markdown",
    "html": "xml", "xml": "xml", "py": "python", "sh": "bash",
}


def language_of(files: list) -> str:
    # files先頭の `path:line` からパスを取り、拡張子を言語名へ写す
    # Take the path from the first `path:line` entry and map its extension to a language name
    path = files[0].split(":")[0]
    name = path.rsplit("/", 1)[-1]
    if "." not in name:
        return ""
    return LANGUAGE_BY_EXTENSION.get(name.rsplit(".", 1)[-1].lower(), "")
```

- [ ] **Step 4: render.py から渡す**

`scripts/digest_md/render.py` の import に追加:

```python
from .code_card.lang import language_of
```

`_card_html` の body 生成行を差し替える:

```python
    body = blocks_html(f.body_md, refs, "        ", language_of(f.files))
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/ -v`
Expected: `test_digest_code_card_lang.py` が PASS。ゴールデンは `data-lang` が増えるため FAIL する

- [ ] **Step 6: ゴールデンを再生成して通す**

Task 3 Step 9 のスクリプトを再実行し、`python3 -m pytest tests/ -v` を通す。
Expected: 全テスト PASS。`grep -c 'data-lang="csharp"' tests/golden/pr-1155-digest.expected.html` が1以上

- [ ] **Step 7: 着色をブラウザで確認する**

```bash
open .agents/skills/pr-independent-review/tests/golden/pr-1155-digest.expected.html
```

注意: goldenの期待HTMLはバンドルが `[BUNDLE]` に差し替わっているため着色されない。着色の目視は Task 3 Step 9 のスクリプトが作った一時ディレクトリの `digest.html`（差し替え前）を開いて行う。スクリプト末尾に `print(d)` を足してパスを得る。
Expected: C#のキーワード・型・文字列・コメントが色分けされ、行番号と `+`/`-` 記号が保たれている

- [ ] **Step 8: コミットする**

```bash
cd "$(git rev-parse --show-toplevel)"
git add .agents/skills/pr-independent-review/
git commit -m "feat(digest): カードの言語をfiles先頭の拡張子から決める"
```

---

### Task 5: patch.diff 照合で削除行の欠落を落とす

**Files:**
- Create: `.agents/skills/pr-independent-review/scripts/digest_md/code_card/patch_guard.py`
- Create: `.agents/skills/pr-independent-review/tests/golden/pr-1155-patch.diff`
- Modify: `.agents/skills/pr-independent-review/scripts/digest_build.py`
- Modify: `.agents/skills/pr-independent-review/tests/test_digest_golden.py`
- Test: `.agents/skills/pr-independent-review/tests/test_digest_patch_guard.py`

**Interfaces:**
- Consumes: `digest_md.code_card.lines.code_card_lines` / `iter_code_cards`、`digest_md.models.Document`
- Produces: `digest_md.code_card.patch_guard.missing_deletion_problems(doc: Document, patch_text: str) -> list[str]` — 問題文の配列（空なら合格）

- [ ] **Step 1: 失敗するテストを書く**

`.agents/skills/pr-independent-review/tests/test_digest_patch_guard.py`:

```python
# 置換なのに削除行を書いていないカードを patch.diff との照合で検出する
# Detect cards that describe a replacement without writing the deleted lines, by cross-checking patch.diff
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.code_card.patch_guard import missing_deletion_problems
from digest_md.models import Document, Finding

PATCH_WITH_DELETION = """diff --git a/Foo.cs b/Foo.cs
--- a/Foo.cs
+++ b/Foo.cs
@@ -36,3 +36,3 @@
     void A()
-        Old(1f);
+        New(rate);
"""

PATCH_PURE_ADDITION = """diff --git a/Foo.cs b/Foo.cs
--- a/Foo.cs
+++ b/Foo.cs
@@ -36,2 +36,3 @@
     void A()
+        New(rate);
"""


def _doc(body: str) -> Document:
    f = Finding(slug="s", title="t", category="critical", severity="critical",
                summary="s", files=["Foo.cs:37"], body_md=body)
    f.id = "F01"
    return Document(meta={}, notes={}, ledger_md="", appendix_md="", findings=[f])


def test_replacement_without_deletion_line_is_reported():
    doc = _doc("```code-card\n+37|        New(rate);\n```")
    problems = missing_deletion_problems(doc, PATCH_WITH_DELETION)
    assert len(problems) == 1 and "F01" in problems[0]


def test_replacement_with_deletion_line_passes():
    doc = _doc("```code-card\n-37|        Old(1f);\n+37|        New(rate);\n```")
    assert missing_deletion_problems(doc, PATCH_WITH_DELETION) == []


def test_pure_addition_passes():
    doc = _doc("```code-card\n+37|        New(rate);\n```")
    assert missing_deletion_problems(doc, PATCH_PURE_ADDITION) == []


def test_card_without_added_lines_is_skipped():
    doc = _doc("```code-card\n 36|    void A()\n```")
    assert missing_deletion_problems(doc, PATCH_WITH_DELETION) == []


def test_unmatched_card_is_skipped():
    # patchのどのhunkとも一致しない抜粋は検証しようがないので落とさない（誤検知を作らない）
    # An excerpt matching no hunk cannot be verified, so it is never failed (no false positives)
    doc = _doc("```code-card\n+99|        Unrelated();\n```")
    assert missing_deletion_problems(doc, PATCH_WITH_DELETION) == []
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/test_digest_patch_guard.py -v`
Expected: FAIL with `ModuleNotFoundError: No module named 'digest_md.code_card.patch_guard'`

- [ ] **Step 3: 実装する**

`scripts/digest_md/code_card/patch_guard.py`:

```python
# 「置換なのに変更前が見えない」カードを patch.diff と照合して落とす。規約の明文化だけでは守られないため
# Fail cards that show a replacement without its "before", cross-checked against patch.diff; prose rules alone were not kept
from __future__ import annotations

from ..models import Document
from .lines import code_card_lines, iter_code_cards


def _hunks(patch_text: str) -> list[dict]:
    # patch.diff を hunk 単位で {追加行集合, 削除行の有無} へ畳む
    # Fold patch.diff into per-hunk records of {added line set, whether it deletes}
    hunks: list[dict] = []
    current: dict | None = None
    for line in patch_text.splitlines():
        if line.startswith("@@"):
            current = {"added": set(), "deletes": False}
            hunks.append(current)
            continue
        if current is None or line.startswith(("+++", "---")):
            continue
        if line.startswith("+"):
            body = line[1:].strip()
            if body:
                current["added"].add(body)
        elif line.startswith("-"):
            current["deletes"] = True
    return hunks


def missing_deletion_problems(doc: Document, patch_text: str) -> list[str]:
    # 追加行を持つカードだけを検査し、対応hunkが全て削除を伴うのに - 行が無ければ問題として返す
    # Inspect only cards with added lines; report when every matched hunk deletes but the card has no "-" line
    hunks = _hunks(patch_text)
    problems = []
    for finding in doc.findings:
        for card in iter_code_cards(finding.body_md):
            lines = code_card_lines(card)
            added = {code.strip() for _, kind, _, code in lines if kind == "add" and code.strip()}
            if not added or any(kind == "del" for _, kind, _, _ in lines):
                continue
            matched = [h for h in hunks if added & h["added"]]
            if matched and all(h["deletes"] for h in matched):
                problems.append(
                    f"{finding.id}: 置換を扱うカードに削除行がありません。"
                    f"patch.diff の該当hunkは行を削除しています。`-<旧行番号>|<コード>` を足してください")
    return problems
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/test_digest_patch_guard.py -v`
Expected: 5件すべて PASS

- [ ] **Step 5: digest_build.py へ組み込む（R3・R4）**

import に追加:

```python
from digest_md.code_card.patch_guard import missing_deletion_problems
```

`main()` の `md_path` 検査の直後へ:

```python
    patch_path = rundir / "patch.diff"
    if not patch_path.is_file():
        print(f"patch.diff がありません: {patch_path}", file=sys.stderr)
        return 1
```

`problems = verify(html, findings)` を次へ差し替える:

```python
    problems = missing_deletion_problems(doc, patch_path.read_text(encoding="utf-8", errors="replace"))
    problems += verify(html, findings)
```

- [ ] **Step 6: patch.diff 不在で落ちることをテストする**

`tests/test_digest_build.py` の末尾へ追記（既存テストが digest.md を書く手順を持つので、それを流用する）:

```python
def test_missing_patch_diff_fails(tmp_path):
    # R4: patch.diff は Step 4 が必ず作る。無いまま生成させない
    # R4: patch.diff is always produced by Step 4; never build without it
    (tmp_path / "digest.md").write_text(_golden_md(), encoding="utf-8")
    r = subprocess.run([sys.executable, str(SCRIPT), str(tmp_path)], capture_output=True, text=True)
    assert r.returncode != 0
    assert "patch.diff" in r.stderr
```

`_golden_md()` は `tests/golden/pr-1155-digest.md` を読む小さなヘルパとして同ファイル内に定義する（既存テストが同じ読み込みをしていればそれへ寄せる）。

- [ ] **Step 7: ゴールデン用の patch.diff を作る**

```bash
cd .agents/skills/pr-independent-review
python3 - <<'PY'
import re, pathlib
src = pathlib.Path.home() / "hermes-agent/data/repos/moorestech_logs/harness/pr-independent-review/runs/pr-1155-r2/patch.diff"
want = {"VanillaGearMachineComponent.cs", "detailLogic.ts", "VanillaMachineProcessorComponent.cs",
        "CleanRoomMachineProcessorComponent.cs", "MachineProcessContext.cs", "style.module.css",
        "index.ts", "capture-machine-qa.ts", "MachineSection.tsx", "CleanRoomMachineTest.cs",
        "MachineFluidIOTest.cs"}
chunks = re.split(r'(?m)^(?=diff --git )', src.read_text(encoding="utf-8"))
keep = [c for c in chunks if any(w in c.split("\n", 1)[0] for w in want)]
out = pathlib.Path("tests/golden/pr-1155-patch.diff")
out.write_text("".join(keep), encoding="utf-8")
print(out, out.stat().st_size)
PY
```

Expected: 約40,000バイトのファイルが作られる

- [ ] **Step 8: ゴールデンテストが patch.diff を置くようにする**

`tests/test_digest_golden.py` の `test_golden_html_is_reproduced` の先頭、`digest.md` をコピーしている行の直後へ:

```python
    (tmp_path / "patch.diff").write_text((GOLDEN / "pr-1155-patch.diff").read_text(encoding="utf-8"), encoding="utf-8")
```

- [ ] **Step 9: golden md へ削除行を足す（R13）**

`tests/golden/pr-1155-digest.md` の最初のカード（`slug: gear-torque-rate`）の `code-card` を、`pr-1155-patch.diff` の該当hunkに合わせて削除行つきへ書き換える。実際の旧コードは次で確認する:

```bash
grep -n -B4 -A4 "SetTorqueRequestRate" .agents/skills/pr-independent-review/tests/golden/pr-1155-patch.diff
```

確認した `-` 行を、対応する `+` 行の直前へ `-<旧行番号>|<コード>` の形で挿入する。**旧行番号は hunk ヘッダ `@@ -<旧開始>,<旧行数> +<新開始>,<新行数> @@` の旧開始からの相対位置で数える**。

- [ ] **Step 10: ゴールデンを再生成してテストを通す**

Task 3 Step 9 のスクリプトへ patch.diff のコピーを足して実行する:

```bash
cd .agents/skills/pr-independent-review
python3 - <<'PY'
import re, shutil, subprocess, sys, tempfile, pathlib
g = pathlib.Path("tests/golden")
d = pathlib.Path(tempfile.mkdtemp())
shutil.copy(g / "pr-1155-digest.md", d / "digest.md")
shutil.copy(g / "pr-1155-patch.diff", d / "patch.diff")
r = subprocess.run([sys.executable, "scripts/digest_build.py", str(d)], capture_output=True, text=True)
print(d, r.returncode, r.stdout, r.stderr)
html = (d / "digest.html").read_text(encoding="utf-8")
html = re.sub(r'(?s)<script id="hljs-bundle">.*?</script>', '<script id="hljs-bundle">[BUNDLE]</script>', html)
(g / "pr-1155-digest.expected.html").write_text(html, encoding="utf-8")
PY
python3 -m pytest tests/ -v
```

Expected: 全テスト PASS。生成HTMLに `class="cl del"` が1件以上ある

- [ ] **Step 11: ガードが実データで誤爆しないことを確認する**

```bash
cd .agents/skills/pr-independent-review
python3 - <<'PY'
import pathlib, sys
sys.path.insert(0, "scripts")
from digest_md.code_card.patch_guard import missing_deletion_problems
from digest_md.findings import assign_ids
from digest_md.parse import parse_document
g = pathlib.Path("tests/golden")
doc = parse_document((g / "pr-1155-digest.md").read_text(encoding="utf-8"))
assign_ids(doc)
for p in missing_deletion_problems(doc, (g / "pr-1155-patch.diff").read_text(encoding="utf-8")):
    print(p)
print("done")
PY
```

Expected: `done` のみ（問題ゼロ）。問題が出た場合は該当カードへ `-` 行を足す（それが本ガードの意図した動作である）

- [ ] **Step 12: コミットする**

```bash
cd "$(git rev-parse --show-toplevel)"
git add .agents/skills/pr-independent-review/
git commit -m "feat(digest): 削除行の欠落をpatch.diff照合でビルド失敗にする"
```

---

### Task 6: 案（options）をカード本文へ描き、手書き代替案を禁止する

**Files:**
- Modify: `.agents/skills/pr-independent-review/scripts/digest_md/render.py`
- Modify: `.agents/skills/pr-independent-review/scripts/digest_md/finding_parser.py`
- Modify: `.agents/skills/pr-independent-review/assets/digest-template.html`（`ul.plain` の直下へ案リストのCSS）
- Modify: `.agents/skills/pr-independent-review/tests/golden/pr-1155-digest.md`
- Test: `.agents/skills/pr-independent-review/tests/test_digest_options.py`

**Interfaces:**
- Consumes: `Finding.options: list`（`finding_parser` が既に埋めている）、`digest_md.findings.OPTION_KEYS = "ABCDEF"`
- Produces: `digest_md.render.options_html(f: Finding, indent: str) -> str` — options が空なら空文字。案キーは `OPTION_KEYS` と同じ採番で、先頭に推奨マークが付く

- [ ] **Step 1: 失敗するテストを書く**

`.agents/skills/pr-independent-review/tests/test_digest_options.py`:

```python
# 案（options）がカード本文へ描かれること、手書き代替案が拒否されることを検証する
# Verify options are rendered into the card body and that hand-written alternatives are rejected
import sys
from pathlib import Path

import pytest

sys.path.insert(0, str(Path(__file__).resolve().parent.parent / "scripts"))

from digest_md.errors import DigestError
from digest_md.finding_parser import finding_from
from digest_md.models import Finding
from digest_md.render import options_html

META = ("```yaml\n"
        "slug: s\ncategory: design-decision\nseverity: medium\nmust_read: true\n"
        "summary: 一言\nfiles: [a/B.cs:1]\noptions:\n  - 直す\n  - 戻す\n"
        "```\n")


def _finding(options):
    return Finding(slug="s", title="t", category="design-decision", severity="medium",
                   summary="一言", files=["a/B.cs:1"], body_md="", options=options)


def test_options_render_as_keyed_list():
    got = options_html(_finding(["供給側へ通す", "元へ戻す"]), "        ")
    assert '<p class="options-head"><strong>選べる案</strong></p>' in got
    assert '<ul class="plain options-list">' in got
    assert '<li><strong>案A</strong><span class="opt-recommended">推奨</span> — 供給側へ通す</li>' in got
    assert "<li><strong>案B</strong> — 元へ戻す</li>" in got


def test_options_are_escaped():
    got = options_html(_finding(["Subject<int> を使う"]), "  ")
    assert "Subject&lt;int&gt; を使う" in got


def test_no_options_renders_nothing():
    assert options_html(_finding([]), "  ") == ""


def test_hand_written_alternatives_paragraph_is_rejected():
    # 案の正本は options 一本。本文の代替案段落は二重管理になるので落とす
    # options is the single source for alternatives; a body paragraph would duplicate it
    with pytest.raises(DigestError) as e:
        finding_from("t", META + "\n**代替案:** **案A（推奨）** — 直す")
    assert "代替案" in str(e.value)


def test_recommendation_key_is_rejected():
    # recommendation は options 先頭から自動で埋まるので書かせない
    # recommendation is auto-filled from the first option, so it must not be written
    with pytest.raises(DigestError) as e:
        finding_from("t", META.replace("options:", "recommendation: 案A: 直す\noptions:"))
    assert "recommendation" in str(e.value)


def test_body_without_alternatives_passes():
    f = finding_from("t", META + "\n**PR側の主張:** 一致させる")
    assert f.options == ["直す", "戻す"]
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/test_digest_options.py -v`
Expected: FAIL with `ImportError: cannot import name 'options_html'`

- [ ] **Step 3: `render.py` に案リストの描画を足す**

import へ `OPTION_KEYS` を足す:

```python
from .findings import OPTION_KEYS, sort_key
```

`_card_html` の直前へ関数を足す:

```python
def options_html(f: Finding, indent: str) -> str:
    # 案はoptionsが正本。キー採番と推奨マークはfindings.jsonと同じ規則で機械的に付ける
    # options is the single source for alternatives; keys and the recommended mark follow findings.json
    if not f.options:
        return ""
    items = []
    for n, summary in enumerate(f.options):
        mark = '<span class="opt-recommended">推奨</span>' if n == 0 else ""
        items.append(f'{indent}  <li><strong>案{OPTION_KEYS[n]}</strong>{mark} — {escape(summary)}</li>')
    body = "\n".join(items)
    return (f'\n{indent}<p class="options-head"><strong>選べる案</strong></p>'
            f'\n{indent}<ul class="plain options-list">\n{body}\n{indent}</ul>')
```

`_card_html` の戻り値で `{body}{extra}` を `{body}{opts}{extra}` へ変え、その手前で組み立てる:

```python
    opts = options_html(f, "        ")
```

（`options` は非suppressedで必須・suppressedでは空なので、suppressedカードには何も出ない）

- [ ] **Step 4: `finding_parser.py` で手書き代替案を拒否する**

`recommended` の予約語チェックの直後へ追記:

```python
    # recommendation は options 先頭から自動で埋まるため、書かせない（案の正本を1箇所に保つ）
    # recommendation is auto-filled from the first option, so writing it would split the source of truth
    if "recommendation" in meta:
        raise DigestError(f"finding「{title}」に recommendation は書けません（options先頭から自動で入ります）")
```

`rest` を作った直後へ本文検査を追記:

```python
    # 案の列挙は options が正本。本文へ代替案を書くと同じ案が2箇所に出て片方が古くなる
    # options is the single source for alternatives; a body copy would go stale on one side
    if "代替案" in rest:
        raise DigestError(
            f"finding「{title}」の本文に代替案を書けません。案は options: へ書いてください"
            f"（コンバータが案A/案B…として描き、先頭へ推奨マークを付けます）")
```

- [ ] **Step 5: テンプレートへ案リストのCSSを足す**

`assets/digest-template.html` の `ul.plain li{...}`（315行目付近）の直下へ追記する。色はTask 7でトークン化するため、この時点では既存の生16進の流儀に合わせて書き、Task 7の置換対象に含める:

```css
  .options-head{ margin:12px 0 4px; font-size:13px; color:var(--text-muted); }
  ul.options-list li{ line-height:1.7; }
  /* 推奨マークは案キーの直後に置く。裁定サイトの推奨一括採用と同じ案を指す / the mark points at the same option the site bulk-accepts */
  .opt-recommended{ display:inline-block; margin-left:6px; padding:1px 6px; border-radius:8px;
    font-size:11px; font-weight:600; background:var(--badge-new-bg); color:var(--badge-new-fg); }
```

（`--badge-new-bg` / `--badge-new-fg` はTask 7で `:root` へ足すトークン。この時点では未定義なので、Task 7 完了までは色が効かない。Task 7 Step 3 のトークン追加で有効になる）

- [ ] **Step 6: ゴールデンmdから手書き代替案を落とす**

`tests/golden/pr-1155-digest.md` には `**代替案:**` 段落が12箇所ある。各段落の内容が対応する `options:` に含まれていることを確かめてから段落を削除する（options側が薄い場合は、段落の情報をoptionsの各要素へ移してから消す）。

```bash
cd .agents/skills/pr-independent-review
grep -n "代替案" tests/golden/pr-1155-digest.md
```

削除後に `recommendation:` 行も全て削除する（先頭optionから自動で入るため）:

```bash
grep -n "^recommendation:" tests/golden/pr-1155-digest.md
```

- [ ] **Step 7: テストを実行して通ることを確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/ -v`
Expected: `test_digest_options.py` の6件が PASS。ゴールデンは案リストが増えるため FAIL する

- [ ] **Step 8: findings.json の recommendation が壊れていないことを確認する**

`recommendation:` を消したので、findings.json の `recommendation` は options 先頭の文言になる。裁定サイトはこれを推奨案の表示に使うため、空になっていないことを確認する:

```bash
cd .agents/skills/pr-independent-review
python3 -c "
import json, pathlib, shutil, subprocess, sys, tempfile
g = pathlib.Path('tests/golden'); d = pathlib.Path(tempfile.mkdtemp())
shutil.copy(g / 'pr-1155-digest.md', d / 'digest.md')
shutil.copy(g / 'pr-1155-patch.diff', d / 'patch.diff')
r = subprocess.run([sys.executable, 'scripts/digest_build.py', str(d)], capture_output=True, text=True)
print(r.returncode, r.stderr)
f = json.load(open(d / 'findings.json'))['findings']
print('empty recommendation:', [x['id'] for x in f if not x['recommendation']])
print('recommended count ok:', all(sum(1 for o in x['options'] if o.get('recommended')) == 1 for x in f if not x['suppressed']))
"
```

Expected: `returncode` が0、空の recommendation が無く、`recommended count ok: True`

- [ ] **Step 9: ゴールデンを再生成してテストを通す**

Task 5 Step 10 のスクリプトを実行し、`python3 -m pytest tests/ -v` を通す。
Expected: 全テスト PASS。`grep -c 'opt-recommended' tests/golden/pr-1155-digest.expected.html` が非suppressedカードの件数と一致する

- [ ] **Step 10: 案が全カードに出ることを目視で確認する**

Task 5 Step 10 のスクリプトが出力した一時ディレクトリの `digest.html` を開く。
Expected: 設計判断・Critical の各カードに「選べる案」の見出しと案A/案B…の一覧が出て、案Aに「推奨」バッジが付いている。suppressedカードには案が出ない

- [ ] **Step 11: コミットする**

```bash
cd "$(git rev-parse --show-toplevel)"
git add .agents/skills/pr-independent-review/
git commit -m "feat(digest): 案をoptionsからカードへ描き手書き代替案を禁止する"
```

---

### Task 7: digest.html テンプレートのダークモード

**Files:**
- Modify: `.agents/skills/pr-independent-review/assets/digest-template.html:21-33`（`:root` トークン）と本文CSS全域
- Test: `.agents/skills/pr-independent-review/tests/test_digest_template_theme.py`

**Interfaces:**
- Consumes: なし（CSSのみ）
- Produces: テンプレート内の CSS 変数群（`--surface` / `--bg` / `--border` / `--text` / `--text-muted` / `--text-subtle` / `--engine` / `--danger` / `--amber` / `--amber-bg` / `--amber-border` に加え、新設する `--code-bg` / `--code-border` / `--code-fg` / `--code-ln` / `--diff-add-bg` / `--diff-add-fg` / `--diff-del-bg` / `--diff-del-fg` / `--badge-new-bg` / `--badge-new-fg` / `--badge-sup-bg` / `--badge-sup-fg` / `--panel-bg` / `--shadow`）

- [ ] **Step 1: 失敗するテストを書く**

`.agents/skills/pr-independent-review/tests/test_digest_template_theme.py`:

```python
# テンプレートがダークモードを持ち、色がトークン化されていることを検証する
# Verify the template ships a dark mode and that colors are tokenized
import re
from pathlib import Path

TEMPLATE = Path(__file__).resolve().parent.parent / "assets" / "digest-template.html"
_STYLE = re.compile(r"(?s)<style>(.*?)</style>")
_HEX = re.compile(r"#[0-9a-fA-F]{3,8}\b")


def _style_text() -> str:
    return "\n".join(_STYLE.findall(TEMPLATE.read_text(encoding="utf-8")))


def test_template_has_dark_media_query():
    assert "@media (prefers-color-scheme: dark)" in _style_text()


def test_template_has_no_theme_toggle():
    # R11: 切替UIは追加しない
    # R11: no manual toggle is added
    text = TEMPLATE.read_text(encoding="utf-8")
    assert "theme-toggle" not in text
    assert "prefers-color-scheme" in text


def test_hex_colors_live_only_in_token_blocks():
    # 色定義は :root と dark メディアクエリ（およびvendorテーマ）に閉じ、部品側は var() を使う
    # Color literals stay inside :root and the dark media query; components use var()
    lines = _style_text().splitlines()
    depth_in_tokens, offenders = False, []
    for line in lines:
        stripped = line.strip()
        if stripped.startswith(":root") or stripped.startswith("@media (prefers-color-scheme: dark)"):
            depth_in_tokens = True
        if depth_in_tokens and stripped == "}":
            depth_in_tokens = False
            continue
        if depth_in_tokens:
            continue
        if stripped.startswith("{{HLJS"):
            continue
        if _HEX.search(stripped):
            offenders.append(stripped)
    assert not offenders, f"トークン外に生の16進が残っています: {offenders[:5]}"
```

- [ ] **Step 2: テストを実行して失敗を確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/test_digest_template_theme.py -v`
Expected: `test_template_has_dark_media_query` と `test_hex_colors_live_only_in_token_blocks` が FAIL

- [ ] **Step 3: `:root` を拡張し、ダークのトークンブロックを足す**

`assets/digest-template.html` の `:root{...}`（21〜33行目）を次へ置き換える:

```css
  :root{
    --surface:#FFFFFF;
    --bg:#FFFFFF;
    --border:#E2E8F0;
    --text:#0F172A;
    --text-muted:#475569;
    --text-subtle:#64748B;
    --engine:#2563EB;
    --danger:#DC2626;
    --amber:#F59E0B;
    --amber-bg:#FFFBEB;
    --amber-border:#FDE68A;
    --panel-bg:#FFFFFF;
    --shadow:rgba(15,23,42,.08);
    --code-bg:#F6F8FA;
    --code-border:#D0D7DE;
    --code-fg:#24292F;
    --code-ln:#8B949E;
    --diff-add-bg:#DAFBE1;
    --diff-add-fg:#1A7F37;
    --diff-del-bg:#FFEBE9;
    --diff-del-fg:#CF222E;
    --badge-new-bg:#DDF4FF;
    --badge-new-fg:#0969DA;
    --badge-sup-bg:#FFF1E5;
    --badge-sup-fg:#BC4C00;
  }
  /* OS設定に追従するダーク配色。切替UIは持たない（毎run使い捨てで状態を引き継がないため） / follows the OS setting; no toggle since each run is disposable */
  @media (prefers-color-scheme: dark){
  :root{
    --surface:#0D1117;
    --bg:#010409;
    --border:#30363D;
    --text:#E6EDF3;
    --text-muted:#B1BAC4;
    --text-subtle:#8B949E;
    --engine:#58A6FF;
    --danger:#FF7B72;
    --amber:#D29922;
    --amber-bg:#2B2513;
    --amber-border:#5A431A;
    --panel-bg:#161B22;
    --shadow:rgba(0,0,0,.6);
    --code-bg:#161B22;
    --code-border:#30363D;
    --code-fg:#E6EDF3;
    --code-ln:#6E7681;
    --diff-add-bg:#12261E;
    --diff-add-fg:#3FB950;
    --diff-del-bg:#25171C;
    --diff-del-fg:#F85149;
    --badge-new-bg:#12283F;
    --badge-new-fg:#58A6FF;
    --badge-sup-bg:#3A2413;
    --badge-sup-fg:#DB8B4E;
  }
  }
```

- [ ] **Step 4: 部品側の生16進を var() へ置き換える**

`<style>` 内の残り全ての16進リテラルを、上のトークンのいずれかへ差し替える。作業手順:

```bash
cd .agents/skills/pr-independent-review
python3 - <<'PY'
import re, pathlib
text = pathlib.Path("assets/digest-template.html").read_text(encoding="utf-8")
style = "\n".join(re.findall(r"(?s)<style>(.*?)</style>", text))
for n, line in enumerate(style.splitlines(), 1):
    if re.search(r"#[0-9a-fA-F]{3,8}\b", line) and "--" not in line:
        print(n, line.strip())
PY
```

出力された各行について、役割に応じたトークンへ置換する（対応表）:

| 元の色の役割 | 置換先 |
|---|---|
| ページ・カードの背景（白系） | `var(--surface)` / `var(--bg)` |
| 罫線・区切り（薄いグレー） | `var(--border)` |
| 本文の文字 | `var(--text)` |
| 補助文・キャプション | `var(--text-muted)` / `var(--text-subtle)` |
| リンク・強調の青 | `var(--engine)` |
| Critical・エラーの赤 | `var(--danger)` |
| 注意・未裁定の黄 | `var(--amber)` / `var(--amber-bg)` / `var(--amber-border)` |
| コードカード背景・枠・文字・行番号 | `var(--code-bg)` / `var(--code-border)` / `var(--code-fg)` / `var(--code-ln)` |
| 追加行・削除行の帯と記号 | `var(--diff-add-bg)` / `var(--diff-add-fg)` / `var(--diff-del-bg)` / `var(--diff-del-fg)` |
| `.badge-new` / `.badge-sup` | `var(--badge-new-bg)` 他 |
| 浮きパネル・影 | `var(--panel-bg)` / `var(--shadow)` |

Task 2 Step 6 で書いた `.code-card` 系のCSSも、この段階でトークン参照へ書き換える。

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `cd .agents/skills/pr-independent-review && python3 -m pytest tests/test_digest_template_theme.py -v`
Expected: 3件 PASS

- [ ] **Step 6: ゴールデンを再生成し、両モードで目視確認する**

Task 5 Step 10 のスクリプトを実行して golden を更新し、出力された一時ディレクトリの `digest.html` を開く。macOS のダーク切替は次で行う:

```bash
osascript -e 'tell app "System Events" to tell appearance preferences to set dark mode to not dark mode'
```

Expected: ダークで本文・カード・コードカード・コメントパネルすべてが暗背景になり、白いブロックが残らない。確認後に同じコマンドで元へ戻す

- [ ] **Step 7: 全テストを通してコミットする**

```bash
cd .agents/skills/pr-independent-review && python3 -m pytest tests/ -v
cd "$(git rev-parse --show-toplevel)"
git add .agents/skills/pr-independent-review/
git commit -m "feat(digest): テンプレートの色をトークン化しOS追従ダークを入れる"
```

---

### Task 8: 裁定サイト（裁定レイヤー・一覧ページ）のダークモード

**Files:**
- Modify: `~/hermes-agent/data/services/pr-review/site/adj_style.py`
- Modify: `~/hermes-agent/data/services/pr-review/site/style.py`
- Modify: `~/hermes-agent/data/services/pr-review/site/inject.py`（インラインstyleがある場合のみ）

**Interfaces:**
- Consumes: digest テンプレート側のトークン名（`--surface` 等）。裁定レイヤーは digest.html へ注入されるため、同じトークン名を参照できる
- Produces: なし（CSS文字列のみ）

**注意: このディレクトリは git 管理外**。変更前にバックアップを取り、変更後にサービスを再起動する。

- [ ] **Step 1: バックアップを取る**

```bash
cd ~/hermes-agent/data/services/pr-review
cp -R site "site.bak-$(date +%Y%m%d-%H%M%S)"
ls -d site.bak-*
```

- [ ] **Step 2: インラインstyleの残りを洗い出す**

```bash
cd ~/hermes-agent/data/services/pr-review/site
grep -n "#[0-9a-fA-F]\{3,8\}" adj_style.py style.py inject.py pages.py | wc -l
grep -n "#[0-9a-fA-F]\{3,8\}" inject.py | head -20
```

Expected: 各ファイルの色定義箇所が一覧できる。`inject.py` に色が無ければ Step 4 は skip する

- [ ] **Step 3: `adj_style.py` にダークブロックを足す**

`ADJ_CSS` 文字列の末尾へ追記する。裁定レイヤーは `!important` で digest 側を上書きしているため、ダーク側も同じ詳細度で書く:

```python
ADJ_CSS += """
/* ---- OS追従のダーク配色。digest側のトークンと同じ役割分担で上書きする ---- */
/* ---- Dark palette following the OS setting, overriding with the same role split as the digest tokens ---- */
@media (prefers-color-scheme: dark){
  .verdict-card[data-adj-state="undecided"]{
    border-color:#5A431A !important;
    box-shadow:inset 6px 0 0 #D29922,0 1px 3px rgba(0,0,0,.6);
  }
  .verdict-card[data-adj-state="decided"]{
    border-color:#1B4721 !important;
    box-shadow:inset 6px 0 0 #3FB950;
  }
  @keyframes adjFlash{
    0%{box-shadow:inset 6px 0 0 #D29922,0 0 0 6px rgba(210,153,34,.35);}
    100%{box-shadow:inset 6px 0 0 #D29922,0 0 0 0 rgba(210,153,34,0);}
  }
}
"""
```

さらに Step 2 で洗い出した残りの色（裁定行の背景・案ボタン・チップ・右下パネル）についても、同じ `@media` ブロック内へ暗色版を追記する。**明るい背景（`#FFF` 系）と濃い文字（`#0F172A` 系）のペアを、暗い背景（`#161B22`）と明るい文字（`#E6EDF3`）へ反転させる**のが原則。

- [ ] **Step 4: `style.py`（一覧・待機画面）にダークブロックを足す**

同様に、CSS文字列の末尾へ `@media (prefers-color-scheme: dark){ ... }` を足し、`body` 背景・カード・テーブル・`pre.excerpt` を暗色へ反転させる。

- [ ] **Step 5: サービスを再起動する**

`pr-review-site` は supervisor 管理の longrun なので、プロセスを落とせば5秒程度で再起動する:

```bash
pkill -f "pr-review/site/app.py"
sleep 8
curl -s -o /dev/null -w "%{http_code}\n" http://127.0.0.1:8931/
```

Expected: `200`

- [ ] **Step 6: 両モードで目視確認する**

```bash
open "http://127.0.0.1:8931/pr/1157"
osascript -e 'tell app "System Events" to tell appearance preferences to set dark mode to not dark mode'
```

Expected: 一覧・裁定UI・カード・案ボタン・右下パネルすべてが暗背景になり、白いブロックが残らない。確認後に同じコマンドで元へ戻す

- [ ] **Step 7: 変更内容をコミットメッセージ本文へ記録する**

このディレクトリは git 管理外のため、moorestech 側へ変更点の記録だけを残す:

```bash
cd "$(git rev-parse --show-toplevel)"
git commit --allow-empty -m "chore(pr-review-site): 裁定レイヤーと一覧ページへOS追従ダークを入れる

git管理外の ~/hermes-agent/data/services/pr-review/site/ 配下を変更した:
- adj_style.py: ADJ_CSS へ prefers-color-scheme: dark ブロックを追加
- style.py: 一覧・待機画面のCSSへ同ブロックを追加
バックアップは同ディレクトリの site.bak-<timestamp> に残っている"
```

---

### Task 9: フォーマット仕様とSKILL.mdの更新

**Files:**
- Modify: `.agents/skills/pr-independent-review/README-digest-format.md`
- Modify: `.agents/skills/pr-independent-review/SKILL.md:438-461`

**Interfaces:**
- Consumes: Task 1〜6 で確定した記法とエラー条件

- [ ] **Step 1: `README-digest-format.md` のコードフェンス節を書き換える**

「### コードフェンス `code-card`」の節を次へ置き換える:

````markdown
### コードフェンス `code-card`

各行は `[フラグ]<行番号>|<コード>`。フラグは次の3種で、`*` は `+` / `-` と併用できる。

| フラグ | 意味 | 行番号 | 見た目 |
|---|---|---|---|
| `+` | 追加行 | 新ファイルの行番号 | 緑帯・`+` グリフ |
| `-` | 削除行 | **旧ファイルの行番号** | 赤帯・`-` グリフ |
| （無し） | 文脈行 | 新ファイルの行番号 | 帯なし・空白グリフ |
| `*` | 注目行（他フラグと併用） | — | 左端のアンバー縦バー＋太字 |

`+` と `-` を同じ行に付けることはできない（エラー）。`|` の最初の1個だけが区切り。
コードは**エスケープせず生のまま**書く（コンバータがエスケープする）。

**置換を扱うカードには必ず `-` 行を書く。** コンバータは同じ `$RUNDIR` の `patch.diff` を読み、
`+` 行が属するhunkが行を削除しているのにカードへ `-` 行が無い場合、**エラーで非0終了する**。
「変更前が見えないと何を何に変えたか読めない」ためで、規約ではなく機械が担保する。

構文着色の言語は **`files` 先頭の拡張子** から自動で決まる（`cs`→csharp、`ts`/`tsx`→typescript、
`css`→css、`json`/`asmdef`→json、`yml`/`yaml`→yaml、`md`→markdown、未知拡張子は無着色）。
フェンスに言語を書く欄は無い。1カードには**単一ファイルの抜粋だけ**を入れること
（複数言語を混ぜると後半が誤着色される）。
````

例示ブロック（冒頭の finding ブロック例）の `code-card` にも `-` 行を1行足し、実例として見せる:

```
    ```code-card
     36|        private void UpdateTorqueRequestRate()
     37|        {
    -38|            _gearEnergyTransformer.SetTorqueRequestRate(idleRate);
    +38|            // 表示の分母・加工速度と同じ導出をそのまま歯車網への要求へ反映する
    *+40|            _gearEnergyTransformer.SetTorqueRequestRate(...);
     41|        }
    ```
```

- [ ] **Step 1.5: `README-digest-format.md` の YAML キー表と注意書きを直す**

`options` の行を次へ差し替える:

```markdown
| `options` | 非suppressedで必須 | 案の要約の配列。**先頭が推奨**。コンバータがカード本文へ案A/案B…として描き、先頭へ推奨マークを付ける |
```

`recommendation` の行を次へ差し替える:

```markdown
| `recommendation` | **書けない** | findings.json の `recommendation` は `options` 先頭から自動で入る。書くとエラーになる |
```

「**注意（コンバータは検査しない）**: 同じ推奨案が `options` 先頭・`recommendation`・カード本文の代替案説明の3箇所に現れる。…」の段落を**削除**し、次へ置き換える:

```markdown
**案の正本は `options:` 1箇所である。** カード本文に `代替案` を書くとコンバータがエラーで落とす
（同じ案が2箇所に出て片方だけ古くなる事故を防ぐため）。案の並び順がそのまま案A/案B…のキーになり、
先頭が推奨として描かれる。案どうしが排他である等の関係は `summary` か `index_label` へ書く。
```

- [ ] **Step 2: `SKILL.md` の Step 7 を更新する**

「**残す規約**」の箇条書きのうち、コード抜粋の行を次へ差し替える:

```markdown
  - コード抜粋は全カード必須（`code-card` フェンス）。patchから機械的に転記する（創作・要約禁止）。
    **置換なら削除行 `-<旧行番号>|<コード>` も必ず転記する**（コンバータが `patch.diff` と照合し、
    欠けていればエラーで落ちる）。1カードには単一ファイルの抜粋だけを入れる（言語は `files` 先頭の
    拡張子から自動判定されるため、複数言語を混ぜると後半が誤着色される）
```

同じ節の実行コマンドの下へ注記を足す:

```markdown
- コンバータは `$RUNDIR/patch.diff` を読む。Step 4 の生成物なので通常は存在するが、
  無い場合はエラーで落ちる（`patch.diff がありません`）
- 案はカード本文へ手で書かない。`options:` へ書けばコンバータが案A/案B…として描き、
  先頭へ推奨マークを付ける。本文に「代替案」を書くとエラーで落ちる
```

- [ ] **Step 3: 記述と実装が一致することを確認する**

```bash
cd .agents/skills/pr-independent-review
grep -n '\-<旧行番号>' README-digest-format.md SKILL.md
grep -n "LANGUAGE_BY_EXTENSION" scripts/digest_md/code_card/lang.py
python3 -m pytest tests/ -v
```

Expected: 両ファイルに削除行の記述があり、全テスト PASS

- [ ] **Step 4: コミットする**

```bash
cd "$(git rev-parse --show-toplevel)"
git add .agents/skills/pr-independent-review/README-digest-format.md .agents/skills/pr-independent-review/SKILL.md
git commit -m "docs(digest): 削除行・言語自動判定・patch照合を仕様へ書く"
```

---

### Task 10: 実データで通しの動作確認

**Files:**
- 変更なし（検証のみ）

**Interfaces:**
- Consumes: Task 1〜9 の成果物すべて

- [ ] **Step 1: 直近の実runで再生成してみる**

`pr-1157` はコンバータ産の `digest.md` と `patch.diff` を持つ実データである。作業コピーで再生成する:

```bash
cd .agents/skills/pr-independent-review
W=$(mktemp -d)
R=~/hermes-agent/data/repos/moorestech_logs/harness/pr-independent-review/runs/pr-1157
cp "$R/digest.md" "$R/patch.diff" "$W/"
python3 scripts/digest_build.py "$W"; echo "exit=$?"
echo "$W"
```

Expected: 次のいずれか。
- `exit=0` … 生成成功。Step 2 へ進む
- `exit=1` で `置換を扱うカードに削除行がありません` … **ガードが意図どおり働いている**。この run の digest.md は旧記法で書かれているため落ちるのが正しい。この場合はエラー文言に finding id が出ていることだけ確認し、`$W/digest.md` へ手で `-` 行を足して再実行し、生成まで到達させる

- [ ] **Step 2: 生成物を検査する**

```bash
grep -c 'class="cl add"' "$W/digest.html"
grep -c 'class="cl del"' "$W/digest.html"
grep -c 'data-lang=' "$W/digest.html"
grep -c 'https://' "$W/digest.html"
python3 -c "import json;d=json.load(open('$W/findings.json'));print(all('-' != l.strip()[:1] for f in d['findings'] for l in f['excerpt'].splitlines()))"
```

Expected: add が1以上、del が1以上、data-lang が1以上、`https://` が0、excerpt 検査が `True`

- [ ] **Step 3: ブラウザで両モードを確認する**

```bash
open "$W/digest.html"
osascript -e 'tell app "System Events" to tell appearance preferences to set dark mode to not dark mode'
```

Expected: 構文着色・差分の帯・グリフ・注目行の縦バーがライト/ダーク両方で読める。確認後に元へ戻す

- [ ] **Step 4: 確認結果を記録してコミットする**

```bash
cd "$(git rev-parse --show-toplevel)"
git commit --allow-empty -m "test(digest): pr-1157の実データで通し確認する

- exit code / cl add / cl del / data-lang / 外部URL0件 / excerptに削除行なし を確認
- ライト・ダーク両モードで目視確認"
```

---

### Task 11: ブランチ全体のコードレビュー（省略不可）

**Files:**
- 変更なし（レビューのみ）

- [ ] **Step 1: moores-code-review スキルでブランチ全体をレビューする**

`moores-code-review` スキルを起動し、`master...feat/review-digest-diff-syntax-dark` の全差分をレビュー対象にする。

- [ ] **Step 2: 指摘を反映する**

機械的な指摘は修正し、設計判断が必要な指摘はユーザーへ提示する。

- [ ] **Step 3: 修正をコミットする**

```bash
cd "$(git rev-parse --show-toplevel)"
git add -A
git commit -m "fix: コードレビュー指摘を反映する"
```

---

## 判断記録（ADR）

設計ADR: `docs/adr/0018-review-digest-code-card-diff-syntax-dark.md`（決定1〜7と棄却案はすべてそちらが正本）

planning 中に生じた判断:

- **code-card 関連コードは `digest_md/code_card/` サブパッケージへ分ける**。`digest_md/` は既に11ファイルで AGENTS.md の「1ディレクトリ10ファイルまで」に達しており、`lines` / `html` / `lang` / `patch_guard` の4モジュールを平置きすると15ファイルになる。責務（code-cardの解析・描画・言語・検証）でまとまる自然な単位でもある。出所: agent前提（AGENTS.md のディレクトリ規約）
- **`code_card_lines` の戻り値を `(num, ins, hl, code)` から `(num, kind, hl, code)` へ変える**。`ins` の bool では add/del/ctx の3値を表せない。読み手は `blocks.py`（描画）・`findings.py`（excerpt）・`patch_guard.py`（検証）の3箇所で、すべて同時に更新する。出所: agent前提
- **patch.diff 照合は「マッチしたhunkが全て削除を伴う」ときだけ落とす**。追加行がどのhunkにも一致しない抜粋（整形して引用した場合など）は検証不能なので通す。レビュー基盤の信頼は false positive で最も速く壊れるため、曖昧なら通す側へ倒す。出所: agent前提
- **ゴールデン比較では127KBのバンドルを `[BUNDLE]` へ正規化する**。期待HTMLにミニファイJSを含めるとレビュー不能な巨大diffになる。代わりに「バンドルが存在し10万文字以上ある」ことをテストで明示的に検査し、正規化が欠落を隠さないようにする。出所: agent前提
- **`render_html` に `assets` 引数を足し、vendor の読み込みは `digest_build.py`（IO層）に置く**。`digest_md/` 配下は文字列を受け取って文字列を返す純関数群という既存の層分けを崩さない。出所: agent前提（既存 `render_html(doc, template, refs)` の設計に従う）
- **`~/hermes-agent/data/services/pr-review/` は git 管理外なので、変更は空コミットのメッセージ本文で追跡する**。バックアップを取ってから触る。出所: agent前提（同ディレクトリに `.git` が無いことの実測）
- **案（`options`）はコンバータがカード本文へ描き、手書きの代替案段落と `recommendation:` は拒否する**。
  コンバータ産の pr-1157 は案の表示が0箇所で、案が出るかが生成subagentの手書き次第だった（実測）。
  これは `.decisions/2026-08-18-推奨案の3重記述は注意書きだけで留める.md` の撤回であり、
  `.decisions/2026-08-19-案はoptionsを正本としてカードへ機械描画する.md` に記録した。
  出所: ユーザー裁定 2026-08-19「裁定の選択肢のフィールドをちゃんと出すようにしてほしい。1167みたいに、それをデフォルトにして」
- **案リストの描画は `render.py` に置き、`findings.py` の `OPTION_KEYS` を共有する**。案キーの採番規則が
  HTML と findings.json で割れると、裁定サイトのボタンとカードの案が食い違う。採番の正本は1箇所に保つ。出所: agent前提
- **PR #1167 の digest は再生成しない**。出所: ユーザー裁定 2026-08-18「順番的にコンバータが作られる前のPRなの？それならしょうがない」

## 配置と前例（spec-architecture-review）

| 追加物 | 配置先 | 前例 |
|---|---|---|
| 行解析 `lines.py` / 描画 `html.py` / 言語 `lang.py` / 検証 `patch_guard.py` | `scripts/digest_md/code_card/`（文字列→文字列の純関数層） | `digest_md/` 既存11モジュールがすべて純関数（IOを持たない） |
| vendor資産の読み込み `load_assets()` | `scripts/digest_build.py`（IO層） | 既存の `TEMPLATE.read_text()` / `findings.json` 書き出しが同ファイルにある |
| 削除行ガードの呼び出し | `digest_build.py` の `main()`（`verify()` と並べる） | 既存 `verify(html, findings)` が「出荷前の機械検査」として同じ位置にある |
| 案リストの描画 `options_html()` | `scripts/digest_md/render.py`（カード組み立ての層） | 既存 `_card_html` が files・summary・suppress_reason を同じ場所で組んでいる |
| CSSトークンとダーク配色 | `assets/digest-template.html` の `<style>` | 既存の `:root` 11トークンが同じ場所にある |
| 着色ドライバJS | テンプレート既存 `<script>` 内の末尾 | コメントUIのJSが同じブロックにまとまっている |

機構選択（leverage-over-replace）: 本planは既存の「digest.md → コンバータ → digest.html / findings.json」機構を凍結・迂回・並行複製しない。追加はすべて既存パイプラインの中の書き手（描画の1段）か読み手（検証の1段）として入る。唯一の能動介入である patch.diff ガードは、既存 `verify()` と同型の「出荷前検査を1つ増やす」形であり、新しい制御経路を作らない。

## 機能パリティ死活表

`digest.html` と裁定サイトで現在生きている操作が、本planの後も生きることを確認した。

| 操作 | 生存 | 根拠 |
|---|---|---|
| 本文選択コメント・図コメント・左下パネル・すべてコピー | ○ | コメントUIは `.figure` / `data-comment-ui` 単位で動き、`pre.code-card` の内部構造を見ていない |
| 裁定ボタンの注入位置 | ○ | `inject.py` は `section[data-finding-id]` を起点にする。カード要素の属性は不変 |
| 裁定レイヤーのCSS | ○ | `adj_style.py` / `inject.py` / `style.py` / `pages.py` に `code-card` / `ins` / `.hl` / `.ln` への参照が1件も無いことを実測済み（`grep` でヒット0） |
| `findings.json` を読む poller と `pr-adjudicated-apply` | ○ | スキーマ不変。`excerpt` は従来どおり「PR後の現行コード」（R9） |
| 裁定サイトの推奨一括採用（未裁定を推奨案で埋める） | ○ | `options[0].recommended` はコンバータが必ず付ける規則のまま。カードの推奨マークも同じ先頭要素を指す（R14） |
| 一覧ページの `pre.excerpt` 表示 | ○ | プレーンテキスト表示のままで、差分記法を持ち込まない |
| `file://` で digest.html を開く運用（Step 8 のクイックトンネル・`open`） | ○ | vendor資産をインライン同梱するため外部参照が発生しない（R6） |

死ぬ操作・退化する操作は無い。

## Execution Handoff

planとADRと`.decisions/`だけで実装できることを確認した（会話でしか決まっていない裁定は `docs/adr/0018-review-digest-code-card-diff-syntax-dark.md` と本planの「判断記録」へ書き落とし済み）。
