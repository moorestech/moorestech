# プレイテスト H: 受け口からの取り込みと日次ダイジェスト Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Mac mini が受け口（Cloudflare Worker + R2）の inbox を5分ごとに取り込んで `moorestech_logs/harness/playtest/` に保存し、Hermes 内蔵 cron が1日1回、前日分の感想全文・進行記録の集計・自動修正ラン結果・クラッシュ件数・**投入候補のバグ報告一覧**を Discord へ投稿し、人がその一覧を見て選んだものだけを手動コマンドで plan C の自動修正ラン inbox へ投入する（ADR 0061 の「取り込み」「日次ダイジェスト」「テスター報告は自動投入しない」）。

**Architecture:** (1) 取り込みは2段。supervisor の periodic（300秒・timeout 600秒）が `scripts/playtest/ingest-dispatch.sh` を呼び、それが `nohup` で worker `scripts/playtest/ingest.sh` を切り離す（`repo-auto-pull` と同型。periodic はメインループ内で同期実行されるため、ダウンロードの長さでトンネル再起動や死活監視を止めない）。worker は単一飛行ロックを持ち、`GET /v1/inbox` をページングして各件を `harness/playtest/{reports|progress}/<steamId>/<id>/` へ `.partial`→`mv` でアトミックに置き、成功したものだけ ack して logs repo を commit/push する。**自動修正ランへの投入はしない**（裁定 2026-09-13）。(2) 集計は Python3 標準ライブラリのみの `scripts/playtest/digest.py`（読み取り・整形）＋ `digest_collect.py`（走査・集計）。前日（JST）分を Markdown で標準出力へ出し、未投入のバグ報告には `scripts/playtest/enqueue-autofix.sh <steamId> <id>` のコマンドをそのまま貼れる形で並べる。全文は `harness/playtest/digests/<date>.md` へ残す。(3) 投入は人が叩く `scripts/playtest/enqueue-autofix.sh`。箱を plan C の `harness/bug-report/inbox/<id>/`（`READY` 付き）へ `.partial`→`mv` で複製し、箱側に `AUTOFIX_QUEUED` マーカーを書いて二重投入を防ぐ。(4) Hermes 内蔵 cron は `--no-agent` で digest を毎日走らせ、stdout をそのまま Discord へ投げる。`--script` は `HERMES_HOME/scripts/` の外を参照できずシンボリックリンクも弾かれるため、実ファイルの shim を1枚置いて repo 側の本体を exec する（既存 `monitors/tiktok-collector-monitor/check.sh` と同じ形）。

**Tech Stack:** bash（curl・git・nohup・always-on supervisor の periodic）、Python 3 標準ライブラリ（`argparse`・`json`・`pathlib`・`collections.Counter`・`datetime`・`unittest`）、Hermes 内蔵 cron（`hermes cron create --script --no-agent --deliver discord:<id>`）、`gh` CLI。

## Requirements

- R1. logs repo レイアウト: `../moorestech_logs` に `harness/playtest/{reports,progress,digests}/` と `harness/playtest/README.md` を追加し、`.gitignore` で動画・連番フレーム・録画リング・取り込み途中の `.partial` を除外する。`README.md` のレイアウト表に1行足す。ベースブランチは `main`（`master` ではない）。受入: PR が `origin/main` 宛で存在する。
- R2. 受け口 admin API ラッパ: `scripts/playtest/lib/receiver-api.sh` が `receiver_inbox_page`・`receiver_get_object`・`receiver_ack` を提供し、`X-Admin-Key` を付ける。`CURL_CMD` で curl を差し替えられる。admin key は**いかなる経路でも標準出力・ログに出さない**。受入: スタブ curl を使うテストで3関数が期待の URL を叩く。
- R3. 取り込み worker: `scripts/playtest/ingest.sh` が `GET /v1/inbox` を `cursor` が空になるまで（最大 `PLAYTEST_INGEST_MAX_PAGES`=20 ページ）辿り、各件について `READY` を取得 → 本文の要約 JSON の `files[]` に列挙されたパスだけを `GET /v1/inbox/{kind}/{steamId}/{id}/{path}` で落とし → `harness/playtest/{reports|progress}/<steamId>/<id>.partial/` に貯めて `mv` で公開 → `ingest.json`（`kind`・`steamId`・`id`・`readyAt`・`ingestedAt`）を添える → ack する。1回の実行で扱うのは `PLAYTEST_INGEST_MAX_ITEMS`=50 件まで、残りは次回。受入: スタブ curl のテストで report/progress の箱が正しい場所に置かれ、`ingest.json` があり、ack が記録される。
- R4. 手動投入コマンド: `scripts/playtest/enqueue-autofix.sh <steamId> <id>` が、取り込み済みの箱を `harness/bug-report/inbox/<id>.partial/` へ複製し `READY` を確かめてから `mv` し、箱に `AUTOFIX_QUEUED` マーカー（本文は `queued at <ISO8601>`）を書く。**取り込み（`ingest.sh`）は自動投入しない**（ADR 0061 の裁定）。`manifest.kind != "bug"` の箱は既定で拒否し、`--force` を付けたときだけ理由をログに出して通す。マーカーが既にある箱は「投入済み」として拒否する（二重投入防止）。箱が無い・`manifest.json` が読めない場合は非0で終わる。受入: テストで bug の箱が `bug-report/inbox/<id>/READY` になり、2回目は拒否され、feedback の箱は `--force` 無しで拒否される。
- R5. 失敗時の挙動: `READY` 本文に `files[]` が無い／パスに `..` や先頭 `/` を含む箱は、`.partial` を消し **ack せず** `[ingest] ERROR` を出して次の箱へ進む（1件の異常で全体を止めない）。ダウンロード失敗・ack 失敗も同じく次回に持ち越す。`GET /v1/inbox` 自体が失敗したらその回は何もせず終了する。受入: `files[]` 無しの箱を混ぜたテストで、その箱だけ未 ack・未配置になり、他の箱は正常に取り込まれる。
- R6. supervisor 登録: `scripts/playtest/ingest-dispatch.sh` が worker を `nohup` で切り離し、即座に戻る。`services.json` へ `kind: periodic`・`interval_seconds: 300`・`timeout_seconds: 600` で登録する手順を `scripts/playtest/README-macmini.md` に書く。受入: `logs/playtest-ingest.log` に `run reason=periodic` マーカーが出て、`logs/playtest-ingest-worker.log` に `[ingest]` 行が出る。
- R7. 日次ダイジェスト: `scripts/playtest/digest.py`（標準ライブラリのみ）が前日（JST）分について (a) プレイ報告の件数（バグ／感想／クラッシュ）、(b) 感想の**全文**、(c) 進行記録の集計＝人数・セッション数・平均プレイ時間・平均研究完了数・到達チャレンジ数の分布・離脱時の最後のイベント上位・離脱時の UI 状態上位・終了理由、(d) 自動修正ラン結果（`harness/bug-report/runs/<id>/fix-result.json` の status・PR番号・base・summary）、(e) **投入候補のバグ報告一覧**＝`manifest.kind == "bug"` かつ `AUTOFIX_QUEUED` の無い箱について、説明文の先頭1行と `scripts/playtest/enqueue-autofix.sh <steamId> <id>` をそのまま貼れる形で並べる、を Markdown で標準出力へ出し、全文を `harness/playtest/digests/<date>.md` へ書く。該当0件の日も見出しと「なし」を出す（沈黙しない）。受入: fixture を並べた `unittest` が全セクションの文字列と enqueue コマンド行を検証する。
- R8. Discord 1メッセージ長対策: `--max-chars`（既定1800）を超えたら標準出力を切り、切った旨と全文の置き場（`digests/<date>.md`）を必ず末尾に示す。受入: 長い感想を入れた fixture で、切り詰め後の出力に置き場のパスが含まれる。
- R9. Hermes cron ジョブ: `scripts/playtest/hermes-cron/moorestech-playtest-digest.sh` を `HERMES_HOME/scripts/` へ**コピー**（symlink 不可）し、`hermes cron create` で `--script`・`--no-agent`・`--deliver discord:<チャンネルID>` のジョブを1件作る手順を README に書く。チャンネルIDと admin key は `~/hermes-agent/data/services/playtest/env.sh` に置き、plan・repo には値を書かない。受入: `hermes cron list` に当該ジョブが出て、`hermes cron run <id>` の1回実行で Discord に投稿される。
- R10. plan A/B/C の改訂メモ: ADR 0061 で変わる点（`manifest.kind`／`steamId`／`buildInfo`、テスター報告は自動投入されず poller が見るのは開発者 rsync 経路と手動投入分のみ、`kind=crash` は自動修正ランを起動せず digest に載せる、`fix-result.json` に `finishedAt` を足す、logs repo のベースは `main`）を、各 plan ファイル先頭のコントローラ用ブロック直後に `> **改訂（ADR 0061 / 2026-09-13）**` として追記する。受入: 3ファイルに改訂ブロックがあり、本文の該当箇所と矛盾しない。
- R11. 最終レビュー: moores-code-review スキルで全ブランチレビューを実行する（省略不可）。
- やらないこと: 受け口本体の実装（plan D）／ゲーム側の送信・進行記録の生成（plan G）／閲覧ダッシュボード（ADR 0061 で棄却）／許可リスト操作 `allowlist.sh`（plan D）／配布ビルドと検証機（plan E）／セーブ互換（plan F）／取り込んだ動画の logs repo への push（容量のため除外し Mac mini のディスクにだけ残す）／**テスター報告の自動修正ランへの自動投入**（裁定 2026-09-13。投入は人が `enqueue-autofix.sh` を叩く）／ACK 済み分の再照会（`GET /v1/inbox?includeAcked=true` は plan E が plan D へ足す予定。本plan は未ACK前提のまま）。

## Global Constraints

### 共有契約 §4（受け口 Worker API）— 逐語転記・変更禁止

> - `POST /v1/session` body `{"ticket":"<hex>"}` → Steam Web API `ISteamUserAuth/AuthenticateUserTicket/v1`（`identity=moorestech-playtest`）で検証 → `{ "steamId": "...", "allowed": true, "token": "<HMAC-JWT 1h>" }`。不許可は 403 `{ "reason": "not-allowed" }`、検証失敗は 401。
> - `PUT /v1/uploads/{kind}/{id}/{path...}`（Bearer token、`kind` は report|progress、1ファイル ≤ 100MB）→ R2 `{kind}s/{steamId}/{id}/{path}` へ保存。
> - `POST /v1/uploads/{kind}/{id}/complete` → R2 に `{kind}s/{steamId}/{id}/READY`（本文 = manifest の要約 JSON）。
> - `GET /v1/inbox?cursor=<opaque>`（`X-Admin-Key` ヘッダ）→ `{ "items": [ { "kind","steamId","id","readyAt" } ], "cursor": "..." }`（READY 済みで未ACKのもの）。
> - `GET /v1/inbox/{kind}/{steamId}/{id}/{path...}`（admin）→ R2 オブジェクト。
> - `POST /v1/inbox/{kind}/{steamId}/{id}/ack`（admin）→ R2 `.../ACKED` を書く。
> - 許可リスト: R2 `config/allowlist.json` = `{ "steamIds": ["..."] }`。Mac mini 側の `scripts/playtest/allowlist.sh add|remove|list <steamId>` が admin API `PUT /v1/allowlist` で更新。
> - Secrets（wrangler secret）: `STEAM_WEB_API_KEY`（publisher key）、`SESSION_HMAC_SECRET`、`ADMIN_KEY`。`STEAM_APP_ID=1958160` は vars。
>
> base は `https://playtest.moores.tech`、実装は本repo `tools/playtest-receiver/`、TypeScript + wrangler、R2 バケット `moorestech-playtest`。

### 共有契約 §6（Mac mini 取り込み）— 逐語転記・変更禁止

> - `scripts/playtest/ingest.sh`（always-on supervisor periodic 300s、timeout 600s、`nohup` で worker 切り離し = repo-auto-pull 型）: `GET /v1/inbox` → 各件を `moorestech_logs/harness/playtest/{reports|progress}/<steamId>/<id>/` へダウンロード → `kind=bug` のプレイ報告だけ plan C の `inbox/<id>/`（READY 付き）へも複製 → ack。
> - 日次ダイジェスト: Hermes 内蔵 cron（`hermes cron`）ジョブ1件。集計スクリプト `scripts/playtest/digest.py` が前日分を集計し Markdown を出し、Hermes が Discord へ投稿。

### §2 のうち本 plan が読むフィールド（逐語）

> プレイ報告: `<GameSystemDirectory>/BugReports/outbox/<id>/` … plan B のファイル群 + `manifest.json` に追加フィールド `kind`（"bug"|"feedback"|"crash"）、`steamId`（string、Steam未起動なら ""）、`buildInfo`（§1、Editorなら null）。
> 進行記録: `<GameSystemDirectory>/ProgressRecords/outbox/<id>/record.json`。

### 実装規約

- 作業ブランチ: `feature/playtest-ingest-digest`（`origin/master` から）。`../moorestech_logs` の変更は同名ブランチを **`origin/main` から**切って push し PR を作る（AGENTS.md「別リポジトリも push して PR」。logs repo の既定ブランチは `main`）。
- Mac mini 固有パスは全て環境変数で受け、既定値を実配置にする: `MOORESTECH_REPO`＝`~/hermes-agent/data/repos/moorestech`、`MOORESTECH_LOGS`＝`~/hermes-agent/data/repos/moorestech_logs`、`PLAYTEST_RECEIVER_BASE`＝`https://playtest.moores.tech`、`PLAYTEST_ENV_FILE`＝`~/hermes-agent/data/services/playtest/env.sh`。テストは全て一時ディレクトリへ差し替えて行う。
- シェルは `#!/usr/bin/env bash` + `set -euo pipefail`。外部コマンドは変数（`CURL_CMD`・`GIT_CMD`）で差し替え可能にする。無音の失敗禁止（必ず `echo "[ingest] ..." >&2`）。
- **秘密値の扱い**: `PLAYTEST_ADMIN_KEY` と Discord チャンネルIDは `~/hermes-agent/data/services/playtest/env.sh`（`chmod 600`）にのみ置く。スクリプト・plan・README・コミットメッセージ・ログに値を書かない。`set -x` を使わない。
- コメント規約（日本語1行 → 英語1行の2行セット、3〜10行ごと）はシェル・Python にも適用。1ファイル200行以下、1ディレクトリ10ファイルまで。
- Python は標準ライブラリのみ（Mac mini の system python3 で動くこと）。`try-catch` は外部境界（外部プロセス・ネットワーク・外部 JSON のパース）に限り、根拠をコメントに書く。
- 各タスク末尾でコミット。コミットメッセージ末尾に以下を付ける:
  ```
  Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01HtaDWGdhRRiNho39vPY5Li
  ```

---

### Task 1: logs repo に playtest レイアウトを足す（別repo・PR必須）

**Files:**
- Create（`../moorestech_logs`）: `harness/playtest/README.md`・`harness/playtest/reports/.gitkeep`・`harness/playtest/progress/.gitkeep`・`harness/playtest/digests/.gitkeep`
- Modify（`../moorestech_logs`）: `.gitignore`・`README.md`

**Interfaces:**
- Produces: ディレクトリ `harness/playtest/{reports,progress,digests}/`。Task 3 の `ingest.sh` と Task 4 の `digest.py` がこの配置を前提にする。

- [ ] **Step 1: ブランチを切って追加する**

```bash
cd ../moorestech_logs && git fetch origin && git checkout -b feature/playtest-ingest origin/main
mkdir -p harness/playtest/reports harness/playtest/progress harness/playtest/digests
touch harness/playtest/reports/.gitkeep harness/playtest/progress/.gitkeep harness/playtest/digests/.gitkeep
```

- [ ] **Step 2: `.gitignore` に容量除外を足す**

`../moorestech_logs/.gitignore` の末尾に追記:
```
# プレイテスト取り込み: 動画・連番フレーム・録画リングと取り込み途中の箱は容量のため除外
# Playtest ingest: videos, frame sequences, recording rings and half-downloaded boxes are excluded for size
harness/playtest/reports/*/*/video.mp4
harness/playtest/reports/*/*/frames/
harness/playtest/reports/*/*/recording/
harness/playtest/reports/*/*.partial/
harness/playtest/progress/*/*.partial/
```

- [ ] **Step 3: README を書く**

`../moorestech_logs/harness/playtest/README.md`:
```markdown
# playtest

ADR 0061（moorestech `docs/adr/0061-steam-closed-playtest-report-receiver-and-save-compat.md`）の取り込み先。
書き手は moorestech `scripts/playtest/ingest.sh`（Mac mini の always-on supervisor が5分ごとに起動）。

- `reports/<steamId>/<id>/` — プレイ報告1件。受け口から落とした `manifest.json`・スナップショット・パケットログ・ログ・スクリーンショットと、取り込み側が書く `ingest.json`。動画と連番フレームは `.gitignore` で除外（Mac mini のディスクにだけ残る）
- `progress/<steamId>/<id>/` — 進行記録1件（`record.json` と `ingest.json`）
- `digests/<YYYY-MM-DD>.md` — 日次ダイジェストの全文。Discord へは先頭 1800 文字だけ出るので、切れた分はここを読む
- `ingest.json` — `{"kind","steamId","id","readyAt","ingestedAt"}`。日次ダイジェストの日付判定は `readyAt`（受け口が付ける）を使う
- `AUTOFIX_QUEUED` — 人が `scripts/playtest/enqueue-autofix.sh` で自動修正ランへ投入したときだけ付く二重投入防止マーカー。**取り込みは自動投入しない**（ADR 0061）
- 閲覧は本人とエージェントのみ。公開PRにはパスと説明だけを書く
```

`../moorestech_logs/README.md` のレイアウト表（```ブロック内）に1行追加:
```
harness/playtest/                       # プレイテストの取り込み(reports/・progress/)と日次ダイジェスト(digests/)
```

- [ ] **Step 4: push して PR を作る**

```bash
cd ../moorestech_logs
git add -A harness/playtest .gitignore README.md
git commit -m "harness: playtest の reports/progress/digests レイアウトと容量除外"
git push -u origin feature/playtest-ingest
gh pr create --base main --title "harness: playtest の取り込みレイアウト" \
  --body "moorestech ADR 0061 / plan H。受け口から取り込んだプレイ報告・進行記録・日次ダイジェストの置き場と容量除外。"
```
Expected: PR URL が出る。URL を本repoの `## 判断記録（ADR）` に書き足す。

---

### Task 2: 受け口 admin API ラッパと取り込み worker

**Files:**
- Create: `scripts/playtest/lib/receiver-api.sh`
- Create: `scripts/playtest/ingest.sh`
- Create: `scripts/playtest/ingest-dispatch.sh`
- Create: `scripts/playtest/README-macmini.md`
- Test: `scripts/playtest/tests/test-ingest.sh`

**Interfaces:**
- Consumes: 受け口 admin API（Global Constraints §4）
- Produces:
  - `harness/playtest/reports/<steamId>/<id>/`・`harness/playtest/progress/<steamId>/<id>/`（Task 3 の `enqueue-autofix.sh` と Task 4 の `digest_collect` が読む）
  - 各箱の `ingest.json` = `{"kind":"report|progress","steamId":"<string>","id":"<string>","readyAt":"<ISO8601>","ingestedAt":"<ISO8601>"}`（Task 4 の `digest_collect.collect_boxes` がこの5キーを読む）
  - シェル関数 `receiver_inbox_page <cursor> <out>`・`receiver_get_object <kind> <steamId> <id> <path> <out>`・`receiver_ack <kind> <steamId> <id>`（Task 3 は使わない。取り込み専用）
- **Produces しないもの**: `harness/bug-report/inbox/` への複製と `AUTOFIX_QUEUED` マーカー。どちらも Task 3 の手動コマンドの責務（ADR 0061「テスターからのバグ報告は自動修正ランへ自動投入しない」）

- [ ] **Step 1: 失敗するテストを書く**

`scripts/playtest/tests/test-ingest.sh`:
```bash
#!/usr/bin/env bash
# curl を偽の受け口に差し替えて、取り込み・ack・異常箱の据え置き・自動投入しないことを検証する
# Verifies ingest, ack, the un-acked broken box and the absence of auto-enqueue, with curl stubbed
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT

LOGS="$TMP/logs"; R2="$TMP/r2"
mkdir -p "$LOGS/harness/playtest" "$LOGS/harness/bug-report/inbox" "$R2/objects"
( cd "$LOGS" && git init -q && git config user.email t@t && git config user.name t \
  && echo x > .gitkeep && git add -A && git commit -qm init )

# 偽の R2: バグ報告・感想・進行記録・files[] が壊れた箱の4件
# Fake R2 with four boxes: a bug report, a feedback report, a progress record and a broken one
mk_object() { mkdir -p "$(dirname "$R2/objects/$1")"; printf '%s' "$2" > "$R2/objects/$1"; }
mk_object report/7656001/20260913_100000_bug1/READY '{"kind":"bug","files":["manifest.json","screenshot.png"]}'
mk_object report/7656001/20260913_100000_bug1/manifest.json '{"kind":"bug","steamId":"7656001","description":"ベルトが止まる"}'
mk_object report/7656001/20260913_100000_bug1/screenshot.png 'PNG'
mk_object report/7656002/20260913_110000_fb1/READY '{"kind":"feedback","files":["manifest.json"]}'
mk_object report/7656002/20260913_110000_fb1/manifest.json '{"kind":"feedback","steamId":"7656002","description":"序盤が長い"}'
mk_object progress/7656001/20260913_120000_pg1/READY '{"files":["record.json"]}'
mk_object progress/7656001/20260913_120000_pg1/record.json '{"schemaVersion":1,"steamId":"7656001","playSeconds":600}'
mk_object report/7656003/20260913_130000_bad1/READY '{"kind":"bug"}'
cat > "$R2/inbox.json" <<'JSON'
{"items":[
 {"kind":"report","steamId":"7656001","id":"20260913_100000_bug1","readyAt":"2026-09-13T01:00:00Z"},
 {"kind":"report","steamId":"7656002","id":"20260913_110000_fb1","readyAt":"2026-09-13T02:00:00Z"},
 {"kind":"progress","steamId":"7656001","id":"20260913_120000_pg1","readyAt":"2026-09-13T03:00:00Z"},
 {"kind":"report","steamId":"7656003","id":"20260913_130000_bad1","readyAt":"2026-09-13T04:00:00Z"}],
 "cursor":null}
JSON

# curl スタブ: -o の出力先へ偽 R2 のファイルを置き、POST は ack として記録する
# curl stub: copies fake-R2 files to the -o target and records POSTs as acks
cat > "$TMP/curl" <<'SH'
#!/usr/bin/env bash
out=""; url=""; method=GET
while [ $# -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift 2 ;;
    -X) method="$2"; shift 2 ;;
    -H|--max-time) shift 2 ;;
    --silent|--show-error|--fail|--location) shift ;;
    *) url="$1"; shift ;;
  esac
done
path="${url#*://*/v1/}"; path="${path%%\?*}"
if [ "$method" = POST ]; then echo "$path" >> "$FAKE_R2/acked.txt"; : > "$out"; exit 0; fi
if [ "$path" = inbox ]; then cp "$FAKE_R2/inbox.json" "$out"; exit 0; fi
src="$FAKE_R2/objects/${path#inbox/}"
[ -f "$src" ] || exit 22
mkdir -p "$(dirname "$out")"; cp "$src" "$out"
SH
chmod +x "$TMP/curl"

run_ingest() {
  MOORESTECH_LOGS="$LOGS" PLAYTEST_ENV_FILE=/dev/null PLAYTEST_ADMIN_KEY=dummy \
  FAKE_R2="$R2" CURL_CMD="$TMP/curl" GIT_PUSH=0 bash "$HERE/../ingest.sh"
}
run_ingest

P="$LOGS/harness/playtest"
[ -f "$P/reports/7656001/20260913_100000_bug1/manifest.json" ] || { echo "NG: バグ報告が置かれていない"; exit 1; }
[ -f "$P/reports/7656001/20260913_100000_bug1/ingest.json" ] || { echo "NG: ingest.json が無い"; exit 1; }
grep -q '"readyAt":"2026-09-13T01:00:00Z"' "$P/reports/7656001/20260913_100000_bug1/ingest.json" || { echo "NG: readyAt が写っていない"; exit 1; }
[ -f "$P/reports/7656002/20260913_110000_fb1/manifest.json" ] || { echo "NG: 感想が置かれていない"; exit 1; }
[ -f "$P/progress/7656001/20260913_120000_pg1/record.json" ] || { echo "NG: 進行記録が置かれていない"; exit 1; }
[ ! -e "$P/reports/7656003/20260913_130000_bad1" ] || { echo "NG: files[] 無しの箱が公開された"; exit 1; }

# 自動投入しない（ADR 0061）。inbox は空のまま、マーカーも付かない
# No auto-enqueue (ADR 0061): the auto-fix inbox stays empty and no marker is written
I="$LOGS/harness/bug-report/inbox"
[ -z "$(ls -A "$I")" ] || { echo "NG: 取り込みが自動修正ランへ自動投入した"; exit 1; }
[ ! -e "$P/reports/7656001/20260913_100000_bug1/AUTOFIX_QUEUED" ] || { echo "NG: 取り込みが AUTOFIX_QUEUED を付けた"; exit 1; }

grep -q 'report/7656001/20260913_100000_bug1/ack' "$R2/acked.txt" || { echo "NG: ack されていない"; exit 1; }
grep -q '20260913_130000_bad1/ack' "$R2/acked.txt" && { echo "NG: 壊れた箱が ack された"; exit 1; }
git -C "$LOGS" log --oneline | grep -q 'playtest: ingest' || { echo "NG: logs repo に commit が無い"; exit 1; }

# ack が失われて再配信された場合: 再ダウンロードせず ack だけやり直す
# When an item is redelivered after a lost ack: re-ack only, never re-download
rm -f "$R2/acked.txt"
BEFORE="$(cat "$P/reports/7656001/20260913_100000_bug1/ingest.json")"
run_ingest
grep -q 'report/7656001/20260913_100000_bug1/ack' "$R2/acked.txt" || { echo "NG: 再 ack されない"; exit 1; }
[ "$BEFORE" = "$(cat "$P/reports/7656001/20260913_100000_bug1/ingest.json")" ] || { echo "NG: 再ダウンロードされた"; exit 1; }
echo OK
```

Run: `bash scripts/playtest/tests/test-ingest.sh`
Expected: FAIL（`ingest.sh` が無い）

- [ ] **Step 2: `lib/receiver-api.sh` を書く**

```bash
#!/usr/bin/env bash
# 受け口 admin API の薄いラッパ。curl は差し替え可能で、admin key は引数にしか現れない
# Thin wrappers over the receiver admin API; curl is swappable and the admin key appears only as an argument
RECEIVER_BASE="${PLAYTEST_RECEIVER_BASE:-https://playtest.moores.tech}"
CURL_CMD="${CURL_CMD:-curl}"
RECEIVER_MAX_TIME="${RECEIVER_MAX_TIME:-120}"

# 共通の curl 呼び出し。$1 は出力先、以降はフラグ…最後に URL
# Shared curl call: $1 is the output path, the rest are flags followed by the URL
receiver_curl() {
  local out="$1"; shift
  "$CURL_CMD" --silent --show-error --fail --location \
    --max-time "$RECEIVER_MAX_TIME" \
    -H "X-Admin-Key: ${PLAYTEST_ADMIN_KEY:?PLAYTEST_ADMIN_KEY が未設定}" \
    -o "$out" "$@"
}

# cursor は不透明文字列なので必ず URL エンコードしてから付ける
# The cursor is opaque, so it is always percent-encoded before being appended
receiver_inbox_page() {
  local cursor="$1" out="$2" url="$RECEIVER_BASE/v1/inbox"
  if [ -n "$cursor" ]; then
    url="$url?cursor=$(python3 -c 'import sys,urllib.parse;print(urllib.parse.quote(sys.argv[1],safe=""))' "$cursor")"
  fi
  receiver_curl "$out" "$url"
}

receiver_get_object() {
  local kind="$1" steam_id="$2" id="$3" path="$4" out="$5"
  receiver_curl "$out" "$RECEIVER_BASE/v1/inbox/$kind/$steam_id/$id/$path"
}

receiver_ack() {
  local kind="$1" steam_id="$2" id="$3"
  receiver_curl /dev/null -X POST "$RECEIVER_BASE/v1/inbox/$kind/$steam_id/$id/ack"
}
```

- [ ] **Step 3: `ingest.sh` を書く**

```bash
#!/usr/bin/env bash
# 受け口の inbox を moorestech_logs へ取り込み、成功したものだけ ack する（ADR 0061）
# 自動修正ランへの投入はしない。人が日次ダイジェストを見て enqueue-autofix.sh で投入する（裁定 2026-09-13）
# Ingests the receiver inbox into moorestech_logs and acks only what succeeded (ADR 0061)
# It never enqueues auto-fix runs; a human picks them from the daily digest via enqueue-autofix.sh
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
# shellcheck source=lib/receiver-api.sh
. "$HERE/lib/receiver-api.sh"

ENV_FILE="${PLAYTEST_ENV_FILE:-$HOME/hermes-agent/data/services/playtest/env.sh}"
# shellcheck disable=SC1090
[ -f "$ENV_FILE" ] && . "$ENV_FILE"

LOGS="${MOORESTECH_LOGS:-$HOME/hermes-agent/data/repos/moorestech_logs}"
PLAYTEST_DIR="$LOGS/harness/playtest"
GIT_CMD="${GIT_CMD:-git}"
GIT_PUSH="${GIT_PUSH:-1}"
MAX_ITEMS="${PLAYTEST_INGEST_MAX_ITEMS:-50}"
MAX_PAGES="${PLAYTEST_INGEST_MAX_PAGES:-20}"
LOCK="${TMPDIR:-/tmp}/moorestech-playtest-ingest.lock"

log() { echo "[ingest] $*" >&2; }
now_utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }

# R2 側の kind（report|progress）と logs 側のディレクトリ名を明示対応させる
# Maps the receiver kind (report|progress) onto the logs directory name explicitly
dest_subdir() {
  case "$1" in
    report) echo "reports" ;;
    progress) echo "progress" ;;
    *) return 1 ;;
  esac
}

ingest_one() {
  local kind="$1" steam_id="$2" id="$3" ready_at="$4"
  local sub; sub="$(dest_subdir "$kind")" || { log "ERROR: 未知の kind=$kind id=$id（ack しない）"; return 1; }
  local dest="$PLAYTEST_DIR/$sub/$steam_id/$id"
  local partial="$PLAYTEST_DIR/$sub/$steam_id/$id.partial"

  # 既に置かれている＝前回 ack だけ失敗した箱。落とし直さず ack だけやり直す
  # An existing dest means only the ack failed last time: never re-download, just re-ack
  if [ ! -d "$dest" ]; then
    rm -rf "$partial"; mkdir -p "$partial"
    receiver_get_object "$kind" "$steam_id" "$id" READY "$partial/READY" \
      || { log "ERROR: READY 取得失敗 $kind/$steam_id/$id"; rm -rf "$partial"; return 1; }
    local files
    files="$(python3 -c '
import json,sys
d=json.load(open(sys.argv[1]))
fs=d.get("files") or []
if not fs: sys.exit(3)
for p in fs:
    if p.startswith("/") or ".." in p.split("/"): sys.exit(4)
    print(p)
' "$partial/READY")" || {
      log "ERROR: READY 本文の files[] が無いか不正なパスを含む: $kind/$steam_id/$id（受け口側の修正が要る。ack しない）"
      rm -rf "$partial"; return 1
    }
    local rel
    while IFS= read -r rel; do
      [ -n "$rel" ] || continue
      mkdir -p "$partial/$(dirname "$rel")"
      receiver_get_object "$kind" "$steam_id" "$id" "$rel" "$partial/$rel" \
        || { log "ERROR: 取得失敗 $id/$rel"; rm -rf "$partial"; return 1; }
    done <<< "$files"
    printf '{"kind":"%s","steamId":"%s","id":"%s","readyAt":"%s","ingestedAt":"%s"}\n' \
      "$kind" "$steam_id" "$id" "$ready_at" "$(now_utc)" > "$partial/ingest.json"
    mkdir -p "$(dirname "$dest")"; mv "$partial" "$dest"
    log "ingested: $kind/$steam_id/$id"
  else
    log "取り込み済み。ack のみやり直す: $kind/$steam_id/$id"
  fi

  receiver_ack "$kind" "$steam_id" "$id" || { log "ERROR: ack 失敗 $id（次回に持ち越し）"; return 1; }
}

commit_logs() {
  [ -d "$LOGS/.git" ] || { log "logs repo が無い: $LOGS"; return 0; }
  ( cd "$LOGS"
    $GIT_CMD add -A harness/playtest || exit 1
    if $GIT_CMD diff --cached --quiet -- harness/playtest; then exit 0; fi
    $GIT_CMD commit -qm "playtest: ingest $(now_utc)" || exit 1
    [ "$GIT_PUSH" = 1 ] || exit 0
    $GIT_CMD push -q || exit 1
  ) || log "ERROR: logs repo の commit/push に失敗（次回に持ち越し）"
}

mkdir "$LOCK" 2>/dev/null || { log "別の取り込みが進行中（$LOCK）"; exit 0; }
WORK="$(mktemp -d)"
trap 'rmdir "$LOCK"; rm -rf "$WORK"' EXIT

ITEMS="$WORK/items.tsv"; : > "$ITEMS"
cursor=""; page=0
while [ "$page" -lt "$MAX_PAGES" ]; do
  page=$((page + 1))
  receiver_inbox_page "$cursor" "$WORK/page.json" \
    || { log "ERROR: /v1/inbox の取得に失敗（page=$page）。今回は何もしない"; exit 0; }
  python3 -c '
import json,sys
d=json.load(open(sys.argv[1]))
for it in d.get("items") or []:
    print("\t".join([it.get("kind",""),it.get("steamId",""),it.get("id",""),it.get("readyAt","")]))
' "$WORK/page.json" >> "$ITEMS"
  cursor="$(python3 -c 'import json,sys;print(json.load(open(sys.argv[1])).get("cursor") or "")' "$WORK/page.json")"
  [ -n "$cursor" ] || break
done

count=0
while IFS=$'\t' read -r kind steam_id id ready_at; do
  [ -n "$kind" ] && [ -n "$id" ] || continue
  count=$((count + 1))
  if [ "$count" -gt "$MAX_ITEMS" ]; then log "上限 $MAX_ITEMS 件に達した。残りは次回"; break; fi
  ingest_one "$kind" "$steam_id" "$id" "$ready_at" || true
done < "$ITEMS"

commit_logs
log "done: 取得 $(wc -l < "$ITEMS" | tr -d ' ') 件を走査"
```

- [ ] **Step 4: `ingest-dispatch.sh` を書く**

```bash
#!/usr/bin/env bash
# supervisor の periodic から worker を nohup で切り離す。periodic は同期実行なので長い処理を直接置かない
# Detaches the worker with nohup from the supervisor periodic; periodics run synchronously so long work must not sit here
set -euo pipefail
REPO="${MOORESTECH_REPO:-~/hermes-agent/data/repos/moorestech}"
LOG="${PLAYTEST_INGEST_LOG:-~/hermes-agent/data/services/always-on/logs/playtest-ingest-worker.log}"
mkdir -p "$(dirname "$LOG")"
nohup /bin/bash "$REPO/scripts/playtest/ingest.sh" >>"$LOG" 2>&1 </dev/null &
echo "playtest-ingest dispatched pid=$!"
```

- [ ] **Step 5: `README-macmini.md` を書く**

`scripts/playtest/README-macmini.md`:
```markdown
# プレイテストの取り込み（Mac mini側）

## 前提

- `~/hermes-agent/data/repos/{moorestech,moorestech_logs}` がある
- `~/hermes-agent/data/services/playtest/env.sh` を `chmod 600` で作る（**値はこのREADMEにもrepoにも書かない**）:
  ```bash
  export PLAYTEST_ADMIN_KEY=...                    # 受け口 Worker の ADMIN_KEY
  export PLAYTEST_DIGEST_DISCORD_CHANNEL_ID=...    # 日次ダイジェストの投稿先
  ```

## supervisor へ登録する

`~/hermes-agent/data/services/always-on/services.json` の `services` 配列へ次を足す（`services.json` はループ毎に再読込されるので supervisor の再起動は不要）:

```json
{
  "name": "playtest-ingest",
  "kind": "periodic",
  "interval_seconds": 300,
  "timeout_seconds": 600,
  "cwd": "~/hermes-agent/data/repos/moorestech",
  "command": ["/bin/bash", "~/hermes-agent/data/repos/moorestech/scripts/playtest/ingest-dispatch.sh"]
}
```

確認（periodic の実行は `supervisor.log` には出ない。サービス個別のログを見る）:

```bash
tail -f ~/hermes-agent/data/services/always-on/logs/playtest-ingest.log        # "run reason=periodic" マーカー
tail -f ~/hermes-agent/data/services/always-on/logs/playtest-ingest-worker.log # "[ingest] ..." 本体
```

## 手動実行

```bash
. ~/hermes-agent/data/services/playtest/env.sh
bash ~/hermes-agent/data/repos/moorestech/scripts/playtest/ingest.sh
```

ロックは `$TMPDIR/moorestech-playtest-ingest.lock`。前回が生きていれば何もせず終わる。

## 成果物

- `moorestech_logs/harness/playtest/{reports,progress}/<steamId>/<id>/`
- **自動修正ランへの投入はここでは起きない**（ADR 0061）。投入は日次ダイジェストを見て人が `scripts/playtest/enqueue-autofix.sh <steamId> <id>` を叩く

## 詰まったとき

- `[ingest] ERROR: READY 本文の files[] が…` → 受け口（plan D）が `complete` で書く要約 JSON に `files[]` が無い。箱は ack されずに残るので、受け口を直せば次の周期で自然に取り込まれる
- `[ingest] ERROR: ack 失敗` → 箱は取り込み済みなので、次の周期で再配信されても再ダウンロードせず ack だけやり直す
```

- [ ] **Step 6: テストを通してコミットする**

Run: `chmod +x scripts/playtest/*.sh scripts/playtest/tests/*.sh && bash scripts/playtest/tests/test-ingest.sh`
Expected: `OK`

```bash
git add scripts/playtest
git commit -m "feat(playtest): 受け口からの取り込みworkerとsupervisor dispatch"
```

---

### Task 3: 自動修正ランへの手動投入コマンド `enqueue-autofix.sh`

**Files:**
- Create: `scripts/playtest/enqueue-autofix.sh`
- Modify: `scripts/playtest/README-macmini.md`（「自動修正ランへ投入する」節を追記）
- Test: `scripts/playtest/tests/test-enqueue-autofix.sh`

**Interfaces:**
- Consumes: Task 2 が置く `harness/playtest/reports/<steamId>/<id>/`（`manifest.json` の `kind`）
- Produces:
  - `harness/bug-report/inbox/<id>/`（`READY` 付き。plan C の `inbox-poller.sh` が消費）
  - 箱の `AUTOFIX_QUEUED`（本文 `queued at <ISO8601>`。Task 4 の `digest_collect.load_reports` が存在の有無を `queued` フラグとして読む）
  - `--force` 時のみ inbox コピー側の `AUTOFIX_FORCED`（本文 `forced kind=<値> at <ISO8601>`。plan C の poller の種別ガードがこの印だけ通す。Task 6 の plan C 改訂メモ2）
- 終了コード: 0=投入した、1=引数・箱・manifest の異常、2=投入済み（マーカーあり）、3=`kind` が bug でなく `--force` も無い

- [ ] **Step 1: 失敗するテストを書く**

`scripts/playtest/tests/test-enqueue-autofix.sh`:
```bash
#!/usr/bin/env bash
# 手動投入コマンドの4経路（投入・二重投入拒否・種別拒否・--force）を検証する
# Verifies the four paths of the manual enqueue command: enqueue, duplicate, kind guard, --force
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"; trap 'rm -rf "$TMP"' EXIT
LOGS="$TMP/logs"
BUG="$LOGS/harness/playtest/reports/7656001/20260913_100000_bug1"
FB="$LOGS/harness/playtest/reports/7656002/20260913_110000_fb1"
mkdir -p "$BUG" "$FB" "$LOGS/harness/bug-report/inbox"
echo '{"kind":"bug","description":"ベルトが止まる"}' > "$BUG/manifest.json"
echo 'READY-summary' > "$BUG/READY"
echo '{"kind":"feedback","description":"序盤が長い"}' > "$FB/manifest.json"
echo 'READY-summary' > "$FB/READY"

run() { MOORESTECH_LOGS="$LOGS" bash "$HERE/../enqueue-autofix.sh" "$@"; }
I="$LOGS/harness/bug-report/inbox"

run 7656001 20260913_100000_bug1
[ -f "$I/20260913_100000_bug1/READY" ] || { echo "NG: inbox に入っていない"; exit 1; }
[ -f "$I/20260913_100000_bug1/manifest.json" ] || { echo "NG: 中身が入っていない"; exit 1; }
[ ! -e "$I/20260913_100000_bug1/AUTOFIX_QUEUED" ] || { echo "NG: マーカーを inbox 側へ持ち込んだ"; exit 1; }
grep -q '^queued at ' "$BUG/AUTOFIX_QUEUED" || { echo "NG: マーカーが無い"; exit 1; }

set +e; run 7656001 20260913_100000_bug1; code=$?; set -e
[ "$code" = 2 ] || { echo "NG: 二重投入が拒否されない（exit=$code）"; exit 1; }

set +e; run 7656002 20260913_110000_fb1; code=$?; set -e
[ "$code" = 3 ] || { echo "NG: 感想が拒否されない（exit=$code）"; exit 1; }
[ ! -e "$I/20260913_110000_fb1" ] || { echo "NG: 感想が投入された"; exit 1; }

run --force 7656002 20260913_110000_fb1
[ -f "$I/20260913_110000_fb1/READY" ] || { echo "NG: --force で投入されない"; exit 1; }
grep -q '^forced kind=feedback' "$I/20260913_110000_fb1/AUTOFIX_FORCED" || { echo "NG: AUTOFIX_FORCED が無い"; exit 1; }
[ ! -e "$I/20260913_100000_bug1/AUTOFIX_FORCED" ] || { echo "NG: --force 無しで FORCED が付いた"; exit 1; }

set +e; run 7656001 存在しないID; code=$?; set -e
[ "$code" = 1 ] || { echo "NG: 箱が無いのに失敗しない（exit=$code）"; exit 1; }
echo OK
```

Run: `bash scripts/playtest/tests/test-enqueue-autofix.sh`
Expected: FAIL（`enqueue-autofix.sh` が無い）

- [ ] **Step 2: `enqueue-autofix.sh` を書く**

```bash
#!/usr/bin/env bash
# 取り込み済みのプレイ報告を1件、自動修正ランの inbox へ投入する（人が日次ダイジェストを見て叩く）
# Enqueues one ingested play report into the auto-fix inbox; a human runs this after reading the daily digest
# ADR 0061: テスター報告の自動投入はしない。投入の判断は人が持つ
# ADR 0061: tester reports are never auto-enqueued; the decision to enqueue belongs to a human
set -euo pipefail

FORCE=0
if [ "${1:-}" = "--force" ]; then FORCE=1; shift; fi
STEAM_ID="${1:-}"; ID="${2:-}"
if [ -z "$STEAM_ID" ] || [ -z "$ID" ]; then
  echo "usage: enqueue-autofix.sh [--force] <steamId> <id>" >&2
  exit 1
fi

LOGS="${MOORESTECH_LOGS:-$HOME/hermes-agent/data/repos/moorestech_logs}"
BOX="$LOGS/harness/playtest/reports/$STEAM_ID/$ID"
BUG_INBOX="$LOGS/harness/bug-report/inbox"
log() { echo "[enqueue] $*" >&2; }
now_utc() { date -u +%Y-%m-%dT%H:%M:%SZ; }

[ -d "$BOX" ] || { log "ERROR: 箱が無い: $BOX"; exit 1; }
[ -f "$BOX/manifest.json" ] || { log "ERROR: manifest.json が無い: $BOX"; exit 1; }

# 二重投入は拒否する。plan C の poller は inbox から箱を持ち去るので、inbox の有無では判定できない
# Refuse duplicates: the plan C poller moves boxes out of the inbox, so the inbox itself is not the record
if [ -f "$BOX/AUTOFIX_QUEUED" ]; then
  log "投入済み: $ID（$(cat "$BOX/AUTOFIX_QUEUED")）。やり直すならマーカーを消してから"
  exit 2
fi

MANIFEST_KIND="$(python3 -c '
import json,sys
try:
    print(json.load(open(sys.argv[1])).get("kind",""))
except Exception:
    print("")
' "$BOX/manifest.json")"
[ -n "$MANIFEST_KIND" ] || { log "ERROR: manifest.json の kind を読めない: $ID"; exit 1; }
if [ "$MANIFEST_KIND" != bug ] && [ "$FORCE" != 1 ]; then
  log "kind=$MANIFEST_KIND は自動修正ランの対象外。投入するなら --force: $ID"
  exit 3
fi
[ "$MANIFEST_KIND" != bug ] && log "--force で kind=$MANIFEST_KIND を投入する: $ID"

# .partial へ組んでから mv で公開する。poller が途中の箱を掴まないため（plan C と同じ作法）
# Build in .partial and publish with mv so the poller never sees a half box (same idiom as plan C)
PARTIAL="$BUG_INBOX/$ID.partial"
mkdir -p "$BUG_INBOX"; rm -rf "$PARTIAL"
cp -R "$BOX" "$PARTIAL"
rm -f "$PARTIAL/AUTOFIX_QUEUED"
# --force の印は inbox 側へ持たせる。poller の種別ガードはこの印がある箱だけ通す（plan C 改訂メモ2）
# The --force marker travels with the inbox copy; the poller's kind guard lets only marked boxes through
[ "$FORCE" = 1 ] && printf 'forced kind=%s at %s\n' "$MANIFEST_KIND" "$(now_utc)" > "$PARTIAL/AUTOFIX_FORCED"
[ -f "$PARTIAL/READY" ] || now_utc > "$PARTIAL/READY"
rm -rf "$BUG_INBOX/$ID"; mv "$PARTIAL" "$BUG_INBOX/$ID"
printf 'queued at %s\n' "$(now_utc)" > "$BOX/AUTOFIX_QUEUED"
log "投入した: $ID（poller が最大60秒で拾う）"
```

- [ ] **Step 3: README に節を足す**

`scripts/playtest/README-macmini.md` の「詰まったとき」の前に挿入:
```markdown
## 自動修正ランへ投入する（人の操作）

テスターのバグ報告は**自動では走らない**（ADR 0061）。毎朝の日次ダイジェストの「投入候補のバグ報告」節に
そのまま貼れるコマンドが並ぶので、直したいものだけ選んで叩く。

```bash
bash ~/hermes-agent/data/repos/moorestech/scripts/playtest/enqueue-autofix.sh <steamId> <id>
```

- 投入すると `moorestech_logs/harness/bug-report/inbox/<id>/` に置かれ、plan C の `inbox-poller.sh` が最大60秒で拾う
- 二度目は `exit 2`（箱の `AUTOFIX_QUEUED` マーカーで判定）。やり直すならマーカーを消す
- 感想・クラッシュは `exit 3` で拒否。どうしても走らせるなら `--force`（inbox 側に `AUTOFIX_FORCED` が付き、poller の種別ガードを通る）
- 結果は `moorestech_logs/harness/bug-report/runs/<id>/fix-result.json`、翌朝のダイジェストの「自動修正ラン」節にも出る
```

- [ ] **Step 4: テストを通してコミットする**

Run: `chmod +x scripts/playtest/enqueue-autofix.sh scripts/playtest/tests/test-enqueue-autofix.sh && bash scripts/playtest/tests/test-enqueue-autofix.sh`
Expected: `OK`

```bash
git add scripts/playtest
git commit -m "feat(playtest): 自動修正ランへの手動投入コマンド"
```

---

### Task 4: 日次ダイジェストの集計と Markdown 出力

**Files:**
- Create: `scripts/playtest/digest_collect.py`
- Create: `scripts/playtest/digest.py`
- Test: `scripts/playtest/tests/test_digest.py`

**Interfaces:**
- Consumes: Task 2 が書く `ingest.json`（`readyAt`・`ingestedAt`・`steamId`・`id`・`kind`）、`manifest.json`（`kind`・`description`・`buildInfo.steamBuildLabel`）、Task 3 が書く `AUTOFIX_QUEUED`（存在するか否かだけ）、進行記録 `record.json`（共有契約 §3）、plan C の `harness/bug-report/runs/<id>/fix-result.json`（`status`・`pr_number`・`base`・`summary`・本plan で足す `finishedAt`）
- Produces: 標準出力の Markdown（Hermes cron `--no-agent` がそのまま Discord へ流す）、`harness/playtest/digests/<date>.md`
- Produces（関数）: `digest_collect.jst_date(iso) -> str`・`load_reports(root, date) -> list[dict]`（各要素に `queued: bool` を含む）・`load_progress(root, date) -> list[dict]`・`aggregate_progress(records) -> dict`・`load_fix_results(runs_root, date) -> tuple[list[dict], int]`

- [ ] **Step 1: 失敗するテストを書く**

`scripts/playtest/tests/test_digest.py`:
```python
#!/usr/bin/env python3
"""日次ダイジェストの集計と出力を fixture で検証する。

Verifies the daily digest aggregation and output against a fixture tree.
"""
import json
import subprocess
import sys
import tempfile
import unittest
from pathlib import Path

SCRIPTS = Path(__file__).resolve().parent.parent
sys.path.insert(0, str(SCRIPTS))
import digest_collect as dc  # noqa: E402


def write_json(path: Path, data: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data), encoding="utf-8")


def build_fixture(root: Path) -> None:
    """前日(2026-09-12 JST)分と対象外の日を1件ずつ混ぜた木を作る
    Builds a tree with boxes for the target day (2026-09-12 JST) plus one out-of-range box"""
    pt = root / "harness" / "playtest"
    bug = pt / "reports" / "7656001" / "20260912_100000_bug1"
    write_json(bug / "ingest.json", {"kind": "report", "steamId": "7656001",
                                     "id": "20260912_100000_bug1", "readyAt": "2026-09-12T05:00:00Z",
                                     "ingestedAt": "2026-09-12T05:01:00Z"})
    write_json(bug / "manifest.json", {"kind": "bug", "description": "ベルトが止まる\n2個目から",
                                       "buildInfo": {"steamBuildLabel": "playtest-20260912-1730"}})
    bug2 = pt / "reports" / "7656001" / "20260912_101000_bug2"
    write_json(bug2 / "ingest.json", {"kind": "report", "steamId": "7656001",
                                      "id": "20260912_101000_bug2", "readyAt": "2026-09-12T05:10:00Z",
                                      "ingestedAt": "2026-09-12T05:11:00Z"})
    write_json(bug2 / "manifest.json", {"kind": "bug", "description": "投入済みのバグ"})
    (bug2 / "AUTOFIX_QUEUED").write_text("queued at 2026-09-12T06:00:00Z\n", encoding="utf-8")
    fb = pt / "reports" / "7656002" / "20260912_110000_fb1"
    write_json(fb / "ingest.json", {"kind": "report", "steamId": "7656002",
                                    "id": "20260912_110000_fb1", "readyAt": "2026-09-12T06:00:00Z",
                                    "ingestedAt": "2026-09-12T06:01:00Z"})
    write_json(fb / "manifest.json", {"kind": "feedback", "description": "序盤の歩きが長い",
                                      "buildInfo": {"steamBuildLabel": "playtest-20260912-1730"}})
    cr = pt / "reports" / "7656003" / "20260912_120000_cr1"
    write_json(cr / "ingest.json", {"kind": "report", "steamId": "7656003",
                                    "id": "20260912_120000_cr1", "readyAt": "2026-09-12T07:00:00Z",
                                    "ingestedAt": "2026-09-12T07:01:00Z"})
    write_json(cr / "manifest.json", {"kind": "crash", "description": ""})
    old = pt / "reports" / "7656004" / "20260901_100000_old1"
    write_json(old / "ingest.json", {"kind": "report", "steamId": "7656004",
                                     "id": "20260901_100000_old1", "readyAt": "2026-09-01T05:00:00Z",
                                     "ingestedAt": "2026-09-01T05:01:00Z"})
    write_json(old / "manifest.json", {"kind": "feedback", "description": "対象外の日"})

    pg1 = pt / "progress" / "7656001" / "20260912_130000_pg1"
    write_json(pg1 / "ingest.json", {"kind": "progress", "steamId": "7656001",
                                     "id": "20260912_130000_pg1", "readyAt": "2026-09-12T08:00:00Z",
                                     "ingestedAt": "2026-09-12T08:01:00Z"})
    write_json(pg1 / "record.json", {"schemaVersion": 1, "steamId": "7656001", "playSeconds": 1200.0,
                                     "endReason": "quit", "reachedChallenges": ["a", "b", "c"],
                                     "completedResearch": ["r1"], "lastUiState": "GameScreen",
                                     "events": [{"type": "challengeCompleted"}, {"type": "buildModeCancelled"}]})
    pg2 = pt / "progress" / "7656002" / "20260912_140000_pg2"
    write_json(pg2 / "ingest.json", {"kind": "progress", "steamId": "7656002",
                                     "id": "20260912_140000_pg2", "readyAt": "2026-09-12T09:00:00Z",
                                     "ingestedAt": "2026-09-12T09:01:00Z"})
    write_json(pg2 / "record.json", {"schemaVersion": 1, "steamId": "7656002", "playSeconds": 600.0,
                                     "endReason": "crash-recovered", "reachedChallenges": [],
                                     "completedResearch": [], "lastUiState": "InventoryScreen",
                                     "events": [{"type": "buildModeCancelled"}]})

    run = root / "harness" / "bug-report" / "runs" / "20260910_090000_bug0"
    write_json(run / "fix-result.json", {"status": "fixed", "pr_number": 1400, "base": "master",
                                         "determinism": "ok", "summary": "ベルト停止を修正",
                                         "finishedAt": "2026-09-12T10:00:00Z"})


class DigestTest(unittest.TestCase):
    def setUp(self):
        self.tmp = tempfile.TemporaryDirectory()
        self.root = Path(self.tmp.name)
        build_fixture(self.root)
        self.date = "2026-09-12"

    def tearDown(self):
        self.tmp.cleanup()

    def test_jst_date_converts_utc(self):
        self.assertEqual(dc.jst_date("2026-09-12T15:30:00Z"), "2026-09-13")
        self.assertEqual(dc.jst_date(""), "")

    def test_load_reports_filters_by_ready_at(self):
        reports = dc.load_reports(self.root / "harness/playtest/reports", self.date)
        self.assertEqual({r["kind"] for r in reports}, {"bug", "feedback", "crash"})
        self.assertEqual(len(reports), 4)

    def test_load_reports_marks_queued(self):
        reports = {r["id"]: r for r in dc.load_reports(
            self.root / "harness/playtest/reports", self.date)}
        self.assertFalse(reports["20260912_100000_bug1"]["queued"])
        self.assertTrue(reports["20260912_101000_bug2"]["queued"])

    def test_aggregate_progress(self):
        records = dc.load_progress(self.root / "harness/playtest/progress", self.date)
        agg = dc.aggregate_progress(records)
        self.assertEqual(agg["testers"], 2)
        self.assertEqual(agg["sessions"], 2)
        self.assertAlmostEqual(agg["meanPlaySeconds"], 900.0)
        self.assertEqual(agg["reachBuckets"]["3-5"], 1)
        self.assertEqual(agg["reachBuckets"]["0"], 1)
        self.assertEqual(agg["lastEvents"]["buildModeCancelled"], 2)
        self.assertEqual(agg["lastUiStates"]["GameScreen"], 1)

    def test_load_fix_results_uses_finished_at(self):
        runs, fallback = dc.load_fix_results(self.root / "harness/bug-report/runs", self.date)
        self.assertEqual(len(runs), 1)
        self.assertEqual(runs[0]["prNumber"], 1400)
        self.assertEqual(fallback, 0)

    def run_digest(self, *extra):
        cmd = [sys.executable, str(SCRIPTS / "digest.py"), "--date", self.date,
               "--logs", str(self.root), *extra]
        return subprocess.run(cmd, capture_output=True, text=True, check=True).stdout

    def test_digest_sections(self):
        out = self.run_digest("--max-chars", "0")
        self.assertIn("# moorestech プレイテスト日次ダイジェスト 2026-09-12", out)
        self.assertIn("バグ 2件 / 感想 1件 / クラッシュ 1件", out)
        self.assertIn("序盤の歩きが長い", out)
        self.assertNotIn("対象外の日", out)
        self.assertIn("人数 2 人", out)
        self.assertIn("平均プレイ時間 15.0 分", out)
        self.assertIn("buildModeCancelled 2件", out)
        self.assertIn("#1400", out)
        self.assertTrue((self.root / "harness/playtest/digests/2026-09-12.md").is_file())

    def test_digest_lists_enqueue_candidates(self):
        """投入候補は未投入のバグだけ。コマンドはそのまま貼れる形で出る
        Only un-enqueued bugs are listed, with a copy-pastable command"""
        out = self.run_digest("--max-chars", "0")
        self.assertIn("## 投入候補のバグ報告", out)
        self.assertIn("enqueue-autofix.sh 7656001 20260912_100000_bug1", out)
        self.assertIn("ベルトが止まる", out)
        self.assertNotIn("enqueue-autofix.sh 7656001 20260912_101000_bug2", out)
        self.assertNotIn("投入済みのバグ", out)

    def test_digest_truncates_and_points_at_archive(self):
        long_box = self.root / "harness/playtest/reports/7656005/20260912_150000_fb2"
        write_json(long_box / "ingest.json", {"kind": "report", "steamId": "7656005",
                                              "id": "20260912_150000_fb2", "readyAt": "2026-09-12T09:30:00Z",
                                              "ingestedAt": "2026-09-12T09:31:00Z"})
        write_json(long_box / "manifest.json", {"kind": "feedback", "description": "あ" * 4000})
        out = self.run_digest("--max-chars", "600")
        self.assertLessEqual(len(out), 700)
        self.assertIn("digests/2026-09-12.md", out)

    def test_empty_day_still_prints_headings(self):
        out = subprocess.run([sys.executable, str(SCRIPTS / "digest.py"), "--date", "2026-01-01",
                              "--logs", str(self.root)], capture_output=True, text=True,
                             check=True).stdout
        self.assertIn("# moorestech プレイテスト日次ダイジェスト 2026-01-01", out)
        self.assertIn("## 進行記録", out)
        self.assertIn("なし", out)


if __name__ == "__main__":
    unittest.main()
```

Run: `python3 scripts/playtest/tests/test_digest.py -v`
Expected: FAIL（`ModuleNotFoundError: No module named 'digest_collect'`）

- [ ] **Step 2: `digest_collect.py` を書く**

```python
#!/usr/bin/env python3
"""取り込み済みのプレイ報告・進行記録・自動修正ラン結果を日付で拾って集計する（標準ライブラリのみ）。

Collects ingested play reports, progress records and auto-fix run results for one day; stdlib only.
"""
from __future__ import annotations

import json
from collections import Counter
from datetime import datetime, timedelta, timezone
from pathlib import Path

JST = timezone(timedelta(hours=9))
REACH_BUCKETS = ((0, 0, "0"), (1, 2, "1-2"), (3, 5, "3-5"), (6, 9, "6-9"))


def jst_date(iso: str) -> str:
    """ISO8601 を JST の YYYY-MM-DD にする。解釈できなければ空文字を返す
    Converts ISO8601 to a JST YYYY-MM-DD; returns an empty string when unparsable"""
    if not iso:
        return ""
    # 外部（受け口・ゲーム本体）が書いた文字列のパースなので例外を隔離する
    # This parses strings written by external producers, so the exception is isolated here
    try:
        parsed = datetime.fromisoformat(iso.replace("Z", "+00:00"))
    except ValueError:
        return ""
    if parsed.tzinfo is None:
        parsed = parsed.replace(tzinfo=timezone.utc)
    return parsed.astimezone(JST).strftime("%Y-%m-%d")


def read_json(path: Path) -> dict:
    """読めない・壊れた JSON は空 dict にする。呼び出し側が件数として報告する
    Returns an empty dict for missing or broken JSON; callers report the count"""
    try:
        return json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def collect_boxes(root: Path, date: str) -> list[dict]:
    """ingest.json の readyAt（無ければ ingestedAt）が指定日の箱を集める
    Collects boxes whose ingest.json readyAt (or ingestedAt) falls on the given day"""
    boxes: list[dict] = []
    if not root.is_dir():
        return boxes
    for ingest_path in sorted(root.glob("*/*/ingest.json")):
        meta = read_json(ingest_path)
        stamp = meta.get("readyAt") or meta.get("ingestedAt") or ""
        if jst_date(stamp) == date:
            boxes.append({"dir": ingest_path.parent, "meta": meta})
    return boxes


def load_reports(root: Path, date: str) -> list[dict]:
    """プレイ報告の manifest から種別・説明文・ビルド識別と、投入済みかどうかを取り出す
    Extracts kind, description, build label and the enqueued flag from each play report manifest"""
    reports = []
    for box in collect_boxes(root, date):
        manifest = read_json(box["dir"] / "manifest.json")
        build_info = manifest.get("buildInfo") or {}
        reports.append({
            "id": box["meta"].get("id") or box["dir"].name,
            "steamId": box["meta"].get("steamId", ""),
            "kind": manifest.get("kind") or "unknown",
            "description": manifest.get("description") or "",
            "buildLabel": build_info.get("steamBuildLabel", ""),
            # AUTOFIX_QUEUED は enqueue-autofix.sh だけが書く。投入候補一覧から外す判定に使う
            # AUTOFIX_QUEUED is written only by enqueue-autofix.sh and removes the box from the candidate list
            "queued": (box["dir"] / "AUTOFIX_QUEUED").is_file(),
            "dir": box["dir"],
        })
    return reports


def load_progress(root: Path, date: str) -> list[dict]:
    """進行記録1件を集計しやすい形へ畳む。離脱地点は最後のイベントと最後の UI 状態で見る
    Flattens one progress record; drop-off is read from the last event and the last UI state"""
    records = []
    for box in collect_boxes(root, date):
        record = read_json(box["dir"] / "record.json")
        events = record.get("events") or []
        records.append({
            "steamId": record.get("steamId") or box["meta"].get("steamId", ""),
            "playSeconds": float(record.get("playSeconds") or 0.0),
            "endReason": record.get("endReason") or "unknown",
            "reached": len(record.get("reachedChallenges") or []),
            "research": len(record.get("completedResearch") or []),
            "lastUiState": record.get("lastUiState") or "unknown",
            "lastEvent": (events[-1].get("type") if events else "none"),
        })
    return records


def bucket_reached(count: int) -> str:
    for low, high, label in REACH_BUCKETS:
        if low <= count <= high:
            return label
    return "10+"


def aggregate_progress(records: list[dict]) -> dict:
    """人数・平均プレイ時間・到達段階分布・離脱地点上位を出す
    Produces tester count, mean play time, reached-stage histogram and top drop-off points"""
    if not records:
        return {"testers": 0, "sessions": 0, "meanPlaySeconds": 0.0, "meanResearch": 0.0,
                "reachBuckets": Counter(), "lastEvents": Counter(),
                "lastUiStates": Counter(), "endReasons": Counter()}
    return {
        "testers": len({r["steamId"] for r in records if r["steamId"]}),
        "sessions": len(records),
        "meanPlaySeconds": sum(r["playSeconds"] for r in records) / len(records),
        "meanResearch": sum(r["research"] for r in records) / len(records),
        "reachBuckets": Counter(bucket_reached(r["reached"]) for r in records),
        "lastEvents": Counter(r["lastEvent"] for r in records),
        "lastUiStates": Counter(r["lastUiState"] for r in records),
        "endReasons": Counter(r["endReason"] for r in records),
    }


def load_fix_results(runs_root: Path, date: str) -> tuple[list[dict], int]:
    """自動修正ランを finishedAt で拾う。欠けている分だけ mtime に落とし、件数を返して出力に出す
    Picks runs by finishedAt; falls back to mtime only for the ones missing it and reports how many"""
    runs: list[dict] = []
    fallback = 0
    if not runs_root.is_dir():
        return runs, fallback
    for result_path in sorted(runs_root.glob("*/fix-result.json")):
        result = read_json(result_path)
        stamp = result.get("finishedAt") or ""
        if stamp:
            day = jst_date(stamp)
        else:
            fallback += 1
            day = datetime.fromtimestamp(result_path.stat().st_mtime, JST).strftime("%Y-%m-%d")
        if day != date:
            continue
        runs.append({
            "id": result_path.parent.name,
            "status": result.get("status") or "missing",
            "prNumber": result.get("pr_number"),
            "base": result.get("base") or "",
            "summary": result.get("summary") or "",
        })
    return runs, fallback
```

- [ ] **Step 3: `digest.py` を書く**

```python
#!/usr/bin/env python3
"""プレイテストの日次ダイジェストを Markdown で標準出力へ出す。

Prints the playtest daily digest as Markdown on stdout.
Hermes cron が --no-agent で呼び、stdout がそのまま Discord へ流れる（要約させないため LLM を挟まない）。
Hermes cron runs it with --no-agent so stdout goes to Discord verbatim, with no LLM in between.
"""
from __future__ import annotations

import argparse
import os
import sys
from collections import Counter
from datetime import datetime, timedelta
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
import digest_collect as dc  # noqa: E402


def top_line(counter: Counter, limit: int) -> str:
    if not counter:
        return "なし"
    return " / ".join(f"{key} {value}件" for key, value in counter.most_common(limit))


def format_counts(reports: list[dict]) -> list[str]:
    counts = Counter(r["kind"] for r in reports)
    lines = ["", "## プレイ報告の件数",
             f"- バグ {counts.get('bug', 0)}件 / 感想 {counts.get('feedback', 0)}件 / "
             f"クラッシュ {counts.get('crash', 0)}件"]
    if counts.get("unknown"):
        lines.append(f"- ⚠ manifest.kind を読めなかった箱 {counts['unknown']}件")
    return lines


def format_feedback(reports: list[dict]) -> list[str]:
    """感想は要約せず全文を出す（ADR 0061「新着の感想全文」）
    Feedback is printed in full, never summarised (ADR 0061)"""
    lines = ["", "## 感想（全文）"]
    items = [r for r in reports if r["kind"] == "feedback"]
    if not items:
        return lines + ["- なし"]
    for report in items:
        label = report["buildLabel"] or "不明"
        lines.append(f"### {report['id']}（SteamID {report['steamId']} / build {label}）")
        lines.append(report["description"].strip() or "（説明文が空）")
    return lines


def format_candidates(reports: list[dict]) -> list[str]:
    """未投入のバグ報告を、そのまま貼れる enqueue コマンド付きで並べる（投入の判断は人が持つ）
    Lists un-enqueued bug reports with a copy-pastable enqueue command; the decision stays with a human"""
    lines = ["", "## 投入候補のバグ報告"]
    items = [r for r in reports if r["kind"] == "bug" and not r["queued"]]
    if not items:
        return lines + ["- なし"]
    for report in items:
        head = (report["description"].strip().splitlines() or ["（説明文が空）"])[0]
        lines.append(f"- {report['id']} … {head}")
        lines.append(f"  `scripts/playtest/enqueue-autofix.sh {report['steamId']} {report['id']}`")
    return lines


def format_progress(agg: dict) -> list[str]:
    lines = ["", "## 進行記録"]
    if agg["sessions"] == 0:
        return lines + ["- なし"]
    lines.append(f"- 人数 {agg['testers']} 人 / セッション {agg['sessions']} 件")
    lines.append(f"- 平均プレイ時間 {agg['meanPlaySeconds'] / 60:.1f} 分")
    lines.append(f"- 平均研究完了数 {agg['meanResearch']:.1f}")
    lines.append("- 到達チャレンジ数の分布: " + top_line(agg["reachBuckets"], 5))
    lines.append("- 離脱時の最後のイベント上位: " + top_line(agg["lastEvents"], 5))
    lines.append("- 離脱時のUI状態上位: " + top_line(agg["lastUiStates"], 5))
    lines.append("- 終了理由: " + top_line(agg["endReasons"], 5))
    return lines


def format_runs(runs: list[dict], fallback: int) -> list[str]:
    lines = ["", "## 自動修正ラン"]
    if not runs:
        lines.append("- なし")
    else:
        lines.append("- " + " / ".join(f"{k} {v}件" for k, v in sorted(Counter(
            r["status"] for r in runs).items())))
        for run in runs:
            pr = f"#{run['prNumber']}" if run["prNumber"] else "PRなし"
            lines.append(f"- {run['id']} … {run['status']} / {pr} / "
                         f"base {run['base'] or '不明'} / {run['summary']}")
    if fallback:
        lines.append(f"- ⚠ finishedAt が無く mtime で日付を判定したラン {fallback}件")
    return lines


def truncate(body: str, limit: int, archive: Path) -> str:
    """Discord の1メッセージ上限に収める。切ったときは必ず全文の置き場を示す
    Fits one Discord message and always points at the archived full text when cut"""
    if limit <= 0 or len(body) <= limit:
        return body
    notice = f"\n…（切り詰め。全文 {len(body)}文字は {archive} ）\n"
    return body[: max(0, limit - len(notice))] + notice


def resolve_date(raw: str) -> str:
    """既定は JST の前日。書式違いは例外で落とす（黙って別の日を集計しない）
    Defaults to yesterday in JST; a malformed value raises rather than silently digesting another day"""
    if raw in ("", "yesterday"):
        return (datetime.now(dc.JST) - timedelta(days=1)).strftime("%Y-%m-%d")
    datetime.strptime(raw, "%Y-%m-%d")
    return raw


def main(argv: list[str] | None = None) -> int:
    default_logs = os.environ.get(
        "MOORESTECH_LOGS", str(Path.home() / "hermes-agent/data/repos/moorestech_logs"))
    parser = argparse.ArgumentParser(description="プレイテスト日次ダイジェスト / playtest daily digest")
    parser.add_argument("--date", default="yesterday", help="YYYY-MM-DD か yesterday")
    parser.add_argument("--logs", default=default_logs, help="moorestech_logs のルート")
    parser.add_argument("--max-chars", type=int, default=1800, help="標準出力の上限文字数")
    parser.add_argument("--no-archive", action="store_true", help="digests/ へ書かない")
    args = parser.parse_args(argv)

    date = resolve_date(args.date)
    logs = Path(args.logs)
    playtest = logs / "harness" / "playtest"
    reports = dc.load_reports(playtest / "reports", date)
    progress = dc.load_progress(playtest / "progress", date)
    runs, fallback = dc.load_fix_results(logs / "harness" / "bug-report" / "runs", date)

    lines = [f"# moorestech プレイテスト日次ダイジェスト {date}"]
    lines += format_counts(reports)
    lines += format_candidates(reports)
    lines += format_feedback(reports)
    lines += format_progress(dc.aggregate_progress(progress))
    lines += format_runs(runs, fallback)
    body = "\n".join(lines) + "\n"

    archive = playtest / "digests" / f"{date}.md"
    if not args.no_archive:
        archive.parent.mkdir(parents=True, exist_ok=True)
        archive.write_text(body, encoding="utf-8")

    sys.stdout.write(truncate(body, args.max_chars, archive))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
```

- [ ] **Step 4: テストを通してコミットする**

Run: `python3 scripts/playtest/tests/test_digest.py -v`
Expected: 9 tests PASS

```bash
git add scripts/playtest
git commit -m "feat(playtest): 前日分の感想・進行記録・投入候補・ラン結果を集計する日次ダイジェスト"
```

---

### Task 5: Hermes 内蔵 cron ジョブ（shim と登録手順）

**Files:**
- Create: `scripts/playtest/hermes-cron/moorestech-playtest-digest.sh`
- Create: `scripts/playtest/hermes-cron/README.md`
- Modify: `scripts/playtest/README-macmini.md`（「日次ダイジェスト」節を追記）

**Interfaces:**
- Consumes: Task 4 の `scripts/playtest/digest.py`
- Produces: `HERMES_HOME/scripts/moorestech-playtest-digest.sh`（コピー実体）と `hermes cron` ジョブ1件

- [ ] **Step 1: shim を書く**

`scripts/playtest/hermes-cron/moorestech-playtest-digest.sh`:
```bash
#!/usr/bin/env bash
# Hermes cron から moorestech の日次ダイジェストを呼ぶ shim。HERMES_HOME/scripts へ「コピー」して使う
# Shim that lets Hermes cron run the moorestech daily digest; it must be COPIED into HERMES_HOME/scripts
#
# Hermes は --script のパスを HERMES_HOME/scripts/ 配下へ解決してから realpath で検査するため、
# repo へのシンボリックリンクは「scripts ディレクトリ外への traversal」として拒否される。
# Hermes resolves --script under HERMES_HOME/scripts/ and then realpath-checks it, so a symlink
# into the repo is rejected as traversal; a real wrapper file is the supported shape.
# 前例 / precedent: ~/.hermes/scripts/monitors/tiktok-collector-monitor/check.sh
set -uo pipefail
REPO="${MOORESTECH_REPO:-~/hermes-agent/data/repos/moorestech}"
export MOORESTECH_LOGS="${MOORESTECH_LOGS:-~/hermes-agent/data/repos/moorestech_logs}"
exec /usr/bin/python3 "$REPO/scripts/playtest/digest.py" --date yesterday
```

- [ ] **Step 2: 登録手順を書く**

`scripts/playtest/hermes-cron/README.md`:
```markdown
# 日次ダイジェストの Hermes cron ジョブ

Hermes 内蔵 cron（launchd の cron ではない）に1件だけジョブを置く。実体は `~/.hermes/cron/jobs.json`。

## 分かっていること（実測 2026-09-13）

- 生きている `HERMES_HOME` は `~/.hermes`（封じ込め env の `data/.hermes` ではない。`data/cron/jobs.json` は古い残骸）
- `--script` のパスは `HERMES_HOME/scripts/` 配下へ解決され、realpath で外へ出るものは拒否される。**symlink は使えない**（実ファイルの shim を置く）
- `--no-agent` を付けるとスクリプトの stdout がそのまま配信され、LLM を通らない。stdout が空だと無投稿になる
- `--deliver discord:<チャンネルID>` の書式は既存ジョブ（doujin-spy 監視・tiktok collector レポート）で使われている
- `--script` を含むジョブの作成はシェルフックの承認を求めることがある。TTY 無しで作るときは `hermes cron --accept-hooks create ...`

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
```

- [ ] **Step 3: `README-macmini.md` に節を足す**

`scripts/playtest/README-macmini.md` の末尾に追記:
```markdown
## 日次ダイジェスト

Hermes 内蔵 cron で毎朝9時に投稿する。手順は `scripts/playtest/hermes-cron/README.md`。

- 手動確認: `MOORESTECH_LOGS=~/hermes-agent/data/repos/moorestech_logs python3 scripts/playtest/digest.py --date yesterday`
- 全文は `moorestech_logs/harness/playtest/digests/<日付>.md`。次の取り込み周期（最大5分）で logs repo へ commit される
```

- [ ] **Step 4: コミットする**

```bash
chmod +x scripts/playtest/hermes-cron/moorestech-playtest-digest.sh
git add scripts/playtest
git commit -m "feat(playtest): 日次ダイジェストのHermes cron shimと登録手順"
```

---

### Task 6: plan A/B/C へ ADR 0061 の改訂メモを追記する

**Files:**
- Modify: `docs/superpowers/plans/2026-09-11-bug-report-a-server-foundation.md:2`（コントローラ用ブロックの直後）
- Modify: `docs/superpowers/plans/2026-09-11-bug-report-b-client-capture-and-report-ui.md:2`
- Modify: `docs/superpowers/plans/2026-09-11-bug-report-c-transport-and-auto-fix-run.md:2`

**Interfaces:**
- Consumes: ADR 0061、共有契約 §1〜§3・§6
- Produces: 3つの plan の先頭にある改訂ブロック（plan A/B/C の実装者が最初に読む）。実装コードは変更しない。

- [ ] **Step 1: plan A に改訂ブロックを追記する**

`2026-09-11-bug-report-a-server-foundation.md` のコントローラ用引用ブロックの直後（3行目）に挿入:
```markdown
> **改訂（ADR 0061 / 2026-09-13・plan H より）:** 配布版のテスターも送り手になり、クラッシュ報告が対象に入った。本planで変わるのは次の2点だけで、他は原文どおり。
> 1. **常時記録のリングを終了時に消さない。** サーバーのスナップショットリングと受信パケットログは、正常終了時もファイルを残す（次回起動時に「前回異常終了」を検知して前回分を送るため）。消すのは新しいセッションが自分のリングを作り直すときだけ。
> 2. **`kind=crash` の箱が存在する。** クラッシュ箱は plan C の自動修正ランを起動しない（日次ダイジェストに件数として載るだけ）。サーバー側の再現ツール（`SnapshotReplayer`・`SnapshotJsonComparer`）の要件は変わらない。
```

- [ ] **Step 2: plan B に改訂ブロックを追記する**

`2026-09-11-bug-report-b-client-capture-and-report-ui.md` の3行目に挿入:
```markdown
> **改訂（ADR 0061 / 2026-09-13・plan H より）:** 受け口（Cloudflare Worker + R2）と進行記録が加わり、manifest が変わった。
> 1. **`manifest.json` に `kind`（"bug"|"feedback"|"crash"）・`steamId`（string、Steam未起動なら ""）・`buildInfo` を足す。** `buildInfo` は `StreamingAssets/build-info.json`（`commit`・`branch`・`masterDataCommit`・`dirty`・`steamBuildLabel`・`builtAt`・`target`）で、原文の `BugReportManifest.Repository` を置き換える。Editor 実行時は `build-info.json` が無いので原文の git probe を使い `buildInfo` は null。
> 2. **報告UIに種別（バグ／感想）の選択を足す。** どちらも同じバンドルを送る。自動修正ランが起動するのはバグだけ。
> 3. **進行記録の outbox を足す。** `<GameSystemDirectory>/ProgressRecords/outbox/<id>/record.json` + `READY`、送信後 `UPLOADED`。中身は共有契約 §3（`playSeconds`・`reachedChallenges`・`completedResearch`・`lastUiState`・`events[]` 等）。イベントは購読で取り、`Update()` の同値判定は足さない。
> 4. **前回異常終了の検知。** `<GameSystemDirectory>/BugReports/last-session/` に正常終了マーカー `CLEAN_EXIT` を置き、起動時に無ければタイトルで送信確認を出す（`kind=crash`）。常時記録のリング（`BugReports/recording/`）は終了時に消さない。
> 5. **送信済みマーカーは2種。** 受け口へ上げたら `UPLOADED`、開発者の rsync 経路（plan C）は `SHIPPED`。
> 6. **Windows 配布ビルドに `ffmpeg.exe` を同梱する**（録画が配布版でも動くこと。同梱は plan E、依存の明示は本plan側）。
```

- [ ] **Step 3: plan C に改訂ブロックを追記する**

`2026-09-11-bug-report-c-transport-and-auto-fix-run.md` の3行目に挿入:
```markdown
> **改訂（ADR 0061 / 2026-09-13・plan H より）:** テスターからの報告は受け口経由で届き、plan H の `scripts/playtest/ingest.sh` が `moorestech_logs/harness/playtest/` へ保存する。**そこから `inbox/<id>/` へ入れるのは自動ではなく、人が日次ダイジェストを見て `scripts/playtest/enqueue-autofix.sh <steamId> <id>` を叩いたときだけ**（裁定 2026-09-13・[[2026-09-13-テスター報告は自動投入せず日次ダイジェストを見て人が自動修正ランへ投入する]]）。本planの inbox 契約（`inbox/*/READY` を古い順に1件・`runs/<id>/` へ移動）と自動修正ランの中身はそのまま。変わるのは次の5点。
> 1. **manifest のコミット参照は `manifest.buildInfo.commit` / `manifest.buildInfo.masterDataCommit`。** `ship-outbox.sh` と `prepare-run.sh` の `manifest.repository.commit` / `masterData.commit` を読み替える（配布版の箱には `repository` が無い）。`buildInfo` が null（Editor実行）のときだけ原文の `repository` を見る。
> 2. **poller は `kind=bug` だけを走らせる。** inbox に入るのは開発者の rsync 経路（感想・クラッシュも運ぶ）と人の手動投入分の2つ。`inbox-poller.sh` は `manifest.kind` を見て `bug` 以外なら `runs/` へ移さず `skipped/<id>/` へ退避し、理由を `[poller]` ログに出す（無音で捨てない）。`kind` が読めない箱も同じく `skipped/` 行き。**例外は箱に `AUTOFIX_FORCED` がある場合**（人が `enqueue-autofix.sh --force` で意図的に入れた印）で、このときは `kind` を問わず走らせ、`[poller] forced: kind=<値>` をログに出す。
> 3. **`kind=crash` は自動修正ランを起動しない。** 日次ダイジェストに件数として載るだけ（ADR 0061）。
> 4. **`fix-result.json` に `finishedAt`（ISO8601・UTC）を足す。** 日次ダイジェストがランの日付をこれで判定する（ラン id の日付はバグ報告が作られた日であってランの日ではない）。書くのはスキル `bug-report-auto-fix` の Step 9 と、poller が result 欠落を埋めるとき。
> 5. **logs repo のベースブランチは `main`。** 本planの Task 2 が `origin/master` と書いているのは誤り（`moorestech_logs` の既定ブランチは `main`）。
```

- [ ] **Step 4: 差分を確認してコミットする**

Run: `git diff --stat docs/superpowers/plans/`
Expected: 3ファイルがそれぞれ +6〜+10 行

```bash
git add docs/superpowers/plans
git commit -m "docs: plan A/B/C に ADR 0061 の改訂メモを追記"
```

---

### Task 7: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

- [ ] **Step 1:** `feature/playtest-ingest-digest` の全差分に対して moores-code-review スキルを実行する（`git diff origin/master...HEAD`）。`../moorestech_logs` 側のブランチ `feature/playtest-ingest` の差分もレビュー対象に含める。
- [ ] **Step 2:** 指摘のうち機械的に確定するものを反映し、`bash scripts/playtest/tests/test-ingest.sh`・`bash scripts/playtest/tests/test-enqueue-autofix.sh`・`python3 scripts/playtest/tests/test_digest.py` を**再実行**して緑を確認する（レビュー反映が判定経路・条件式に触れたら必ず再実行する。テスト通過の記憶で代替しない）。
- [ ] **Step 3:** 設計判断が残る指摘は AskUserQuestion でまとめて裁定を仰ぐ。
- [ ] **Step 4:** 反映をコミットし、`pr-create` で本repoの PR を作る。本文に logs repo 側 PR の URL を書く。

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置 | 前例・根拠 |
|---|---|---|---|
| 1 | `ingest.sh`・`ingest-dispatch.sh`・`enqueue-autofix.sh`・`digest.py`・`digest_collect.py`・README | `scripts/playtest/` | plan C が `scripts/bugreport/` に運搬・poller を置いた前例と同型。Mac mini 固有パスは env の既定値にし本体は環境非依存 |
| 2 | 受け口 API ラッパ | `scripts/playtest/lib/receiver-api.sh` | 1ファイル200行以下・1ディレクトリ10ファイルの規約に合わせ、curl 差し替え点を1箇所へ集約。plan C の `SSH_CMD`/`RSYNC_CMD`/`GIT_CMD` 差し替えと同じ思想 |
| 3 | dispatch/worker の2段構え | `ingest-dispatch.sh` が `nohup` で `ingest.sh` を切り離す | `~/hermes-agent/data/services/always-on/scripts/repo-auto-pull-dispatch.sh` が `nohup` で `repo-auto-pull.py` を切り離すのと同型。periodic は supervisor のメインループで同期実行されるため（CLAUDE.md 落とし穴1） |
| 4 | supervisor 登録 | `services.json` の `periodic`（300s・timeout 600s） | 既存 `unity-modal-watchdog`（120s・timeout 600s）と同じ書き方。`services.json` はループ毎に再読込されるので supervisor 再起動は不要 |
| 5 | ラン結果・取り込み物の置き場 | `moorestech_logs/harness/playtest/` | AGENTS.md「実行記録はコードrepoに置かず `../moorestech_logs/harness/` へ」。`harness/bug-report/` と兄弟 |
| 6 | 自動修正ランへの受け渡し | 人が叩く `enqueue-autofix.sh` → plan C の `harness/bug-report/inbox/<id>/`（`READY` 付き・`.partial`→`mv`） | plan C の `ship-outbox.sh` が使う契約をそのまま満たす（書き手が「開発者のMacBook」「人の手動投入」の2人になるだけ）。poller 側は一切変更しない（種別ガードと `AUTOFIX_FORCED` の尊重だけ Task 6 の改訂メモで plan C 側に足す） |
| 6a | 投入の起動主体 | 取り込みではなく人の明示コマンド | ADR 0061 の裁定「テスターからのバグ報告は自動修正ランへ自動投入しない」。`release-playtest.sh`（plan E）と同じ「起動は手動コマンド」の形 |
| 7 | Hermes cron の shim | `HERMES_HOME/scripts/moorestech-playtest-digest.sh`（repo からコピー） | `~/.hermes/scripts/monitors/tiktok-collector-monitor/check.sh` が同じ理由（symlink は traversal 判定で拒否）で実ファイルの wrapper になっている |
| 8 | 投稿方式 | `--no-agent`（stdout をそのまま配信） | 既存の `--no-agent` ジョブ群（FANZA 売上・chrome-tab-reaper・daily-5am-digest）と同型。ADR 0061 の「感想全文」を LLM に要約させないための機構選択でもある |
| 9 | 日付の正 | 箱の `ingest.json.readyAt`（受け口が付ける） | ファイル mtime やラン id の日付接頭辞に依存しない。`fix-result.json` は `finishedAt` を plan C 側へ足して同じ形に揃える |

**Phase 1.5 データフロー地図**

```
受け口(R2 READY) →［plan H ingest.sh］→ moorestech_logs/harness/playtest/ →［digest.py］→ Discord →（人が読んで選ぶ）
                                                        ↑ AUTOFIX_QUEUED     ↓
                                          ［plan H enqueue-autofix.sh（人が起動）］
                                                        ↓
                                     harness/bug-report/inbox/<id>/ →［plan C inbox-poller.sh（既存・無改造）］→ runs/<id>/fix-result.json →（翌朝の digest へ）
```

本planの新規コンポーネントは全て**書き手**（共有の置き場へ書くだけ）と**読み手**（置かれたものを読んで整形するだけ）で、既存パイプラインへの交差点（分岐・逆流・並行経路）を足さない。plan C の poller は購読も呼び出しもされず、既存どおり `inbox/*/READY` を見るだけである。`enqueue-autofix.sh` は自動化された連鎖の一部ではなく**人が起点の書き手**であり、取り込みと自動修正ランの間に意図的な人の関門を1つ置く（ADR 0061 の裁定）。

**検査4（機構選択）: 受動的統合案と能動介入案の比較**

- 採用（受動的統合）: 取り込みは既存 inbox 契約の**前段に1人書き手を増やすだけ**。plan C の poller・ロック・スキル・`unattended-gate.py` を一切触らない。
- 棄却（能動介入）: 取り込み側が poller を直接呼ぶ／poller に受け口ポーリングを内蔵する。→ 単一飛行ロックの所有者が2つになり、rsync 経路と受け口経路でランの直列性が壊れる。既存機構を無傷で活かすと成立しない理由が無いため棄却。
- 棄却（能動介入）: 種別ゲートを投入コマンド側だけに置く。→ 開発者 rsync 経路（plan C）から感想・クラッシュが直接 inbox に入ると素通りする。ゲートは**両方の入口の合流点である poller**にも要る（Task 6 の plan C 改訂メモ 2）。投入コマンド側の `--force` は `AUTOFIX_FORCED` マーカーで poller のゲートに意図を伝える（ゲートを消さずに例外を明示する形）。

**Phase 2.5 死活表（既存機構にぶら下がる操作）**

| 操作 | 計画後も生きるか | 根拠 |
|---|---|---|
| plan C の rsync 経路（開発者の MacBook → inbox） | 生きる | 触らない。inbox に別の書き手が増えるだけ |
| plan C の `inbox-poller.sh` の単一飛行ロック | 生きる | 別プロセス・別ロック（`moorestech-playtest-ingest.lock`）。poller のロックは共有しない |
| supervisor の他 periodic（トンネル再起動・死活監視・unity-modal-watchdog） | 生きる | dispatch が即座に戻るためメインループを塞がない（`timeout_seconds: 600` は保険） |
| 既存 Hermes cron ジョブ11件 | 生きる | ジョブを1件足すだけ。`jobs.json` の他エントリに触れない |
| logs repo の `logs-sync` 自動コミット | 生きる | `ingest.sh` の commit は `harness/playtest` だけを `git add` する。他のパスをステージしない |

## 判断記録（ADR）

- 設計ADR: `docs/adr/0061-steam-closed-playtest-report-receiver-and-save-compat.md`（正）、`docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md`（改訂3裁定以外は有効）。裁定: `.decisions/2026-09-13-プレイテスト報告の受け口はCloudflare Worker+R2としMac miniは取り込むだけにする.md`・`2026-09-13-感想とテレメトリは日次ダイジェストをHermes経由でDiscordへ投稿して読む.md`・`2026-09-13-感想もポーズメニューの報告UIで受け種別バグと感想を選ばせる.md`・`2026-09-13-クラッシュは次回起動時に前回の異常終了を検知し記録を送るか聞く.md`・`2026-09-13-プレイ状況はセッションサマリとイベント列を自動送信する.md`・**`2026-09-13-テスター報告は自動投入せず日次ダイジェストを見て人が自動修正ランへ投入する.md`**
- 共有契約: セッションの `scratchpad/plans/shared-contracts.md` §4・§6（本plan の Global Constraints へ逐語転記済み。plan D〜H で共有）。ただし §6 の「`kind=bug` のプレイ報告だけ plan C の `inbox/<id>/` へも複製」は 2026-09-13 の後続裁定（ADR 0061 追記）で**取り込みの責務から外れ、人の手動コマンドへ移った**。契約の他の項目は変更なし
- 進行記録 `record.json`（共有契約 §3）の正本は plan G の「共有契約」節。ADR 0060 の裁定6・裁定9 で **`missing` 列の追加（`headerMissing` は廃止してこの列へ畳んだ）**・**`blockPlaced.data` が `{"count":N}` の区間合計**・**イベント名 `craftExecuted` → `craftRequested`** に改訂済み。本plan の digest が読む `playSeconds`・`reachedChallenges`・`completedResearch`・`lastUiState`・`endReason` は改訂の影響を受けない（読みは `.get()` で未知キーを拒否しない）
- **テスター報告は自動投入しない**（ユーザー裁定 2026-09-13）: ADR 0061 追記「テスターからのバグ報告は自動修正ランへ自動投入しない。取り込みは moorestech_logs への保存までで止め、日次ダイジェストを見て人が手動コマンドで plan C の inbox へ投入する」。ADR 0057 の inbox 監視→自動ランは開発者本人の rsync 経路にだけ残る。棄却案: 全件自動投入し類似はラン内で既存PRへ寄せる／自動投入だが1日の上限件数
- **`--force` は `AUTOFIX_FORCED` マーカーで poller に伝える**（agent前提）: 種別ゲートは inbox の合流点（poller）にも要るため、投入側の `--force` がゲートを素通りできると片方が無意味になる。ゲートを残したまま例外を明示する形として、`--force` のときだけ inbox コピーに印を付け poller がそれを尊重する
- **ダウンロード対象は `READY` 本文の `files[]` から取る**（plan D へ反映済み）: 共有契約 §4 には R2 のオブジェクト一覧 API が無く、`GET /v1/inbox/{...}/{path}` は1オブジェクト取得しかできない。「`POST /v1/uploads/{kind}/{id}/complete` が書く READY の要約 JSON に、その箱の全ファイルの相対パス配列 `files` を含める」は plan D 側へ反映済み（コーディネータ確認 2026-09-13）。一覧が無い箱は ack せずエラーログを出して据え置く（fail-closed。受け口を直せば次の周期で自然に流れる）
- **本plan は未ACK前提のまま**（コーディネータ裁定 2026-09-13）: ACK 済みも引ける照会（`GET /v1/inbox?includeAcked=true`）は plan E が plan D へ足す予定。本plan の取り込みは「未ACKのものが `GET /v1/inbox` に出続ける」契約だけに依存し、`includeAcked` は使わない
- **`fix-result.json` に `finishedAt` を足す**（agent前提。Task 5 で plan C へ申し送り）: ラン id の日付接頭辞は「バグ報告が作られた日」であってランが終わった日ではない。前日分ダイジェストを id で絞ると、古い報告に対する今日のランが永久にどの日のダイジェストにも載らない。`finishedAt` を正とし、欠けている分だけ mtime に落として**件数を出力に明示**する
- **日付の正は `readyAt`（受け口が付ける時刻）**（agent前提）: 取り込みの遅延・再実行で集計対象が動かないようにするため。`ingest.json` に写して digest がそれだけを読む
- **`--no-agent` で投稿する**（agent前提）: ADR 0061 は「感想全文」を要求しており、LLM を挟むと要約・整形が入りうる。既存の `--no-agent` ジョブ群と同型。エージェントモード用のプロンプト本文は README に保存し、切り替えを可能にしておく（既存「ポケモンカードPSA10最安値監視」ジョブが `--no-agent` とプロンプトを両方持っている前例に倣う）
- **Hermes の `--script` は `HERMES_HOME/scripts/` 内の実ファイルでなければならない**（実測 2026-09-13）: `cron/scheduler_script.py` が `resolve()` 後に `relative_to(scripts_dir)` で検査するため、repo へのシンボリックリンクは拒否される。前例 `~/.hermes/scripts/monitors/tiktok-collector-monitor/check.sh` のコメントに同じ理由が書かれている。**生きている `HERMES_HOME` は `~/.hermes`**（プロセス実測。CLAUDE.md が書く封じ込め `data/.hermes` ではなく、`data/cron/jobs.json` は古い残骸）
- **logs repo の PR ベースは `main`**（実測）: `moorestech_logs` の `origin/HEAD` は `main`。plan C Task 2 の `origin/master` は誤りで、Task 6 の改訂メモで訂正する
- **取り込みは1件ずつ独立に失敗する**（agent前提）: 1つの壊れた箱で全体を止めない。異常箱は ack されないため受け口に残り、直せば次の周期で流れる。証拠（未 ack 状態）は R2 側にあり Mac mini の再起動・`/tmp` の消去では失われない
- **二重投入防止は箱側の `AUTOFIX_QUEUED` マーカー**（agent前提）: poller は `inbox` から箱を持ち去るので、inbox の有無では「投入済みか」を判定できない。判定の記録は logs repo 側の箱（永続・git 管理）に置く。日次ダイジェストの投入候補一覧も同じマーカーで絞るため、投入した報告は翌日の候補から自然に消える
- Task 1 の logs repo PR URL: （実装時に転記）
- Task 7 のレビュー結果と AskUserQuestion の裁定: （実装時に転記）

## Self-Review

1. **Requirements coverage:** R1→Task 1、R2→Task 2 Step 2、R3→Task 2 Step 3、R4→Task 3、R5→Task 2 Step 3（`files[]` 検査・`|| true`）、R6→Task 2 Step 4・5、R7→Task 4（`format_candidates` が (e)）、R8→Task 4（`truncate`）、R9→Task 5、R10→Task 6、R11→Task 7。漏れなし。
2. **Placeholder scan:** 「実装時に転記」は判断記録の実行結果欄のみ（plan C と同形式）。タスク本文にコード無しステップは無い。
3. **Type consistency:** `ingest.json` の5キー（`kind`・`steamId`・`id`・`readyAt`・`ingestedAt`）は Task 2 の `printf` と Task 4 の `collect_boxes`/`load_reports` で一致。`AUTOFIX_QUEUED` は Task 3 だけが書き（本文 `queued at <ISO8601>`）、Task 4 の `load_reports` は**存在の有無だけ**を `queued: bool` として読む（本文形式に依存しない）。`AUTOFIX_FORCED` は Task 3 が inbox コピーに書き、Task 6 の plan C 改訂メモ2 が poller 側の読み手を規定する。`fix-result.json` の `finishedAt` は Task 4 の `load_fix_results` と Task 6 の plan C 改訂メモ4で一致。`load_fix_results` はタプル `(list, int)` を返し、`digest.py` も2値で受けている。`enqueue-autofix.sh` の終了コード（0/1/2/3）は Interfaces・README・テストの3箇所で一致。
4. **保留・縮退経路の解消可能性:** 保留は4つ。(a) `files[]` 欠落 → 解消条件「受け口が `complete` で `files` を書く」（plan D 反映済み）。未 ack の証拠は R2 側にあり Mac mini の再起動・`/tmp` 消去・プロセス死でも失われない（封鎖状態の寿命 < 証拠の寿命）。最小構成: inbox 0件（何もせず終了）・1件かつそれが壊れている（他が無くても次回再試行される）・初回実行（ディレクトリが無い＝`mkdir -p` で作る）をテストに含めた。(b) ack 失敗 → 次回は `dest` 既存として再 ack のみ。テストで再配信ケースを検証。(c) 上限50件超過 → 未 ack のまま残り次回の `GET /v1/inbox` に再び現れる（ack しないものは inbox から消えない契約）。(d) **バグ報告が自動修正ランへ回らない状態**は今回の裁定で「保留」ではなく**既定の状態**になった。解消の主体は人であり、解消の証拠（`AUTOFIX_QUEUED` の不在）と解消の呼びかけ（日次ダイジェストの投入候補一覧）はどちらも logs repo と毎朝の Discord 投稿にあり、揮発領域にもプロセスメモリにも置かれない。人が投入しなければ永久に走らないが、それは無音ではなく**毎朝候補として再掲され続ける**（候補が消えるのは投入したときだけ）。0件の日も「なし」を出すので、一覧が出ない＝ダイジェストが止まっている異常として気づける。
   **裁定反映で直した点:** `ingest.sh` から自動複製と `AUTOFIX_QUEUED` の自動付与を外した結果、旧設計にあった「`mv` 成功後・複製前にプロセスが死ぬと恒久保留になる」穴自体が消えた（取り込みは保存と ack しかしない）。代わりに投入は人が起点の単発コマンドになり、失敗しても人がその場で結果を見て再実行できる。
5. **「決定的にする」を根拠にした選択規則:** 日付判定を `readyAt` にしたのは決定性のためだけでなく、「取り込みの遅延・再実行で集計対象の集合が変わらない」という下流の正しさのため。`sorted(glob())` の順序は digest の表示順にしか効かず、集計値（人数・平均・分布）は順序不変。投入候補一覧の並び順も表示順のみで、どれを選ぶかは人が決めるため下流に構造を作らない。`Counter.most_common` の同数タイブレークも同様。
   **Self-Review で直した点:** 自動修正ランの日付をラン id の接頭辞（決定的だが「報告日」であってラン日ではない）で絞っていた。決定性は正しさの保証ではないため、`fix-result.json` に `finishedAt` を足す形（Task 6 の plan C 改訂メモ4）へ変更し、欠落時の mtime フォールバックは件数を出力に出すようにした。

## Execution Handoff

planが完成し`docs/superpowers/plans/2026-09-13-playtest-h-ingest-and-daily-digest.md`に保存されました。新規セッションを開き、以下を貼り付けて実装を開始してください:

```
subagent-driven-development スキルを使って、以下の実装planを実行してください。

- plan: docs/superpowers/plans/2026-09-13-playtest-h-ingest-and-daily-digest.md
- 作業場所: feature/playtest-ingest-digest（moores-wt new で使い捨てworktreeを切る。Unityは使わないので --no-editor でよい）
- まずplan全文を読み、`## Requirements`・`## Global Constraints`・`## 判断記録（ADR）`を全タスク共通の制約として扱ってください
- 進捗管理はsubagent-driven-developmentスキルの規定に従ってください（SDD本体はplanのチェックボックス＋進捗台帳、単一subagent実装モードは報告ファイル＋進捗台帳が正）
- planの最終タスク（moores-code-reviewスキルによる全ブランチレビュー）は省略不可です
```
