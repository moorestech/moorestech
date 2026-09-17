# プレイテストの取り込み（Mac mini側）

## 前提

- `/Users/sakastudio/hermes-agent/data/repos/{moorestech,moorestech_logs}` がある
- `/Users/sakastudio/hermes-agent/data/services/playtest/env.sh` を `chmod 600` で作る（**値はこのREADMEにもrepoにも書かない**）:
  ```bash
  export PLAYTEST_ADMIN_KEY=...                    # 受け口 Worker の ADMIN_KEY
  export PLAYTEST_DIGEST_DISCORD_CHANNEL_ID=...    # 日次ダイジェストの投稿先
  ```

## supervisor へ登録する

`/Users/sakastudio/hermes-agent/data/services/always-on/services.json` の `services` 配列へ次を足す（`services.json` はループ毎に再読込されるので supervisor の再起動は不要）。
supervisor.py の `run_once` はシェルを介さず `subprocess.run(cmd, cwd=cwd, ...)` を呼ぶため `~` は展開されない。既存の `repo-auto-pull` エントリ（`services.json` 実物）と同じく **絶対パスで書く**:

```json
{
  "name": "playtest-ingest",
  "kind": "periodic",
  "interval_seconds": 300,
  "timeout_seconds": 600,
  "cwd": "/Users/sakastudio/hermes-agent/data/repos/moorestech",
  "command": ["/bin/bash", "/Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/ingest-dispatch.sh"]
}
```

**必ず本体 clone（`/Users/sakastudio/hermes-agent/data/repos/moorestech`）のスクリプトを指すこと。** `moores-wt` が切るタスク用worktree（`moorestech-worktrees/<name>`）は兄弟パス（`../moorestech_logs`・`../../services/...`）が本体と異なる位置にずれるため、そこを指すと壊れる。

`ingest.sh`・`ingest-dispatch.sh`・`enqueue-autofix.sh`・`allowlist.sh`・`digest.py` の既定パスはいずれも **`$HOME` を経由せずスクリプト自身の位置から導出**する（supervisor は HOME を封じ込め用ディレクトリへ差し替えるため）。既定を上書きしたい場合は `services.json` の `command` に環境変数を渡すか、`env.sh` で export すればよい（`MOORESTECH_REPO`・`MOORESTECH_LOGS`・`PLAYTEST_ENV_FILE`・`PLAYTEST_INGEST_LOG`）。

確認（periodic の実行は `supervisor.log` には出ない。サービス個別のログを見る）:

```bash
tail -f /Users/sakastudio/hermes-agent/data/services/always-on/logs/playtest-ingest.log        # "run reason=periodic" マーカー
tail -f /Users/sakastudio/hermes-agent/data/services/always-on/logs/playtest-ingest-worker.log # "[ingest] ..." 本体
```

## 手動実行

```bash
. /Users/sakastudio/hermes-agent/data/services/playtest/env.sh
bash /Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/ingest.sh
```

ロックは logs repo の `.git/moorestech-playtest-ingest.lock`（PID 入り。`$TMPDIR` に依らないので supervisor 配下と手動実行でも排他が効き、`git add` にも掴まれない）。前回のプロセスが生きていれば何もせず終わり、死んでいれば（SIGKILL・OOM・再起動等で trap が走らず残った場合）理由をログして奪取する。

## 成果物

- `moorestech_logs/harness/playtest/{reports,progress}/<steamId>/<id>/`
- **自動修正ランへの投入はここでは起きない**（ADR 0061）。投入は日次ダイジェストを見て人が `scripts/playtest/enqueue-autofix.sh <steamId> <id>` を叩く

## 自動修正ランへ投入する（人の操作）

テスターのバグ報告は**自動では走らない**（ADR 0061）。毎朝の日次ダイジェストの「投入候補のバグ報告」節に
そのまま貼れるコマンドが並ぶので、直したいものだけ選んで叩く。

```bash
bash /Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/enqueue-autofix.sh <steamId> <id>
```

- 投入すると `moorestech_logs/harness/bug-report/inbox/<id>/` に置かれ、plan C の `inbox-poller.sh` が最大60秒で拾う
- 二度目は `exit 2`（箱の `AUTOFIX_QUEUED` マーカーで判定）。やり直すならマーカーを消す
- `harness/bug-report/runs/<id>` が既にあると `exit 5`（poller が `.duplicate` へ隔離してランを起こさないため）。やり直すなら旧ランを改名・退避してからマーカーを消す
- 元箱に `READY` が無い（取り込みが完結していない）箱、steamId/id が安全な単一パスセグメントでない（空・`.`・`..`・`/`・`\`・制御文字）引数は `exit 1` で拒否する
- 感想・クラッシュは `exit 3` で拒否。どうしても走らせるなら `--force`（inbox 側に `AUTOFIX_FORCED` が付き、poller の種別ガードを通る）
- 結果は `moorestech_logs/harness/bug-report/runs/<id>/fix-result.json`、翌朝のダイジェストの「自動修正ラン」節にも出る

## 詰まったとき

- `[ingest] ERROR: READY 本文の files[] が…` → READY 要約（クライアントの `PlaytestUploader.ComposeSummary()` が書き、受け口は転写するだけ）に `files[]` が無い。`files` を書く前の古い配布ビルドからの箱。ack されずに残るので理由を確認して扱いを決める
- `[ingest] ERROR: ack 失敗` → 箱は取り込み済みなので、次の周期で再配信されても再ダウンロードせず ack だけやり直す

## 日次ダイジェスト

Hermes 内蔵 cron で毎朝9時に投稿する。手順は `scripts/playtest/hermes-cron/README.md`。

- 手動確認: `/usr/bin/python3 /Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/digest.py --date yesterday`（logs の既定はスクリプト位置から導出。別の場所なら `MOORESTECH_LOGS`）
- 全文は `moorestech_logs/harness/playtest/digests/<日付>.md`。次の取り込み周期（最大5分）で logs repo へ commit される
