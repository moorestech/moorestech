# .dev-hooks — 差分観点リマインダ（共通フック）

編集差分を正規表現ルールに照合し、一致したら「この観点で再チェックして」という文言を
コーディングエージェント（Claude Code / Codex）へ自動で流す仕組み。

参考: 構文・差分解析でエージェントに観点を注入するアイデア
(https://zenn.dev/manalink_dev/articles/coding-agent-with-syntax-tree-analyze)

## 構成

```
.dev-hooks/
  check-diff.mjs   # 共通フック本体（node。mac/windows・claude/codex 共通）
  rules.json       # ルール定義（ここだけ編集すれば拡張できる）
  README.md
.claude/settings.json  # PostToolUse から check-diff.mjs を呼ぶ
.codex/hooks.json      # 同上（Codex 用）
```

- 発火タイミング: ファイル編集ツール（Edit/Write/MultiEdit/NotebookEdit/apply_patch 等）の **実行後**（PostToolUse）。
- 判定対象: `tool_input` 全体（編集後の内容・file_path・patch を含む）。
- 出力: 一致時のみ `hookSpecificOutput.additionalContext` で観点を注入（非ブロッキング）。一致しなければ無言で終了。
- 安全側: 入力parse失敗・rules.json欠損など何かあれば必ず exit 0（エージェントを止めない）。

## ルールの足し方（拡張）

`rules.json` の `rules` 配列に1要素足すだけ。スクリプトは触らない。

```json
{
  "id": "一意なID",
  "description": "何のためのルールか（人間向けメモ）",
  "pattern": "正規表現（tool_input 全体に対して照合）",
  "flags": "i",                       // 任意。正規表現フラグ（例: 大文字小文字無視）
  "tools": ["Edit", "Write"],          // 任意。対象ツール限定。省略時は全編集ツール
  "message": "エージェントに流す文言"
}
```

例: `async void` を書いたら警告したい

```json
{
  "id": "no-async-void",
  "pattern": "async\\s+void",
  "flags": "",
  "message": "⚠ async void を検出。Task 戻り値に変更し、例外が握り潰されないか確認してください。"
}
```

- `pattern` は JSON 文字列なのでバックスラッシュは `\\` でエスケープする。
- 複数ルールが一致したら、各 message を空行区切りで連結して注入する。

## 確認

- Claude Code: `/hooks` で登録状況を確認。
- Codex: `/hooks` で確認・trust（スクリプト変更後は再trustが必要）。

## 手動テスト

```bash
echo '{"tool_name":"Edit","tool_input":{"file_path":"Foo.cs","new_string":"public string GetSaveState(){}"}}' \
  | node .dev-hooks/check-diff.mjs
```

一致すれば `hookSpecificOutput.additionalContext` を含む JSON が出力される。
