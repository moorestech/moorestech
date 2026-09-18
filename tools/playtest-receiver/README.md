# playtest-receiver — プレイテスト報告の受け口（Cloudflare Worker + R2）

配布版 moorestech（Steam プレイテスト）からプレイ報告と進行記録を受け取り、R2 に貯める Worker。
Mac mini（plan H の `scripts/playtest/ingest.sh`）が管理APIで取り込む。設計は `docs/adr/0058-steam-closed-playtest-report-receiver-and-save-compat.md`。

## エンドポイント

| メソッド | パス | 認証 | 用途 |
|---|---|---|---|
| POST | `/v1/session` | なし（Steamチケット） | チケット検証＋許可リスト照合＋1時間トークン発行 |
| PUT | `/v1/uploads/{kind}/{id}/{path...}` | `Authorization: Bearer` | 1ファイル保存。`Content-Length` 必須（欠落411・非数値400・100MiB超413） |
| POST | `/v1/uploads/{kind}/{id}/complete` | `Authorization: Bearer` | `READY` と未ACK索引を書く |
| GET | `/v1/inbox?cursor=` | `X-Admin-Key` | 未ACKの一覧 |
| GET | `/v1/inbox/{kind}/{steamId}/{id}/{path...}` | `X-Admin-Key` | 個別ファイル取得 |
| POST | `/v1/inbox/{kind}/{steamId}/{id}/ack` | `X-Admin-Key` | `ACKED` を書き索引を消す |
| GET / PUT | `/v1/allowlist` | `X-Admin-Key` | 許可SteamIDの取得・全置換 |

`kind` は `report` / `progress`。R2 のキーは `reports/{steamId}/{id}/...` と `progress/{steamId}/{id}/...`（`src/keys.ts` の `KIND_PREFIX` が正本）。

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
3. デプロイ前に `wrangler.toml` の `compatibility_date`（現在 `2026-08-22`。手元 workerd テスト環境が解釈できる上限に固定してある）を Cloudflare の最新日付へ見直す。
4. デプロイする:
   ```bash
   pnpm run deploy
   ```
5. DNS: `wrangler.toml` の `routes` に `playtest.moores.tech` を `custom_domain = true` で書いてあるので、`pnpm run deploy` が moores.tech ゾーンへ CNAME を作る。作られない場合は Cloudflare ダッシュボード → Workers & Pages → moorestech-playtest-receiver → Settings → Domains & Routes → Add → Custom domain に `playtest.moores.tech` を追加する。**cloudflared のトンネル（Mac mini）とは無関係の経路なので、`~/.cloudflared/*.yml` は触らない。**
6. Mac mini 側の env ファイルを作る。`scripts/playtest/allowlist.sh`（Task 5）はここから `PLAYTEST_RECEIVER_BASE`・`PLAYTEST_ADMIN_KEY` を読む。ヒアドキュメントは Markdown リスト内の字下げでコピー時に終端行を見失うため、`echo` を積み上げる形にしてある:
   ```bash
   mkdir -p ~/hermes-agent/data/services/playtest
   {
     echo 'export PLAYTEST_RECEIVER_BASE=https://playtest.moores.tech'
     echo 'export PLAYTEST_ADMIN_KEY=<手順2でADMIN_KEYに入れた値と同じもの>'
   } > ~/hermes-agent/data/services/playtest/env.sh
   chmod 600 ~/hermes-agent/data/services/playtest/env.sh
   ```
   既定パスと異なる場所に置く場合は `PLAYTEST_ENV_FILE` でそのパスを指す。
7. 許可リストへ最初のテスターを入れる（手順1で `tools/playtest-receiver` へ `cd` した状態のままなので、`scripts/playtest/allowlist.sh` はリポジトリルートへ戻ってから呼ぶ）:
   ```bash
   . ~/hermes-agent/data/services/playtest/env.sh
   (cd ../.. && scripts/playtest/allowlist.sh add <steamId>)
   ```

## 動作確認

```bash
. ~/hermes-agent/data/services/playtest/env.sh
BASE="$PLAYTEST_RECEIVER_BASE"
curl -s -o /dev/null -w '%{http_code}\n' "$BASE/v1/inbox"                          # 401 を期待
curl -s -H "X-Admin-Key: $PLAYTEST_ADMIN_KEY" "$BASE/v1/allowlist"                 # {"steamIds":[...]}
curl -s -o /dev/null -w '%{http_code}\n' -X POST -d '{"ticket":"00"}' "$BASE/v1/session"  # 401 を期待（無効チケット）
```

## 開発

```bash
pnpm test        # vitest（@cloudflare/vitest-pool-workers。ネットワークへは出ない）
pnpm typecheck
pnpm dev         # ローカル wrangler dev
```

Steam Web API の呼び出しは `handle(request, env, steamFetch)` の第3引数で差し替えられる。テストは必ず差し替えること。
