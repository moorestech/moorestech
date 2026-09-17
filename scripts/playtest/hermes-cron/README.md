# 日次ダイジェストの Hermes cron ジョブ

Hermes 内蔵 cron（launchd の cron ではない）に1件だけジョブを置く。実体は `~/.hermes/cron/jobs.json`。

## 分かっていること（実測 2026-09-13）

- 生きている `HERMES_HOME` は `~/.hermes`（封じ込め env の `data/.hermes` ではない。`data/cron/jobs.json` は古い残骸）
- `--script` のパスは `HERMES_HOME/scripts/` 配下へ解決され、realpath で外へ出るものは拒否される。**symlink は使えない**（実ファイルの shim を置く）
- `--no-agent` を付けるとスクリプトの stdout がそのまま配信され、LLM を通らない。stdout が空だと無投稿になる
- `--deliver discord:<チャンネルID>` の書式は既存ジョブ（doujin-spy 監視・tiktok collector レポート）で使われている
- `--script` を含むジョブの作成はシェルフックの承認を求めることがある。TTY 無しで作るときは `hermes cron --accept-hooks create ...`
- shim（`moorestech-playtest-digest.sh`）は `HERMES_HOME/scripts/` へコピーされた実体として動くため、自分の位置から repo を逆算できない。`MOORESTECH_REPO`・`MOORESTECH_LOGS` の既定値は `$HOME` にも依存しない絶対パスにしてある（cron 実行時の HOME が実ホームか封じ込め用かは Hermes 側の実装に依存し不定なため）。詳細はスクリプト冒頭のコメント参照

## 手順

```bash
# 1. shim を実ファイルとして置く（symlink 不可）
install -m 755 ~/hermes-agent/data/repos/moorestech/scripts/playtest/hermes-cron/moorestech-playtest-digest.sh \
  ~/.hermes/scripts/moorestech-playtest-digest.sh

# 2. 単体で動くか確かめる（Discord には出ない）
~/.hermes/scripts/moorestech-playtest-digest.sh | head -40

# 3. ジョブを作る。チャンネルIDは env ファイルから読み、コマンドラインにも履歴にも値を書かない
. ~/hermes-agent/data/services/playtest/env.sh
~/hermes-agent/venv/bin/hermes cron --accept-hooks create "0 9 * * *" \
  --name "moorestech プレイテスト日次ダイジェスト" \
  --script moorestech-playtest-digest.sh \
  --no-agent \
  --deliver "discord:${PLAYTEST_DIGEST_DISCORD_CHANNEL_ID}" \
  "毎朝9時の定期実行タスクです。

目的: moorestech プレイテストの前日分ダイジェストを、このDiscordチャンネルへ報告する。

実行手順:
1. \`moorestech-playtest-digest.sh\` を実行する。
2. stdout に出力された Markdown を、そのまま最終回答として送信する。
3. 要約・整形・並べ替えをしない。感想は全文を載せる（ADR 0061）。

補足:
- 「投入候補のバグ報告」に並ぶ enqueue コマンドは、人が選んで叩くためのもの。エージェントが勝手に実行しない。
- 該当0件の日も見出しだけの短い投稿になる（沈黙は異常のサイン）。
- Discord の1メッセージ上限で切れた場合、末尾に全文の置き場（moorestech_logs/harness/playtest/digests/<日付>.md）が出る。
- スクリプトが失敗した場合は、エラー内容を簡潔に報告する。"

# 4. 確認と単発実行
~/hermes-agent/venv/bin/hermes cron list
~/hermes-agent/venv/bin/hermes cron run <job-id>
```

`--no-agent` が付いているので上のプロンプト本文は実行時には使われない（既存の
「ポケモンカードPSA10最安値監視」ジョブと同じ形で、`--no-agent` を外して
エージェントモードへ切り替えるときの本文として保存しておく）。
