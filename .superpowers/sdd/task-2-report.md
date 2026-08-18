# Task 2 完了報告: Markdown→HTML変換層の実装

## 実装内容

Task 2 では、digest.md 片のMarkdown→HTML変換層を実装した。以下3ファイルを新規作成:

1. `.agents/skills/pr-independent-review/scripts/digest_md/inline.py` (35行)
   - `escape(text: str) -> str`: 文字列をHTMLエスケープ（& < > " ' の順）
   - `inline_html(text: str, refs: dict) -> str`: インライン記法を変換
     - `**強調**` → `<strong>...</strong>`
     - `` `code` `` → `<code>...</code>`（内容はエスケープ済み）
     - `[F:slug]` → `<a href="#fid">FID</a>`（未定義はエラー）

2. `.agents/skills/pr-independent-review/scripts/digest_md/blocks.py` (98行)
   - `code_card_html(body: str, indent: str) -> str`: code-cardフェンスをHTMLへ
     - 各行 `[フラグ]<行番号>|<コード>` を `<pre class="code-card">` へ
     - `+` フラグ → `<ins>` でマーク、`*` フラグ → `<span class="hl">` でマーク
   - `blocks_html(md: str, refs: dict, indent: str) -> str`: ブロック記法を変換
     - 段落・リスト・h3・コードフェンス（code-card/plain）に対応
     - 未対応記法 `>` `|` `#1-#6` → DigestError で落とす
     - 未知フェンス言語 → DigestError で落とす
   - ヘルパー関数: `_collect_fence()` / `_collect_list()` / `_collect_paragraph()`

3. `.agents/skills/pr-independent-review/tests/test_digest_blocks.py` (53行)
   - 7つのテストケース（全PASS）

## テスト結果

```
============================= test session starts ==============================
...
.agents/skills/pr-independent-review/tests/test_digest_blocks.py::test_escape_order_keeps_ampersand_first PASSED
.agents/skills/pr-independent-review/tests/test_digest_blocks.py::test_inline_html_converts_strong_code_and_ref PASSED
.agents/skills/pr-independent-review/tests/test_digest_blocks.py::test_inline_html_unknown_ref_is_error PASSED
.agents/skills/pr-independent-review/tests/test_digest_blocks.py::test_code_card_marks_ins_and_hl PASSED
.agents/skills/pr-independent-review/tests/test_digest_blocks.py::test_blocks_html_paragraph_and_list PASSED
.agents/skills/pr-independent-review/tests/test_digest_blocks.py::test_blocks_html_rejects_unknown_syntax PASSED
.agents/skills/pr-independent-review/tests/test_digest_blocks.py::test_blocks_html_rejects_unknown_fence PASSED

============================== 7 passed in 0.13s ===============================
```

**TDDの証拠:**

- RED: `ModuleNotFoundError: No module named 'digest_md.blocks'` で開始
- GREEN: 実装後 7/7 PASS に到達
- 実装はブリーフのサンプルコード写経（変更なし）

## 品質確認

- **ファイル行数**: 35/98/53行（全て200行以下 ✓）
- **コメント**: 日本語→英語の2行セットを3〜10行ごと（要件通り）
- **エスケープ順序**: `&` を最優先（後続の実体参照二重エスケープ防止）
- **エラーハンドリング**: 未定義参照・未対応記法は DigestError で明示的に落とす
- **インターフェース**: Task 1 の `digest_md.parse.DigestError` を再利用（新規定義なし）
- **symlink**: `.claude/skills/` は `.agents/skills/` へのsymlink（編集なし）

## 変更ファイル

- `.agents/skills/pr-independent-review/scripts/digest_md/inline.py` (新規)
- `.agents/skills/pr-independent-review/scripts/digest_md/blocks.py` (新規)
- `.agents/skills/pr-independent-review/tests/test_digest_blocks.py` (新規)

## コミット

```
303b809e3 feat(digest): Markdown片のHTML化とcode-card行マークアップを追加する
```

## 懸念事項

なし。ブリーフのサンプルコードが完全で、要件も明確であったため、写経のみで完結。
