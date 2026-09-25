# playtest-receiver — プレイテスト報告の受け口（Cloudflare Worker + R2）

配布版 moorestech（Steam プレイテスト）からプレイ報告と進行記録を受け取り、R2 に貯める Worker。
Mac mini（plan H の `scripts/playtest/ingest.sh`）が管理APIで取り込む。設計は `docs/adr/0058-steam-closed-playtest-report-receiver-and-save-compat.md`。

## エンドポイント

| メソッド | パス | 認証 | 用途 |
|---|---|---|---|
| POST | `/v1/session` | なし（Steamチケット） | チケット検証＋1時間トークン発行 |
| POST | `/v1/uploads/{kind}/{id}/prepare` | `Authorization: Bearer` | ファイル宣言を受け、ファイルごとの署名付き R2 PUT URL を発行する |
| PUT | `/v1/uploads/{kind}/{id}/{path...}` | なし（410固定） | 廃止。旧クライアントの中継PUTは410 `direct-upload-required`（ADR 0064） |
| POST | `/v1/uploads/{kind}/{id}/complete` | `Authorization: Bearer` | R2の実オブジェクトを宣言と照合し、揃っていれば `READY` と未ACK索引を書く |
| GET | `/v1/inbox?cursor=` | `X-Admin-Key` | 未ACKの一覧 |
| GET | `/v1/inbox/{kind}/{steamId}/{id}/{path...}` | `X-Admin-Key` | 個別ファイル取得 |
| POST | `/v1/inbox/{kind}/{steamId}/{id}/ack` | `X-Admin-Key` | `ACKED` を書き索引を消す |

`kind` は `report` / `progress`。R2 のキーは `reports/{steamId}/{id}/...` と `progress/{steamId}/{id}/...`（`src/keys.ts` の `KIND_PREFIX` が正本）。

Worker はアップロードのバイト列を中継しない。クライアントは `prepare` で署名付き URL を取り、R2 へ直接 PUT する（Workers 無料プランの CPU 上限のため。ADR 0064）。

### `POST /v1/uploads/{kind}/{id}/prepare`

リクエスト本文（1ファイル100MiB＝`maxFileBytes`まで、箱全体128ファイル＝`maxBundleFiles`・256MiB＝`maxBundleBytes`まで。値は `contract.json` が正）:
```json
{ "generation": 1, "files": [{ "path": "manifest.json", "bytes": 10 }, { "path": "frames/frame_1.jpg", "bytes": 20 }] }
```
成功（200）:
```json
{ "outcome": "prepared", "uploads": [{ "path": "manifest.json", "bytes": 10, "url": "https://<account>.r2.cloudflarestorage.com/<bucket>/...?X-Amz-..." }], "conflicts": [], "expiresInSeconds": 3600 }
```
ACK済みの箱は書き込みをせず冪等に `{ "outcome": "acked" }`（200）を返す（ACK は受付時・`DECLARED` を書く直前・URL を返す直前の3回確かめる）。クライアントの失敗分類を左右する `outcome` の値と `declaration-conflict` / `declaration-unreadable` の理由語は `contract.json`（`prepareOutcomes` / `declarationConflictReason` / `declarationUnreadableReason`）が正。

- **宣言は世代付きの write-once**: `generation` は1以上の整数（欠落・非整数・0以下は400 `bad-request`）。`DECLARED` は `{ generation, files }` で保存する。最初の prepare が `DECLARED` を条件付き put（キーが無いときだけ）で書く。以後の prepare は、同じ世代なら path+bytes の集合（順序は問わない）の完全一致だけを許して書かずに URL を出し直す。上の世代なら既存宣言の部分集合（全 path が既存宣言にあり各 bytes が一致）だけを許し、読んだ etag での条件付き put（CAS）で置き換える（送れないファイルを外した再試行のため。件数・総量は増えない）。同世代の集合違い・上の世代での path 追加や bytes 変更・下の世代は409 `{ "reason": "declaration-conflict" }` で拒否して `DECLARED` を書き換えない。同時 prepare に条件付き put で負けたら1回だけ読み直して判定し直し、2回負けたら409 `declaration-conflict`。クライアントはこれを箱固有の恒久失敗として数える（complete の409 `incomplete` / `not-prepared` は再試行対象で、区別は `reason`）。
- **読めない `DECLARED`**: JSON として壊れている・宣言検査に通らない・`generation` が無い `DECLARED` は無い扱いにせず、prepare も complete も409 `{ "reason": "declaration-unreadable" }`（warn に理由）。人が箱を調べるまで進めない。
- **送信済みは除外・長さ違いは conflicts**: 存在と長さの判定は complete と同じ照合（箱の prefix の list 1回）を使う。宣言どおりの長さで既にあるファイルは `uploads` に載せない。キーが無いファイルだけに URL を出す。宣言と長さの違うキーが既にあるファイルは `If-None-Match: *` で上書きできないため URL を出さず、`conflicts: [{ "path", "expectedBytes", "actualBytes" }]` で返して warn する（`conflicts` は空でも常に配列）。全部送信済みなら `uploads` は空配列で、クライアントは PUT せず complete の照合に任せる。
- **設定漏れ**: `R2_ACCOUNT_ID` / `R2_BUCKET_NAME` / `R2_ACCESS_KEY_ID` / `R2_SECRET_ACCESS_KEY` のどれかが空なら URL を発行せず500 `{ "reason": "server-misconfigured" }`（`console.error` に空の設定名）。

エラーは `{ "reason": <string> }` 形（本文が JSON でない/`files`が配列でない等は400 `bad-request`、宣言0件は400 `empty-declaration`、危険パス400 `bad-path`、予約名400 `reserved-name`、重複パス400 `duplicate-path`、ファイル数超過は413 `too-many-files`、1ファイル超過は413 `too-large`、合計超過は413 `bundle-too-large`）。発行URLは `Content-Length` と `If-None-Match: *` を含めて署名するため（`X-Amz-SignedHeaders=content-length;host;if-none-match`）、宣言と違う長さのPUTと、既にあるキーへの上書きをR2自身が拒否する。

### PUT（署名付きURLへ直接）

クライアントは `prepare` が返した URL へ、宣言どおりの `Content-Length` と `If-None-Match: *` を付けて PUT する（Bearer トークンは不要。署名がそれを兼ねる）。Worker はこの通信を経由しない。キーが既にあると R2 は412を返すので、クライアントはそのファイルを送信済み扱いにして続行する（存在と長さは complete が照合する）。

### `POST /v1/uploads/{kind}/{id}/complete`

本文は任意で `{ "manifest": "<原文>", "skipped": [...] }`（無くても・壊れていても READY は書かれる。`manifest`/`skipped` は取り込みの診断補助）。
Worker は宣言済みファイルを R2 で列挙し、存在と長さを照合してから `READY` を書く。揃っていれば200 `{ "ready": true, "fileCount": <n> }`（ACK済みは書かずに冪等の200 `{ "ready": true }`。ACK は受付時と `READY` を書く直前に確かめる）。1つでも欠けや長さ違いがあれば `READY` を書かず409 `{ "reason": "incomplete", "missing": [{ "path": ..., "expectedBytes": ..., "actualBytes": <実際の長さ or null> }] }`。宣言（`prepare`）が無い箱への complete は409 `{ "reason": "not-prepared" }`、`DECLARED` が読めない箱は409 `{ "reason": "declaration-unreadable" }`。`READY` の `files` はこの照合で確定した一覧で、クライアントの申告は使わない。

## 初回セットアップ

0. Cloudflare へ認証する（未認証だと以下の `wrangler` コマンドが対話ログインで止まる、または複数アカウント環境では account 選択で失敗する）。複数アカウントを使い分けるマシンでは**認証プロファイル**でこのディレクトリを対象アカウントへ固定する:
   ```bash
   cd tools/playtest-receiver
   pnpm exec wrangler auth list                  # 作成済みプロファイルの一覧（無ければ auth create <profile> でログイン）
   pnpm exec wrangler auth activate <profile>    # 一度だけ。以後このディレクトリ配下の wrangler / pnpm run deploy はそのアカウントで動く
   pnpm exec wrangler whoami                     # 紐付け先のアカウントを確認（whoami は --profile を受け付けない）
   ```
   - 紐付けずに1回だけ指定するなら各コマンドへ `--profile <profile>` を付ける。解除は `wrangler auth deactivate`。
   - **`CLOUDFLARE_API_TOKEN` が環境にあるとプロファイルより優先される**ので、プロファイルを使うシェルでは `unset CLOUDFLARE_API_TOKEN` しておく。プロファイルを使わない環境（CI 等）は従来どおり `wrangler login` か `CLOUDFLARE_API_TOKEN` でよい。
   - `whoami` に Account が複数出る場合は `CLOUDFLARE_ACCOUNT_ID` を対象アカウントの ID に設定する。`wrangler.toml` に `account_id` は書かない（秘密ではないが環境依存の値のため、環境変数側で解決する）。
   - `routes` の `playtest.moores.tech`（custom domain）へ出せるのは **moores.tech ゾーンを持つアカウント（sakastudio@moores.tech）だけ**。別アカウントへデプロイすると手順4が失敗する。
1. R2 バケットを作る:
   ```bash
   cd tools/playtest-receiver
   pnpm install
   pnpm exec wrangler r2 bucket create moorestech-playtest
   ```
2. secrets を入れる（値は Steamworks パートナーサイトと `openssl rand -hex 32` から）:
   ```bash
   pnpm exec wrangler secret put STEAM_WEB_API_KEY     # Steamworksパートナーサイトのpublisher key
   pnpm exec wrangler secret put SESSION_HMAC_SECRET   # openssl rand -hex 32
   pnpm exec wrangler secret put ADMIN_KEY             # openssl rand -hex 32
   ```
2b. R2 の署名付き URL 用に API トークンを作る（Cloudflare ダッシュボード → R2 → Manage R2 API Tokens → Create API token）。
    権限は「Object Read & Write」、対象バケットは `moorestech-playtest` だけに限定する。表示される Access Key ID と Secret Access Key を secrets に入れる:
    ```bash
    pnpm exec wrangler secret put R2_ACCESS_KEY_ID
    pnpm exec wrangler secret put R2_SECRET_ACCESS_KEY
    ```
    アカウント ID とバケット名は `wrangler.toml` の `[vars]`（`R2_ACCOUNT_ID` / `R2_BUCKET_NAME`）。
    クライアントは Worker が返す署名付き URL（`https://<account>.r2.cloudflarestorage.com/<bucket>/<key>?X-Amz-...`）へ直接 PUT する。
    Worker はバイト列を中継しない（Workers 無料プランの CPU 上限のため。ADR 0064）。
3. デプロイ前に `wrangler.toml` の `compatibility_date`（現在 `2026-08-22`。手元 workerd テスト環境が解釈できる上限に固定してある）を Cloudflare の最新日付へ見直す。
4. デプロイする:
   ```bash
   pnpm run deploy
   ```
5. DNS: `wrangler.toml` の `routes` に `playtest.moores.tech` を `custom_domain = true` で書いてあるので、`pnpm run deploy` が moores.tech ゾーンへ CNAME を作る。作られない場合は Cloudflare ダッシュボード → Workers & Pages → moorestech-playtest-receiver → Settings → Domains & Routes → Add → Custom domain に `playtest.moores.tech` を追加する。**cloudflared のトンネル（Mac mini）とは無関係の経路なので、`~/.cloudflared/*.yml` は触らない。**
6. Mac mini 側の env ファイルを作る。ingest がこの env から `PLAYTEST_RECEIVER_BASE`・`PLAYTEST_ADMIN_KEY` を読む。ヒアドキュメントは Markdown リスト内の字下げでコピー時に終端行を見失うため、`echo` を積み上げる形にしてある:
   ```bash
   mkdir -p ~/hermes-agent/data/services/playtest
   {
     echo 'export PLAYTEST_RECEIVER_BASE=https://playtest.moores.tech'
     echo 'export PLAYTEST_ADMIN_KEY=<手順2でADMIN_KEYに入れた値と同じもの>'
   } > ~/hermes-agent/data/services/playtest/env.sh
   chmod 600 ~/hermes-agent/data/services/playtest/env.sh
   ```
   既定パスと異なる場所に置く場合は `PLAYTEST_ENV_FILE` でそのパスを指す。
## 動作確認

```bash
. ~/hermes-agent/data/services/playtest/env.sh
BASE="$PLAYTEST_RECEIVER_BASE"
curl -s -o /dev/null -w '%{http_code}\n' "$BASE/v1/inbox"                          # 401 を期待
curl -s -o /dev/null -w '%{http_code}\n' -X POST -d '{"ticket":"00"}' "$BASE/v1/session"  # 401 を期待（無効チケット）
```

## 開発

```bash
pnpm test        # vitest（@cloudflare/vitest-pool-workers。ネットワークへは出ない）
pnpm typecheck
pnpm dev         # ローカル wrangler dev
```

Steam Web API の呼び出しは `handle(request, env, steamFetch)` の第3引数で差し替えられる。テストは必ず差し替えること。
