# プレイテスト D: 受け口 Worker + R2・起動時照合ゲート・アップローダ Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** プレイ報告・進行記録を受け取る Cloudflare Worker + R2（`playtest.moores.tech`）を本repo `tools/playtest-receiver/` に作り、配布版クライアントが起動時に Steam 認証チケットで許可リストと照合して不許可・到達不能ならタイトルで止まり、outbox の `READY` 箱を受け口へアップロードして `UPLOADED` を付ける。

**Architecture:** (1) 受け口は単一の Cloudflare Worker（TypeScript・wrangler）。`POST /v1/session` が Steam Web API `ISteamUserAuth/AuthenticateUserTicket/v1` でチケットを検証し、R2 の許可リストと突き合わせて HMAC(HS256) の1時間トークンを返す。`PUT /v1/uploads/...` と `POST /v1/uploads/.../complete` がトークン検証のうえ R2 へ書き、`READY` と「未ACK索引」オブジェクトを置く。`GET/POST /v1/inbox...` と `GET/PUT /v1/allowlist` は `X-Admin-Key` の管理APIで、Mac mini（plan H）と `scripts/playtest/allowlist.sh` が使う。(2) クライアント側は新アセンブリ `Client.PlaytestReceiver`。`PlaytestSteamTicketProvider` が `SteamUser.GetAuthTicketForWebApi("moorestech-playtest")` とコールバック `GetTicketForWebApiResponse_t` からチケットhexを取り、`PlaytestReceiverClient`（`HttpClient` + UniTask）が受け口を叩き、`PlaytestSession` がトークンを保持・期限前に再取得する。(3) `PlaytestLaunchGate.EvaluateAsync()` が「`StreamingAssets/build-info.json` が在り、かつ Steam が動いている」ときだけ照合を行い、結果を `PlaytestGateResult` に固定する。タイトル（uGUI の MainMenu シーン）では表示専用の `PlaytestLaunchGateView` が既存 `ServerConnectPopup` に理由を出し、開始経路（`LocalGameLauncher.StartLocalGame` / `ConnectServer.Connect`）が唯一の関所として拒否する。(4) `PlaytestUploader` が2つの outbox（`BugReports/outbox`・`ProgressRecords/outbox`）を走査し、`READY` かつ `UPLOADED` 無しの箱を古い順に PUT → complete → `UPLOADED` で送る。失敗は箱に試行回数を刻んで次回へ持ち越し、5回で `UPLOAD_FAILED` にして後続を塞がない。

**Tech Stack:** TypeScript（Cloudflare Workers・R2・WebCrypto）、wrangler、vitest + `@cloudflare/vitest-pool-workers`、pnpm、bash、Unity C#（`Client.PlaytestReceiver`・`Client.MainMenu`・`Client.Starter`・`Client.Tests`）、Steamworks.NET 20.2.0、`System.Net.Http.HttpClient` + UniTask、Localization CSV。

## Requirements

- R1. Worker 足場とトークン: `tools/playtest-receiver/` に pnpm + TypeScript + wrangler + vitest（`@cloudflare/vitest-pool-workers`）のプロジェクトを作り、`src/token.ts` が HS256 の署名付きトークンを発行・検証する（payload `{ sub: steamId, iat, exp }`、既定寿命3600秒）。ルータ `src/index.ts` は未知パスへ `404 {"error":"not_found"}` を返す。受入: `pnpm test` で「署名したトークンが検証を通る」「秘密鍵が違うと落ちる」「exp 超過で落ちる」「改竄で落ちる」、および未知パスが 404 JSON を返すテストが通る。
- R2. Steam 検証と許可リスト: `src/steamAuth.ts` が `https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/?key=&appid=&ticket=&identity=moorestech-playtest` を叩き、`response.params.result === "OK"` のとき `steamid` を返し、それ以外は失敗を返す。`fetch` は引数で差し替え可能。`src/allowlist.ts` が R2 `config/allowlist.json`（`{ "steamIds": ["..."] }`）を読み書きし、オブジェクト不在は空リストとして扱う。受入: `pnpm test` で「OK応答から steamid が取れる」「error 応答は失敗」「result!=OK は失敗」「許可リスト不在時は空」「書いた内容が読める」。
- R3. `POST /v1/session`: body `{"ticket":"<hex>"}`。チケット検証失敗は `401 {"reason":"invalid-ticket"}`、検証成功だが許可リスト外は `403 {"reason":"not-allowed"}`、許可なら `200 { "steamId", "allowed": true, "token" }`。body が JSON でない・`ticket` が16進でないときは `400 {"reason":"bad-request"}`。受入: 4パターンすべての HTTP テストが通る。
- R4. アップロード: `PUT /v1/uploads/{kind}/{id}/{path...}`（`Authorization: Bearer <token>`）が R2 `{prefix}/{steamId}/{id}/{path}` へ保存する（`kind` は `report`|`progress`、prefix はそれぞれ `reports`/`progress`）。`Content-Length` が 100MiB 超は `413 {"reason":"too-large"}`。`kind` 不正は `400`、トークン不正・期限切れは `401`、`path` に `..`・空セグメント・先頭 `/`・`\` があれば `400 {"reason":"bad-path"}`。`POST /v1/uploads/{kind}/{id}/complete` が `{prefix}/{steamId}/{id}/READY`（本文＝リクエストボディの要約JSON）と索引 `index/pending/{kind}/{steamId}/{id}` を書く。受入: HTTP テストで「PUTしたオブジェクトがR2に入る」「completeでREADYと索引が出来る」「.. を含むパスは400」「他人のtokenでは他人のprefixに書けない（keyがtokenのsteamId固定である）」「巨大Content-Lengthは413」。
- R5. 管理API: `GET /v1/inbox?cursor=`（`X-Admin-Key`）が索引を列挙して `{ "items": [{kind,steamId,id,readyAt}], "cursor": string|null }` を返す。`GET /v1/inbox/{kind}/{steamId}/{id}/{path...}` がオブジェクトを返す（不在は404）。`POST /v1/inbox/{kind}/{steamId}/{id}/ack` が `{prefix}/{steamId}/{id}/ACKED` を書き索引を消す。`GET /v1/allowlist` が現在のリスト、`PUT /v1/allowlist` が `{"steamIds":[...]}` で全置換する。管理キー不一致・欠落はすべて `401 {"reason":"unauthorized"}`。受入: HTTP テストで「adminキー無しは401」「completeした2件がinboxに出る」「ackすると出なくなりACKEDが出来る」「allowlistのGET/PUTが往復する」。
- R6. デプロイ手順: `tools/playtest-receiver/README.md` に R2 バケット作成・`wrangler secret put`（`STEAM_WEB_API_KEY`・`SESSION_HMAC_SECRET`・`ADMIN_KEY`）・`wrangler deploy`・`playtest.moores.tech` のカスタムドメイン設定・動作確認 curl を書く。`wrangler.toml` に R2 binding（bucket `moorestech-playtest`）と `vars.STEAM_APP_ID = "1958160"`、`routes` のカスタムドメインを書く。受入: README のコマンドだけで初見の開発者がデプロイまで到達できる（手順に未定義の値が無い）。
- R7. 許可リスト操作: `scripts/playtest/allowlist.sh add|remove|list <steamId>` が管理APIを叩いて許可リストを更新・表示する。設定は `${PLAYTEST_ENV_FILE:-$HOME/hermes-agent/data/services/playtest/env.sh}` から `PLAYTEST_RECEIVER_BASE`・`PLAYTEST_ADMIN_KEY` を読む。`curl` は `CURL_CMD` で差し替え可能。受入: `scripts/playtest/tests/test-allowlist.sh` が curl スタブで「add で1件増える」「同じIDを2回addしても重複しない」「remove で消える」「list が現在のIDを1行ずつ出す」「未設定のenvは即エラー終了」を検証して `OK` を出す。
- R8. クライアント基盤: 新アセンブリ `Client.PlaytestReceiver` に、`PlaytestSteamTicketProvider`（`SteamUser.GetAuthTicketForWebApi` とコールバックからチケットhexを15秒以内に取る／Steam未初期化なら `null`）、`PlaytestReceiverClient : IPlaytestReceiverApi`（`HttpClient` + UniTask、`PostSessionAsync`・`PutFileAsync`・`PostCompleteAsync`）、`PlaytestSession`（`IPlaytestSessionLookup` で SteamId とトークン期限を読み、書き込みはアセンブリ内部のみ。45分でトークンを取り直す）、`PlaytestBuildInfoFile`（`StreamingAssets/build-info.json` の存在判定）を置く。受入: EditMode 単体テストで「トークン期限が45分未満なら再取得しない／超えたら再取得する」「build-info.json が無い環境で `Exists` が false」。
- R9. 起動時照合: `PlaytestLaunchGate.EvaluateAsync()` が `PlaytestGateResult` を返し `PlaytestLaunchGate.Current` に固定する。判定は純関数 `PlaytestGateDecision.Decide(...)` に切り出す。`build-info.json` 不在、または Steam 非稼働は `DeveloperMode`（何もしない）。それ以外でチケット取得失敗は `TicketFailed`、`/v1/session` が 403 は `NotAllowed`、401 は `TicketFailed`、到達不能・その他は `Unreachable`、200 は `Allowed`。`DeveloperMode` と `Allowed` 以外はゲーム開始を拒否する。受入: EditMode 単体テストで上記6分岐すべてが `Decide` で確定し、拒否側は `IsBlocked == true`。
- R10. タイトルで止める: `Client.MainMenu/Playtest/PlaytestLaunchGateView` が MainMenu シーンで `EvaluateAsync()` を回し、判定中は `ui.playtest.checking`、拒否時は理由文言を既存 `ServerConnectPopup` に出す。開始経路 `LocalGameLauncher.StartLocalGame()` と `ConnectServer.Connect()` は `PlaytestLaunchGate.Current.IsBlocked` のとき何もせず理由を出して `Debug.LogWarning` する。文言は `Localization/localization.csv` に `ui.playtest.checking`・`ui.playtest.notAllowed`・`ui.playtest.unreachable`・`ui.playtest.ticketFailed` を english/japanese/german で追加する。受入: EditMode 単体テストで「`IsBlocked` のとき `LocalGameLauncher.StartLocalGame()` がシーンを読み込まない」、Editor 実機で MainMenu が従来どおり開始できる（`DeveloperMode`）。
- R11. アップロード: `PlaytestUploader.UploadPendingAsync()` が `BugReports/outbox`（kind=report）と `ProgressRecords/outbox`（kind=progress）の `READY` かつ `UPLOADED`・`UPLOAD_FAILED` 無しの箱を古い順に、マーカー以外の全ファイルを相対パスで PUT → complete → `UPLOADED` の順に送る。100MiB 超のファイルは送らず complete 本文の `skipped[]` に理由付きで載せる。失敗した箱は `UPLOAD_ATTEMPTS`（回数と最終理由）を増やして中断し、5回目の失敗で `UPLOAD_FAILED` を書いて以後は飛ばす。すべての分岐で `Debug.LogWarning` に理由を出す。受入: EditMode 単体テストで「READYのみの箱が送られUPLOADEDが付く」「UPLOADED済みは再送されない」「失敗すると回数が増え箱は残る」「5回目でUPLOAD_FAILEDが付き、後続の箱が送られる」「巨大ファイルはskippedに載り箱自体は成功する」。
- R12. 送信直後の起動: `Client.PlaytestReceiver` の `IPlaytestUploadRequester.RequestUpload()` を plan B の `BugReportSubmitActionHandler` が書き出し成功後に呼び、`PlaytestUploader` が多重起動せず1本だけ走る。起動直後の1回は `PlaytestLaunchGateView` が `Allowed` のとき呼ぶ。受入: EditMode 単体テストで「実行中に2回 `RequestUpload()` しても走行は1本」。
- R13. 受け口の通し確認: `wrangler deploy` 後に `playtest.moores.tech` へ curl で「adminキー無し401」「allowlist PUT→GET」「不許可SteamIDのsession 403」を確認し、結果を判断記録に書く。受入: 3つの curl の実出力を判断記録へ転記する。
- やらないこと: 進行記録の生成（plan G）／Mac mini の取り込み・日次ダイジェスト（plan H）／配布ビルドと steamcmd と検証機（plan E）／セーブ互換（plan F）／plan B 本体（バンドル生成・報告UI・`BugReportOutbox`・`BugReportManifest`・`BuildInfoWriter`）の実装／R2 のライフサイクル削除ポリシー／受け口の閲覧サイト／テスターごとの流量制限。

## Global Constraints

**共有契約 §4（受け口 Worker API）— 逐語転記（変更禁止）:**

- 受け口 Worker API（base `https://playtest.moores.tech`、実装は本repo `tools/playtest-receiver/`、TypeScript + wrangler、R2 バケット `moorestech-playtest`）
- `POST /v1/session` body `{"ticket":"<hex>"}` → Steam Web API `ISteamUserAuth/AuthenticateUserTicket/v1`（`identity=moorestech-playtest`）で検証 → `{ "steamId": "...", "allowed": true, "token": "<HMAC-JWT 1h>" }`。不許可は 403 `{ "reason": "not-allowed" }`、検証失敗は 401。
- `PUT /v1/uploads/{kind}/{id}/{path...}`（Bearer token、`kind` は report|progress、1ファイル ≤ 100MB）→ R2 `{kind}s/{steamId}/{id}/{path}` へ保存。
- `POST /v1/uploads/{kind}/{id}/complete` → R2 に `{kind}s/{steamId}/{id}/READY`（本文 = manifest の要約 JSON）。
  - 改訂（2026-09-17 裁定・実装で追加済み）: 要約 JSON は `{kind, id, fileCount, files, skipped, manifest}`。`files` は PUT に成功した相対パスの配列（見送り分は含めない）で、plan H の `ingest.sh` はこれだけを取得する（`.decisions/2026-09-17-プレイテスト取り込みのファイル一覧はクライアントがREADY要約のfilesに書く.md`）。
- `GET /v1/inbox?cursor=<opaque>`（`X-Admin-Key` ヘッダ）→ `{ "items": [ { "kind","steamId","id","readyAt" } ], "cursor": "..." }`（READY 済みで未ACKのもの）。
- `GET /v1/inbox/{kind}/{steamId}/{id}/{path...}`（admin）→ R2 オブジェクト。
- `POST /v1/inbox/{kind}/{steamId}/{id}/ack`（admin）→ R2 `.../ACKED` を書く。
- 許可リスト: R2 `config/allowlist.json` = `{ "steamIds": ["..."] }`。Mac mini 側の `scripts/playtest/allowlist.sh add|remove|list <steamId>` が admin API `PUT /v1/allowlist` で更新。
- Secrets（wrangler secret）: `STEAM_WEB_API_KEY`（publisher key）、`SESSION_HMAC_SECRET`、`ADMIN_KEY`。`STEAM_APP_ID=1958160` は vars。

**共有契約 §5（クライアント側の受け口利用）— 逐語転記（変更禁止）:**

- 起動時: `SteamManager.Initialized` なら `SteamUser.GetAuthTicketForWebApi("moorestech-playtest")` → `POST /v1/session`。`allowed=false` または到達不能ならタイトルで停止し理由を表示（[[起動時照合はオンライン必須]]）。Steam未初期化（Editor・自作ビルド直起動）は照合をスキップし開発者モード（rsync経路）。
- 送信: `BugReports/outbox` と `ProgressRecords/outbox` の `READY` かつ `UPLOADED` 無しを起動直後と送信直後に順次アップロード（失敗は次回に持ち越し、理由をログ）。本体がネットワークを触るのは配布版だけ（Steam初期化済みかつ build-info.json 存在時）。

**契約の解釈（実装で必ずこの形にする。plan H と一致させること）:**

- §4 の R2 キー `{kind}s/...` を字義どおり適用すると `kind=progress` が `progresss` になる。プレフィックスは表で固定する: `report → reports/`、`progress → progress/`（§6 のローカル配置 `{reports|progress}` と揃える）。この表は `tools/playtest-receiver/src/keys.ts` の `KIND_PREFIX` が唯一の正本。
- §5 の `SteamManager.Initialized` は Assembly-CSharp にあり asmdef 側から参照できない（`moorestech_client/Assets/PersonalAssets/moorestech-client-private/Steamworks.NET/SteamManager.cs` に asmdef 無し）。同義の判定として `Steamworks.SteamAPI.IsSteamRunning()` とチケット取得の成否を使う。
- 「到達不能なら止める」と「Steam未初期化なら開発者モード」の境目: `build-info.json` 不在または Steam 非稼働のときだけ照合をスキップする。配布版（`build-info.json` 有り）で Steam が動いているのにチケットが取れない・401 が返るのは**止める**側に倒す（fail-closed）。

**依存（未実装の前提。着手前に確認する）:**

- plan B（`docs/superpowers/plans/2026-09-11-bug-report-b-client-capture-and-report-ui.md`）は**未実装**。本planは plan B の `BugReportOutbox`（`READY` マーカー名）・`BugReportManifest`（`manifest.json`）・`GameSystemPaths.BugReportOutboxDirectory`・`BugReportSubmitActionHandler` が存在することを前提にする。plan B のマージ前に本planへ着手する場合、Task 8 の `BugReportSubmitActionHandler` 改修（R12）だけを最後に回し、他タスクは `GameSystemPaths` に本planが足す `ProgressRecordOutboxDirectory` と、本planが足す `PlaytestOutboxScanner`（マーカー名を定数で持つ）だけで完結させる。
- plan E（配布ビルド）が `StreamingAssets/build-info.json` を焼く。本planは**存在判定と読み取りしかしない**。共有契約 §1 の C# 型 `BuildInfo` は `Client.Game/InGame/BugReport/BuildInfo.cs`（plan B/E 側）に置かれ、`Client.Game` は `Client.PlaytestReceiver` の下流なので本planからは参照しない。本planが持つのはパス定数と `File.Exists` のみ。
- `ProgressRecords/outbox`（共有契約 §2）のパスは本planが `GameSystemPaths` に追加する（`ProgressRecordDirectory`・`ProgressRecordOutboxDirectory`）。plan G は重複定義しないこと。
- **Steam の初期化そのものは本planの外にあるが、未確認の穴である。** `SteamManager` は `moorestech_client/Assets/Scenes/Game/MainMenu.unity` に配置済みで `moorestech_client/steam_appid.txt`（1958160）も存在する（2026-09-13 起票セッションで確認）。残る未確認は `SteamAPI.RestartAppIfNecessary(AppId_t.Invalid)` のままで Steam 経由起動時に AppID が正しく解決されるか。Task 7 で MainMenu シーンの実在を確認し、無ければ判断記録に書いて plan E の裁定に回す（本planでは AppId の焼き込みまでは扱わない）。

**実装規約:**

- 作業ブランチ: `feature/playtest-receiver`。`moores-wt new feature/playtest-receiver` で使い捨て worktree を切って作業する（CLAUDE.local.md）。
- `.cs` を変更したら `uloop compile --project-path ./moorestech_client`。EditMode テストは `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.PlaytestReceiver\..*"`（Domain Reload エラーは45秒待ってリトライ）。
- Worker は `cd tools/playtest-receiver && pnpm install`／`pnpm test`／`pnpm typecheck`。`pnpm test` はネットワークへ出ない（Steam Web API は必ず fetch 差し替えで検証する）。
- ネットワーク送受信は AGENTS.md が認める外部境界。`try-catch` を使ってよいのは `PlaytestReceiverClient` の HTTP 呼び出し・`File.OpenRead`、`PlaytestSessionResponse` の応答 JSON パース、`PlaytestSteamTicketProvider` のネイティブ interop、`PlaytestOutboxScanner`/`PlaytestUploadAttemptLog` のファイル I/O だけで（実装時に外部境界として拡張。catch を持たない `finally` の状態復帰は対象外）、`EditorProcessRunner.cs:39-50` と同じく **`try` の直上に日英2行で「なぜここが境界か」を書く**。それ以外の try-catch は禁止。
- fail-closed で拒否・スキップする経路（照合不許可・到達不能・チケット失敗・巨大ファイルのスキップ・箱の恒久失敗）は必ず `Debug.LogWarning`／`Debug.LogError`（Worker 側は `console.warn`）に理由を出す。無音の縮退は禁止。
- コメントは「// 日本語 → // English」2行セット、3〜10行ごと。1ファイル200行以下、1ディレクトリ10ファイル以下。partial・`Func<>`・デフォルト引数・単純getter/setter プロパティ禁止。イベント通知は UniRx。`Update()` ポーリング禁止。
- Worker の TypeScript にも日英2行コメント規約と200行/10ファイル制限を適用する。
- 型名・ファイル名は本plan記載のとおり（`PlaytestReceiverConfig`・`PlaytestSteamTicketProvider`・`IPlaytestReceiverApi`・`PlaytestReceiverClient`・`PlaytestApiResult`・`PlaytestSessionResponse`・`IPlaytestSessionLookup`・`PlaytestSession`・`PlaytestSessionResult`・`PlaytestSessionResponse`・`PlaytestUploadPath`・`PlaytestGateStatus`・`PlaytestGateResult`・`PlaytestGateDecision`・`PlaytestLaunchGate`・`PlaytestBuildInfoFile`・`PlaytestLaunchGateView`・`PlaytestOutboxScanner`・`PlaytestOutboxBox`・`PlaytestUploadAttemptLog`・`IPlaytestUploadRequester`・`PlaytestUploader`・`PlaytestUploadRunner`・`PlaytestUploadFailurePolicy`）。
- `.meta` ファイルは絶対に手で作らない。新規 `.asmdef`・`.cs` を足したら `uloop compile` を回し、Unity が生成した `.meta` をコミットに含める。
- Unity のシーン・Prefab をテキストで編集しない。MainMenu シーンへのコンポーネント追加は `uloop execute-dynamic-code` 経由のみ。
- 各タスク末尾でコミット。コミットメッセージ末尾に以下を付ける:
  ```
  Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01HtaDWGdhRRiNho39vPY5Li
  ```

---

### Task 1: Worker の足場・HMACトークン・ルータ骨格

**Files:**
- Create: `tools/playtest-receiver/package.json`
- Create: `tools/playtest-receiver/tsconfig.json`
- Create: `tools/playtest-receiver/wrangler.toml`
- Create: `tools/playtest-receiver/vitest.config.ts`
- Create: `tools/playtest-receiver/.gitignore`
- Create: `tools/playtest-receiver/src/env.ts`
- Create: `tools/playtest-receiver/src/http.ts`
- Create: `tools/playtest-receiver/src/token.ts`
- Create: `tools/playtest-receiver/src/index.ts`
- Test: `tools/playtest-receiver/test/token.test.ts`・`tools/playtest-receiver/test/router.test.ts`

**Interfaces:**
- Consumes: なし（本planの最初のタスク）
- Produces:
  - `export interface Env { BUCKET: R2Bucket; STEAM_APP_ID: string; STEAM_WEB_API_KEY: string; SESSION_HMAC_SECRET: string; ADMIN_KEY: string }`（`src/env.ts`）
  - `export function json(body: unknown, status?: number): Response`、`export function fail(reason: string, status: number): Response`、`export function requireAdmin(request: Request, env: Env): Response | null`（`src/http.ts`）
  - `export const TOKEN_TTL_SECONDS = 3600`、`export async function signToken(secret: string, steamId: string, nowSeconds: number): Promise<string>`、`export async function verifyToken(secret: string, token: string, nowSeconds: number): Promise<string | null>`（成功で steamId、失敗で null）（`src/token.ts`）
  - `export async function handle(request: Request, env: Env, steamFetch: typeof fetch): Promise<Response>` と `export default { fetch }`（`src/index.ts`）

- [ ] **Step 1: プロジェクトを作って依存を入れる**

```bash
mkdir -p tools/playtest-receiver/src tools/playtest-receiver/test
cd tools/playtest-receiver
pnpm init
pnpm add -D typescript@^5.7.2 wrangler@^4 vitest@^4 @cloudflare/vitest-pool-workers@^0.22   # 実装時: vitest 4 + pool 0.22（config は defineConfig + cloudflareTest() プラグイン形式、types は @cloudflare/vitest-pool-workers/types）
```
Expected: `node_modules/` と `pnpm-lock.yaml` が出来る。`@cloudflare/vitest-pool-workers` が要求する vitest のバージョン範囲が合わなければ peer 警告が出るので、警告に書かれた範囲へ `pnpm add -D vitest@<範囲>` で合わせる（別のテスト機構へ逃げない）。

- [ ] **Step 2: 設定ファイルを書く**

`tools/playtest-receiver/package.json`（`pnpm init` の生成物を次の内容へ置き換える。`devDependencies` は Step 1 で実際に入った値のまま残す）:
```json
{
  "name": "moorestech-playtest-receiver",
  "private": true,
  "version": "0.0.1",
  "type": "module",
  "scripts": {
    "typecheck": "tsc --noEmit",
    "test": "vitest run",
    "test:watch": "vitest",
    "deploy": "wrangler deploy",
    "dev": "wrangler dev"
  }
}
```

`tools/playtest-receiver/tsconfig.json`:
```json
{
  "compilerOptions": {
    "target": "es2022",
    "module": "es2022",
    "moduleResolution": "bundler",
    "lib": ["es2022"],
    "types": ["@cloudflare/workers-types/2023-07-01"],
    "strict": true,
    "noUncheckedIndexedAccess": true,
    "noEmit": true,
    "skipLibCheck": true
  },
  "include": ["src", "test"]
}
```
（`@cloudflare/workers-types` は `@cloudflare/vitest-pool-workers` の依存として入る。`pnpm typecheck` が型定義を見つけられないときだけ `pnpm add -D @cloudflare/workers-types` を足す）

`tools/playtest-receiver/wrangler.toml`:
```toml
name = "moorestech-playtest-receiver"
main = "src/index.ts"
compatibility_date = "2026-09-01"

# プレイテスト受け口の固定ドメイン。DNSはmoorestechのCloudflareアカウントのmoores.techゾーンに属する
# Fixed domain for the playtest receiver; DNS lives in the moores.tech zone of the moorestech Cloudflare account
routes = [{ pattern = "playtest.moores.tech", custom_domain = true }]

[vars]
STEAM_APP_ID = "1958160"

[[r2_buckets]]
binding = "BUCKET"
bucket_name = "moorestech-playtest"
```

`tools/playtest-receiver/vitest.config.ts`:
```ts
import { defineWorkersConfig } from "@cloudflare/vitest-pool-workers/config";

// 実物のR2（miniflare）でテストする。R2の偽物を書くと索引とマーカーの整合を守れない
// Tests run against a real (miniflare) R2; a hand-written fake would not keep markers and the index consistent
export default defineWorkersConfig({
  test: {
    include: ["test/**/*.test.ts"],
    poolOptions: {
      workers: {
        wrangler: { configPath: "./wrangler.toml" },
        miniflare: {
          bindings: {
            STEAM_WEB_API_KEY: "test-steam-key",
            SESSION_HMAC_SECRET: "test-hmac-secret",
            ADMIN_KEY: "test-admin-key",
          },
        },
      },
    },
  },
});
```

`tools/playtest-receiver/.gitignore`:
```
node_modules/
.wrangler/
dist/
```

- [ ] **Step 3: 失敗するテストを書く**

`tools/playtest-receiver/test/token.test.ts`:
```ts
import { describe, expect, it } from "vitest";
import { signToken, verifyToken, TOKEN_TTL_SECONDS } from "../src/token";

const SECRET = "test-hmac-secret";
const NOW = 1_800_000_000;

describe("token", () => {
  it("署名したトークンは同じ秘密鍵で検証を通りsteamIdを返す", async () => {
    const token = await signToken(SECRET, "76561198000000001", NOW);
    expect(await verifyToken(SECRET, token, NOW + 10)).toBe("76561198000000001");
  });

  it("秘密鍵が違うと検証に失敗する", async () => {
    const token = await signToken(SECRET, "76561198000000001", NOW);
    expect(await verifyToken("other-secret", token, NOW + 10)).toBeNull();
  });

  it("有効期限を過ぎると検証に失敗する", async () => {
    const token = await signToken(SECRET, "76561198000000001", NOW);
    expect(await verifyToken(SECRET, token, NOW + TOKEN_TTL_SECONDS + 1)).toBeNull();
  });

  it("本文を改竄すると検証に失敗する", async () => {
    const token = await signToken(SECRET, "76561198000000001", NOW);
    const [header, , signature] = token.split(".");
    const forged = btoa(JSON.stringify({ sub: "76561198000000002", iat: NOW, exp: NOW + 60 }))
      .replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
    expect(await verifyToken(SECRET, `${header}.${forged}.${signature}`, NOW + 10)).toBeNull();
  });

  it("形が壊れたトークンは検証に失敗する", async () => {
    expect(await verifyToken(SECRET, "not-a-token", NOW)).toBeNull();
  });
});
```

`tools/playtest-receiver/test/router.test.ts`:
```ts
import { env } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import { handle } from "../src/index";
import type { Env } from "../src/env";

const noNetwork: typeof fetch = (async () => {
  throw new Error("テストからネットワークへ出てはいけない / tests must not reach the network");
}) as unknown as typeof fetch;

describe("router", () => {
  it("知らないパスはJSONの404を返す", async () => {
    const response = await handle(new Request("https://playtest.moores.tech/nope"), env as unknown as Env, noNetwork);
    expect(response.status).toBe(404);
    expect(await response.json()).toEqual({ reason: "not-found" });
  });

  it("知っているパスでもメソッドが違えば405を返す", async () => {
    const response = await handle(new Request("https://playtest.moores.tech/v1/session"), env as unknown as Env, noNetwork);
    expect(response.status).toBe(405);
  });
});
```

- [ ] **Step 4: 実行して失敗を確認する**

Run: `cd tools/playtest-receiver && pnpm test`
Expected: `src/token.ts` と `src/index.ts` が無く、モジュール解決エラーで FAIL

- [ ] **Step 5: 実装する**

`tools/playtest-receiver/src/env.ts`:
```ts
// Workerが受け取るbindings。secretsは wrangler secret put で設定する
// Bindings the Worker receives; secrets are provisioned with `wrangler secret put`
export interface Env {
  BUCKET: R2Bucket;
  STEAM_APP_ID: string;
  STEAM_WEB_API_KEY: string;
  SESSION_HMAC_SECRET: string;
  ADMIN_KEY: string;
}
```

`tools/playtest-receiver/src/http.ts`:
```ts
import type { Env } from "./env";

export function json(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "content-type": "application/json; charset=utf-8" },
  });
}

export function fail(reason: string, status: number): Response {
  return json({ reason }, status);
}

// 管理APIの共通関門。長さの違いも含めて定数時間で比べ、鍵の推測を助けない
// Shared gate for the admin API; compares in constant time, including length, so it leaks nothing
export function requireAdmin(request: Request, env: Env): Response | null {
  const presented = request.headers.get("x-admin-key") ?? "";
  if (!constantTimeEquals(presented, env.ADMIN_KEY)) {
    console.warn("[admin] rejected a request without a matching X-Admin-Key");
    return fail("unauthorized", 401);
  }
  return null;
}

function constantTimeEquals(a: string, b: string): boolean {
  const left = new TextEncoder().encode(a);
  const right = new TextEncoder().encode(b);
  let diff = left.length ^ right.length;
  const length = Math.max(left.length, right.length);
  for (let i = 0; i < length; i++) diff |= (left[i] ?? 0) ^ (right[i] ?? 0);
  return diff === 0;
}
```

`tools/playtest-receiver/src/token.ts`:
```ts
export const TOKEN_TTL_SECONDS = 3600;

interface TokenPayload {
  sub: string;
  iat: number;
  exp: number;
}

export async function signToken(secret: string, steamId: string, nowSeconds: number): Promise<string> {
  const header = encode(JSON.stringify({ alg: "HS256", typ: "JWT" }));
  const payload: TokenPayload = { sub: steamId, iat: nowSeconds, exp: nowSeconds + TOKEN_TTL_SECONDS };
  const body = encode(JSON.stringify(payload));
  const signature = await sign(secret, `${header}.${body}`);
  return `${header}.${body}.${signature}`;
}

// 検証は「形・署名・期限」の3段。どれで落ちてもnullへ畳み、呼び出し側は401だけを返す
// Verification is shape, signature, expiry; any failure collapses to null and the caller answers 401
export async function verifyToken(secret: string, token: string, nowSeconds: number): Promise<string | null> {
  const parts = token.split(".");
  if (parts.length !== 3) return null;
  const [header, body, signature] = parts as [string, string, string];

  const expected = await sign(secret, `${header}.${body}`);
  if (signature.length !== expected.length) return null;
  let diff = 0;
  for (let i = 0; i < expected.length; i++) diff |= signature.charCodeAt(i) ^ expected.charCodeAt(i);
  if (diff !== 0) return null;

  const payload = decodePayload(body);
  if (payload === null) return null;
  if (payload.exp <= nowSeconds) return null;
  if (payload.sub.length === 0) return null;
  return payload.sub;
}

function decodePayload(body: string): TokenPayload | null {
  // 外から来た文字列のJSONパースは境界。壊れた入力はnullへ隔離する
  // Parsing a caller-supplied string is a boundary; malformed input is isolated into null
  try {
    const text = atob(body.replace(/-/g, "+").replace(/_/g, "/"));
    const parsed = JSON.parse(text) as Partial<TokenPayload>;
    if (typeof parsed.sub !== "string" || typeof parsed.exp !== "number" || typeof parsed.iat !== "number") return null;
    return { sub: parsed.sub, iat: parsed.iat, exp: parsed.exp };
  } catch {
    return null;
  }
}

async function sign(secret: string, message: string): Promise<string> {
  const key = await crypto.subtle.importKey(
    "raw",
    new TextEncoder().encode(secret),
    { name: "HMAC", hash: "SHA-256" },
    false,
    ["sign"],
  );
  const mac = await crypto.subtle.sign("HMAC", key, new TextEncoder().encode(message));
  return base64Url(new Uint8Array(mac));
}

function encode(text: string): string {
  return base64Url(new TextEncoder().encode(text));
}

function base64Url(bytes: Uint8Array): string {
  let binary = "";
  for (const byte of bytes) binary += String.fromCharCode(byte);
  return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
}
```

`tools/playtest-receiver/src/index.ts`（Task 2〜4 で分岐を足していく。この時点では 404/405 だけ）:
```ts
import type { Env } from "./env";
import { fail } from "./http";

// Steam Web APIへのfetchを引数で受ける。テストは差し替え、本番はglobalThis.fetchを渡す
// The Steam Web API fetch is injected so tests can replace it; production passes globalThis.fetch
export async function handle(request: Request, env: Env, steamFetch: typeof fetch): Promise<Response> {
  const url = new URL(request.url);
  const segments = url.pathname.split("/").filter((segment) => segment.length > 0);

  if (segments[0] !== "v1") return fail("not-found", 404);

  if (segments.length === 2 && segments[1] === "session") {
    if (request.method !== "POST") return fail("method-not-allowed", 405);
    return fail("not-found", 404);
  }

  return fail("not-found", 404);
}

export default {
  fetch(request: Request, env: Env): Promise<Response> {
    return handle(request, env, globalThis.fetch);
  },
};
```
（`steamFetch` は Task 2 で使う。tsconfig に `noUnusedParameters` を入れていないのはこのため）

- [ ] **Step 6: テストを実行して通ることを確認する**

Run: `cd tools/playtest-receiver && pnpm test && pnpm typecheck`
Expected: 7件 PASS、型エラー無し

- [ ] **Step 7: コミットする**

```bash
git add tools/playtest-receiver
git commit -m "feat(playtest): 受け口Workerの足場とHMACトークン"
```

---

### Task 2: Steam チケット検証・許可リスト・`POST /v1/session`

**Files:**
- Create: `tools/playtest-receiver/src/steamAuth.ts`
- Create: `tools/playtest-receiver/src/allowlist.ts`
- Create: `tools/playtest-receiver/src/session.ts`
- Modify: `tools/playtest-receiver/src/index.ts`（`/v1/session` の分岐を実装へ差し替える）
- Test: `tools/playtest-receiver/test/steamAuth.test.ts`・`tools/playtest-receiver/test/allowlist.test.ts`・`tools/playtest-receiver/test/session.test.ts`

**Interfaces:**
- Consumes: Task 1 の `Env`・`json`・`fail`・`signToken`・`verifyToken`・`handle`
- Produces:
  - `export const STEAM_IDENTITY = "moorestech-playtest"`、`export interface SteamTicketResult { steamId: string | null; reason: string }`、`export async function authenticateUserTicket(steamFetch: typeof fetch, env: Env, ticketHex: string): Promise<SteamTicketResult>`（`src/steamAuth.ts`）
  - `export const ALLOWLIST_KEY = "config/allowlist.json"`、`export async function readAllowlist(bucket: R2Bucket): Promise<string[]>`、`export async function writeAllowlist(bucket: R2Bucket, steamIds: string[]): Promise<void>`（`src/allowlist.ts`）
  - `export async function postSession(request: Request, env: Env, steamFetch: typeof fetch): Promise<Response>`（`src/session.ts`）

- [ ] **Step 1: 失敗するテストを書く**

`tools/playtest-receiver/test/steamAuth.test.ts`:
```ts
import { env } from "cloudflare:test";
import { describe, expect, it } from "vitest";
import { authenticateUserTicket, STEAM_IDENTITY } from "../src/steamAuth";
import type { Env } from "../src/env";

const workerEnv = env as unknown as Env;

function respond(body: unknown): typeof fetch {
  return (async () => new Response(JSON.stringify(body), { status: 200 })) as unknown as typeof fetch;
}

describe("authenticateUserTicket", () => {
  it("OK応答からsteamIdを取り出す", async () => {
    const result = await authenticateUserTicket(
      respond({ response: { params: { result: "OK", steamid: "76561198000000001", ownersteamid: "76561198000000001" } } }),
      workerEnv,
      "aabb",
    );
    expect(result.steamId).toBe("76561198000000001");
  });

  it("appid・identity・ticketをクエリに載せる", async () => {
    let seen = "";
    const spy = (async (input: RequestInfo) => {
      seen = typeof input === "string" ? input : (input as Request).url;
      return new Response(JSON.stringify({ response: { params: { result: "OK", steamid: "1" } } }), { status: 200 });
    }) as unknown as typeof fetch;
    await authenticateUserTicket(spy, workerEnv, "aabb");
    expect(seen).toContain("appid=1958160");
    expect(seen).toContain(`identity=${STEAM_IDENTITY}`);
    expect(seen).toContain("ticket=aabb");
  });

  it("error応答は失敗として理由を返す", async () => {
    const result = await authenticateUserTicket(
      respond({ response: { error: { errorcode: 101, errordesc: "Invalid ticket" } } }),
      workerEnv,
      "aabb",
    );
    expect(result.steamId).toBeNull();
    expect(result.reason).toContain("101");
  });

  it("resultがOK以外なら失敗", async () => {
    const result = await authenticateUserTicket(
      respond({ response: { params: { result: "Expired", steamid: "76561198000000001" } } }),
      workerEnv,
      "aabb",
    );
    expect(result.steamId).toBeNull();
  });

  it("Steam Web APIが落ちていれば失敗として畳む", async () => {
    const broken = (async () => {
      throw new Error("connection reset");
    }) as unknown as typeof fetch;
    const result = await authenticateUserTicket(broken, workerEnv, "aabb");
    expect(result.steamId).toBeNull();
    expect(result.reason).toContain("connection reset");
  });
});
```

`tools/playtest-receiver/test/allowlist.test.ts`:
```ts
import { env } from "cloudflare:test";
import { beforeEach, describe, expect, it } from "vitest";
import { ALLOWLIST_KEY, readAllowlist, writeAllowlist } from "../src/allowlist";
import type { Env } from "../src/env";

const bucket = (env as unknown as Env).BUCKET;

describe("allowlist", () => {
  beforeEach(async () => {
    await bucket.delete(ALLOWLIST_KEY);
  });

  it("オブジェクトが無ければ空リストになる", async () => {
    expect(await readAllowlist(bucket)).toEqual([]);
  });

  it("書いた内容が読める", async () => {
    await writeAllowlist(bucket, ["76561198000000001", "76561198000000002"]);
    expect(await readAllowlist(bucket)).toEqual(["76561198000000001", "76561198000000002"]);
  });

  it("同じIDを渡しても重複しない", async () => {
    await writeAllowlist(bucket, ["76561198000000001", "76561198000000001"]);
    expect(await readAllowlist(bucket)).toEqual(["76561198000000001"]);
  });

  it("壊れたJSONは空リストとして扱う", async () => {
    await bucket.put(ALLOWLIST_KEY, "{ broken");
    expect(await readAllowlist(bucket)).toEqual([]);
  });
});
```

`tools/playtest-receiver/test/session.test.ts`:
```ts
import { env } from "cloudflare:test";
import { beforeEach, describe, expect, it } from "vitest";
import { handle } from "../src/index";
import { ALLOWLIST_KEY, writeAllowlist } from "../src/allowlist";
import { verifyToken } from "../src/token";
import type { Env } from "../src/env";

const workerEnv = env as unknown as Env;

function steamOk(steamId: string): typeof fetch {
  return (async () =>
    new Response(JSON.stringify({ response: { params: { result: "OK", steamid: steamId } } }), { status: 200 })) as unknown as typeof fetch;
}
const steamNg: typeof fetch = (async () =>
  new Response(JSON.stringify({ response: { error: { errorcode: 101, errordesc: "Invalid ticket" } } }), { status: 200 })) as unknown as typeof fetch;

function sessionRequest(body: string): Request {
  return new Request("https://playtest.moores.tech/v1/session", { method: "POST", body });
}

describe("POST /v1/session", () => {
  beforeEach(async () => {
    await workerEnv.BUCKET.delete(ALLOWLIST_KEY);
  });

  it("許可されたSteamIDへトークンを発行する", async () => {
    await writeAllowlist(workerEnv.BUCKET, ["76561198000000001"]);
    const response = await handle(sessionRequest(JSON.stringify({ ticket: "aabb" })), workerEnv, steamOk("76561198000000001"));
    expect(response.status).toBe(200);
    const body = (await response.json()) as { steamId: string; allowed: boolean; token: string };
    expect(body.steamId).toBe("76561198000000001");
    expect(body.allowed).toBe(true);
    expect(await verifyToken(workerEnv.SESSION_HMAC_SECRET, body.token, Math.floor(Date.now() / 1000))).toBe("76561198000000001");
  });

  it("許可リストに無ければ403", async () => {
    await writeAllowlist(workerEnv.BUCKET, ["76561198000000002"]);
    const response = await handle(sessionRequest(JSON.stringify({ ticket: "aabb" })), workerEnv, steamOk("76561198000000001"));
    expect(response.status).toBe(403);
    expect(await response.json()).toEqual({ reason: "not-allowed" });
  });

  it("許可リストが空でも誰も通さない", async () => {
    const response = await handle(sessionRequest(JSON.stringify({ ticket: "aabb" })), workerEnv, steamOk("76561198000000001"));
    expect(response.status).toBe(403);
  });

  it("チケット検証に失敗すれば401", async () => {
    const response = await handle(sessionRequest(JSON.stringify({ ticket: "aabb" })), workerEnv, steamNg);
    expect(response.status).toBe(401);
    expect(await response.json()).toEqual({ reason: "invalid-ticket" });
  });

  it("ticketが16進でなければ400", async () => {
    const response = await handle(sessionRequest(JSON.stringify({ ticket: "zz" })), workerEnv, steamOk("1"));
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-request" });
  });

  it("bodyがJSONでなければ400", async () => {
    const response = await handle(sessionRequest("not json"), workerEnv, steamOk("1"));
    expect(response.status).toBe(400);
  });
});
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `cd tools/playtest-receiver && pnpm test`
Expected: `src/steamAuth.ts`・`src/allowlist.ts`・`src/session.ts` が無く FAIL

- [ ] **Step 3: 実装する**

`tools/playtest-receiver/src/steamAuth.ts`:
```ts
import type { Env } from "./env";

export const STEAM_IDENTITY = "moorestech-playtest";
const STEAM_ENDPOINT = "https://partner.steam-api.com/ISteamUserAuth/AuthenticateUserTicket/v1/";

export interface SteamTicketResult {
  steamId: string | null;
  reason: string;
}

interface SteamResponseBody {
  response?: {
    params?: { result?: string; steamid?: string };
    error?: { errorcode?: number; errordesc?: string };
  };
}

// Steam Web APIは外部サービス境界。到達失敗も応答の形の崩れも「検証できなかった」へ畳む
// The Steam Web API is an external boundary; unreachability and malformed bodies both collapse to "not verified"
export async function authenticateUserTicket(steamFetch: typeof fetch, env: Env, ticketHex: string): Promise<SteamTicketResult> {
  const url = `${STEAM_ENDPOINT}?key=${encodeURIComponent(env.STEAM_WEB_API_KEY)}&appid=${encodeURIComponent(env.STEAM_APP_ID)}&ticket=${encodeURIComponent(ticketHex)}&identity=${encodeURIComponent(STEAM_IDENTITY)}`;

  let body: SteamResponseBody;
  try {
    const response = await steamFetch(url, { method: "GET" });
    if (!response.ok) {
      console.warn(`[steam] AuthenticateUserTicket returned HTTP ${response.status}`);
      return { steamId: null, reason: `http-${response.status}` };
    }
    body = (await response.json()) as SteamResponseBody;
  } catch (error) {
    const message = error instanceof Error ? error.message : String(error);
    console.warn(`[steam] AuthenticateUserTicket failed: ${message}`);
    return { steamId: null, reason: message };
  }

  const error = body.response?.error;
  if (error !== undefined) {
    console.warn(`[steam] ticket rejected: ${error.errorcode} ${error.errordesc}`);
    return { steamId: null, reason: `steam-error-${error.errorcode ?? "unknown"}` };
  }

  const params = body.response?.params;
  if (params?.result !== "OK" || typeof params.steamid !== "string" || params.steamid.length === 0) {
    console.warn(`[steam] unexpected result: ${params?.result ?? "missing"}`);
    return { steamId: null, reason: `result-${params?.result ?? "missing"}` };
  }
  return { steamId: params.steamid, reason: "ok" };
}
```

`tools/playtest-receiver/src/allowlist.ts`:
```ts
export const ALLOWLIST_KEY = "config/allowlist.json";

export async function readAllowlist(bucket: R2Bucket): Promise<string[]> {
  const object = await bucket.get(ALLOWLIST_KEY);
  if (object === null) {
    console.warn(`[allowlist] ${ALLOWLIST_KEY} is absent; treating the allowlist as empty`);
    return [];
  }

  const text = await object.text();
  // R2の中身は人手のPUTでも壊れうる境界。壊れていたら全員不許可へ倒し、理由をログへ出す
  // R2 content can be corrupted by a hand-made PUT; on damage fail closed to "nobody allowed" and log why
  try {
    const parsed = JSON.parse(text) as { steamIds?: unknown };
    if (!Array.isArray(parsed.steamIds)) {
      console.warn("[allowlist] steamIds is not an array; treating the allowlist as empty");
      return [];
    }
    return parsed.steamIds.filter((value): value is string => typeof value === "string");
  } catch (error) {
    console.warn(`[allowlist] failed to parse ${ALLOWLIST_KEY}: ${error instanceof Error ? error.message : String(error)}`);
    return [];
  }
}

export async function writeAllowlist(bucket: R2Bucket, steamIds: string[]): Promise<void> {
  const unique = [...new Set(steamIds)];
  await bucket.put(ALLOWLIST_KEY, JSON.stringify({ steamIds: unique }), {
    httpMetadata: { contentType: "application/json" },
  });
}
```

`tools/playtest-receiver/src/session.ts`:
```ts
import { readAllowlist } from "./allowlist";
import type { Env } from "./env";
import { fail, json } from "./http";
import { authenticateUserTicket } from "./steamAuth";
import { signToken } from "./token";

const TICKET_PATTERN = /^[0-9a-fA-F]{2,8192}$/;

export async function postSession(request: Request, env: Env, steamFetch: typeof fetch): Promise<Response> {
  const ticket = await readTicket(request);
  if (ticket === null) return fail("bad-request", 400);

  const verified = await authenticateUserTicket(steamFetch, env, ticket);
  if (verified.steamId === null) return fail("invalid-ticket", 401);

  const allowlist = await readAllowlist(env.BUCKET);
  if (!allowlist.includes(verified.steamId)) {
    console.warn(`[session] ${verified.steamId} is not on the allowlist`);
    return fail("not-allowed", 403);
  }

  const token = await signToken(env.SESSION_HMAC_SECRET, verified.steamId, Math.floor(Date.now() / 1000));
  return json({ steamId: verified.steamId, allowed: true, token });
}

async function readTicket(request: Request): Promise<string | null> {
  // クライアントが送るJSONのパースは外部入力境界。壊れた入力は400へ隔離する
  // Parsing client-supplied JSON is an external-input boundary; malformed input is isolated into a 400
  try {
    const body = (await request.json()) as { ticket?: unknown };
    if (typeof body.ticket !== "string" || !TICKET_PATTERN.test(body.ticket)) return null;
    return body.ticket;
  } catch {
    return null;
  }
}
```

`tools/playtest-receiver/src/index.ts` の先頭へ `import { postSession } from "./session";` を足し、`/v1/session` 分岐を差し替える:
```ts
  if (segments.length === 2 && segments[1] === "session") {
    if (request.method !== "POST") return fail("method-not-allowed", 405);
    return postSession(request, env, steamFetch);
  }
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd tools/playtest-receiver && pnpm test && pnpm typecheck`
Expected: 全 PASS

- [ ] **Step 5: コミットする**

```bash
git add tools/playtest-receiver
git commit -m "feat(playtest): Steamチケット検証と許可リストとセッション発行"
```

---

### Task 3: アップロード（`PUT /v1/uploads/...`・`POST .../complete`）と未ACK索引

**Files:**
- Create: `tools/playtest-receiver/src/keys.ts`
- Create: `tools/playtest-receiver/src/uploads.ts`
- Modify: `tools/playtest-receiver/src/index.ts`（`/v1/uploads` の分岐を足す）
- Test: `tools/playtest-receiver/test/keys.test.ts`・`tools/playtest-receiver/test/uploads.test.ts`

**Interfaces:**
- Consumes: Task 1 の `Env`・`fail`・`json`・`verifyToken`、Task 2 の `handle` のルータ構造
- Produces:
  - `export const KIND_PREFIX = { report: "reports", progress: "progress" } as const`、`export type PlaytestKind = keyof typeof KIND_PREFIX`、`export function isKind(value: string): value is PlaytestKind`、`export function bundlePrefix(kind: PlaytestKind, steamId: string, id: string): string`、`export function pendingIndexKey(kind: PlaytestKind, steamId: string, id: string): string`、`export function parsePendingIndexKey(key: string): { kind: PlaytestKind; steamId: string; id: string } | null`、`export function isSafeSegment(segment: string): boolean`、`export function joinSafePath(segments: string[]): string | null`（`src/keys.ts`）
  - `export const MAX_FILE_BYTES = 100 * 1024 * 1024`、`export const READY_MARKER = "READY"`、`export const ACKED_MARKER = "ACKED"`、`export async function putUpload(request: Request, env: Env, kind, id, pathSegments): Promise<Response>`、`export async function completeUpload(request: Request, env: Env, kind, id): Promise<Response>`（`src/uploads.ts`）

- [ ] **Step 1: 失敗するテストを書く**

`tools/playtest-receiver/test/keys.test.ts`:
```ts
import { describe, expect, it } from "vitest";
import { bundlePrefix, isKind, isSafeSegment, joinSafePath, KIND_PREFIX, parsePendingIndexKey, pendingIndexKey } from "../src/keys";

describe("keys", () => {
  it("kindごとのプレフィックスは表のとおりで、progressは重ねたsを付けない", () => {
    expect(KIND_PREFIX.report).toBe("reports");
    expect(KIND_PREFIX.progress).toBe("progress");
  });

  it("知らないkindは弾く", () => {
    expect(isKind("report")).toBe(true);
    expect(isKind("progress")).toBe(true);
    expect(isKind("config")).toBe(false);
  });

  it("バンドルのプレフィックスを組み立てる", () => {
    expect(bundlePrefix("report", "76561198000000001", "20260913_120000_aaaa1111"))
      .toBe("reports/76561198000000001/20260913_120000_aaaa1111");
  });

  it("索引キーは往復する", () => {
    const key = pendingIndexKey("progress", "76561198000000001", "20260913_120000_aaaa1111");
    expect(key).toBe("index/pending/progress/76561198000000001/20260913_120000_aaaa1111");
    expect(parsePendingIndexKey(key)).toEqual({ kind: "progress", steamId: "76561198000000001", id: "20260913_120000_aaaa1111" });
  });

  it("形の違う索引キーはnullになる", () => {
    expect(parsePendingIndexKey("index/pending/report/only-two")).toBeNull();
    expect(parsePendingIndexKey("reports/a/b/c")).toBeNull();
  });

  it("危ないセグメントを拒否する", () => {
    expect(isSafeSegment("logs")).toBe(true);
    expect(isSafeSegment("..")).toBe(false);
    expect(isSafeSegment(".")).toBe(false);
    expect(isSafeSegment("")).toBe(false);
    expect(isSafeSegment("a\\b")).toBe(false);
  });

  it("安全なセグメントだけを連結する", () => {
    expect(joinSafePath(["snapshots", "tick_600.json"])).toBe("snapshots/tick_600.json");
    expect(joinSafePath(["..", "etc"])).toBeNull();
    expect(joinSafePath([])).toBeNull();
  });
});
```

`tools/playtest-receiver/test/uploads.test.ts`:
```ts
import { env } from "cloudflare:test";
import { beforeEach, describe, expect, it } from "vitest";
import { handle } from "../src/index";
import { signToken } from "../src/token";
import { pendingIndexKey } from "../src/keys";
import type { Env } from "../src/env";

const workerEnv = env as unknown as Env;
const STEAM_ID = "76561198000000001";
const ID = "20260913_120000_aaaa1111";
const noNetwork: typeof fetch = (async () => {
  throw new Error("tests must not reach the network");
}) as unknown as typeof fetch;

async function bearer(steamId = STEAM_ID): Promise<string> {
  return `Bearer ${await signToken(workerEnv.SESSION_HMAC_SECRET, steamId, Math.floor(Date.now() / 1000))}`;
}

async function clean(): Promise<void> {
  const listed = await workerEnv.BUCKET.list({ limit: 1000 });
  await Promise.all(listed.objects.map((object) => workerEnv.BUCKET.delete(object.key)));
}

describe("uploads", () => {
  beforeEach(clean);

  it("PUTしたファイルがtokenのsteamId配下へ入る", async () => {
    const response = await handle(
      new Request(`https://playtest.moores.tech/v1/uploads/report/${ID}/logs/unity.log`, {
        method: "PUT",
        headers: { authorization: await bearer() },
        body: "hello",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(200);
    const stored = await workerEnv.BUCKET.get(`reports/${STEAM_ID}/${ID}/logs/unity.log`);
    expect(await stored?.text()).toBe("hello");
  });

  it("URLに他人のsteamIdは現れずtokenだけが置き場を決める", async () => {
    await handle(
      new Request(`https://playtest.moores.tech/v1/uploads/progress/${ID}/record.json`, {
        method: "PUT",
        headers: { authorization: await bearer("76561198000000009") },
        body: "{}",
      }),
      workerEnv,
      noNetwork,
    );
    expect(await workerEnv.BUCKET.get(`progress/76561198000000009/${ID}/record.json`)).not.toBeNull();
    expect(await workerEnv.BUCKET.get(`progress/${STEAM_ID}/${ID}/record.json`)).toBeNull();
  });

  it("トークンが無ければ401", async () => {
    const response = await handle(
      new Request(`https://playtest.moores.tech/v1/uploads/report/${ID}/a.txt`, { method: "PUT", body: "x" }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(401);
  });

  it("kindが不正なら400", async () => {
    const response = await handle(
      new Request(`https://playtest.moores.tech/v1/uploads/config/${ID}/a.txt`, {
        method: "PUT",
        headers: { authorization: await bearer() },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
  });

  // URLコンストラクタは生の「..」を先に畳んでしまうため、逸脱の試行はパーセントエンコードで送る
  // The URL constructor collapses a raw ".." first, so traversal attempts are sent percent-encoded
  it("..を含むパスは400で何も書かない", async () => {
    const response = await handle(
      new Request(`https://playtest.moores.tech/v1/uploads/report/${ID}/%2E%2E/%2E%2E/etc/passwd`, {
        method: "PUT",
        headers: { authorization: await bearer() },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
    expect((await workerEnv.BUCKET.list({ limit: 10 })).objects).toHaveLength(0);
  });

  it("Content-Lengthが100MiBを超えたら413", async () => {
    const response = await handle(
      new Request(`https://playtest.moores.tech/v1/uploads/report/${ID}/video.mp4`, {
        method: "PUT",
        headers: { authorization: await bearer(), "content-length": String(100 * 1024 * 1024 + 1) },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(413);
    expect(await response.json()).toEqual({ reason: "too-large" });
  });

  it("idが..なら400で何も書かない", async () => {
    const response = await handle(
      new Request("https://playtest.moores.tech/v1/uploads/report/%2E%2E/a.txt", {
        method: "PUT",
        headers: { authorization: await bearer() },
        body: "x",
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
    expect((await workerEnv.BUCKET.list({ limit: 10 })).objects).toHaveLength(0);
  });

  it("completeでREADYと未ACK索引が出来る", async () => {
    const summary = JSON.stringify({ kind: "bug", fileCount: 1 });
    const response = await handle(
      new Request(`https://playtest.moores.tech/v1/uploads/report/${ID}/complete`, {
        method: "POST",
        headers: { authorization: await bearer() },
        body: summary,
      }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(200);
    const ready = await workerEnv.BUCKET.get(`reports/${STEAM_ID}/${ID}/READY`);
    expect(await ready?.text()).toBe(summary);
    expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, ID))).not.toBeNull();
  });
});
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `cd tools/playtest-receiver && pnpm test`
Expected: `src/keys.ts`・`src/uploads.ts` が無く FAIL

- [ ] **Step 3: 実装する**

`tools/playtest-receiver/src/keys.ts`:
```ts
// R2のプレフィックス表。共有契約§4の「{kind}s」を字義適用すると progresss になるため表で固定する
// Prefix table for R2; the contract's literal "{kind}s" would yield "progresss", so the mapping is pinned here
export const KIND_PREFIX = { report: "reports", progress: "progress" } as const;

export type PlaytestKind = keyof typeof KIND_PREFIX;

const PENDING_ROOT = "index/pending";
const SEGMENT_PATTERN = /^[A-Za-z0-9._-]+$/;

export function isKind(value: string): value is PlaytestKind {
  return value === "report" || value === "progress";
}

export function bundlePrefix(kind: PlaytestKind, steamId: string, id: string): string {
  return `${KIND_PREFIX[kind]}/${steamId}/${id}`;
}

export function pendingIndexKey(kind: PlaytestKind, steamId: string, id: string): string {
  return `${PENDING_ROOT}/${kind}/${steamId}/${id}`;
}

export function parsePendingIndexKey(key: string): { kind: PlaytestKind; steamId: string; id: string } | null {
  const segments = key.split("/");
  if (segments.length !== 5) return null;
  if (segments[0] !== "index" || segments[1] !== "pending") return null;
  const [, , kind, steamId, id] = segments as [string, string, string, string, string];
  if (!isKind(kind)) return null;
  return { kind, steamId, id };
}

// 逸脱の入口はここ1箇所。「.」「..」「空」「区切り文字混入」を弾けば連結後のキーは必ずprefix配下に入る
// This is the only entry point for traversal; rejecting ".", "..", empty and separators keeps every key under the prefix
export function isSafeSegment(segment: string): boolean {
  if (segment.length === 0 || segment === "." || segment === "..") return false;
  return SEGMENT_PATTERN.test(segment);
}

export function joinSafePath(segments: string[]): string | null {
  if (segments.length === 0) return null;
  for (const segment of segments) {
    if (!isSafeSegment(segment)) return null;
  }
  return segments.join("/");
}
```

`tools/playtest-receiver/src/uploads.ts`:
```ts
import type { Env } from "./env";
import { fail, json } from "./http";
import { bundlePrefix, joinSafePath, pendingIndexKey, type PlaytestKind } from "./keys";
import { verifyToken } from "./token";

export const MAX_FILE_BYTES = 100 * 1024 * 1024;
export const READY_MARKER = "READY";
export const ACKED_MARKER = "ACKED";

// Bearerトークンだけが置き場を決める。URLにsteamIdは出さないので他人の領域へは書けない
// Only the bearer token decides the destination; the URL carries no steamId, so nobody can write into another's area
async function authorize(request: Request, env: Env): Promise<string | null> {
  const header = request.headers.get("authorization") ?? "";
  if (!header.startsWith("Bearer ")) return null;
  return verifyToken(env.SESSION_HMAC_SECRET, header.slice("Bearer ".length), Math.floor(Date.now() / 1000));
}

export async function putUpload(request: Request, env: Env, kind: PlaytestKind, id: string, pathSegments: string[]): Promise<Response> {
  const steamId = await authorize(request, env);
  if (steamId === null) return fail("unauthorized", 401);

  const relativePath = joinSafePath(pathSegments);
  if (relativePath === null) {
    console.warn(`[upload] rejected an unsafe path: ${pathSegments.join("/")}`);
    return fail("bad-path", 400);
  }

  const declared = Number(request.headers.get("content-length") ?? "0");
  if (Number.isFinite(declared) && declared > MAX_FILE_BYTES) {
    console.warn(`[upload] ${steamId}/${id}/${relativePath} declares ${declared} bytes, over the limit`);
    return fail("too-large", 413);
  }

  await env.BUCKET.put(`${bundlePrefix(kind, steamId, id)}/${relativePath}`, request.body);
  return json({ stored: relativePath });
}

export async function completeUpload(request: Request, env: Env, kind: PlaytestKind, id: string): Promise<Response> {
  const steamId = await authorize(request, env);
  if (steamId === null) return fail("unauthorized", 401);

  const summary = await request.text();
  await env.BUCKET.put(`${bundlePrefix(kind, steamId, id)}/${READY_MARKER}`, summary, {
    httpMetadata: { contentType: "application/json" },
  });

  // 未ACKの列挙をR2の全走査にしないため、READYと対の索引オブジェクトを置く。ackで消す
  // A paired index object keeps "pending" enumerable without scanning all of R2; ack deletes it
  await env.BUCKET.put(pendingIndexKey(kind, steamId, id), "");
  return json({ ready: true });
}
```

`tools/playtest-receiver/src/index.ts` に import と分岐を足す:
```ts
import { isKind, isSafeSegment } from "./keys";
import { completeUpload, putUpload } from "./uploads";
```
```ts
  if (segments[1] === "uploads" && segments.length >= 4) {
    const kind = segments[2] as string;
    const id = segments[3] as string;
    if (!isKind(kind)) return fail("bad-kind", 400);
    // idもキーの一部なので同じ検査を通す。ここを抜かすと ".." のidでprefixを抜けられる
    // The id is part of the key too, so it takes the same check; skipping it would let ".." escape the prefix
    if (!isSafeSegment(id)) return fail("bad-path", 400);

    const rest = segments.slice(4);
    if (rest.length === 1 && rest[0] === "complete") {
      if (request.method !== "POST") return fail("method-not-allowed", 405);
      return completeUpload(request, env, kind, id);
    }
    if (request.method !== "PUT") return fail("method-not-allowed", 405);
    return putUpload(request, env, kind, id, rest);
  }
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd tools/playtest-receiver && pnpm test && pnpm typecheck`
Expected: 全 PASS

- [ ] **Step 5: コミットする**

```bash
git add tools/playtest-receiver
git commit -m "feat(playtest): 受け口のアップロードとREADY・未ACK索引"
```

---

### Task 4: 管理API（inbox 列挙・取得・ack、許可リスト GET/PUT）と README・デプロイ手順

**Files:**
- Create: `tools/playtest-receiver/src/admin.ts`
- Create: `tools/playtest-receiver/README.md`
- Modify: `tools/playtest-receiver/src/index.ts`（`/v1/inbox`・`/v1/allowlist` の分岐を足す）
- Test: `tools/playtest-receiver/test/admin.test.ts`

**Interfaces:**
- Consumes: Task 1〜3 の `requireAdmin`・`bundlePrefix`・`pendingIndexKey`・`parsePendingIndexKey`・`READY_MARKER`・`ACKED_MARKER`・`readAllowlist`・`writeAllowlist`
- Produces（`src/admin.ts`）:
  - `export async function getInbox(request: Request, env: Env): Promise<Response>` → `{ items: { kind, steamId, id, readyAt }[], cursor: string | null }`
  - `export async function getInboxObject(request: Request, env: Env, kind: PlaytestKind, steamId: string, id: string, pathSegments: string[]): Promise<Response>`
  - `export async function postAck(request: Request, env: Env, kind: PlaytestKind, steamId: string, id: string): Promise<Response>`
  - `export async function getAllowlist(request: Request, env: Env): Promise<Response>` → `{ steamIds: string[] }`
  - `export async function putAllowlist(request: Request, env: Env): Promise<Response>`

- [ ] **Step 1: 失敗するテストを書く**

`tools/playtest-receiver/test/admin.test.ts`:
```ts
import { env } from "cloudflare:test";
import { beforeEach, describe, expect, it } from "vitest";
import { handle } from "../src/index";
import { signToken } from "../src/token";
import type { Env } from "../src/env";

const workerEnv = env as unknown as Env;
const STEAM_ID = "76561198000000001";
const ADMIN = { "x-admin-key": "test-admin-key" };
const noNetwork: typeof fetch = (async () => {
  throw new Error("tests must not reach the network");
}) as unknown as typeof fetch;

async function clean(): Promise<void> {
  const listed = await workerEnv.BUCKET.list({ limit: 1000 });
  await Promise.all(listed.objects.map((object) => workerEnv.BUCKET.delete(object.key)));
}

async function upload(kind: string, id: string, path: string, body: string): Promise<void> {
  const token = await signToken(workerEnv.SESSION_HMAC_SECRET, STEAM_ID, Math.floor(Date.now() / 1000));
  await handle(
    new Request(`https://playtest.moores.tech/v1/uploads/${kind}/${id}/${path}`, {
      method: "PUT",
      headers: { authorization: `Bearer ${token}` },
      body,
    }),
    workerEnv,
    noNetwork,
  );
  await handle(
    new Request(`https://playtest.moores.tech/v1/uploads/${kind}/${id}/complete`, {
      method: "POST",
      headers: { authorization: `Bearer ${token}` },
      body: JSON.stringify({ kind: "bug" }),
    }),
    workerEnv,
    noNetwork,
  );
}

describe("admin api", () => {
  beforeEach(clean);

  it("adminキーが無ければ401", async () => {
    const response = await handle(new Request("https://playtest.moores.tech/v1/inbox"), workerEnv, noNetwork);
    expect(response.status).toBe(401);
    expect(await response.json()).toEqual({ reason: "unauthorized" });
  });

  it("adminキーが違えば401", async () => {
    const response = await handle(
      new Request("https://playtest.moores.tech/v1/inbox", { headers: { "x-admin-key": "wrong" } }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(401);
  });

  it("completeした2件がinboxに出る", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "x");
    await upload("progress", "20260913_130000_bbbb2222", "record.json", "{}");
    const response = await handle(new Request("https://playtest.moores.tech/v1/inbox", { headers: ADMIN }), workerEnv, noNetwork);
    const body = (await response.json()) as { items: { kind: string; steamId: string; id: string; readyAt: string }[]; cursor: string | null };
    expect(body.items).toHaveLength(2);
    expect(body.items.map((item) => item.id).sort()).toEqual(["20260913_120000_aaaa1111", "20260913_130000_bbbb2222"]);
    expect(body.items[0]?.steamId).toBe(STEAM_ID);
    expect(Number.isNaN(Date.parse(body.items[0]?.readyAt ?? ""))).toBe(false);
  });

  it("inboxの個別ファイルを取れる。不在は404", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "hello");
    const ok = await handle(
      new Request(`https://playtest.moores.tech/v1/inbox/report/${STEAM_ID}/20260913_120000_aaaa1111/a.txt`, { headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    expect(await ok.text()).toBe("hello");
    const missing = await handle(
      new Request(`https://playtest.moores.tech/v1/inbox/report/${STEAM_ID}/20260913_120000_aaaa1111/none.txt`, { headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    expect(missing.status).toBe(404);
  });

  it("ackするとinboxから消えACKEDが出来る", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "x");
    const acked = await handle(
      new Request(`https://playtest.moores.tech/v1/inbox/report/${STEAM_ID}/20260913_120000_aaaa1111/ack`, { method: "POST", headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    expect(acked.status).toBe(200);
    expect(await workerEnv.BUCKET.get(`reports/${STEAM_ID}/20260913_120000_aaaa1111/ACKED`)).not.toBeNull();
    const inbox = await handle(new Request("https://playtest.moores.tech/v1/inbox", { headers: ADMIN }), workerEnv, noNetwork);
    expect(((await inbox.json()) as { items: unknown[] }).items).toHaveLength(0);
  });

  it("許可リストがGET/PUTで往復する", async () => {
    const put = await handle(
      new Request("https://playtest.moores.tech/v1/allowlist", {
        method: "PUT",
        headers: ADMIN,
        body: JSON.stringify({ steamIds: [STEAM_ID] }),
      }),
      workerEnv,
      noNetwork,
    );
    expect(put.status).toBe(200);
    const get = await handle(new Request("https://playtest.moores.tech/v1/allowlist", { headers: ADMIN }), workerEnv, noNetwork);
    expect(await get.json()).toEqual({ steamIds: [STEAM_ID] });
  });

  it("許可リストPUTの本文が壊れていれば400で現状を壊さない", async () => {
    await handle(
      new Request("https://playtest.moores.tech/v1/allowlist", { method: "PUT", headers: ADMIN, body: JSON.stringify({ steamIds: [STEAM_ID] }) }),
      workerEnv,
      noNetwork,
    );
    const broken = await handle(
      new Request("https://playtest.moores.tech/v1/allowlist", { method: "PUT", headers: ADMIN, body: "{ nope" }),
      workerEnv,
      noNetwork,
    );
    expect(broken.status).toBe(400);
    const get = await handle(new Request("https://playtest.moores.tech/v1/allowlist", { headers: ADMIN }), workerEnv, noNetwork);
    expect(await get.json()).toEqual({ steamIds: [STEAM_ID] });
  });
});
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `cd tools/playtest-receiver && pnpm test`
Expected: `src/admin.ts` が無く FAIL

- [ ] **Step 3: 実装する**

`tools/playtest-receiver/src/admin.ts`:
```ts
import { readAllowlist, writeAllowlist } from "./allowlist";
import type { Env } from "./env";
import { fail, json, requireAdmin } from "./http";
import { bundlePrefix, joinSafePath, parsePendingIndexKey, pendingIndexKey, type PlaytestKind } from "./keys";
import { ACKED_MARKER } from "./uploads";

const INBOX_PAGE_SIZE = 100;
const PENDING_PREFIX = "index/pending/";

export async function getInbox(request: Request, env: Env): Promise<Response> {
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;

  const cursor = new URL(request.url).searchParams.get("cursor") ?? undefined;
  const listed = await env.BUCKET.list({ prefix: PENDING_PREFIX, limit: INBOX_PAGE_SIZE, cursor });

  const items = [];
  for (const object of listed.objects) {
    const parsed = parsePendingIndexKey(object.key);
    if (parsed === null) {
      console.warn(`[inbox] skipped an index key with an unexpected shape: ${object.key}`);
      continue;
    }
    items.push({ kind: parsed.kind, steamId: parsed.steamId, id: parsed.id, readyAt: object.uploaded.toISOString() });
  }

  return json({ items, cursor: listed.truncated ? listed.cursor : null });
}

export async function getInboxObject(
  request: Request,
  env: Env,
  kind: PlaytestKind,
  steamId: string,
  id: string,
  pathSegments: string[],
): Promise<Response> {
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;

  const relativePath = joinSafePath(pathSegments);
  if (relativePath === null) return fail("bad-path", 400);

  const object = await env.BUCKET.get(`${bundlePrefix(kind, steamId, id)}/${relativePath}`);
  if (object === null) return fail("not-found", 404);
  return new Response(object.body, { headers: { "content-type": "application/octet-stream" } });
}

// ackは「索引を消してACKEDを書く」の2手。索引を先に消すと取り込み側から見えなくなるので順序を固定する
// Ack is two steps: write ACKED, then drop the index, so a crash in between leaves the item still visible
export async function postAck(request: Request, env: Env, kind: PlaytestKind, steamId: string, id: string): Promise<Response> {
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;

  await env.BUCKET.put(`${bundlePrefix(kind, steamId, id)}/${ACKED_MARKER}`, new Date().toISOString());
  await env.BUCKET.delete(pendingIndexKey(kind, steamId, id));
  return json({ acked: true });
}

export async function getAllowlist(request: Request, env: Env): Promise<Response> {
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;
  return json({ steamIds: await readAllowlist(env.BUCKET) });
}

export async function putAllowlist(request: Request, env: Env): Promise<Response> {
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;

  // 全置換なので壊れた本文で上書きしない。パースに失敗したら現状を残して400を返す
  // This replaces the whole list, so a malformed body must never overwrite it; parse failures keep the current list
  let steamIds: string[];
  try {
    const body = (await request.json()) as { steamIds?: unknown };
    if (!Array.isArray(body.steamIds) || body.steamIds.some((value) => typeof value !== "string")) {
      return fail("bad-request", 400);
    }
    steamIds = body.steamIds as string[];
  } catch {
    return fail("bad-request", 400);
  }

  await writeAllowlist(env.BUCKET, steamIds);
  console.warn(`[allowlist] replaced with ${steamIds.length} steamIds`);
  return json({ steamIds: [...new Set(steamIds)] });
}
```

`tools/playtest-receiver/src/index.ts` に import と分岐を足す:
```ts
import { getAllowlist, getInbox, getInboxObject, postAck, putAllowlist } from "./admin";
```
```ts
  if (segments[1] === "allowlist" && segments.length === 2) {
    if (request.method === "GET") return getAllowlist(request, env);
    if (request.method === "PUT") return putAllowlist(request, env);
    return fail("method-not-allowed", 405);
  }

  if (segments[1] === "inbox") {
    if (segments.length === 2) {
      if (request.method !== "GET") return fail("method-not-allowed", 405);
      return getInbox(request, env);
    }
    if (segments.length < 6) return fail("not-found", 404);

    const kind = segments[2] as string;
    const steamId = segments[3] as string;
    const id = segments[4] as string;
    if (!isKind(kind)) return fail("bad-kind", 400);
    if (!isSafeSegment(steamId) || !isSafeSegment(id)) return fail("bad-path", 400);

    const rest = segments.slice(5);
    if (rest.length === 1 && rest[0] === "ack") {
      if (request.method !== "POST") return fail("method-not-allowed", 405);
      return postAck(request, env, kind, steamId, id);
    }
    if (request.method !== "GET") return fail("method-not-allowed", 405);
    return getInboxObject(request, env, kind, steamId, id, rest);
  }
```

- [ ] **Step 4: テストを実行して通ることを確認する**

Run: `cd tools/playtest-receiver && pnpm test && pnpm typecheck`
Expected: 全 PASS

- [ ] **Step 5: README を書く**

`tools/playtest-receiver/README.md`:
```markdown
# playtest-receiver — プレイテスト報告の受け口（Cloudflare Worker + R2）

配布版 moorestech（Steam プレイテスト）からプレイ報告と進行記録を受け取り、R2 に貯める Worker。
Mac mini（plan H の `scripts/playtest/ingest.sh`）が管理APIで取り込む。設計は `docs/adr/0061-steam-closed-playtest-report-receiver-and-save-compat.md`。

## エンドポイント

| メソッド | パス | 認証 | 用途 |
|---|---|---|---|
| POST | `/v1/session` | なし（Steamチケット） | チケット検証＋許可リスト照合＋1時間トークン発行 |
| PUT | `/v1/uploads/{kind}/{id}/{path...}` | `Authorization: Bearer` | 1ファイル保存（≤100MiB） |
| POST | `/v1/uploads/{kind}/{id}/complete` | `Authorization: Bearer` | `READY` と未ACK索引を書く |
| GET | `/v1/inbox?cursor=` | `X-Admin-Key` | 未ACKの一覧 |
| GET | `/v1/inbox/{kind}/{steamId}/{id}/{path...}` | `X-Admin-Key` | 個別ファイル取得 |
| POST | `/v1/inbox/{kind}/{steamId}/{id}/ack` | `X-Admin-Key` | `ACKED` を書き索引を消す |
| GET / PUT | `/v1/allowlist` | `X-Admin-Key` | 許可SteamIDの取得・全置換 |

`kind` は `report` / `progress`。R2 のキーは `reports/{steamId}/{id}/...` と `progress/{steamId}/{id}/...`（`src/keys.ts` の `KIND_PREFIX` が正本）。

## 初回セットアップ

1. R2 バケットを作る:
   ```bash
   cd tools/playtest-receiver
   pnpm install
   pnpm exec wrangler r2 bucket create moorestech-playtest
   ```
2. secrets を入れる（値は Steam パートナーサイトと `openssl rand -hex 32` から）:
   ```bash
   pnpm exec wrangler secret put STEAM_WEB_API_KEY     # Steamworks の publisher key
   pnpm exec wrangler secret put SESSION_HMAC_SECRET   # openssl rand -hex 32
   pnpm exec wrangler secret put ADMIN_KEY             # openssl rand -hex 32
   ```
3. デプロイする:
   ```bash
   pnpm run deploy
   ```
4. DNS: `wrangler.toml` の `routes` に `playtest.moores.tech` を `custom_domain = true` で書いてあるので、`pnpm run deploy` が moores.tech ゾーンへ CNAME を作る。作られない場合は Cloudflare ダッシュボード → Workers & Pages → moorestech-playtest-receiver → Settings → Domains & Routes → Add → Custom domain に `playtest.moores.tech` を追加する。**cloudflared のトンネル（Mac mini）とは無関係の経路なので、`~/.cloudflared/*.yml` は触らない。**
5. 許可リストへ最初のテスターを入れる: `scripts/playtest/allowlist.sh add <steamId>`

## 動作確認

```bash
BASE=https://playtest.moores.tech
curl -s -o /dev/null -w '%{http_code}\n' "$BASE/v1/inbox"                          # 401 を期待
curl -s -H "X-Admin-Key: $PLAYTEST_ADMIN_KEY" "$BASE/v1/allowlist"                 # {"steamIds":[...]}
curl -s -X POST -d '{"ticket":"00"}' "$BASE/v1/session"                            # 401（無効チケット）
```

## 開発

```bash
pnpm test        # vitest（@cloudflare/vitest-pool-workers。ネットワークへは出ない）
pnpm typecheck
pnpm dev         # ローカル wrangler dev
```

Steam Web API の呼び出しは `handle(request, env, steamFetch)` の第3引数で差し替えられる。テストは必ず差し替えること。
```

- [ ] **Step 6: README のコマンド行を検算してコミットする**

Run: `cd tools/playtest-receiver && pnpm exec wrangler --version && pnpm test`
Expected: wrangler のバージョンが出て、テストが全 PASS

```bash
git add tools/playtest-receiver
git commit -m "feat(playtest): 受け口の管理APIとデプロイ手順"
```

---

### Task 5: `scripts/playtest/allowlist.sh`（Mac mini から許可リストを操作する）

**Files:**
- Create: `scripts/playtest/allowlist.sh`
- Create: `scripts/playtest/README.md`
- Test: `scripts/playtest/tests/test-allowlist.sh`

**Interfaces:**
- Consumes: Task 4 の `GET /v1/allowlist`・`PUT /v1/allowlist`（`X-Admin-Key`）
- Produces: `scripts/playtest/allowlist.sh add|remove|list [<steamId>]`。設定は `${PLAYTEST_ENV_FILE:-$HOME/hermes-agent/data/services/playtest/env.sh}` から `PLAYTEST_RECEIVER_BASE`（既定 `https://playtest.moores.tech`）と `PLAYTEST_ADMIN_KEY`（必須）。`CURL_CMD` で curl を差し替え可能

- [ ] **Step 1: 失敗するテストを書く**

`scripts/playtest/tests/test-allowlist.sh`:
```bash
#!/usr/bin/env bash
# allowlist.sh を curl スタブで検証する。スタブはJSONファイルを許可リストの実体として使う
# Verifies allowlist.sh with a curl stub that keeps the allowlist in a JSON file
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"
TMP="$(mktemp -d)"
trap 'rm -rf "$TMP"' EXIT

echo '{"steamIds":[]}' > "$TMP/state.json"

# curl スタブ: -X PUT なら --data を state.json へ書き、そうでなければ state.json を返す
# curl stub: with -X PUT it stores --data into state.json, otherwise it prints state.json
cat > "$TMP/curl" <<'SH'
#!/usr/bin/env bash
state="$STATE_FILE"
method=GET; data=""
while [ $# -gt 0 ]; do
  case "$1" in
    -X) method="$2"; shift 2;;
    --data) data="$2"; shift 2;;
    *) shift;;
  esac
done
if [ "$method" = "PUT" ]; then printf '%s' "$data" > "$state"; printf '%s' "$data"; else cat "$state"; fi
SH
chmod +x "$TMP/curl"

cat > "$TMP/env.sh" <<'SH'
PLAYTEST_RECEIVER_BASE=https://example.invalid
PLAYTEST_ADMIN_KEY=dummy-admin-key
SH

run() { PLAYTEST_ENV_FILE="$TMP/env.sh" CURL_CMD="$TMP/curl" STATE_FILE="$TMP/state.json" bash "$HERE/../allowlist.sh" "$@"; }

run add 76561198000000001 >/dev/null
grep -q '76561198000000001' "$TMP/state.json" || { echo "NG: addで追加されていない"; exit 1; }

run add 76561198000000001 >/dev/null
count="$(python3 -c "import json,sys;print(len(json.load(open(sys.argv[1]))['steamIds']))" "$TMP/state.json")"
[ "$count" = "1" ] || { echo "NG: 同じIDのaddで重複した ($count)"; exit 1; }

run add 76561198000000002 >/dev/null
[ "$(run list | wc -l | tr -d ' ')" = "2" ] || { echo "NG: listが2行でない"; exit 1; }
run list | grep -qx '76561198000000002' || { echo "NG: listにIDが出ない"; exit 1; }

run remove 76561198000000001 >/dev/null
grep -q '76561198000000001' "$TMP/state.json" && { echo "NG: removeで消えていない"; exit 1; }

# 未設定のenvは即エラー終了する（無音で通さない）
# Missing settings must fail loudly rather than silently proceed
cat > "$TMP/empty-env.sh" <<'SH'
PLAYTEST_RECEIVER_BASE=https://example.invalid
SH
if PLAYTEST_ENV_FILE="$TMP/empty-env.sh" CURL_CMD="$TMP/curl" STATE_FILE="$TMP/state.json" bash "$HERE/../allowlist.sh" list >/dev/null 2>&1; then
  echo "NG: PLAYTEST_ADMIN_KEY 未設定でも動いてしまった"; exit 1
fi

# 引数不足・未知のサブコマンドも失敗する
# Missing arguments and unknown subcommands must fail too
if run add >/dev/null 2>&1; then echo "NG: steamId 無しの add が通った"; exit 1; fi
if run nope >/dev/null 2>&1; then echo "NG: 未知のサブコマンドが通った"; exit 1; fi

echo "OK"
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `bash scripts/playtest/tests/test-allowlist.sh`
Expected: `allowlist.sh` が無いので失敗

- [ ] **Step 3: `allowlist.sh` を書く**

`scripts/playtest/allowlist.sh`:
```bash
#!/usr/bin/env bash
# プレイテスト受け口の許可SteamIDを管理APIで操作する（ADR 0061）。全置換PUTなので必ずGET→編集→PUTの順で行う
# Manages the playtest allowlist through the admin API (ADR 0061); PUT replaces the list, so always GET, edit, then PUT
set -euo pipefail

ENV_FILE="${PLAYTEST_ENV_FILE:-$HOME/hermes-agent/data/services/playtest/env.sh}"
# shellcheck disable=SC1090
[ -f "$ENV_FILE" ] && . "$ENV_FILE"

BASE="${PLAYTEST_RECEIVER_BASE:-https://playtest.moores.tech}"
ADMIN_KEY="${PLAYTEST_ADMIN_KEY:?PLAYTEST_ADMIN_KEY が未設定です（$ENV_FILE に書いてください）}"
CURL_CMD="${CURL_CMD:-curl}"

log() { echo "[allowlist] $*" >&2; }

fetch_list() {
  "$CURL_CMD" -sS -f -H "X-Admin-Key: $ADMIN_KEY" "$BASE/v1/allowlist"
}

# 現在のリストへ1件足す/引く。編集はpythonのjsonに任せ、順序は入力順を保つ
# Adds or removes one entry; python's json does the editing and input order is preserved
edit_list() {
  local mode="$1" steam_id="$2" current
  current="$(fetch_list)"
  printf '%s' "$current" | python3 -c '
import json, sys
mode, steam_id = sys.argv[1], sys.argv[2]
ids = json.load(sys.stdin).get("steamIds", [])
if mode == "add":
    if steam_id not in ids:
        ids.append(steam_id)
else:
    ids = [value for value in ids if value != steam_id]
print(json.dumps({"steamIds": ids}))
' "$mode" "$steam_id"
}

put_list() {
  "$CURL_CMD" -sS -f -X PUT -H "X-Admin-Key: $ADMIN_KEY" -H "Content-Type: application/json" --data "$1" "$BASE/v1/allowlist" >/dev/null
}

require_steam_id() {
  [ "${1:-}" != "" ] || { log "steamId を指定してください: $0 $2 <steamId>"; exit 2; }
}

case "${1:-}" in
  add)
    require_steam_id "${2:-}" add
    put_list "$(edit_list add "$2")"
    log "added: $2"
    ;;
  remove)
    require_steam_id "${2:-}" remove
    put_list "$(edit_list remove "$2")"
    log "removed: $2"
    ;;
  list)
    fetch_list | python3 -c "import json,sys;[print(value) for value in json.load(sys.stdin).get('steamIds', [])]"
    ;;
  *)
    log "使い方: $0 add|remove|list [<steamId>]"
    exit 2
    ;;
esac
```

（`edit_list` は「スクリプトを `-c` で、JSON を stdin で」渡す。heredoc を2つ並べる書き方（`python3 - ... <<'PY' <<<"$current"`）は bash が最後のリダイレクトだけを stdin に採るため、スクリプト本文が捨てられて壊れる。上の形を必ず使うこと）

- [ ] **Step 4: README を書く**

`scripts/playtest/README.md`:
```markdown
# プレイテスト運用スクリプト（Mac mini）

受け口 Worker は `tools/playtest-receiver/`。ここには Mac mini 側から叩く運用スクリプトを置く。

## 設定

`~/hermes-agent/data/services/playtest/env.sh`（git 管理外・実シークレット）:
```
PLAYTEST_RECEIVER_BASE=https://playtest.moores.tech
PLAYTEST_ADMIN_KEY=<wrangler secret put ADMIN_KEY で入れたのと同じ値>
```
別の場所に置く場合は `PLAYTEST_ENV_FILE` で指す。

## 許可リスト

```bash
scripts/playtest/allowlist.sh list
scripts/playtest/allowlist.sh add 76561198000000001
scripts/playtest/allowlist.sh remove 76561198000000001
```

許可リストは全置換 PUT で更新する。`add`/`remove` は内部で GET → 編集 → PUT を行うため、
2人が同時に実行すると後勝ちで片方の変更が消える。人手運用なので排他は設けていない。

不許可にした瞬間から新しいセッショントークンは出なくなるが、発行済みトークンは最大1時間有効で、
その間はアップロードだけ通る。起動時照合（クライアント）は次回起動から効く。

## テスト

```bash
bash scripts/playtest/tests/test-allowlist.sh   # OK と出れば合格
```
```

- [ ] **Step 5: テストを実行して通ることを確認する**

Run: `chmod +x scripts/playtest/allowlist.sh scripts/playtest/tests/test-allowlist.sh && bash scripts/playtest/tests/test-allowlist.sh`
Expected: `OK`

- [ ] **Step 6: コミットする**

```bash
git add scripts/playtest
git commit -m "feat(playtest): 許可リスト操作スクリプトとMac mini側README"
```

---

### Task 6: クライアント基盤（新アセンブリ・Steamチケット・受け口クライアント・セッション）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Client.PlaytestReceiver.asmdef`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/PlaytestReceiverConfig.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/PlaytestBuildInfoFile.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/IPlaytestSessionLookup.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/PlaytestSession.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Steam/PlaytestSteamTicketProvider.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/PlaytestApiResult.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/IPlaytestReceiverApi.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/PlaytestReceiverClient.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Tests.asmdef`（references に `Client.PlaytestReceiver` を追加）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestSessionTest.cs`・`PlaytestBuildInfoFileTest.cs`

**Interfaces:**
- Consumes: `Steamworks`（`com.rlabrecque.steamworks.net`）、`Game.Paths.GameSystemPaths`、UniTask
- Produces:
  - `public static class PlaytestReceiverConfig { public const string DefaultBaseUrl = "https://playtest.moores.tech"; public const string SteamIdentity = "moorestech-playtest"; public const int TicketTimeoutSeconds = 15; public const int HttpTimeoutSeconds = 60; public const int TokenRefreshAfterSeconds = 2700; public const long MaxFileBytes = 100L * 1024 * 1024; public static string BaseUrl { get; } }`（`BaseUrl` は環境変数 `MOORESTECH_PLAYTEST_RECEIVER_BASE` があればそれ、無ければ `DefaultBaseUrl`）
  - `public static class PlaytestBuildInfoFile { public static string Path { get; } public static bool Exists(); }`
  - `public interface IPlaytestSessionLookup { string SteamId { get; } bool HasToken { get; } }`
  - `public sealed class PlaytestSession : IPlaytestSessionLookup { public PlaytestSession(IPlaytestReceiverApi api, IPlaytestSteamTicketProvider ticketProvider); public string SteamId { get; } public bool HasToken { get; } public UniTask<PlaytestSessionResult> AuthenticateAsync(DateTime utcNow, CancellationToken token); public UniTask<string> GetValidTokenAsync(DateTime utcNow, CancellationToken token); }`
  - `public sealed class PlaytestSessionResult { public PlaytestSessionOutcome Outcome; public string SteamId; public string Detail; }`、`public enum PlaytestSessionOutcome { Allowed, NotAllowed, TicketRejected, TicketUnavailable, Unreachable }`
  - `public interface IPlaytestSteamTicketProvider { bool IsSteamRunning(); UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token); }` と実装 `public sealed class PlaytestSteamTicketProvider : IPlaytestSteamTicketProvider`（チケットが取れなければ `null`）
  - `public sealed class PlaytestApiResult { public int StatusCode; public string Body; public string TransportError; public bool IsTransportFailure => TransportError != null; }`
  - `public interface IPlaytestReceiverApi { UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token); UniTask<PlaytestApiResult> PutFileAsync(string bearerToken, string kind, string bundleId, string relativePath, string absoluteFilePath, CancellationToken token); UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, string kind, string bundleId, string summaryJson, CancellationToken token); }`
  - `public sealed class PlaytestReceiverClient : IPlaytestReceiverApi`

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestSessionTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestSessionTest
    {
        [Test]
        public void 許可されればトークンを保持しSteamIdが読める()
        {
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-1\"}" });
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));

            var result = session.AuthenticateAsync(new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestSessionOutcome.Allowed, result.Outcome);
            Assert.AreEqual("7656", session.SteamId);
            Assert.IsTrue(session.HasToken);
        }

        [Test]
        public void 期限内はトークンを取り直さない()
        {
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-1\"}" });
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var issuedAt = new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc);
            session.AuthenticateAsync(issuedAt, CancellationToken.None).GetAwaiter().GetResult();

            var token = session.GetValidTokenAsync(issuedAt.AddSeconds(PlaytestReceiverConfig.TokenRefreshAfterSeconds - 1), CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("tok-1", token);
            Assert.AreEqual(1, api.SessionCallCount);
        }

        [Test]
        public void 期限が近づいたらトークンを取り直す()
        {
            var api = new FakeApi();
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-1\"}" });
            api.SessionResponses.Add(new PlaytestApiResult { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok-2\"}" });
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var issuedAt = new DateTime(2026, 9, 13, 0, 0, 0, DateTimeKind.Utc);
            session.AuthenticateAsync(issuedAt, CancellationToken.None).GetAwaiter().GetResult();

            var token = session.GetValidTokenAsync(issuedAt.AddSeconds(PlaytestReceiverConfig.TokenRefreshAfterSeconds + 1), CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual("tok-2", token);
            Assert.AreEqual(2, api.SessionCallCount);
        }

        [Test]
        public void 応答コードごとに結末が分かれる()
        {
            AssertOutcome(new PlaytestApiResult { StatusCode = 403, Body = "{\"reason\":\"not-allowed\"}" }, PlaytestSessionOutcome.NotAllowed);
            AssertOutcome(new PlaytestApiResult { StatusCode = 401, Body = "{\"reason\":\"invalid-ticket\"}" }, PlaytestSessionOutcome.TicketRejected);
            AssertOutcome(new PlaytestApiResult { StatusCode = 500, Body = "" }, PlaytestSessionOutcome.Unreachable);
            AssertOutcome(new PlaytestApiResult { TransportError = "name resolution failed" }, PlaytestSessionOutcome.Unreachable);
        }

        [Test]
        public void チケットが取れなければ受け口を叩かない()
        {
            var api = new FakeApi();
            var session = new PlaytestSession(api, new FakeTicketProvider(null));

            var result = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();

            Assert.AreEqual(PlaytestSessionOutcome.TicketUnavailable, result.Outcome);
            Assert.AreEqual(0, api.SessionCallCount);
        }

        private static void AssertOutcome(PlaytestApiResult response, PlaytestSessionOutcome expected)
        {
            var api = new FakeApi();
            api.SessionResponses.Add(response);
            var session = new PlaytestSession(api, new FakeTicketProvider("aabb"));
            var result = session.AuthenticateAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
            Assert.AreEqual(expected, result.Outcome, result.Detail);
        }

        private sealed class FakeTicketProvider : IPlaytestSteamTicketProvider
        {
            private readonly string _ticketHex;
            public FakeTicketProvider(string ticketHex) { _ticketHex = ticketHex; }
            public bool IsSteamRunning() { return true; }
            public UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token) { return UniTask.FromResult(_ticketHex); }
        }

        private sealed class FakeApi : IPlaytestReceiverApi
        {
            public readonly List<PlaytestApiResult> SessionResponses = new();
            public int SessionCallCount;

            public UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token)
            {
                var response = SessionResponses[SessionCallCount];
                SessionCallCount++;
                return UniTask.FromResult(response);
            }

            public UniTask<PlaytestApiResult> PutFileAsync(string bearerToken, string kind, string bundleId, string relativePath, string absoluteFilePath, CancellationToken token)
            {
                return UniTask.FromResult(new PlaytestApiResult { StatusCode = 200, Body = "{}" });
            }

            public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, string kind, string bundleId, string summaryJson, CancellationToken token)
            {
                return UniTask.FromResult(new PlaytestApiResult { StatusCode = 200, Body = "{}" });
            }
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestBuildInfoFileTest.cs`:
```csharp
using System.IO;
using Client.PlaytestReceiver;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestBuildInfoFileTest
    {
        [Test]
        public void パスはStreamingAssets直下のbuildInfoJsonを指す()
        {
            StringAssert.EndsWith(Path.Combine("StreamingAssets", "build-info.json"), PlaytestBuildInfoFile.Path);
        }

        [Test]
        public void Editor実行では配布ビルドの印が無い()
        {
            // 配布ビルドだけがbuild-info.jsonを持つ。Editorに置かれていたら照合が誤発火する
            // Only distribution builds carry build-info.json; one left in the Editor would misfire the gate
            Assert.IsFalse(PlaytestBuildInfoFile.Exists(), $"Editorに {PlaytestBuildInfoFile.Path} が残っている");
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `Client.PlaytestReceiver` が無くコンパイルエラー

- [ ] **Step 3: アセンブリ定義を作る**

`moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Client.PlaytestReceiver.asmdef`:
```json
{
    "name": "Client.PlaytestReceiver",
    "rootNamespace": "",
    "references": [
        "Game.Paths",
        "Nuget",
        "UniTask",
        "UniRx",
        "Client.Localization",
        "com.rlabrecque.steamworks.net"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```
`Client.Tests/Tests.asmdef` の `references` 配列末尾に `"Client.PlaytestReceiver"` を足す。

> `com.rlabrecque.steamworks.net` の asmdef は `includePlatforms` が Editor と Standalone に限定されている。本アセンブリは `includePlatforms` を空（全プラットフォーム）にするため、Standalone/Editor 以外のプラットフォームへビルドすると参照エラーになる。moorestech は Windows/macOS Standalone しかビルドしないので許容し、この判断は「判断記録」に残す。

- [ ] **Step 4: 実装する**

`Client.PlaytestReceiver/PlaytestReceiverConfig.cs`:
```csharp
using System;
using UnityEngine;

namespace Client.PlaytestReceiver
{
    // 受け口の接続先と時間の定数。検証機・ステージング用に環境変数でbaseだけ差し替えられる
    // Connection and timing constants for the receiver; only the base URL can be overridden by env for verification machines
    public static class PlaytestReceiverConfig
    {
        public const string DefaultBaseUrl = "https://playtest.moores.tech";
        public const string SteamIdentity = "moorestech-playtest";
        public const string BaseUrlEnvironmentVariable = "MOORESTECH_PLAYTEST_RECEIVER_BASE";
        public const int TicketTimeoutSeconds = 15;
        public const int HttpTimeoutSeconds = 60;

        // 受け口のトークン寿命は3600秒。45分で取り直し、送信中に切れる窓を作らない
        // The receiver's token lives 3600s; refreshing at 45 min leaves no window where it expires mid-upload
        public const int TokenRefreshAfterSeconds = 2700;
        public const long MaxFileBytes = 100L * 1024 * 1024;

        public static string BaseUrl
        {
            get
            {
                var overridden = Environment.GetEnvironmentVariable(BaseUrlEnvironmentVariable);
                if (string.IsNullOrEmpty(overridden)) return DefaultBaseUrl;
                Debug.Log($"[PlaytestReceiver] base URL overridden by {BaseUrlEnvironmentVariable}: {overridden}");
                return overridden;
            }
        }
    }
}
```

`Client.PlaytestReceiver/PlaytestBuildInfoFile.cs`:
```csharp
using System.IO;
using UnityEngine;

namespace Client.PlaytestReceiver
{
    // 配布ビルドの印。plan E がビルド時に焼き、本アセンブリは在るかどうかしか見ない
    // The distribution-build marker; plan E bakes it at build time and this assembly only checks its presence
    public static class PlaytestBuildInfoFile
    {
        public const string FileName = "build-info.json";

        public static string Path => System.IO.Path.Combine(Application.streamingAssetsPath, FileName);

        public static bool Exists()
        {
            return File.Exists(Path);
        }
    }
}
```

`Client.PlaytestReceiver/Http/PlaytestApiResult.cs`:
```csharp
namespace Client.PlaytestReceiver.Http
{
    // HTTPの結末。到達できたかどうかと、到達できたときの状態コードを分けて持つ
    // The outcome of one HTTP call; reachability and the status code are kept apart
    public sealed class PlaytestApiResult
    {
        public int StatusCode;
        public string Body;
        public string TransportError;

        public bool IsTransportFailure => TransportError != null;
        public bool IsSuccess => TransportError == null && StatusCode >= 200 && StatusCode < 300;
    }
}
```

`Client.PlaytestReceiver/Http/IPlaytestReceiverApi.cs`:
```csharp
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Client.PlaytestReceiver.Http
{
    public interface IPlaytestReceiverApi
    {
        UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token);
        UniTask<PlaytestApiResult> PutFileAsync(string bearerToken, string kind, string bundleId, string relativePath, string absoluteFilePath, CancellationToken token);
        UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, string kind, string bundleId, string summaryJson, CancellationToken token);
    }
}
```

`Client.PlaytestReceiver/Http/PlaytestReceiverClient.cs`:
```csharp
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver.Http
{
    // 受け口への実HTTP。HttpClientは接続を使い回すため1本を持ち回る（前例 Client.WebUiHost/Vite/ViteHealthProbe.cs）
    // Real HTTP to the receiver; a single HttpClient is reused for connection pooling (precedent: ViteHealthProbe)
    public sealed class PlaytestReceiverClient : IPlaytestReceiverApi
    {
        private static readonly HttpClient Client = CreateClient();

        private readonly string _baseUrl;

        public PlaytestReceiverClient(string baseUrl)
        {
            _baseUrl = baseUrl.TrimEnd('/');
        }

        public UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/session")
            {
                Content = new StringContent($"{{\"ticket\":\"{ticketHex}\"}}", Encoding.UTF8, "application/json"),
            };
            return SendAsync(request, token);
        }

        public UniTask<PlaytestApiResult> PutFileAsync(string bearerToken, string kind, string bundleId, string relativePath, string absoluteFilePath, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Put, $"{_baseUrl}/v1/uploads/{kind}/{bundleId}/{relativePath}")
            {
                Content = new StreamContent(File.OpenRead(absoluteFilePath)),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return SendAsync(request, token);
        }

        public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, string kind, string bundleId, string summaryJson, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/uploads/{kind}/{bundleId}/complete")
            {
                Content = new StringContent(summaryJson, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return SendAsync(request, token);
        }

        private static async UniTask<PlaytestApiResult> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            // ネットワーク送受信は外部境界。到達失敗をTransportErrorへ隔離し、呼び出し側は状態コードだけを見る
            // Network I/O is an external boundary; unreachability is isolated into TransportError for the caller
            try
            {
                using (request)
                using (var response = await Client.SendAsync(request, token))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return new PlaytestApiResult { StatusCode = (int)response.StatusCode, Body = body };
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var message = exception.GetBaseException().Message;
                Debug.LogWarning($"[PlaytestReceiver] request to {request.RequestUri} failed: {message}");
                return new PlaytestApiResult { TransportError = message };
            }
        }

        private static HttpClient CreateClient()
        {
            return new HttpClient { Timeout = TimeSpan.FromSeconds(PlaytestReceiverConfig.HttpTimeoutSeconds) };
        }
    }
}
```

`Client.PlaytestReceiver/Steam/PlaytestSteamTicketProvider.cs`:
```csharp
using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Steamworks;
using UnityEngine;

namespace Client.PlaytestReceiver.Steam
{
    public interface IPlaytestSteamTicketProvider
    {
        bool IsSteamRunning();
        UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token);
    }

    // Web API用の認証チケットを取る。SteamManagerはAssembly-CSharp側にあり参照できないのでネイティブへ直接聞く
    // Obtains the Web API auth ticket; SteamManager lives in Assembly-CSharp and is unreachable, so we ask Steam directly
    public sealed class PlaytestSteamTicketProvider : IPlaytestSteamTicketProvider
    {
        private UniTaskCompletionSource<string> _pending;
        private Callback<GetTicketForWebApiResponse_t> _callback;

        public bool IsSteamRunning()
        {
            // ネイティブ呼び出しはdllが無い環境で例外になる境界。存在しなければ「Steamは動いていない」に畳む
            // The native call throws where the dll is absent; that boundary collapses to "Steam is not running"
            try
            {
                return SteamAPI.IsSteamRunning();
            }
            catch (Exception exception)
            {
                Debug.Log($"[PlaytestReceiver] Steam is unavailable: {exception.GetBaseException().Message}");
                return false;
            }
        }

        public async UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token)
        {
            _pending = new UniTaskCompletionSource<string>();
            _callback = Callback<GetTicketForWebApiResponse_t>.Create(OnTicketReceived);

            if (!TryRequestTicket()) return FinishWithNull();

            var (timedOut, ticketHex) = await _pending.Task.TimeoutWithoutException(TimeSpan.FromSeconds(PlaytestReceiverConfig.TicketTimeoutSeconds), cancellationToken: token);
            DisposeCallback();

            if (timedOut)
            {
                Debug.LogWarning($"[PlaytestReceiver] Steam did not answer GetAuthTicketForWebApi within {PlaytestReceiverConfig.TicketTimeoutSeconds}s");
                return null;
            }
            return ticketHex;
        }

        private bool TryRequestTicket()
        {
            // Steam未初期化ならSteamworksが例外を投げる境界。ここで畳んで「チケット無し」にする
            // Steamworks throws when uninitialized; that boundary is folded here into "no ticket"
            try
            {
                SteamUser.GetAuthTicketForWebApi(PlaytestReceiverConfig.SteamIdentity);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[PlaytestReceiver] GetAuthTicketForWebApi failed: {exception.GetBaseException().Message}");
                return false;
            }
        }

        private string FinishWithNull()
        {
            DisposeCallback();
            return null;
        }

        private void OnTicketReceived(GetTicketForWebApiResponse_t response)
        {
            if (response.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogWarning($"[PlaytestReceiver] web api ticket result was {response.m_eResult}");
                _pending.TrySetResult(null);
                return;
            }
            _pending.TrySetResult(ToHex(response.m_rgubTicket, response.m_cubTicket));
        }

        private void DisposeCallback()
        {
            _callback?.Dispose();
            _callback = null;
        }

        private static string ToHex(byte[] ticket, int length)
        {
            var builder = new StringBuilder(length * 2);
            for (var index = 0; index < length; index++) builder.Append(ticket[index].ToString("x2"));
            return builder.ToString();
        }
    }
}
```

`Client.PlaytestReceiver/IPlaytestSessionLookup.cs`:
```csharp
namespace Client.PlaytestReceiver
{
    // 読み取り面。トークンの取得・更新はアセンブリ内部（PlaytestSession）だけが行う
    // Read-only face; acquiring and refreshing the token stays inside this assembly
    public interface IPlaytestSessionLookup
    {
        string SteamId { get; }
        bool HasToken { get; }
    }
}
```

`Client.PlaytestReceiver/PlaytestSession.cs`:
```csharp
using System;
using System.Threading;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.PlaytestReceiver
{
    public enum PlaytestSessionOutcome
    {
        Allowed,
        NotAllowed,
        TicketRejected,
        TicketUnavailable,
        Unreachable,
    }

    public sealed class PlaytestSessionResult
    {
        public PlaytestSessionOutcome Outcome;
        public string SteamId;
        public string Detail;
    }

    public sealed class PlaytestSession : IPlaytestSessionLookup
    {
        private readonly IPlaytestReceiverApi _api;
        private readonly IPlaytestSteamTicketProvider _ticketProvider;

        private string _token;
        private DateTime _tokenIssuedAtUtc;

        public PlaytestSession(IPlaytestReceiverApi api, IPlaytestSteamTicketProvider ticketProvider)
        {
            _api = api;
            _ticketProvider = ticketProvider;
        }

        public string SteamId { get; private set; }
        public bool HasToken => _token != null;

        public async UniTask<PlaytestSessionResult> AuthenticateAsync(DateTime utcNow, CancellationToken token)
        {
            var ticketHex = await _ticketProvider.RequestWebApiTicketHexAsync(token);
            if (ticketHex == null)
            {
                return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.TicketUnavailable, Detail = "no web api ticket" };
            }

            var response = await _api.PostSessionAsync(ticketHex, token);
            if (response.IsTransportFailure)
            {
                return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.Unreachable, Detail = response.TransportError };
            }

            // 状態コードの意味は受け口の契約（§4）そのまま。ここが唯一の対応表
            // Status codes carry the receiver's contract (§4) verbatim; this is the single mapping table
            if (response.StatusCode == 403) return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.NotAllowed, Detail = response.Body };
            if (response.StatusCode == 401) return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.TicketRejected, Detail = response.Body };
            if (response.StatusCode != 200) return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.Unreachable, Detail = $"HTTP {response.StatusCode}" };

            var parsed = JObject.Parse(response.Body);
            SteamId = (string)parsed["steamId"];
            _token = (string)parsed["token"];
            _tokenIssuedAtUtc = utcNow;
            return new PlaytestSessionResult { Outcome = PlaytestSessionOutcome.Allowed, SteamId = SteamId };
        }

        // 送信の直前に呼ぶ。45分を超えていたら取り直し、取り直せなければnullを返して呼び出し側が持ち越す
        // Called right before an upload; refreshes past 45 minutes and returns null so the caller defers on failure
        public async UniTask<string> GetValidTokenAsync(DateTime utcNow, CancellationToken token)
        {
            var age = utcNow - _tokenIssuedAtUtc;
            if (_token != null && age.TotalSeconds < PlaytestReceiverConfig.TokenRefreshAfterSeconds) return _token;

            var result = await AuthenticateAsync(utcNow, token);
            if (result.Outcome == PlaytestSessionOutcome.Allowed) return _token;

            _token = null;
            Debug.LogWarning($"[PlaytestReceiver] could not refresh the session token: {result.Outcome} {result.Detail}");
            return null;
        }
    }
}
```

- [ ] **Step 5: コンパイルとテストを通す**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount 0`（`.asmdef` の `.meta` が Unity により生成される）

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.PlaytestReceiver\..*"`
Expected: 7件 PASS

- [ ] **Step 6: コミットする（生成された `.meta` を含める）**

```bash
git add moorestech_client/Assets/Scripts/Client.PlaytestReceiver moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver moorestech_client/Assets/Scripts/Client.Tests/Tests.asmdef
git commit -m "feat(playtest): 受け口クライアントとSteamチケット取得とセッション保持"
```

---

### Task 7: 起動時照合ゲートとタイトルでの停止（ローカライズ込み）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Gate/PlaytestGateResult.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Gate/PlaytestGateDecision.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Gate/PlaytestLaunchGate.cs`
- Create: `moorestech_client/Assets/Scripts/Client.MainMenu/Playtest/PlaytestLaunchGateView.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/LocalGameLauncher.cs`（開始前の関所）
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/ConnectServer.cs:33-40`（`Connect()` 冒頭の関所）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Client.Starter.asmdef`（references に `Client.PlaytestReceiver`）
- Modify: `Localization/localization.csv`（末尾に4行追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`（`dummyText` を更新）
- Modify: `moorestech_client/Assets/Scenes/Game/MainMenu.unity`（`uloop execute-dynamic-code` 経由でのみ。手編集禁止）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestGateDecisionTest.cs`・`PlaytestLaunchGateBlockTest.cs`

**Interfaces:**
- Consumes: Task 6 の `PlaytestSession`・`PlaytestSessionOutcome`・`PlaytestBuildInfoFile`・`IPlaytestSteamTicketProvider`
- Produces:
  - `public enum PlaytestGateStatus { DeveloperMode, Allowed, NotAllowed, Unreachable, TicketFailed }`
  - `public readonly struct PlaytestGateResult { public readonly PlaytestGateStatus Status; public readonly string Detail; public PlaytestGateResult(PlaytestGateStatus status, string detail); public bool IsBlocked { get; } public LocalizationKey ReasonKey { get; } }`
  - `public static class PlaytestGateDecision { public static PlaytestGateResult Decide(bool hasBuildInfo, bool isSteamRunning, PlaytestSessionOutcome outcome, string detail); public static PlaytestGateResult DeveloperMode { get; } }`
  - `public static class PlaytestLaunchGate { public static PlaytestGateResult Current { get; } public static UniTask<PlaytestGateResult> EvaluateAsync(PlaytestSession session, IPlaytestSteamTicketProvider ticketProvider, DateTime utcNow, CancellationToken token); public static void SetCurrent(PlaytestGateResult result); public static bool RejectStart(string callerName); }`
  - ローカライズキー `ui.playtest.checking`／`ui.playtest.notAllowed`／`ui.playtest.unreachable`／`ui.playtest.ticketFailed`

- [ ] **Step 1: ローカライズ行を追加して生成する**

`Localization/localization.csv` 末尾に追加（列: `key,Source,english,japanese,german`）:
```
ui.playtest.checking,Checking playtest access…,Checking playtest access…,プレイテストの参加資格を確認しています…,Playtest-Zugang wird geprüft …
ui.playtest.notAllowed,This Steam account is not on the playtest list. Ask the developer to add you.,This Steam account is not on the playtest list. Ask the developer to add you.,このSteamアカウントはプレイテストの参加者一覧にありません。開発者に追加を依頼してください。,Dieses Steam-Konto steht nicht auf der Playtest-Liste. Bitte den Entwickler um Aufnahme.
ui.playtest.unreachable,Could not reach the playtest server. An internet connection is required to play. ({p0}),Could not reach the playtest server. An internet connection is required to play. ({p0}),プレイテストサーバーに接続できませんでした。プレイにはインターネット接続が必要です。（{p0}）,Der Playtest-Server ist nicht erreichbar. Zum Spielen wird eine Internetverbindung benötigt. ({p0})
ui.playtest.ticketFailed,Steam authentication failed. Restart Steam and launch the game from your Steam library. ({p0}),Steam authentication failed. Restart Steam and launch the game from your Steam library. ({p0}),Steam認証に失敗しました。Steamを再起動し、ライブラリからゲームを起動してください。（{p0}）,Steam-Authentifizierung fehlgeschlagen. Starte Steam neu und starte das Spiel aus deiner Bibliothek. ({p0})
```
`_CompileRequester.cs` の `dummyText` を `uuidgen | tr a-z A-Z` の値へ差し替える。

Run: `cd moorestech_web/webui && pnpm gen:i18n && cd ../.. && uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount 0`。`LocalizationKeys.Ui.Playtest.NotAllowed` が C# から参照できるようになる

- [ ] **Step 2: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestGateDecisionTest.cs`:
```csharp
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Gate;
using Mooresmaster.Localization.Generated;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestGateDecisionTest
    {
        [Test]
        public void buildInfoが無ければ開発者モードで素通しする()
        {
            var result = PlaytestGateDecision.Decide(false, true, PlaytestSessionOutcome.NotAllowed, "");
            Assert.AreEqual(PlaytestGateStatus.DeveloperMode, result.Status);
            Assert.IsFalse(result.IsBlocked);
        }

        [Test]
        public void Steamが動いていなければ自作ビルド直起動として素通しする()
        {
            var result = PlaytestGateDecision.Decide(true, false, PlaytestSessionOutcome.TicketUnavailable, "");
            Assert.AreEqual(PlaytestGateStatus.DeveloperMode, result.Status);
            Assert.IsFalse(result.IsBlocked);
        }

        [Test]
        public void 配布ビルドで許可されれば通す()
        {
            var result = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.Allowed, "");
            Assert.AreEqual(PlaytestGateStatus.Allowed, result.Status);
            Assert.IsFalse(result.IsBlocked);
        }

        [Test]
        public void 不許可は理由付きで止める()
        {
            var result = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.NotAllowed, "");
            Assert.AreEqual(PlaytestGateStatus.NotAllowed, result.Status);
            Assert.IsTrue(result.IsBlocked);
            Assert.AreEqual(LocalizationKeys.Ui.Playtest.NotAllowed.Key, result.ReasonKey.Key);
        }

        [Test]
        public void 到達不能は止める()
        {
            var result = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.Unreachable, "dns");
            Assert.AreEqual(PlaytestGateStatus.Unreachable, result.Status);
            Assert.IsTrue(result.IsBlocked);
            Assert.AreEqual(LocalizationKeys.Ui.Playtest.Unreachable.Key, result.ReasonKey.Key);
        }

        [Test]
        public void 配布ビルドでSteamが動いているのにチケットが取れないのは止める()
        {
            var unavailable = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.TicketUnavailable, "");
            var rejected = PlaytestGateDecision.Decide(true, true, PlaytestSessionOutcome.TicketRejected, "");
            Assert.AreEqual(PlaytestGateStatus.TicketFailed, unavailable.Status);
            Assert.AreEqual(PlaytestGateStatus.TicketFailed, rejected.Status);
            Assert.IsTrue(unavailable.IsBlocked);
            Assert.IsTrue(rejected.IsBlocked);
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestLaunchGateBlockTest.cs`:
```csharp
using Client.PlaytestReceiver.Gate;
using Client.Starter;
using NUnit.Framework;
using UnityEngine.SceneManagement;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestLaunchGateBlockTest
    {
        [TearDown]
        public void RestoreGate()
        {
            PlaytestLaunchGate.SetCurrent(PlaytestGateDecision.DeveloperMode);
        }

        [Test]
        public void 止められている間はローカル開始でシーンを読み込まない()
        {
            PlaytestLaunchGate.SetCurrent(new PlaytestGateResult(PlaytestGateStatus.NotAllowed, ""));
            var before = SceneManager.GetActiveScene().name;

            LocalGameLauncher.StartLocalGame();

            Assert.AreEqual(before, SceneManager.GetActiveScene().name);
        }

        [Test]
        public void 開発者モードでは関所が拒否しない()
        {
            PlaytestLaunchGate.SetCurrent(PlaytestGateDecision.DeveloperMode);
            Assert.IsFalse(PlaytestLaunchGate.RejectStart("test"));
        }

        [Test]
        public void 止められているときRejectStartはtrueを返す()
        {
            PlaytestLaunchGate.SetCurrent(new PlaytestGateResult(PlaytestGateStatus.Unreachable, "dns"));
            Assert.IsTrue(PlaytestLaunchGate.RejectStart("test"));
        }
    }
}
```

- [ ] **Step 3: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `Client.PlaytestReceiver.Gate` が無くコンパイルエラー

- [ ] **Step 4: ゲートを実装する**

`Client.PlaytestReceiver/Gate/PlaytestGateResult.cs`:
```csharp
using Mooresmaster.Localization.Generated;

namespace Client.PlaytestReceiver.Gate
{
    public enum PlaytestGateStatus
    {
        DeveloperMode,
        Allowed,
        NotAllowed,
        Unreachable,
        TicketFailed,
    }

    // 照合の結末。止めるかどうかと、タイトルに出す理由の文言キーをここだけで決める
    // The verdict of the check; whether to stop and which reason string the title shows is decided only here
    public readonly struct PlaytestGateResult
    {
        public readonly PlaytestGateStatus Status;
        public readonly string Detail;

        public PlaytestGateResult(PlaytestGateStatus status, string detail)
        {
            Status = status;
            Detail = detail;
        }

        public bool IsBlocked => Status != PlaytestGateStatus.DeveloperMode && Status != PlaytestGateStatus.Allowed;

        public LocalizationKey ReasonKey
        {
            get
            {
                if (Status == PlaytestGateStatus.NotAllowed) return LocalizationKeys.Ui.Playtest.NotAllowed;
                if (Status == PlaytestGateStatus.TicketFailed) return LocalizationKeys.Ui.Playtest.TicketFailed;
                return LocalizationKeys.Ui.Playtest.Unreachable;
            }
        }
    }
}
```

`Client.PlaytestReceiver/Gate/PlaytestGateDecision.cs`:
```csharp
namespace Client.PlaytestReceiver.Gate
{
    // 判定は純関数に閉じる。HTTPもSteamも触らないのでEditModeテストで全分岐を固定できる
    // The decision is a pure function; touching neither HTTP nor Steam lets EditMode tests pin every branch
    public static class PlaytestGateDecision
    {
        public static PlaytestGateResult DeveloperMode => new(PlaytestGateStatus.DeveloperMode, "");

        public static PlaytestGateResult Decide(bool hasBuildInfo, bool isSteamRunning, PlaytestSessionOutcome outcome, string detail)
        {
            // 配布ビルドの印が無い、またはSteamが動いていない = 開発者の自作ビルド。照合せずrsync経路に任せる
            // No distribution marker or no Steam means a developer's own build; skip the check and leave it to the rsync path
            if (!hasBuildInfo || !isSteamRunning) return DeveloperMode;

            if (outcome == PlaytestSessionOutcome.Allowed) return new PlaytestGateResult(PlaytestGateStatus.Allowed, "");
            if (outcome == PlaytestSessionOutcome.NotAllowed) return new PlaytestGateResult(PlaytestGateStatus.NotAllowed, detail);

            // 配布ビルドでSteamが動いているのにチケットが通らないのは異常。fail-closedで止める
            // A distribution build with Steam running but no usable ticket is abnormal; fail closed and stop
            if (outcome == PlaytestSessionOutcome.TicketUnavailable || outcome == PlaytestSessionOutcome.TicketRejected)
            {
                return new PlaytestGateResult(PlaytestGateStatus.TicketFailed, detail);
            }
            return new PlaytestGateResult(PlaytestGateStatus.Unreachable, detail);
        }
    }
}
```

`Client.PlaytestReceiver/Gate/PlaytestLaunchGate.cs`:
```csharp
using System;
using System.Threading;
using Client.PlaytestReceiver.Steam;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver.Gate
{
    // 起動時照合の唯一の関所。開始経路（ローカル開始・サーバー接続）はここへ問い合わせてから進む
    // The single gate for the launch check; every start path asks here before proceeding
    public static class PlaytestLaunchGate
    {
        // シーン跨ぎで持ち回る必要があり、MainMenuシーンにはDIコンテナが無いのでstaticで保持する
        // The verdict must survive a scene load and the MainMenu scene has no DI container, so it is held statically
        public static PlaytestGateResult Current { get; private set; } = PlaytestGateDecision.DeveloperMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlayMode()
        {
            Current = PlaytestGateDecision.DeveloperMode;
        }

        public static void SetCurrent(PlaytestGateResult result)
        {
            Current = result;
        }

        public static async UniTask<PlaytestGateResult> EvaluateAsync(PlaytestSession session, IPlaytestSteamTicketProvider ticketProvider, DateTime utcNow, CancellationToken token)
        {
            var hasBuildInfo = PlaytestBuildInfoFile.Exists();
            var isSteamRunning = ticketProvider.IsSteamRunning();
            if (!hasBuildInfo || !isSteamRunning)
            {
                Debug.Log($"[PlaytestReceiver] developer mode (buildInfo={hasBuildInfo}, steam={isSteamRunning}); skipping the launch check");
                SetCurrent(PlaytestGateDecision.DeveloperMode);
                return Current;
            }

            var authenticated = await session.AuthenticateAsync(utcNow, token);
            var result = PlaytestGateDecision.Decide(true, true, authenticated.Outcome, authenticated.Detail ?? "");
            if (result.IsBlocked)
            {
                Debug.LogError($"[PlaytestReceiver] launch blocked: {result.Status} {result.Detail}");
            }
            SetCurrent(result);
            return result;
        }

        // 開始を拒否したら必ず理由をログへ出す。無音で押せないボタンにしない
        // Every refusal logs its reason; a silently dead button is forbidden
        public static bool RejectStart(string callerName)
        {
            if (!Current.IsBlocked) return false;
            Debug.LogWarning($"[PlaytestReceiver] {callerName} refused: {Current.Status} {Current.Detail}");
            return true;
        }
    }
}
```

- [ ] **Step 5: 開始経路に関所を挿す**

`Client.Starter/LocalGameLauncher.cs` の `StartLocalGame()` 冒頭へ:
```csharp
        public static void StartLocalGame()
        {
            // プレイテスト配布版で照合に落ちていたら開始しない（ADR 0061・オンライン必須）
            // A distribution build that failed the playtest check never starts (ADR 0061, online required)
            if (PlaytestLaunchGate.RejectStart(nameof(StartLocalGame))) return;
```
（先頭に `using Client.PlaytestReceiver.Gate;` を足し、`Client.Starter.asmdef` の references に `"Client.PlaytestReceiver"` を追加する）

`Client.MainMenu/ConnectServer.cs` の `Connect()` 冒頭へ:
```csharp
        private void Connect()
        {
            // 同じ関所を通す。判定はPlaytestLaunchGate 1箇所にしか無い
            // The same gate is consulted here; the decision lives only in PlaytestLaunchGate
            if (PlaytestLaunchGate.RejectStart(nameof(Connect)))
            {
                serverConnectPopup.SetText(Localize.Get(PlaytestLaunchGate.Current.ReasonKey));
                return;
            }
```
（`using Client.PlaytestReceiver.Gate;` を足す。`Client.MainMenu` は Assembly-CSharp なので asmdef の追記は不要）

- [ ] **Step 6: タイトルの表示コンポーネントを書く**

`Client.MainMenu/Playtest/PlaytestLaunchGateView.cs`:
```csharp
using System;
using Client.Localization;
using Client.MainMenu.PopUp;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Gate;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.MainMenu.Playtest
{
    // タイトルでの表示専用オブザーバ。開始の可否は決めず、照合を回して理由を出すだけ
    // A display-only observer on the title screen; it never decides whether the game may start, it only shows the reason
    public class PlaytestLaunchGateView : MonoBehaviour
    {
        [SerializeField] private ServerConnectPopup messagePopup;

        private void Start()
        {
            EvaluateAsync().Forget();
        }

        private async UniTaskVoid EvaluateAsync()
        {
            var ticketProvider = new PlaytestSteamTicketProvider();
            if (!PlaytestBuildInfoFile.Exists() || !ticketProvider.IsSteamRunning())
            {
                PlaytestLaunchGate.SetCurrent(PlaytestGateDecision.DeveloperMode);
                return;
            }

            messagePopup.SetText(Localize.Get(Mooresmaster.Localization.Generated.LocalizationKeys.Ui.Playtest.Checking));

            var session = new PlaytestSession(new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl), ticketProvider);
            var result = await PlaytestLaunchGate.EvaluateAsync(session, ticketProvider, DateTime.UtcNow, destroyCancellationToken);

            if (result.IsBlocked)
            {
                messagePopup.SetText(Localize.GetFormatted(result.ReasonKey, new[] { result.Detail ?? "" }));
                return;
            }

            messagePopup.gameObject.SetActive(false);
        }
    }
}
```
（照合を通ったあとのアップロード起動は Task 8 でこのメソッドの末尾へ足す。本タスク単体でコンパイルとテストが通る状態にしておく）

- [ ] **Step 7: MainMenu シーンへコンポーネントを置く（`uloop execute-dynamic-code` 経由・手編集禁止）**

Run: `uloop execute-dynamic-code --project-path ./moorestech_client` に次のコードを渡す:
```csharp
var scenePath = "Assets/Scenes/Game/MainMenu.unity";
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);

// 既に置かれていれば二重に作らない
// Never create a second one if it already exists
var existing = UnityEngine.Object.FindObjectOfType<Client.MainMenu.Playtest.PlaytestLaunchGateView>();
if (existing != null) return $"already present on {existing.gameObject.name}";

var popup = UnityEngine.Object.FindObjectOfType<Client.MainMenu.PopUp.ServerConnectPopup>(true);
if (popup == null) return "ServerConnectPopup がシーンに無い";

var host = new GameObject("PlaytestLaunchGate");
var view = host.AddComponent<Client.MainMenu.Playtest.PlaytestLaunchGateView>();
var serialized = new UnityEditor.SerializedObject(view);
serialized.FindProperty("messagePopup").objectReferenceValue = popup;
serialized.ApplyModifiedPropertiesWithoutUndo();

UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
return $"added PlaytestLaunchGateView, popup={popup.name}";
```
Expected: `added PlaytestLaunchGateView, popup=...`

Run: `uloop get-hierarchy --project-path ./moorestech_client`
Expected: `PlaytestLaunchGate` が MainMenu シーンのルートに居る

- [ ] **Step 8: Steam の初期化が実際に起きるかを確認する（判断記録の材料）**

Run: `uloop execute-dynamic-code --project-path ./moorestech_client` に次のコードを渡す:
```csharp
var scenePath = "Assets/Scenes/Game/MainMenu.unity";
UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath, UnityEditor.SceneManagement.OpenSceneMode.Single);
var manager = UnityEngine.Object.FindObjectOfType<SteamManager>(true);
var appIdFile = System.IO.File.Exists(System.IO.Path.Combine(Application.dataPath, "..", "steam_appid.txt"));
return $"SteamManagerInMainMenu={manager != null}, steam_appid.txt={appIdFile}";
```
Expected: 2つの真偽値が返る。**どちらかが false なら、配布ビルドで Steam が初期化されず照合が常に開発者モードへ落ちる。**その事実と値を「判断記録」へ書き、`SteamManager` の配置と AppID 焼き込み（`SteamAPI.RestartAppIfNecessary(AppId_t.Invalid)` の是正）を plan E の課題として `bd create` で積む。本planではここまで（コード側の分岐は既に fail-closed で正しい）。

- [ ] **Step 9: テストを通す**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount 0`

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.PlaytestReceiver\..*"`
Expected: 全 PASS

- [ ] **Step 10: Editor でタイトルが従来どおり動くことを確認する**

Run: `uloop control-play-mode --project-path ./moorestech_client --action Play`（MainMenu シーンを開いた状態で）→ `uloop get-logs --project-path ./moorestech_client --log-type Log`
Expected: `[PlaytestReceiver] developer mode (buildInfo=False, steam=...)` が出て、ポップアップは出ず、「ローカルでプレイ」でゲームが始まる。確認後 `uloop control-play-mode --action Stop`

- [ ] **Step 11: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.PlaytestReceiver moorestech_client/Assets/Scripts/Client.MainMenu moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs moorestech_client/Assets/Scenes/Game/MainMenu.unity Localization/localization.csv moorestech_web/webui/src/shared/i18n/generated
git commit -m "feat(playtest): 起動時照合ゲートとタイトルでの停止表示"
```

---

### Task 8: outbox のアップロード（`PlaytestUploader`）と送信直後の起動

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestOutboxBox.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestOutboxScanner.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestUploadAttemptLog.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestUploader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestUploadRunner.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs`（`ProgressRecordDirectory`・`ProgressRecordOutboxDirectory` を追加）
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/Playtest/PlaytestLaunchGateView.cs`（照合通過後に `RequestUpload` を1回）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/BugReportSubmitActionHandler.cs`（plan B の成果物。書き出し成功後に `PlaytestUploadRunner.Instance.RequestUpload(...)`）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef`（references に `Client.PlaytestReceiver`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestOutboxScannerTest.cs`・`PlaytestUploaderTest.cs`

**Interfaces:**
- Consumes: Task 6 の `IPlaytestReceiverApi`・`PlaytestSession`・`PlaytestReceiverConfig.MaxFileBytes`、plan B の `BugReportOutbox.ReadyMarkerFileName`（`"READY"`）
- Produces:
  - `public sealed class PlaytestOutboxBox { public string Directory; public string BundleId; public string Kind; }`（`Kind` は `"report"`／`"progress"`）
  - `public static class PlaytestOutboxScanner { public const string ReadyMarker = "READY"; public const string UploadedMarker = "UPLOADED"; public const string FailedMarker = "UPLOAD_FAILED"; public const string AttemptsMarker = "UPLOAD_ATTEMPTS"; public static IReadOnlyList<PlaytestOutboxBox> ScanPending(string reportOutbox, string progressOutbox); public static IReadOnlyList<string> ListPayloadFiles(string boxDirectory); public static string ToRelativePath(string boxDirectory, string filePath); public static bool IsSendablePath(string relativePath); }`
  - `public static class PlaytestUploadAttemptLog { public const int MaxAttempts = 5; public static int Increment(string boxDirectory, string reason); public static void MarkFailed(string boxDirectory, string reason); public static void MarkUploaded(string boxDirectory); }`
  - `public sealed class PlaytestUploader { public PlaytestUploader(IPlaytestReceiverApi api, PlaytestSession session, string reportOutbox, string progressOutbox); public UniTask<int> UploadPendingAsync(DateTime utcNow, CancellationToken token); }`（戻り値は送れた箱の数）
  - `public sealed class PlaytestUploadRunner { public static PlaytestUploadRunner Instance { get; } public PlaytestUploadRunner(IPlaytestReceiverApi api, string reportOutbox, string progressOutbox); public void RequestUpload(PlaytestSession session); }`（走行中の再要求は無視して1本に保つ。`Instance` は本番の依存で組んだもので、テストは自前の引数で組む）
  - `GameSystemPaths.ProgressRecordDirectory`（`<GameSystemDirectory>/ProgressRecords`）・`ProgressRecordOutboxDirectory`（`.../outbox`）

- [ ] **Step 1: 失敗するテストを書く**

`moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestOutboxScannerTest.cs`:
```csharp
using System.IO;
using System.Linq;
using Client.PlaytestReceiver.Upload;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestOutboxScannerTest
    {
        private string _root;

        [SetUp]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "playtest-scanner-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void DeleteRoot()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void READYがある箱だけを古い順にkind付きで拾う()
        {
            var reports = MakeOutbox("BugReports");
            var progress = MakeOutbox("ProgressRecords");
            MakeBox(reports, "20260913_120000_bbbb", withReady: true);
            MakeBox(reports, "20260913_110000_aaaa", withReady: true);
            MakeBox(reports, "20260913_130000_cccc", withReady: false);
            MakeBox(progress, "20260913_125959_dddd", withReady: true);

            var boxes = PlaytestOutboxScanner.ScanPending(reports, progress);

            Assert.AreEqual(3, boxes.Count);
            Assert.AreEqual(new[] { "20260913_110000_aaaa", "20260913_120000_bbbb", "20260913_125959_dddd" }, boxes.Select(box => box.BundleId).ToArray());
            Assert.AreEqual("report", boxes[0].Kind);
            Assert.AreEqual("progress", boxes[2].Kind);
        }

        [Test]
        public void UPLOADED済みとUPLOAD_FAILED済みは拾わない()
        {
            var reports = MakeOutbox("BugReports");
            var progress = MakeOutbox("ProgressRecords");
            var uploaded = MakeBox(reports, "20260913_120000_bbbb", withReady: true);
            File.WriteAllText(Path.Combine(uploaded, PlaytestOutboxScanner.UploadedMarker), "");
            var failed = MakeBox(reports, "20260913_121000_cccc", withReady: true);
            File.WriteAllText(Path.Combine(failed, PlaytestOutboxScanner.FailedMarker), "");

            Assert.IsEmpty(PlaytestOutboxScanner.ScanPending(reports, progress));
        }

        [Test]
        public void マーカーは送らず中身だけを相対パスで拾う()
        {
            var reports = MakeOutbox("BugReports");
            var box = MakeBox(reports, "20260913_120000_bbbb", withReady: true);
            Directory.CreateDirectory(Path.Combine(box, "logs"));
            File.WriteAllText(Path.Combine(box, "logs", "unity.log"), "x");
            File.WriteAllText(Path.Combine(box, "manifest.json"), "{}");

            var files = PlaytestOutboxScanner.ListPayloadFiles(box)
                .Select(file => PlaytestOutboxScanner.ToRelativePath(box, file))
                .OrderBy(path => path)
                .ToArray();

            Assert.AreEqual(new[] { "logs/unity.log", "manifest.json" }, files);
        }

        [Test]
        public void 受け口が受け取れない文字を含む相対パスは送らない()
        {
            Assert.IsTrue(PlaytestOutboxScanner.IsSendablePath("logs/unity.log"));
            Assert.IsTrue(PlaytestOutboxScanner.IsSendablePath("frames/frame_0001.jpg"));
            Assert.IsFalse(PlaytestOutboxScanner.IsSendablePath("logs/ユニティ.log"));
            Assert.IsFalse(PlaytestOutboxScanner.IsSendablePath("logs/a b.log"));
            Assert.IsFalse(PlaytestOutboxScanner.IsSendablePath("../escape.log"));
        }

        private string MakeOutbox(string name)
        {
            var path = Path.Combine(_root, name, "outbox");
            Directory.CreateDirectory(path);
            return path;
        }

        private static string MakeBox(string outbox, string bundleId, bool withReady)
        {
            var box = Path.Combine(outbox, bundleId);
            Directory.CreateDirectory(box);
            if (withReady) File.WriteAllText(Path.Combine(box, PlaytestOutboxScanner.ReadyMarker), "");
            return box;
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestUploaderTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver;
using Client.PlaytestReceiver.Http;
using Client.PlaytestReceiver.Steam;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver
{
    public class PlaytestUploaderTest
    {
        private string _root;
        private string _reports;
        private string _progress;

        [SetUp]
        public void CreateRoot()
        {
            _root = Path.Combine(Path.GetTempPath(), "playtest-upload-" + Path.GetRandomFileName());
            _reports = Path.Combine(_root, "BugReports", "outbox");
            _progress = Path.Combine(_root, "ProgressRecords", "outbox");
            Directory.CreateDirectory(_reports);
            Directory.CreateDirectory(_progress);
        }

        [TearDown]
        public void DeleteRoot()
        {
            Directory.Delete(_root, true);
        }

        [Test]
        public void READYの箱が送られUPLOADEDが付く()
        {
            var box = MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{\"kind\":\"bug\"}"));
            var api = new FakeApi();
            var sent = Upload(api);

            Assert.AreEqual(1, sent);
            Assert.AreEqual(new[] { "manifest.json" }, api.PutPaths.ToArray());
            Assert.AreEqual(1, api.CompleteCount);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void UPLOADED済みは再送されない()
        {
            MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeApi();
            Upload(api);
            Upload(api);

            Assert.AreEqual(1, api.CompleteCount);
        }

        [Test]
        public void 失敗すると試行回数が増え箱は残る()
        {
            var box = MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeApi { PutResult = new PlaytestApiResult { TransportError = "offline" } };

            Assert.AreEqual(0, Upload(api));
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
            Assert.AreEqual("1", File.ReadAllText(Path.Combine(box, PlaytestOutboxScanner.AttemptsMarker)).Split('\n')[0]);
        }

        [Test]
        public void 5回失敗した箱はUPLOAD_FAILEDになり後続の箱を塞がない()
        {
            var stuck = MakeBox(_reports, "20260913_110000_aaaa", ("manifest.json", "{}"));
            var api = new FakeApi { PutResult = new PlaytestApiResult { TransportError = "offline" } };
            for (var attempt = 0; attempt < PlaytestUploadAttemptLog.MaxAttempts; attempt++) Upload(api);

            Assert.IsTrue(File.Exists(Path.Combine(stuck, PlaytestOutboxScanner.FailedMarker)));

            var later = MakeBox(_reports, "20260913_120000_bbbb", ("manifest.json", "{}"));
            api.PutResult = new PlaytestApiResult { StatusCode = 200, Body = "{}" };

            Assert.AreEqual(1, Upload(api));
            Assert.IsTrue(File.Exists(Path.Combine(later, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 巨大ファイルは送らずskippedに載せて箱自体は完了する()
        {
            var box = MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var huge = Path.Combine(box, "video.mp4");
            using (var stream = new FileStream(huge, FileMode.Create))
            {
                stream.SetLength(PlaytestReceiverConfig.MaxFileBytes + 1);
            }
            var api = new FakeApi();

            Assert.AreEqual(1, Upload(api));
            Assert.AreEqual(new[] { "manifest.json" }, api.PutPaths.ToArray());
            StringAssert.Contains("video.mp4", api.LastSummary);
            StringAssert.Contains("skipped", api.LastSummary);
            Assert.IsTrue(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void トークンが取れなければ何も送らず箱を残す()
        {
            var box = MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{}"));
            var api = new FakeApi { SessionResult = new PlaytestApiResult { TransportError = "offline" } };

            Assert.AreEqual(0, Upload(api));
            Assert.IsEmpty(api.PutPaths);
            Assert.IsFalse(File.Exists(Path.Combine(box, PlaytestOutboxScanner.UploadedMarker)));
        }

        [Test]
        public void 走行中に再要求しても走行は1本に保たれる()
        {
            MakeBox(_reports, "20260913_120000_aaaa", ("manifest.json", "{}"));
            MakeBox(_reports, "20260913_130000_bbbb", ("manifest.json", "{}"));
            var gate = new UniTaskCompletionSource<PlaytestApiResult>();
            var api = new FakeApi { PendingPut = gate };
            var runner = new PlaytestUploadRunner(api, _reports, _progress);
            var session = new PlaytestSession(api, new AlwaysTicketProvider());

            runner.RequestUpload(session);
            runner.RequestUpload(session);

            // 1本目が最初のPUTで止まっている間は、2本目が走っていないので PUT は1回しか起きない
            // While the first run is parked on its first PUT, no second run exists, so exactly one PUT happened
            Assert.AreEqual(1, api.PutAttemptCount);
            gate.TrySetResult(new PlaytestApiResult { TransportError = "offline" });
        }

        private int Upload(FakeApi api)
        {
            var session = new PlaytestSession(api, new AlwaysTicketProvider());
            var uploader = new PlaytestUploader(api, session, _reports, _progress);
            return uploader.UploadPendingAsync(DateTime.UtcNow, CancellationToken.None).GetAwaiter().GetResult();
        }

        private static string MakeBox(string outbox, string bundleId, params (string Name, string Content)[] files)
        {
            var box = Path.Combine(outbox, bundleId);
            Directory.CreateDirectory(box);
            foreach (var file in files) File.WriteAllText(Path.Combine(box, file.Name), file.Content);
            File.WriteAllText(Path.Combine(box, PlaytestOutboxScanner.ReadyMarker), "");
            return box;
        }

        private sealed class AlwaysTicketProvider : IPlaytestSteamTicketProvider
        {
            public bool IsSteamRunning() { return true; }
            public UniTask<string> RequestWebApiTicketHexAsync(CancellationToken token) { return UniTask.FromResult("aabb"); }
        }

        private sealed class FakeApi : IPlaytestReceiverApi
        {
            public readonly List<string> PutPaths = new();
            public int CompleteCount;
            public int PutAttemptCount;
            public string LastSummary = "";
            public UniTaskCompletionSource<PlaytestApiResult> PendingPut;
            public PlaytestApiResult PutResult = new() { StatusCode = 200, Body = "{}" };
            public PlaytestApiResult SessionResult = new() { StatusCode = 200, Body = "{\"steamId\":\"7656\",\"allowed\":true,\"token\":\"tok\"}" };

            public UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token)
            {
                return UniTask.FromResult(SessionResult);
            }

            public UniTask<PlaytestApiResult> PutFileAsync(string bearerToken, string kind, string bundleId, string relativePath, string absoluteFilePath, CancellationToken token)
            {
                PutAttemptCount++;
                if (PendingPut != null) return PendingPut.Task;
                if (PutResult.IsSuccess) PutPaths.Add(relativePath);
                return UniTask.FromResult(PutResult);
            }

            public UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, string kind, string bundleId, string summaryJson, CancellationToken token)
            {
                CompleteCount++;
                LastSummary = summaryJson;
                return UniTask.FromResult(new PlaytestApiResult { StatusCode = 200, Body = "{}" });
            }
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `Client.PlaytestReceiver.Upload` が無くコンパイルエラー

- [ ] **Step 3: パスを足す**

`moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs` の `WorldCacheDirectory` の直後へ:
```csharp
        // 進行記録のoutbox。plan G が書き、plan D のアップローダが送る
        // The outbox for progress records; plan G writes them and plan D's uploader ships them
        public static string ProgressRecordDirectory => DirectoryCreator(GameSystemDirectory, "ProgressRecords");
        public static string ProgressRecordOutboxDirectory => DirectoryCreator(ProgressRecordDirectory, "outbox");
```

- [ ] **Step 4: 走査とマーカーを実装する**

`Client.PlaytestReceiver/Upload/PlaytestOutboxBox.cs`:
```csharp
namespace Client.PlaytestReceiver.Upload
{
    // 送る単位。kindは受け口のパス（report|progress）と同じ語をそのまま持つ
    // One shippable unit; Kind carries the receiver's own word (report|progress) verbatim
    public sealed class PlaytestOutboxBox
    {
        public string Directory;
        public string BundleId;
        public string Kind;
    }
}
```

`Client.PlaytestReceiver/Upload/PlaytestOutboxScanner.cs`:
```csharp
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace Client.PlaytestReceiver.Upload
{
    public static class PlaytestOutboxScanner
    {
        public const string ReadyMarker = "READY";
        public const string UploadedMarker = "UPLOADED";
        public const string FailedMarker = "UPLOAD_FAILED";
        public const string AttemptsMarker = "UPLOAD_ATTEMPTS";
        public const string ShippedMarker = "SHIPPED";

        // 受け口は [A-Za-z0-9._-] のセグメントしか受け取らない（Worker の isSafeSegment と同じ規則）
        // The receiver accepts only [A-Za-z0-9._-] segments; this mirrors the Worker's isSafeSegment
        private static readonly Regex SegmentPattern = new("^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

        private static readonly string[] Markers = { ReadyMarker, UploadedMarker, FailedMarker, AttemptsMarker, ShippedMarker };

        public static IReadOnlyList<PlaytestOutboxBox> ScanPending(string reportOutbox, string progressOutbox)
        {
            var boxes = new List<PlaytestOutboxBox>();
            boxes.AddRange(ScanOne(reportOutbox, "report"));
            boxes.AddRange(ScanOne(progressOutbox, "progress"));

            // 箱のIDは yyyyMMdd_HHmmss_<hex> なので辞書順が時刻順になる。古い順に送る
            // Bundle ids are yyyyMMdd_HHmmss_<hex>, so lexicographic order is chronological; ship oldest first
            return boxes.OrderBy(box => box.BundleId, System.StringComparer.Ordinal).ToList();
        }

        public static IReadOnlyList<string> ListPayloadFiles(string boxDirectory)
        {
            return Directory.GetFiles(boxDirectory, "*", SearchOption.AllDirectories)
                .Where(path => !Markers.Contains(Path.GetFileName(path)))
                .ToList();
        }

        public static string ToRelativePath(string boxDirectory, string filePath)
        {
            var relative = filePath.Substring(boxDirectory.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        public static bool IsSendablePath(string relativePath)
        {
            var segments = relativePath.Split('/');
            return segments.Length != 0 && segments.All(segment => SegmentPattern.IsMatch(segment));
        }

        private static IEnumerable<PlaytestOutboxBox> ScanOne(string outbox, string kind)
        {
            if (!Directory.Exists(outbox)) yield break;

            foreach (var directory in Directory.GetDirectories(outbox))
            {
                if (!File.Exists(Path.Combine(directory, ReadyMarker))) continue;
                if (File.Exists(Path.Combine(directory, UploadedMarker))) continue;
                if (File.Exists(Path.Combine(directory, FailedMarker))) continue;
                yield return new PlaytestOutboxBox { Directory = directory, BundleId = Path.GetFileName(directory), Kind = kind };
            }
        }
    }
}
```

`Client.PlaytestReceiver/Upload/PlaytestUploadAttemptLog.cs`:
```csharp
using System.IO;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload
{
    // 失敗した箱の試行回数。上限に達したら見送り印を打ち、後続の箱が永久に塞がれるのを防ぐ
    // Attempt counter per failed box; hitting the cap parks the box so it can never block later ones forever
    public static class PlaytestUploadAttemptLog
    {
        public const int MaxAttempts = 5;

        public static int Increment(string boxDirectory, string reason)
        {
            var path = Path.Combine(boxDirectory, PlaytestOutboxScanner.AttemptsMarker);
            var previous = Read(path);
            var attempts = previous + 1;
            File.WriteAllText(path, $"{attempts}\n{reason}");

            if (attempts >= MaxAttempts)
            {
                MarkFailed(boxDirectory, reason);
                return attempts;
            }

            Debug.LogWarning($"[PlaytestReceiver] upload attempt {attempts}/{MaxAttempts} failed for {Path.GetFileName(boxDirectory)}: {reason}");
            return attempts;
        }

        public static void MarkFailed(string boxDirectory, string reason)
        {
            File.WriteAllText(Path.Combine(boxDirectory, PlaytestOutboxScanner.FailedMarker), reason);
            Debug.LogError($"[PlaytestReceiver] giving up on {Path.GetFileName(boxDirectory)} after {MaxAttempts} attempts: {reason}. 手動で送る場合は rsync 経路を使うこと");
        }

        public static void MarkUploaded(string boxDirectory)
        {
            File.WriteAllText(Path.Combine(boxDirectory, PlaytestOutboxScanner.UploadedMarker), System.DateTime.UtcNow.ToString("o"));
        }

        private static int Read(string path)
        {
            if (!File.Exists(path)) return 0;
            var head = File.ReadAllText(path).Split('\n')[0];
            return int.TryParse(head, out var attempts) ? attempts : 0;
        }
    }
}
```

- [ ] **Step 5: アップローダを実装する**

`Client.PlaytestReceiver/Upload/PlaytestUploader.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload
{
    public sealed class PlaytestUploader
    {
        private readonly IPlaytestReceiverApi _api;
        private readonly PlaytestSession _session;
        private readonly string _reportOutbox;
        private readonly string _progressOutbox;

        public PlaytestUploader(IPlaytestReceiverApi api, PlaytestSession session, string reportOutbox, string progressOutbox)
        {
            _api = api;
            _session = session;
            _reportOutbox = reportOutbox;
            _progressOutbox = progressOutbox;
        }

        public async UniTask<int> UploadPendingAsync(DateTime utcNow, CancellationToken token)
        {
            var boxes = PlaytestOutboxScanner.ScanPending(_reportOutbox, _progressOutbox);
            if (boxes.Count == 0) return 0;

            var bearer = await _session.GetValidTokenAsync(utcNow, token);
            if (bearer == null)
            {
                Debug.LogWarning($"[PlaytestReceiver] deferring {boxes.Count} box(es): no valid session token");
                return 0;
            }

            var sent = 0;
            foreach (var box in boxes)
            {
                if (await UploadOneAsync(bearer, box, token)) sent++;
            }
            return sent;
        }

        private async UniTask<bool> UploadOneAsync(string bearer, PlaytestOutboxBox box, CancellationToken token)
        {
            var skipped = new List<object>();
            var files = PlaytestOutboxScanner.ListPayloadFiles(box.Directory);
            var putCount = 0;

            foreach (var file in files)
            {
                var relativePath = PlaytestOutboxScanner.ToRelativePath(box.Directory, file);
                if (!PlaytestOutboxScanner.IsSendablePath(relativePath))
                {
                    skipped.Add(new { path = relativePath, reason = "unsupported-characters" });
                    Debug.LogWarning($"[PlaytestReceiver] skipping {relativePath}: the receiver only accepts [A-Za-z0-9._-] path segments");
                    continue;
                }

                var length = new FileInfo(file).Length;
                if (length > PlaytestReceiverConfig.MaxFileBytes)
                {
                    skipped.Add(new { path = relativePath, reason = "too-large", bytes = length });
                    Debug.LogWarning($"[PlaytestReceiver] skipping {relativePath}: {length} bytes exceeds the 100MiB limit");
                    continue;
                }

                var result = await _api.PutFileAsync(bearer, box.Kind, box.BundleId, relativePath, file, token);
                if (!result.IsSuccess)
                {
                    PlaytestUploadAttemptLog.Increment(box.Directory, DescribeFailure(relativePath, result));
                    return false;
                }
                putCount++;
            }

            var summary = JsonConvert.SerializeObject(new
            {
                kind = box.Kind,
                id = box.BundleId,
                fileCount = putCount,
                skipped,
                manifest = ReadManifestSummary(box.Directory),
            });

            var completed = await _api.PostCompleteAsync(bearer, box.Kind, box.BundleId, summary, token);
            if (!completed.IsSuccess)
            {
                PlaytestUploadAttemptLog.Increment(box.Directory, DescribeFailure("complete", completed));
                return false;
            }

            PlaytestUploadAttemptLog.MarkUploaded(box.Directory);
            Debug.Log($"[PlaytestReceiver] uploaded {box.Kind}/{box.BundleId} ({putCount} files, {skipped.Count} skipped)");
            return true;
        }

        // manifest.json はplan Bが書く。取り込み側の一覧表示用に本文をそのまま要約へ載せる
        // plan B writes manifest.json; its raw text rides along in the summary for the ingest side's listing
        private static string ReadManifestSummary(string boxDirectory)
        {
            var path = Path.Combine(boxDirectory, "manifest.json");
            if (!File.Exists(path)) return null;
            return File.ReadAllText(path);
        }

        private static string DescribeFailure(string what, PlaytestApiResult result)
        {
            return result.IsTransportFailure ? $"{what}: {result.TransportError}" : $"{what}: HTTP {result.StatusCode} {result.Body}";
        }
    }
}
```

`Client.PlaytestReceiver/Upload/PlaytestUploadRunner.cs`:
```csharp
using System;
using Client.PlaytestReceiver.Http;
using Cysharp.Threading.Tasks;
using Game.Paths;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload
{
    // 起動直後と報告送信直後の2箇所から呼ばれる入口。走行中の再要求は無視して1本に保つ
    // The entry point called right after launch and right after a report; re-requests while running are ignored
    public sealed class PlaytestUploadRunner
    {
        public static PlaytestUploadRunner Instance { get; } = new(
            new PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl),
            GameSystemPaths.BugReportOutboxDirectory,
            GameSystemPaths.ProgressRecordOutboxDirectory);

        private readonly IPlaytestReceiverApi _api;
        private readonly string _reportOutbox;
        private readonly string _progressOutbox;

        private bool _running;

        public PlaytestUploadRunner(IPlaytestReceiverApi api, string reportOutbox, string progressOutbox)
        {
            _api = api;
            _reportOutbox = reportOutbox;
            _progressOutbox = progressOutbox;
        }

        public void RequestUpload(PlaytestSession session)
        {
            if (_running)
            {
                Debug.Log("[PlaytestReceiver] an upload run is already in flight; this request rides on it");
                return;
            }
            _running = true;
            RunAsync(session).Forget();
        }

        private async UniTaskVoid RunAsync(PlaytestSession session)
        {
            // 途中で何が起きても走行フラグを必ず戻す。戻し損ねると以後のアップロードが恒久停止する
            // The in-flight flag is always cleared; leaking it would permanently stop every later upload
            try
            {
                var uploader = new PlaytestUploader(_api, session, _reportOutbox, _progressOutbox);
                var sent = await uploader.UploadPendingAsync(DateTime.UtcNow, Application.exitCancellationToken);
                Debug.Log($"[PlaytestReceiver] upload run finished: {sent} box(es) sent");
            }
            finally
            {
                _running = false;
            }
        }
    }
}
```

- [ ] **Step 6: 起動直後の1回を挿す**

`Client.MainMenu/Playtest/PlaytestLaunchGateView.cs`（Task 7 で作ったもの）の `EvaluateAsync` 末尾、`messagePopup.gameObject.SetActive(false);` の直後へ:
```csharp
            // 照合を通った配布版は、前回持ち越した箱をここで送り始める（起動直後の1回）
            // A distribution build that passed the check starts shipping any deferred boxes here (the once-per-launch run)
            PlaytestUploadRunner.Instance.RequestUpload(session);
```
（先頭に `using Client.PlaytestReceiver.Upload;` を足す）

- [ ] **Step 7: 送信直後のフックを挿す（plan B の成果物を変更する）**

`Client.WebUiHost/Game/Actions/BugReportSubmitActionHandler.cs` の書き出し成功後（`PauseMenuStateService.RequestClose()` の直前）へ:
```csharp
            // 配布版なら書けた箱をその場で送りにいく。開発者モードではセッションが無いので何も起きない
            // On a distribution build the freshly written box is shipped at once; developer mode has no session and does nothing
            if (PlaytestLaunchGate.Current.Status == PlaytestGateStatus.Allowed)
            {
                PlaytestUploadRunner.Instance.RequestUpload(_playtestSession);
            }
```
`BugReportSubmitActionHandler` のコンストラクタに `PlaytestSession playtestSession` を足して `_playtestSession` に保持し、`MainGameModelRegistration.cs` で `builder.Register<PlaytestSession>(Lifetime.Singleton)` を登録する。`Client.WebUiHost.asmdef` の references に `"Client.PlaytestReceiver"` を足す。

> plan B がまだマージされていない場合、このステップは飛ばして「plan B マージ後に本フックを入れる」を判断記録へ書き、`bd create` で積むこと。飛ばしても Step 6（起動直後の1回）は動く。

- [ ] **Step 8: テストを通す**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ErrorCount 0`

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.PlaytestReceiver\..*"`
Expected: 全 PASS（Task 6・7 の分を含む）

- [ ] **Step 9: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.PlaytestReceiver moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs
git commit -m "feat(playtest): outboxの受け口アップロードと送信直後の起動"
```

---

### Task 9: デプロイと受け口の通し確認

**Files:**
- Modify: `tools/playtest-receiver/README.md`（実際のデプロイで判明した差分だけを反映する）
- Modify: `docs/superpowers/plans/2026-09-13-playtest-d-receiver-worker-r2-and-launch-gate.md`（本plan末尾の「判断記録」へ実出力を転記する）

**Interfaces:**
- Consumes: Task 1〜5 の成果物すべて
- Produces: 稼働中の `https://playtest.moores.tech` と、許可リストに開発者本人の SteamID が1件入った状態

- [ ] **Step 1: R2 バケットと secrets を用意する**

```bash
cd tools/playtest-receiver
pnpm install
pnpm exec wrangler r2 bucket create moorestech-playtest
openssl rand -hex 32   # SESSION_HMAC_SECRET 用。出力は端末に残さず即 secret put へ貼る
pnpm exec wrangler secret put SESSION_HMAC_SECRET
openssl rand -hex 32   # ADMIN_KEY 用
pnpm exec wrangler secret put ADMIN_KEY
pnpm exec wrangler secret put STEAM_WEB_API_KEY   # Steamworks パートナーサイトの publisher key
```
Expected: 3つの secret が `pnpm exec wrangler secret list` に並ぶ。**値は端末ログにも本planにも残さない。**

- [ ] **Step 2: デプロイして DNS を確認する**

Run: `cd tools/playtest-receiver && pnpm run deploy`
Expected: デプロイ成功と `playtest.moores.tech` の割り当てが出る。出ない場合は README の手順4（ダッシュボードでカスタムドメイン追加）を行い、README をその実態に合わせて直す

Run: `dig +short playtest.moores.tech`
Expected: Cloudflare のアドレスが返る

- [ ] **Step 3: 3本の curl で通しを確認する**

```bash
BASE=https://playtest.moores.tech
. ~/hermes-agent/data/services/playtest/env.sh

curl -s -o /dev/null -w 'inbox-without-key: %{http_code}\n' "$BASE/v1/inbox"
scripts/playtest/allowlist.sh add <開発者本人のSteamID>
scripts/playtest/allowlist.sh list
curl -s -o /dev/null -w 'session-with-bad-ticket: %{http_code}\n' -X POST -d '{"ticket":"00"}' "$BASE/v1/session"
```
Expected: `inbox-without-key: 401`、`list` に追加した SteamID が1行、`session-with-bad-ticket: 401`

- [ ] **Step 4: 実出力を判断記録へ転記してコミットする**

本plan末尾「判断記録（ADR）」の「Task 9 の実出力」欄に3行の結果を貼る（SteamID は下4桁だけ・鍵は貼らない）。

```bash
git add tools/playtest-receiver/README.md docs/superpowers/plans/2026-09-13-playtest-d-receiver-worker-r2-and-launch-gate.md
git commit -m "chore(playtest): 受け口のデプロイと通し確認の記録"
```

---

### Task 10: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

- [ ] **Step 1:** `moores-code-review` を起動し、`feature/playtest-receiver` の全コミット（master からの差分）をレビューする。TypeScript（`tools/playtest-receiver/`）は汎用reviewer群の ts 観点、C# は moores 設計レンズ（層責務・前例一致・fail-closed のログ）を含める。
- [ ] **Step 2:** 機械的指摘を反映し、`cd tools/playtest-receiver && pnpm test && pnpm typecheck`、`bash scripts/playtest/tests/test-allowlist.sh`、`uloop compile --project-path ./moorestech_client`、`uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.PlaytestReceiver\..*"` をすべて再実行する。
- [ ] **Step 3:** 反映が判定経路（`PlaytestGateDecision.Decide` の分岐・`PlaytestLaunchGate.RejectStart`・`verifyToken`・`isSafeSegment`・`PlaytestUploadAttemptLog` の上限判定）に触れたら、**Task 9 の3本の curl を反映後のコードを deploy し直したうえで再実施する**（テスト通過とログ無音は代替にならない）。
- [ ] **Step 4:** 設計判断が要る指摘だけを AskUserQuestion で裁定に出す。

---

### Task 11: セッション終了可能状態にすること

- [ ] **Step 1:** `git status` で未コミットが無いことを確認する（`.moorestech-external-revisions.json` が Unity に書き換えられていたら `git checkout --` で戻す）。
- [ ] **Step 2:** `bd note <本planのissue id> "plan D 完了: <最終コミット> / 受け口: playtest.moores.tech / Steam初期化の確認結果: ..."`。
- [ ] **Step 3:** Task 7 Step 8 で AppID 未解決が見つかった場合はそれを `bd create` で plan E の子として積む。
- [ ] **Step 4:** PR を作り、`moores-wt rm <worktree名>` で worktree と Unity Editor を畳む（CLAUDE.local.md の撤収規約）。

---

## 配置と前例（spec-architecture-review）

| # | 配置決定 | 層・アセンブリ | 前例パス | 判定 |
|---|---|---|---|---|
| 1 | 受け口 Worker 一式 | `tools/playtest-receiver/`（Unity 外・独立 pnpm プロジェクト） | `tools/DeadMemberAudit/`（repo 内の独立ツール。ビルド系は `tools/` に置く慣習）、`moorestech_web/webui/`（pnpm + vitest の前例） | ok（`tools/` に node プロジェクトを置くのは初。`webui` と別ロックファイルにするのは**新規パターン**として注目点へ） |
| 2 | 受け口クライアント・Steamチケット・セッション・ゲート・アップローダ | 新アセンブリ `Client.PlaytestReceiver`（`moorestech_client/Assets/Scripts/Client.PlaytestReceiver/`） | 層マップ `Client.* = 表示・入力・ローカル状態`。`Client.Network`（サーバー通信）とは役割が別（配布運用の外部サービス連携）。`com.rlabrecque.steamworks.net` を参照するアセンブリを新設し、既存の広参照アセンブリへ Steam 依存を持ち込まない | ok（新アセンブリの新設理由あり。Steamworks の `includePlatforms` 制限は判断記録へ） |
| 3 | Steam 初期化状態の判定に `SteamManager.Initialized` を使わない | `PlaytestSteamTicketProvider.IsSteamRunning()`（`SteamAPI.IsSteamRunning`） | `moorestech_client/Assets/PersonalAssets/moorestech-client-private/Steamworks.NET/SteamManager.cs`（asmdef を持たず Assembly-CSharp。asmdef 側から参照不能） | ok（契約 §5 の字義は満たせないため同義判定へ置換。理由を Global Constraints に明記済み） |
| 4 | HTTP 機構は `HttpClient` + UniTask | `Client.PlaytestReceiver/Http/PlaytestReceiverClient.cs` | 一次実装の前例は `moorestech_client/Assets/Scripts/Client.WebUiHost/Vite/ViteHealthProbe.cs`（`static readonly HttpClient` ＋境界 try-catch ＋タイムアウト）。`UserSender.cs` の `UnityWebRequest` コルーチンは**役割が違う**（三者コードの投げっぱなしログ送信で、エラー処理も再送も無い） | ok（役割で前例を選択。機構一致だけの `UserSender` は引用しない） |
| 5 | 境界 try-catch の書き方 | `PlaytestReceiverClient.SendAsync`・`PlaytestSteamTicketProvider`・Worker の `steamAuth`／`allowlist`／`session` | `Client.WebUiHost/Editor/EditorProcessRunner.cs:39-50`（`try` 直上に日英2行で「なぜ境界か」、`Debug.LogError` に `[Component]` 接頭辞と `GetBaseException().Message`） | ok（同形式を逐語で踏襲） |
| 6 | タイトルでの停止表示 | `Client.MainMenu/Playtest/PlaytestLaunchGateView`（Assembly-CSharp）＋既存 `ServerConnectPopup` の再利用 | `Client.MainMenu/ConnectServer.cs:38`（`serverConnectPopup.SetText(Localize.GetFormatted(denyReason.Key, denyReason.TextParams))`）。ADR 0040 の WebUI 全画面ゲート（`features/eventLanguageGate/`）は **MainGame シーン読込後にしか出せない**（CEF は `MainGameUI.prefab` にしか無い）ため、タイトルの停止表示には使えない | ok（役割同型の前例は uGUI 側の `ServerConnectPopup` 一択） |
| 7 | 停止の enforcement | `LocalGameLauncher.StartLocalGame()` と `ConnectServer.Connect()` が `PlaytestLaunchGate.RejectStart()` を1関数だけ呼ぶ | AGENTS.md「同種の条件分岐は文脈が集まっている側の一箇所へ揃える」。`InitializeProprieties.TryCreateRemoteConnection` の deny 判定が呼び出し側1箇所に集まっているのと同型 | ok（判定は `PlaytestGateDecision` 1箇所。View は表示専用オブザーバに徹し制御に参加しない＝層マップの「購読は表示専用に限る」に適合） |
| 8 | 拒否理由の値型 | `PlaytestGateResult`（`readonly struct`＋`LocalizationKey ReasonKey`） | `Client.Starter/RemoteConnectionDenyReason.cs`（`readonly struct` に `LocalizationKey Key` と `TextParams`） | ok |
| 9 | outbox のパス | `Game.Paths/GameSystemPaths.cs` へ `ProgressRecordDirectory`・`ProgressRecordOutboxDirectory` を追加 | 既存 `SaveFileDirectory`・`WorldCacheDirectory`、plan B が同ファイルへ `BugReportOutboxDirectory` を足す設計 | ok（パス連結を `Game.Paths` 以外で行わない規約に適合） |
| 10 | 進行記録の送信トリガ | `PlaytestUploadRunner.RequestUpload(session)` を「起動直後（View）」「報告送信直後（plan B の ActionHandler）」から押す | AGENTS.md「状態変化の検知は購読で。`Update()` で毎tick同値判定をしない」＝ outbox の変化はポーリングせず、書いた側が直後にプッシュする | ok |
| 11 | R2 キーの `kind → prefix` 表 | `tools/playtest-receiver/src/keys.ts` の `KIND_PREFIX` を単一の正本にし、C# 側は `PlaytestOutboxBox.Kind` で `report`/`progress` の語だけを持つ | 層マップ「判断を内包するサービスは呼び出し側に段取りを残さない」＝ prefix の組み立てを Worker 側に閉じ、クライアントは kind を渡すだけ | ok |
| 12 | 許可リスト操作の置き場 | `scripts/playtest/allowlist.sh`（Mac mini 運用スクリプト。repo 内） | plan C `scripts/bugreport/ship-outbox.sh`（bash＋外部コマンド差し替え＋`scripts/bugreport/tests/`） | ok（同形式で踏襲） |

**データフロー（Phase 1.5）:**

```
起動 → MainMenu シーン → PlaytestLaunchGateView（読み手・表示専用）
        └─駆動─> PlaytestLaunchGate.EvaluateAsync
                    → PlaytestSteamTicketProvider（Steam）→ PlaytestReceiverClient → Worker /v1/session
                    → PlaytestGateDecision.Decide ──書き手──> ［共有状態 PlaytestLaunchGate.Current］
                                                                 ↑読み手: LocalGameLauncher / ConnectServer / BugReportSubmitActionHandler
Allowed → PlaytestUploadRunner.RequestUpload → PlaytestUploader → outbox 走査 → PUT/complete → UPLOADED
報告送信直後（plan B）→ 同じ RequestUpload
```
新規要素はすべて「書き手（`Current` を1回書く・outbox にマーカーを書く）」と「読み手（`Current` を読んで開始可否を決める）」で、既存の開始フローに分岐を足すのは `RejectStart` の early return 1箇所のみ。下流へ制御を返す `bool` は `RejectStart` だけで、これは既存の `TryCreateRemoteConnection` と同型の拒否判定である。

**機構選択（検査4）:** 起動時照合は「動作中の開始フロー（`LocalGameLauncher`・`ConnectServer`）に拒否を差し込む」能動介入である。受動的統合案「照合結果を購読してボタンの `interactable` を落とすだけにし、開始フロー自体は無傷にする」と比較した。受動案は (a) ボタン経由以外の開始（`EventModeAutoStart`・将来の自動起動）を止められない、(b) シーンのボタン参照を新たに配線する必要があり `MainMenu.unity` への変更が増える、の2点で「不許可なら止める」（ADR 0061・オンライン必須）を保証できない。よって能動介入を採り、介入点は**1関数の early return のみ**に絞った（表示は受動側＝View に寄せた）。

**死活表（Phase 2.5）:** MainMenu の既存操作が本planでどうなるか。

| 操作 | 計画後 | 根拠 |
|---|---|---|
| 「ローカルでプレイ」 | 生きる（開発者モード・Allowed のとき） | `RejectStart` が `IsBlocked` のときだけ true |
| 「サーバーに接続」 | 生きる（同上） | 同じ関所を1回呼ぶだけ |
| 言語ドロップダウン | 生きる | `LanguageSetting` に触れない |
| 全データリセット | 生きる | `ResetAllDataButton` に触れない |
| ゲーム終了 | 生きる | `QuitGame` に触れない |
| サーバー接続失敗ポップアップ | 生きる | `ServerConnectPopup.SetText` の呼び出しが1つ増えるだけ（新規UIを作らない） |
| Editor から MainGame シーンを直接 Play | 生きる | `build-info.json` が無いので常に `DeveloperMode` |

退化する操作は無い。**唯一の挙動変化は「配布版で照合に落ちたときにゲームを開始できない」で、これは ADR 0061 の裁定そのもの**である。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0061-steam-closed-playtest-report-receiver-and-save-compat.md`（正）、`docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md`（改訂3裁定を除き有効）、`docs/adr/0040-event-mode-language-select-gate.md`（タイトルゲートの前例）
- 裁定: `.decisions/2026-09-13-報告受け口はSteam認証チケットをWorkerで検証しSteamIDを報告に付ける.md`、`.decisions/2026-09-13-起動時にWorkerの許可リストでSteamIDを照合し個別に参加停止できるようにする.md`、`.decisions/2026-09-13-起動時照合はオンライン必須でWorkerに届かなければタイトルで止める.md`、`.decisions/2026-09-13-プレイテスト報告の受け口はCloudflare Worker+R2としMac miniは取り込むだけにする.md`
- 共有契約: セッションの scratchpad `plans/shared-contracts.md` §4・§5（本plan の Global Constraints へ逐語転記済み）

**planning 中に生じた判断:**

- **`kind=progress` の R2 プレフィックスを `progress/` にする（`progresss/` にしない）** — agent前提。共有契約 §4 の `{kind}s` を字義適用すると `progresss` になるが、§6 のローカル配置は `{reports|progress}` であり、字義適用は明らかな誤り。`src/keys.ts` の `KIND_PREFIX` を単一の正本とし、plan H の取り込みも同じ表に合わせる。**契約と字義が食い違う唯一の箇所なので、plan H 実装時に必ず突き合わせること。**
- **`GET /v1/allowlist` を追加する** — agent前提。共有契約 §4 は更新経路 `PUT /v1/allowlist` しか定めていないが、`allowlist.sh list` と「全置換 PUT のための read-modify-write」に読み取り経路が要る。契約の拡張であって矛盾ではない（既定の経路は変えていない）。
- **未ACK索引 `index/pending/{kind}/{steamId}/{id}` を置く** — agent前提。契約は `GET /v1/inbox` が「READY 済みで未ACK」を返すことだけを定める。R2 は suffix 検索ができないため、READY と対の空オブジェクトを索引として置き、ack で消す。ack は「ACKED を書く → 索引を消す」の順にし、途中でクラッシュしても項目が消えない（再取り込みは冪等）。
- **`complete` の2つの書き込み（READY と索引）の中断はクライアントの再試行で解ける** — agent前提。Worker が READY を書いた直後にクラッシュすると索引だけが欠け、その箱は `GET /v1/inbox` から永久に見えなくなる。これを恒久化させないのは**クライアントが 200 を受け取るまで `UPLOADED` を書かない**ためで、次回起動の走査で同じ箱がもう一度 complete され、両方が書き直される（R2 の put は同一キーの上書きなので冪等）。逆にクライアントが 200 を受け取った直後に落ちて `UPLOADED` を書けなかった場合も、再送で同じキーが上書きされるだけで取り込み側に重複は生まれない。**この「再試行が唯一の解除経路」なので、`complete` を「失敗しても UPLOADED を書く」形に変えてはならない。**
- **箱を送る順序（`BundleId` の辞書順＝時刻順）に下流依存は無い** — agent前提。決定的だから正しいのではなく、箱ごとに R2 のキーが分かれ、取り込み（plan H）も1件ずつ独立に処理するため、どの順で送っても下流の結果は同型になる。ID は `yyyyMMdd_HHmmss_<8桁hex>` で同点はほぼ起こらず、起きても順序が結果を変えない。`GET /v1/inbox` の返す順（R2 のキー順）についても同じ理由で plan H は順序に依存してはならない。
- **発行済みトークンは失効させない** — agent前提。許可リストから外した直後でも、発行済みの1時間トークンでアップロードだけは通る。起動時照合は次回起動から効くため、参加停止の即時性は「起動できない」で担保される（裁定の主旨と一致）。アップロードだけを即時に止める必要が出たら、`/v1/uploads` でも許可リストを引く形へ変える。
- **`Client.PlaytestReceiver` は `includePlatforms` を空にする** — agent前提。参照先の `com.rlabrecque.steamworks.net` は Editor と Standalone に限定されているため、Standalone/Editor 以外へビルドすると参照エラーになる。moorestech は Windows/macOS Standalone しかビルドしないので許容する。将来 Android/WebGL を足すならこのアセンブリを分割する。
- **`SteamManager` の配置と AppID 焼き込みは本planの外** — agent前提。`SteamManager` は MainMenu.unity に配置済み・`steam_appid.txt` は存在（起票セッションで確認済み）。`SteamAPI.RestartAppIfNecessary(AppId_t.Invalid)` のままで Steam 経由起動時に AppID が解決されるかだけが未確認。Task 7 Step 8 で実測し、未配置なら plan E の課題として `bd create` する。**本planのコードは fail-closed 側に倒してあるため、Steam が初期化されない状態では「開発者モードで素通し」になり、配布版としては照合が効かない。**この一点だけが本planの完成度をビルド側に依存させている。
- **開発者が Steam を起動したまま自作ビルド（`build-info.json` 付き）を動かすと止まる** — agent前提。回避は「自分の SteamID を `allowlist.sh add` で入れる」か「Steam を落として起動する」。専用のバイパス環境変数は作らない（ADR 0061 の「オンライン必須」を穴だらけにしないため）。
- **アップロードの恒久失敗を `UPLOAD_FAILED` で打ち切る** — agent前提。契約 §5 は「失敗は次回に持ち越し」としか言わない。100MiB 超などで恒久的に失敗する箱があると毎回同じ失敗を繰り返すため、5回で見送り印を打ち、理由を `Debug.LogError` に出す（無音で捨てない）。見送った箱は rsync 経路で手動回収できる。
- **`Client.PlaytestReceiver` は `BuildInfo` 型（共有契約 §1）を参照しない** — agent前提。`BuildInfo` は `Client.Game/InGame/BugReport/` に置かれ、`Client.Game` は `Client.PlaytestReceiver` の下流になる（逆参照は循環）。本planは `build-info.json` の存在判定しかしないため、パス定数だけを重複させる。中身を読む必要が出たら `Game.Paths` 相当の共有層へ型を移す。
- **`tools/playtest-receiver` は `moorestech_web/webui` と別の pnpm プロジェクトにする** — agent前提。webui の `pnpm-workspace.yaml` は webui 配下に閉じており、Worker は React/Vite と依存が全く重ならない。ワークスペース化はレビューの注目点として提示する。
- **Task 9 の実出力**: 未実施（2026-09-15 無人実装セッション）。このマシンに wrangler の認証（OAuth ログイン／Workers 権限の API token）と Steamworks publisher key（`STEAM_WEB_API_KEY`）が無く、`wrangler r2 bucket create`・`secret put`・`deploy` を実行できなかった。`pnpm exec wrangler deploy --dry-run` はバンドル成功（20KiB、bindings `BUCKET`・`STEAM_APP_ID`）。実デプロイと3本の curl は bd `moorestech-uet4.1` に子タスクとして積み、実施後にここへ転記する。
- **Task 7 Step 8 の実測**: `SteamManagerInMainMenu=True, steam_appid.txt=True`（Task 7 実装時に MainMenu.unity を開いて実測）。両方 true なので `SteamManager` の配置漏れ・AppID 未配置は無く、plan E への `bd create` は不要。`SteamAPI.RestartAppIfNecessary(AppId_t.Invalid)` のままで Steam 経由起動時に AppID が解決されるかは Editor からは観測できず未確認のまま（配布ビルドでの確認事項）。
- **Task 8 Step 6 の可否**: 実施。plan B は PR #1352 でマージ済みだったため `BugReportSubmitActionHandler` への送信直後フック（`IPlaytestUploadRequester` を DI で受ける形）を本planで入れた。

### 無人実装セッション（2026-09-15）でコントローラーが下した裁定 — 裁定サイトでの追認待ち

- A1 ブランチ名: 指示の `feature/save-migration-chain` は別plan（セーブ互換）の既存ブランチだったため、plan記載の `feature/playtest-receiver` を採用（前セッションの Task 1 コミットを継承）。
- A2 CI: `tools/playtest-receiver` の `pnpm test`/`typecheck` と `scripts/playtest/tests` を CI に載せるか（現状ジョブ無し・本PRでは追加していない）。
- A3 compatibility_date: plan の `2026-09-01` は手元 workerd 未対応のため `2026-08-22` へ。README に「デプロイ前に見直す」を明記。
- A4 PUT の Content-Length: plan コード例は fail-open だったが Global Constraints の fail-closed を優先し、欠落 411 / 不正 400 / 超過 413。
- A5 再 ack: 索引が無くても ACKED があれば 200（冪等）。at-least-once 前提と整合。
- A6 HttpClient.Timeout: Infinite にし呼び出し毎 CancelAfter（session/complete 60s、PUT 60s+128KiB/s 換算・上限 900s）。
- A7 パス文字: クライアントはセグメント毎に percent-encode、Worker は decodeURIComponent 後に空・`.`・`..`・区切り・制御文字のみ拒否（UTF-8 名を許容）。plan H は R2 キーを再デコードしてはならない。
- A8 try/finally: catch 無しの finally による状態復帰は try-catch 禁止の対象外と解釈（PlaytestSession の走行フラグ）。
- A9 照合中の関所: 配布版では EvaluateAsync 開始時点で `Checking`（IsBlocked）を固定し結果で置換（ポップアップを閉じて開始できる fail-open を封鎖）。
- A10 EventModeAutoStart: `AfterSceneLoad` で View の Start より先に走り関所を素通しする（Task 7 レビュー Minor #8）。イベントモードビルドは照合対象外とするか、関所を EventModeAutoStart にも挿すか。
- A11 送信可否判定: `PlaytestUploadPath.ForFile` に一本化（Worker 規則と同一）。plan のテスト（ASCII 限定）は書き換えた。
- A12 PlaytestSession の1本化: `PlaytestLaunchGate.Session` を保持し、DI には `IPlaytestUploadRequester` のみ登録（開発者モードで Session=null のため RegisterInstance は不可）。
- A13 Task 9 デプロイ: このマシンに wrangler 認証（OAuth/API token）と STEAM_WEB_API_KEY が無く未実施（bd moorestech-uet4.1）。`wrangler deploy --dry-run` はバンドル成功。
- A14 plan 文書の追随: 型名一覧に `PlaytestUploadPath`・`PlaytestSessionResult`・`IPlaytestUploadRequester` を追記、try-catch 許容箇所に File.OpenRead と JSON パースを追記、Task 1 ブリーフの依存版（vitest 4 / pool 0.22）を更新する（最終レビュー時に実施予定）。

### 全ブランチレビュー（moores-code-review 2026-09-15-0517）で保留した設計判断 — 裁定待ち

無人セッションのため AskUserQuestion は使わず、以下を裁定サイト（独立レビュー）へ回す。各項目の案A〜C の全文は `moorestech_logs/harness/moores-code-review/runs/2026-09-15-0517/integrated.md` §設計判断。推奨は各案A。

- **D1（C1・7系統一致）配布版の関所が「照合前」「関所外」の起動経路を素通しする（fail-open）**: `Current` の初期値と `ResetOnPlayMode` が `DeveloperMode` のため、`EventModeAutoStart`（`AfterSceneLoad`、View の `Start` より先）と `StandaloneTerrainQaBootstrap:93`（`LoadScene` 直呼び）が許可リスト未照合で開始できる。案A（推奨）: 未評価＝配布版なら `Checking`（Blocked）を初期値にする `InitialVerdict()` を置き、`StandaloneTerrainQaBootstrap` にも `RejectStart` を1行足す。案B: `TryLoadGameInitializer` ラッパへ関所を移す（ADR 0058 の再裁定）。案C: 未加入2経路に個別に `RejectStart` を足す。
- **D2（C2）受け口 base URL の環境変数上書き**: `PlaytestReceiverConfig` の `MOORESTECH_PLAYTEST_RECEIVER_URL`（plan のコード例由来）が「バイパス環境変数は作らない」裁定の穴。案A（推奨）: 環境変数を廃止し `DefaultBaseUrl` 固定。案B: `build-info.json` に `receiverBaseUrl` を持たせ plan E が焼く。案C: 開発ビルドだけ env を許す。
- **D3（C3）開発者モード判定が3箇所に分裂**: `RequiresCheck`・`Decide(hasBuildInfo, isSteamRunning, …)`・View の early return。案A（推奨）: `Decide(outcome, detail)` へ縮め `RequiresCheck` を正本に、`SetCurrent` を private 化。案B: 逆に `Decide` 1本に畳む。案C: `PlaytestLaunchEnvironment` 型へ分離。
- **D4（C4）押し場2箇所の gate 判定複製**: `PlaytestLaunchGateView` と `BugReportSubmitActionHandler` が同じ「Allowed なら RequestUpload」を持つ。案A（推奨）: `PlaytestLaunchGate.RequestUploadIfAllowed(requester, callerName)` を足し押し場を1行に。案B: `IPlaytestUploadRequester.RequestUpload(callerName)` にして Runner が gate を読む。案C: `IPlaytestGateLookup` 注入。
- **D5（C7）一時的失敗（回線断・5xx・408/429）が恒久失敗と同じ5回カウンタに合流**: 案A（推奨）: `PlaytestUploadFailureKind { Unauthorized, Retryable, PermanentForFile }` で分類し恒久由来のみ上限に数える。案B: 一時失敗に別上限（20回 or 30日）。案C: 現状維持。
- **D6（C9）`EvaluateAsync` が例外・打ち切りで抜けると `Checking` に固着**: 案A（推奨）: catch 無し try/finally で `Checking` のまま抜ける経路を `LogError` ＋ `Unreachable` へ倒す。案B: 現状＋ログのみ。案C: View の再 Start で再照合可能にする。
- **D7（C12）`TicketUnavailable` が「チケット不可」と「別の認証が走行中」を1値に潰す**: 案A（推奨）: `AuthenticationInFlight` を足し `Decide` を網羅 switch に。案B: 8 variant へ細分（localization 2キー追加）。案C: `Detail` で切り分け（現状）。
- **D8（C14）結果型 `PlaytestSessionResult` / `PlaytestApiResult` の public 可変フィールド**: 案A（推奨）: private ctor ＋ 種別ごとの static factory ＋ readonly。案B: 抽象基底＋3派生。案C: 生成箇所の規律のみ。C15 の残り（`HasToken`/`SteamId` の削除）はこの裁定後に一括。
- **D9（C17）2つの関所で拒否時の見え方が非対称**（ローカル開始は無反応）: 案A（推奨）: `PlaytestGateResult.BlockedMessage()` を足し `StartLocal` に `ServerConnectPopup` を配線（uloop 経由）。案B: `RejectStart(callerName, out blocked)`。案C: View の popup を閉じられなくする。
- **D10（C19）`readAllowlist` が破損・型不正を `[]` に畳み、`allowlist.sh` の全置換 PUT で既存テスターが消える**: 案A（推奨）: `{status:"ok"|"unreadable"}` を返し、GET は 503 `allowlist-unreadable`、session は 500。案B: 200 本文に `source` を足す。案C: `allowlist.sh` 側だけ厳格化。
- **D11（C27）`completeUpload` が無検査本文で ACK 済みの箱を pending へ復活させる**: 案A（推奨）: `getInbox` が ACKED を head で除外（真実は ACKED マーカー）。案B: ack を delete(index)→put(ACKED) 順にし索引不在を真実に。案C: 索引の customMetadata に state。いずれも complete への Content-Length 3段検査＋JSON 確認は必要。
- **D12（C28）try-catch の許可境界（決定論 confirmed 3件: `File.OpenRead`・Steamworks interop）**: 案A（推奨）: AGENTS.md の許可境界に「OS/ネイティブ境界（ファイルI/O・P/Invoke）」を明記。案B: コードを規約に合わせる（事前検査へ置換）。案C: 現状（毎回 confirmed が出続ける）。
- **D13（W13/W14）送信済みファイルの追跡と箱の刈り取り**: 途中失敗で既送ファイルを再送・送信済みバンドルがディスクに残り続ける。案A（推奨）: `UPLOAD_SENT` 記録で既送をスキップし `MarkUploaded` で箱を刈る。案B: `MarkUploaded` で箱ディレクトリを削除。案C: 現状維持（ADR に1行）。
- **設計判断未満・未適用の Critical**: C5（走行中 RequestUpload のテスト）は D4 後に、C16（`#region Internal` 16件）は次パスで、C15 の残り3件は D8 後に。
- **Warning 45件・Info 38件**は integrated.md に全件列挙（Warning のうち特に: `RegisterInstance` が全 MainGame 起動で Runner を生成／`Decide` と `ReasonKey` の fall-through 既定／`UPLOAD_ATTEMPTS` 破損時に上限へ到達しない／`ConnectServer.Connect` の関所にテスト無し／R2 put が宣言長と実バイト数を照合しない）。

## Execution Handoff

planが完成し `docs/superpowers/plans/2026-09-13-playtest-d-receiver-worker-r2-and-launch-gate.md` に保存されました。新規セッションを開き、以下を貼り付けて実装を開始してください:

```
subagent-driven-development スキルを使って、以下の実装planを実行してください。

- plan: docs/superpowers/plans/2026-09-13-playtest-d-receiver-worker-r2-and-launch-gate.md
- 作業場所: feature/playtest-receiver（`moores-wt new feature/playtest-receiver` で使い捨て worktree を切ってそのパスで作業）
- まずplan全文を読み、`## Requirements`・`## Global Constraints`・`## 判断記録（ADR）`を全タスク共通の制約として扱ってください
- 進捗管理はsubagent-driven-developmentスキルの規定に従ってください（SDD本体はplanのチェックボックス＋進捗台帳、単一subagent実装モードは報告ファイル＋進捗台帳が正）
- planの最終タスク（moores-code-reviewスキルによる全ブランチレビュー）は省略不可です
```
