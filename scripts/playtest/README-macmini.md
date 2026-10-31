# プレイテストの取り込み（Mac mini側）

## 前提

- 本体 clone と、その兄弟の `moorestech_logs` がある
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

`ingest.sh`・`ingest-dispatch.sh`・`fetch-report.sh`・`digest.py` の既定パスはいずれも **`$HOME` を経由せずスクリプト自身の位置から導出**する（supervisor は HOME を封じ込め用ディレクトリへ差し替えるため）。既定を上書きしたい場合は `services.json` の `command` に環境変数を渡すか、`env.sh` で export すればよい（`MOORESTECH_REPO`・`MOORESTECH_LOGS`・`PLAYTEST_ENV_FILE`・`PLAYTEST_INGEST_LOG`）。

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

- 生データの正本は受け口の R2（バケット `moorestech-playtest`、`reports/<steamId>/<id>/...`・`progress/...`）だけ。取り込みは手元へ全件をコピーしない
- `moorestech_logs/harness/playtest/{reports,progress}/<steamId>/<id>/` … 報告を読むのに要る小物だけ（`manifest.json`・`READY`・`ingest.json`・`frames.tsv`・`screenshot.png`・`logs/`・`repo/*.diff|*.txt|*.json`・`record.json`・`export-markers/`。10MB 超は除く）と、R2 にだけ置いた物の一覧 `remote.json`。どれを写すかは `r2_store/box_layout.py` の許可リストが正

## 1 件の生データを取る（バグ調査）

```bash
bash scripts/playtest/r2_store/fetch-report.sh <steamId または dev-機材名> <報告ID>    # 本体 clone で実行する
bash scripts/playtest/r2_store/fetch-report.sh --out <置き場> --kind progress <steamId> <id>   # 置き場・種別を指定する
```

- READY の `files[]` を全部 R2 から取り、`__attachments/` の断片から bundle を復元して、取得先を stdout に 1 行出す（既定 `$TMPDIR/moorestech-playtest-fetch/<kind>/<steamId>/<id>`。logs repo には置かない）
- 取得先が既にあれば止まる。片付けで R2 から消した報告は理由（purgedAt）を出して `exit 3`
- 取った箱は `python3 scripts/bugreport/triage.py <箱> --out <箱の外>` で Unity 無しに読める。サーバー状態の再生は Editor の `uloop execute-dynamic-code` から `Server.Boot.Replay.BugReportBundleTools`（`ReplayCheck`・`DumpPackets`・`MaterializeWorld`）を 1 行呼ぶ

## 片付いた報告の R2 削除（片付けから 7 日）

- `bash scripts/playtest/r2_store/prune.sh` で削除対象の一覧だけを出す。確かめてから `--execute` で消す（受け口の `DELETE /v1/inbox/<kind>/<steamId>/<id>`。`ACKED` と `PURGED` の印だけが残り、以後の取得は 410）
- 対象は `moorestech_logs/.state/report-resolutions.json`（git 管理外）の `{"resolutions": {"<kind>/<steamId>/<id>": {"outcome": "merged"|"rejected", "resolvedAt": "YYYY-MM-DDTHH:MM:SSZ"}}}` で、`resolvedAt` から 7 日たった物。消した物は `.state/r2-purges.json` に残り、次回は選ばない
- **この記録の書き手はまだ無い**（「片付いた」をどう判定するかは未決）。書き手が無い間は削除対象が出ない。定期実行（Hermes cron）も書き手が決まってから登録する
- R2 のライフサイクル規則は途中のアップロードの中止（7 日）だけ。日数で一律に消す規則は足さない（未片付けの報告まで消えるため）

## zip で届いた報告の取り込み

`bash scripts/playtest/import-export-zip.sh <zip>`。箱ごとに受け口の `import` で署名付き URL をもらい、全ファイルを R2 へ PUT してから、小物だけを logs へ置き、最後に受け口の `import/complete` で確定する（受け口が全ファイルの存在と長さを確かめてから `ACKED` を書く）。R2 に長さの違う同名ファイルがある・PUT に失敗した・確定できなかった箱は取り込まない（手元の箱も戻し、理由を出して exit 1。再実行でやり直せる）。PUT の制限時間は `RECEIVER_MAX_TIME`（既定120秒）に大きさ÷`R2_PUT_MIN_BYTES_PER_SEC`（既定 1000000）秒を足した値。

## 詰まったとき

- `[ingest] ERROR: READY 本文の files[] が…` → READY 要約（クライアントの `PlaytestUploader.ComposeSummary()` が書き、受け口は転写するだけ）に `files[]` が無い。`files` を書く前の古い配布ビルドからの箱。ack されずに残るので理由を確認して扱いを決める
- `[ingest] ERROR: READY 本文の files[] が取り込み側の予約名…と衝突` → payload に `ingest.json`・`READY`・`remote.json`（大文字小文字無視）や `*.partial` 始まりのパスがある。上書き・所在の偽装になるので ack しない
- `[ingest] [WARN] files[] が空（全ファイル見送り…）` → クライアントが全ファイルを見送った正規の箱。READY と ingest.json だけで公開して ack する。ダイジェストでは「manifest.json/record.json が無い箱」の件数に出る
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

開発機の報告は `reports/dev-<機材名>/<id>/` に取り込まれる。Steamには問い合わせず「開発機 <機材名>」を表示名にし、ダイジェストにも表示する。
