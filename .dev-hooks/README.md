# .dev-hooks — エージェント共通フック（観点リマインダ・意思決定台帳・考古学基盤）

編集差分の観点注入、意思決定台帳(.decisions/)の運用、Beadsタスク台帳の運用、
生セッションログの退避を、Claude Code / Codex 共通のフックとして担う。

参考: 構文・差分解析でエージェントに観点を注入するアイデア
(https://zenn.dev/manalink_dev/articles/coding-agent-with-syntax-tree-analyze)

## 構成

```
.dev-hooks/
  check-diff.mjs                 # 差分観点リマインダ本体（node。mac/windows・claude/codex 共通）
  rules.json                     # ルール定義（ここだけ編集すれば拡張できる）
  decisions-index.mjs            # 意思決定台帳(.decisions/)の目次と運用ルールをSessionStartで注入
  decisions-ruling-reminder.mjs  # AskUserQuestion完了時に台帳への記録をリマインド（Claude Codeのみ）
  decisions-format-check.mjs     # .decisions/レコードの書式検査（違反はexit 2で差し戻し）
  beads-prime.mjs                # Beads台帳(bd)の概況と役割分担ルールをSessionStartで注入
  beads-guard.mjs                # 破壊的bd/doltコマンドの物理拒否とpublic誤送信ガード（PreToolUse）
  poll-guard.mjs                 # 同一Bashコマンド3連続＝ポーリングを拒否し正しい待ち方を再注入（PreToolUse 全ツール。Claudeのみ登録 — リセット判定に全ツールイベントが要るため）
  beads-sync-watch.mjs           # Dolt同期障害の復旧誘導＋claim/createへのセッション出自刻印（PostToolUse）
  beads-learn-capture.mjs        # 応答末尾の「LEARN: 一行」をbd noteへ自動保存（Claude Stop）
  logs-sync.mjs                  # Claude/Codex生JSONLをprivateの../moorestech_logsへ退避（Stop/SessionEnd等）
  commit-map.mjs                 # コミットsha↔セッションIDの対応表をmoorestech_logsへ追記（PostToolUse Bash）
  README.md
.claude/settings.json  # 各フックの呼び出し登録（Claude Code 用）
.codex/hooks.json      # 同上（Codex 用。SessionStart/PostCompact/PostToolUseに対応分を登録）
```

## 考古学基盤（moorestech_logs 連携）

- `logs-sync.mjs` / `commit-map.mjs` / beads系は、兄弟ディレクトリに private repo `moorestech_logs` がある時だけ動く（無ければ全て沈黙）。
- Beadsのデータ同期先も moorestech_logs（`refs/dolt/data`）。public本体repoへは同期しない（beads-guardが誤送信を物理拒否する）。
- 新しいマシンでは `../moorestech_logs` を clone し、`bd bootstrap` でDoltデータを復元すれば全hookが有効になる。

- 発火タイミング: ファイル編集ツール（Edit/Write/MultiEdit/NotebookEdit/apply_patch 等）の **実行後**（PostToolUse）。
- 判定対象: `tool_input` 全体（編集後の内容・file_path・patch を含む）。
- 出力: 一致時のみ `hookSpecificOutput.additionalContext` で観点を注入（非ブロッキング）。一致しなければ無言で終了。
- 安全側: 入力parse失敗・rules.json欠損など何かあれば必ず exit 0（エージェントを止めない）。

## クロスプラットフォーム（mac / windows / claude / codex）

前提: どのOSでも `node` が PATH に通っていること。

- 本体スクリプトは純Nodeで、`import.meta.url` から自分の隣の `rules.json` を解決する **CWD非依存**設計。どのカレントから起動されても動く。
- Claude Code: `${CLAUDE_PROJECT_DIR}` をClaude自身が展開するため全OSで動作。
- Codex: コマンドは相対パス `node ".dev-hooks/check-diff.mjs"`。cmd.exe / PowerShell / bash いずれでも展開不要で動く。
  - 注意: Codexを **リポジトリのサブディレクトリから起動**した場合、相対パスが解決できないことがある。その場合は POSIX 環境なら `node "$(git rev-parse --show-toplevel)/.dev-hooks/check-diff.mjs"`、Windows なら絶対パスに変更する。

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
