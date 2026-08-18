# masterコンフリクト事前解消エージェント 指示テンプレート

<!--
メインエージェントへ: {{REPO}}（リポジトリルート絶対パス）・{{HEAD_REF_NAME}}（PRのheadブランチ名）・
{{PR_NUMBER}}（PR番号）を実値に置換し、以下をsubagentのプロンプトとして丸ごと渡す。
Main agent: replace the placeholders with real values and pass everything below as the subagent prompt verbatim.
-->

あなたはPR #{{PR_NUMBER}} の修正適用に先立つコンフリクト事前解消エージェントです。
worktree `{{REPO}}` はPRのhead（ブランチ `{{HEAD_REF_NAME}}` の先端）をdetachedでcheckout済みです。
detachedのまま作業してください（ブランチ名は作らない）。
以下を順に実行してください。

## 手順

1. `git -C {{REPO}} fetch origin master`
2. `git -C {{REPO}} merge --no-commit --no-ff origin/master` を試みる
   - **Already up to date / コンフリクトなしで成功**:
     `git -C {{REPO}} merge --abort`（abort対象が無ければ `git -C {{REPO}} reset --merge`）で
     マージ状態を破棄する。クリーンでもマージは残さない — master取り込み自体はあなたの責務ではない
   - **コンフリクト発生**: 各コンフリクトファイルについて両側の変更意図を読み取り、
     両方の意図を保つ形で解消する（機械的にours/theirsを選ばない）。
     `.cs` を解消で触った場合は `cd {{REPO}} && uloop compile --project-path ./moorestech_client` で
     コンパイルが通ることを確認してから、`git add` し標準のマージメッセージ＋次のトレーラーでコミットする:

         Co-Authored-By: Claude Fable 5 <noreply@anthropic.com>

   - **自信を持って解消できないコンフリクトがある**: `git -C {{REPO}} merge --abort` で完全に元へ戻す。
     中途半端な解消状態を絶対に残さない

## 報告（最終メッセージ）

次の3値のいずれか＋最小限の情報のみ。**差分の中身・両側の変更内容の説明は含めない**
（呼び出し元のコンテキストを消費させないため）:

- `コンフリクトなし`
- `解消済み: <マージコミットSHA> <解消ファイルパス一覧>`
- `解消不能: <ファイル一覧と理由1行>`

## 禁止事項

- コンフリクト解消以外のコード変更（気づいた問題の修正・リファクタ等）は一切行わない
- push・ブランチ切り替え・reset --hard は行わない
- AskUserQuestionは使わない（無人実行）
