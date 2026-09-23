# Codex での派遣・待機・生存確認

SKILL.md の「派遣する」「待つ」「生存を確認する」を Codex(`spawn_agent` / `wait_agent`)で
実行するときの具体手順。実行環境に依存しない規則(規模ゲート・台帳・レビュー・モデルの
ティア)は SKILL.md が正。

## 派遣

- `spawn_agent` で派遣する。SKILL.md やプロンプトテンプレートにある「フォアグラウンド」の
  指定は Claude Code の Agent ツール向けで、Codex には該当しない。Codex の `spawn_agent` は
  すぐ戻るので、待ちは下の「待ち」節で行う。
- SKILL.md の `opus` / `sonnet` / `haiku` はモデルのティア名として読み、Codex で使える
  モデルの同じ段(最上位 / 標準 / 最安)に置き換えて明示する。

## 待ち(ポーリング禁止)

- 待つときは `wait_agent` を **`timeout_ms: 1800000`(30 分)** で呼ぶ。上限で丸められた
  場合は結果の要約に timeout adjustment が出るので、以後はその値を使う。
- `timed_out: true` は「まだ終わっていない」という意味しか持たない。同じ値で wait をやり直す。
  タイムアウトのたびに進捗コメント・`list_agents`・`send_message` を挟まない。
- 待ちの間にできる並行作業(次タスクのブリーフ作成、レビューパッケージ作成など)があれば
  先に済ませてから wait する。やることが無ければ wait するだけにする。

**根拠(2026-09-22 別 repo(cmux-connector)の Codex SDD 実行):** `wait_agent` を `timeout_ms: 60000` で 188 回
呼び、122 回が空振り(`timed_out: true`)だった。待ちの直後のリクエストが親の入力トークンの
68.8%(26.8M / 38.9M)を占め、進捗コメント・生存確認も待ちのたびに挟まっていた。Codex の
システムプロンプトの「wait は分単位で」という指示だけでは防げなかったので、ここに数値で書く。

## 生存確認・継続

- 生存確認は `list_agents`。行うのは **2 回続けて wait がタイムアウトしたとき(= 1 時間
  応答が無いとき)に 1 回だけ**と、compaction 後・途中終了の疑いがあるときの再派遣前。
  生きている subagent を死んだと決めつけて再派遣し、同じ worktree を二重編集した実事故がある。
- 生きている subagent への追加指示は `send_message` / `followup_task`。

## 危険信号

- `wait_agent` を分未満の timeout で繰り返す
- タイムアウトのたびに進捗報告・`list_agents` を挟む
