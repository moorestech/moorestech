# プレイテストの取り込み（Mac mini側）

## 前提

- `/Users/sakastudio/hermes-agent/data/repos/{moorestech,moorestech_logs,moorestech_playtest_raw}` がある（`moorestech_playtest_raw` は git 管理外のただのディレクトリ。無いと取り込みは受け口に触らず止まるので先に `mkdir` する）
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

`ingest.sh`・`ingest-dispatch.sh`・`enqueue-autofix.sh`・`digest.py` の既定パスはいずれも **`$HOME` を経由せずスクリプト自身の位置から導出**する（supervisor は HOME を封じ込め用ディレクトリへ差し替えるため）。既定を上書きしたい場合は `services.json` の `command` に環境変数を渡すか、`env.sh` で export すればよい（`MOORESTECH_REPO`・`MOORESTECH_LOGS`・`MOORESTECH_PLAYTEST_RAW`・`PLAYTEST_RAW_RETENTION_DAYS`・`PLAYTEST_ENV_FILE`・`PLAYTEST_INGEST_LOG`）。

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

ロックは logs repo の `.git/moorestech-playtest-ingest.lock`（PID 入り。`$TMPDIR` に依らないので supervisor 配下と手動実行でも排他が効き、`git add` にも掴まれない）。PID を書き終えたディレクトリを rename で置くので、PID の無いロックは公開されない。前回のプロセスが生きていれば何もせず終わり、死んでいれば（SIGKILL・OOM・再起動等で trap が走らず残った場合）理由をログして奪取する。奪取は `.lock.reclaim` の下で直列化され、奪取中に死んだ残骸はその回で退避だけして次回に奪取する。

## 成果物

- `moorestech_logs/harness/playtest/{reports,progress}/<steamId>/<id>/` … 報告を読むのに要る小さな物だけ（`manifest.json`・`READY`・`ingest.json`・`frames.tsv`・`screenshot.png`・`logs/`・`repo/*.diff` 等・`record.json`）と、生データの保存先と期限を書いた `raw.json`
- `moorestech_playtest_raw/{reports,progress}/<steamId>/<id>/` … 生データ（`snapshots/`・`world/`・`video.mp4`・`frames/`・`recording/`・`crashDumps/`・`__attachments/`・`repo/*.bundle`・`repo/untracked/` 等。許可リストに無い物と 10MB 超の物はすべてこちら）。同じ `raw.json` を置く

## 生データ置き場（logs repo の外・期限つき）

logs repo は「会話とコミットを結んで、なぜそう作ったかを辿る」ための git repo で、置いた物は 5 分ごとの自動コミットで永久履歴になる。そのため tick スナップショットやワールドなどの生データは logs repo に入れず、git 管理外の `MOORESTECH_PLAYTEST_RAW`（既定は本体 clone の兄弟 `../moorestech_playtest_raw`）へ箱と同じ相対パスで置く。どれを残すかは `raw_store/raw_layout.py` の許可リストが正。

- 期限は取り込み（`ingestedAt`）から `PLAYTEST_RAW_RETENTION_DAYS` 日（既定 60）。取り込み時に `raw.json` の `expiresAt` へ確定する
- 期限削除: `python3 scripts/playtest/raw_store/prune.py [-n]`。消した物・消せなかった物だけを stdout に出し、何もしなかった回は無言（Hermes の no-agent cron で Discord へ流す。登録は `hermes-cron/README.md`）。`-n` は判定だけ。logs 側の `raw.json` は残る
- 読む側: `enqueue-autofix.sh` は生データを inbox の箱へ複写してから投入する。期限切れ・欠落なら理由を出して `exit 7` で投入しない。`find-reports.sh` は `raw=<状態>` 列と理由行、ダッシュボードは報告画面の警告で示す。状態は `none`（raw.json が無い旧い箱か生データの無い箱）・`ok`・`expired`（期限後に削除済み）・`missing`（期限前なのに欠落＝事故）・`broken`（raw.json を読めない）
- 既存の箱の移行（一回きり）: `bash scripts/playtest/raw_store/migrate.sh -n` で一覧と容量を確かめ、`bash scripts/playtest/raw_store/migrate.sh` で実行する。箱ごとに生データ置き場へ複写して sha256 で突き合わせてから、logs 側の git 管理のファイルを `git rm`、git 管理外（動画・連番等）を削除し、最後に `harness/playtest` だけを commit する（push はしない。logs-sync か手動で push）。取り込みと同じロックを取る。中断しても再実行で続きから進む。`ingest.json` の無いディレクトリ（エージェントが置いた調査メモ等）は一覧に出すだけで触らない。git の過去の履歴は書き換えないので、remote の容量は減らない
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
- 感想・クラッシュは `exit 3`、遠隔実行印のある箱は `exit 6` で拒否。どうしても走らせるなら `--force`（inbox 側に理由を含む `AUTOFIX_FORCED` が付き、poller の種別ガードを通る）
- 生データ（スナップショット・ワールド等）は生データ置き場から inbox の箱へ複写される。期限切れ・欠落なら `exit 7` で投入しない（理由が出る）
- 結果は `moorestech_logs/harness/bug-report/runs/<id>/fix-result.json`、翌朝のダイジェストの「自動修正ラン」節にも出る

## 詰まったとき

- `[ingest] ERROR: READY 本文の files[] が…` → READY 要約（クライアントの `PlaytestUploader.ComposeSummary()` が書き、受け口は転写するだけ）に `files[]` が無い。`files` を書く前の古い配布ビルドからの箱。ack されずに残るので理由を確認して扱いを決める
- `[ingest] ERROR: READY 本文の files[] が取り込み側の予約名…と衝突` → payload に `ingest.json`・`READY`・`AUTOFIX_QUEUED`・`AUTOFIX_FORCED`・`fix-result.json`・`run.env`・`raw.json`（大文字小文字無視）や `*.partial` 始まりのパスがある。マーカー偽装・上書きになるので ack しない
- `[ingest] [WARN] files[] が空（全ファイル見送り…）` → クライアントが全ファイルを見送った正規の箱。READY と ingest.json だけで公開して ack する。ダイジェストでは「manifest.json/record.json が無い箱」の件数に出る
- `[ingest] ERROR: 生データ置き場が無い` → `MOORESTECH_PLAYTEST_RAW`（既定 `../moorestech_playtest_raw`）が無い。受け口には触らず止まっている。`mkdir` するか env で指す
- `[enqueue] ERROR: 生データを揃えられないので投入しない（expired …）` → 期限（取り込みから 60 日）を過ぎて生データが消えた箱。再現材料が無いので投入できない。`missing` は期限前の欠落（置き場の移動・手動削除など）なので原因を調べる
- `[ingest] ERROR: ack 失敗` → 箱は取り込み済みなので、次の周期で再配信されても再ダウンロードせず ack だけやり直す

## 日次ダイジェスト

Hermes 内蔵 cron で毎朝9時に投稿する。手順は `scripts/playtest/hermes-cron/README.md`。

- 手動確認: `/usr/bin/python3 /Users/sakastudio/hermes-agent/data/repos/moorestech/scripts/playtest/digest.py --date yesterday`（logs の既定はスクリプト位置から導出。別の場所なら `MOORESTECH_LOGS`）
- 全文は `moorestech_logs/harness/playtest/digests/<日付>.md`。次の取り込み周期（最大5分）で logs repo へ commit される

## 開発者トークン

管理キーを設定した環境で次を実行する。機材名は小文字英数字で始まる1〜32文字の小文字英数字・ハイフン。

```bash
bash scripts/playtest/dev-token.sh issue macbook
bash scripts/playtest/dev-token.sh list
bash scripts/playtest/dev-token.sh revoke macbook
```

`issue` の標準出力はトークン1行だけ。Unity の `moorestech/Playtest/Developer Token` に貼り付けて保存する。生トークンをログ・repoへ残さない。`list` は `machine<TAB>createdAt`、`revoke` は該当機材の送信を即時失効する。すべて成功0・失敗1。

開発機の報告は `reports/dev-<機材名>/<id>/` に取り込まれる。Steamには問い合わせず「開発機 <機材名>」を表示名にし、ダイジェストにも表示する。自動修正への投入はテスターと同じく人が選ぶ。
