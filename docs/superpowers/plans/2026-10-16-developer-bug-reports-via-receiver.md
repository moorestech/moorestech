# 開発者のバグ報告を受け口へ送る Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Editor と開発ビルドのプレイ報告を、機材ごとの開発者トークンで本番の受け口（Cloudflare Worker + R2）へ送り、Mac mini の既存の取り込み・ダイジェスト・自動修正投入の導線に乗せる。あわせて、報告の調査を速くする道具（停滞診断の同梱、トリアージ、所在一覧）を足す。

**Architecture:**
- 受け口: 開発者トークンのハッシュ台帳を R2 に置き、`/v1/session/developer` で通常のセッショントークン（sub=`dev-<機材名>`）に交換する。
- クライアント: `PlaytestSession` の認証材料を「Steam チケット」と「開発者トークン」の差し替え可能な資格情報に分ける。`PlaytestUploadRunner` は起動形態とトークンの有無から送信経路（Steam／開発者トークン／ローカル保存のみ）を1か所で決め、送信状況をバグ報告画面へ流す。
- 報告の確保: Editor では、報告を取るときに未push コミットの git bundle と列車同期の停滞診断を箱へ入れる。
- 運搬スクリプト（rsync）は削除する。

**Tech Stack:** Unity 6000.3.8f1 / C#（UniTask, UniRx, VContainer）、Cloudflare Workers（TypeScript, vitest + miniflare R2）、React + zod（webui, vitest）、bash + python3（Mac mini スクリプト）

## Requirements

1. 受け口は機材名つきの開発者トークンを発行・一覧・失効できる。発行・一覧・失効は管理キーでだけ通る。
   - 受け入れ: vitest で、発行→交換→アップロード→失効後の交換401 が通る。管理キーが無いと401。
2. 開発者トークンを交換すると、Steam のセッションと同じ形の応答（steamId=`dev-<機材名>`, allowed, token, expiresAt）が返る。その token で上げた箱は `reports/dev-<機材名>/<id>/` に置かれる。
   - 受け入れ: vitest。
3. 受け口は `dev-` の sub からの進行記録（kind=progress）の prepare・complete を 403 で拒否し、理由を warn に出す。また `dev-` の sub の prepare・complete では毎回台帳を確かめ、失効済みの機材なら 401 `developer-token-revoked` にする（失効を即時に効かせる）。
   - 受け入れ: vitest（失効直後に、発行済みセッションでの prepare が 401）。
4. Mac mini から `scripts/playtest/dev-token.sh issue|list|revoke <機材名>` で開発者トークンを扱える。取り込みは `dev-*` の Steam 表示名を Steam に問い合わせず「開発機 <機材名>」とし、ダイジェストにはそれが出る。
   - 受け入れ: bash テスト。
5. 開発者トークンは Unity のエディタ拡張（メニュー `moorestech/Playtest/Developer Token`）で貼り付け・保存・削除・接続確認ができる。保存先は機材ローカルのファイル `<GameSystemDirectory>/PlaytestReceiver/developer-token.txt` で、Editor と開発ビルドが同じファイルを読む。
   - 受け入れ: EditMode テストで保存・読込・削除の往復。接続確認は実機で確かめる。
6. 送信経路は次のとおり1か所で決める。
   - 配布版 → Steam 認証で全 outbox。
   - Editor・開発ビルドでトークンが読めた → 開発者トークン認証で、報告用 outbox（バグ報告・感想・異常終了）だけ。進行記録は送らない。
   - それ以外 → 送らず「ローカル保存のみ」とし、理由を状態に載せる。
   - 受け入れ: EditMode テストで3経路と、進行記録が送られないこと。
7. 溜まっている箱は、トークン設定後の MainGame 起動時と報告送信時に送られる。再生停止で中断した送信は、次の再生で再送される。
   - 受け入れ: EditMode テストで起動時の押し出し1回。実機で、溜まっていた箱が届くこと。
8. バグ報告画面に送信状況を1行出す。表示は「ローカル保存のみ（理由）＋件数」「送信待ち N 件」「受け口に拒否された（トークン失効の可能性）＋件数」「すべて送信済み」のどれか。4言語の文言をそろえる。
   - 受け入れ: webui vitest、C# の DTO テスト、unityプレイ録画テストで目視。
9. Editor で報告を取るとき、moorestech と moorestech_master の未push コミットを `repo/commits.bundle`・`repo/master-commits.bundle` にし、状態を `repo/bundle-status.txt` へ書く。書式は従来の運搬スクリプトと同じ `<label>: <state>`。失敗は manifest の missing にも残す。
   - 受け入れ: EditMode テスト（一時 git repo で created / not-needed / skipped-no-commit）。prepare-run.sh の既存テストが通る。
10. 報告の確保時に、今回の再生中に書かれた列車同期の停滞診断（`stall-*.json`、新しい順に最大8件）を `logs/train-sync/` へ入れる。
    - 受け入れ: EditMode テスト。
11. `scripts/bugreport/triage.py <箱>` が Unity 無しで次を出す: manifest 要約、Error と Warning の行、`[TrainSynchronization]` の行、列車ごとの tick 時系列（自動運転・速度・時刻表・位置）、レールの有向辺一覧、動画のコンタクトシート。
    - 受け入れ: python テスト（合成した箱）と、実際の箱での手動実行。
12. `scripts/playtest/find-reports.sh` が、受け口の未取り込み・取り込み済みの報告（新しい順）・自動修正の inbox とランを1回で並べる。
    - 受け入れ: bash テスト（偽の受け口と偽のディレクトリ）。
13. 運搬スクリプト一式（`ship-outbox.sh`・launchd plist・そのテスト）を削除し、それを前提にした記述（README・コメント・bug-report-auto-fix スキル・ADR 0057/0061 の状態行）を新経路に合わせる。
    - 受け入れ: `git grep -n "ship-outbox\|bugreport-shipper\|rsync path"` が履歴文書（docs/superpowers/plans/ 配下の旧plan）以外で0件。
14. 実機で確かめる。
    - Mac mini の作業 worktree の Editor と、winbuild（Windows）の Editor の両方で、トークン設定→報告送信→Mac mini の取り込み（`reports/dev-<機材名>/<id>/`）まで通す。
    - Mac では bundle-status.txt と commits.bundle の中身も確かめる。
    - 受け入れ: 取り込み済みディレクトリのパスとファイル一覧を報告に残す。

**やらないこと（スコープ境界）:**
- 配布版の Steam 認証経路の変更。
- タイトルゲート（同意・前回異常終了）の挙動変更。開発者経路の押し場は、MainGame 起動と報告送信の2つにする。
- 開発者報告の自動修正への自動投入（裁定: 人が投入する）。
- Steam 表示名キャッシュの掃除。

## Global Constraints

- 受け口の送り先は本番 `https://playtest.moores.tech`。staging は使わない。
- 開発者トークン経路の選択（`PlaytestDeliveryRouteResolver.Resolve` の DeveloperMode 分岐）は `#if UNITY_EDITOR || DEVELOPMENT_BUILD` で閉じる。配布ビルドではこの分岐が消え、開発者トークンを読まない。経路の型そのものは #if で囲まない（テストから使えるように）（前例: `PlaytestReceiverConfig.BaseUrl` の環境変数上書き）。
- 開発者トークンの形は `mdt_` + 小文字16進64桁。機材名は `^[a-z0-9][a-z0-9-]{0,31}$`。セッションの sub は `dev-<機材名>`。
- R2 の台帳キー:
  - `devtokens/by-hash/<sha256hex>` → `{"machine": "...", "createdAt": "<ISO>"}`
  - `devtokens/by-machine/<machine>` → `{"hash": "<sha256hex>", "createdAt": "<ISO>"}`
  - トークンの生値は R2 に置かない。
- 1ファイル200行未満。1ディレクトリ10ファイルまで。partial・`Func<>`・デフォルト引数・単純 getter/setter は禁止。主要処理に日英2行コメントを付ける。
- try-catch は外部境界（プロセス起動・ネットワーク・外部入力パース・ディスクIO）だけ。catch した失敗はログと manifest の Missing の両方に残す。
- 無音の縮退は禁止。送らない・拒否する経路は理由をログに出し、バグ報告画面の送信状況にも出す。
- `.meta` を手で作らない。Prefab やシーンを手で編集しない。
- .cs を変えたら `uloop compile --project-path ./moorestech_client` を通す。
- 常駐サービス・wrangler・launchd を触る前に `~/hermes-agent/data/HERMES-OPS.md` を読む。秘密値の env ファイルは `>` で作り直さない。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反2件・修正2件 / BugReport直下とClient.Tests/BugReport直下が10ファイル超のため新設をRepository/・Diagnostics/へ。パリティ: 配布版のSteam経路・タイトルゲートは不変（死ぬ操作なし）
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強6・弱11・第3バケツ3 / 強: 判別union化・Resolveを評価済み2状態に・PlaytestKind型・allSent表示を裁定どおりに（4件反映）、Board読み書き分離はこのまま。弱: 4件反映・7件このまま

---

## 配置と前例

| # | 項目 | 配置先 | 機構・前例 |
|---|---|---|---|
| 1 | 開発者トークン台帳 `devTokenStore.ts` | tools/playtest-receiver/src/devTokens/ | R2 に JSON。前例: `bundleMarkers.ts` の R2 マーカー |
| 2 | `/v1/session/developer` | src/routes/developerSession.ts | 前例: `routes/session.ts`（同形の応答・`signToken`） |
| 3 | 管理 API `/v1/admin/dev-tokens` | src/routes/adminDevTokens.ts | 前例: `routes/admin.ts` の `requireAdmin` 先行 |
| 4 | dev の進行記録拒否 | src/devTokens/developerSubject.ts（prepare・complete から呼ぶ） | 判定は1関数に集める |
| 5 | `dev-token.sh` と API ラッパ | scripts/playtest/ と lib/receiver-api.sh | 前例: `receiver_inbox_page` |
| 6 | 資格情報の差し替え `IPlaytestSessionCredential` | Client.PlaytestReceiver/Credentials/ | `PlaytestSession` の Steam 固定部分（L55-68）を抽出 |
| 7 | 開発者トークンファイル `PlaytestDeveloperTokenFile` | Client.PlaytestReceiver/Credentials/ | 前例: `Client.RemoteExec/Access/RemoteExecAccessFile.cs`（GameSystemPaths 配下のローカル鍵ファイル） |
| 8 | 送信経路の決定 `PlaytestDeliveryRouteResolver` | Client.PlaytestReceiver/Delivery/ | 前例: `PlaytestLaunchProfile.Decide`（起動形態の判定を1か所で） |
| 9 | 送信状況 `PlaytestDeliveryStatusBoard` | Client.PlaytestReceiver/Delivery/ | 静的な ReactiveProperty。前例: `PlaytestUploadRunner` の静的状態と `ResetOnPlayMode` |
| 10 | 画面への配信 | Client.WebUiHost/Game/Topics/C2/PauseMenuTopic.cs に `Delivery` を追加 | 前例: 同 topic の `BugReport` 状態（イベント＋スナップショット＋購読） |
| 11 | MainGame 起動時の押し出し `PlaytestBootUploadTrigger` | Client.Starter/Playtest/ | `IStartable`。前例: `MainGameInteractionRegistration.cs:129` |
| 12 | エディタ拡張 `PlaytestDeveloperTokenWindow` | moorestech_client/Assets/Scripts/Editor/Playtest/ | 前例: `Scripts/Editor/MapAuthoring/MapAuthoringWindow.cs`（MenuItem `moorestech/...`） |
| 13 | git bundle `BugReportCommitBundles` | Client.Game/InGame/BugReport/Repository/ | 前例: `BugReportRepositoryFiles.cs`（Editor 起源だけで git を起動） |
| 14 | 停滞診断の同梱 `TrainSyncDiagnosticsCopier` | Client.Game/InGame/BugReport/Diagnostics/（直下は既に11ファイル） | 前例: `BugReportWorldFilesCopier.cs` |
| 15 | 停滞診断の置き場 `TrainSynchronizationDiagnosticDirectory` | Client.Game/InGame/Train/Network/Diagnostics/ | 書き手（`MainGameInteractionRegistration.cs:188`）と読み手が同じ定義を使う |
| 16 | `triage.py` | scripts/bugreport/triage/ | python3 標準ライブラリのみ。ffmpeg は任意 |
| 17 | `find-reports.sh` | scripts/playtest/ | 前例: `ingest.sh` の env 読み込みと `receiver-api.sh` |

データフロー（送信）: 報告送信／MainGame 起動 → `IPlaytestUploadRequester.RequestUpload` → `PlaytestUploadRunner` が経路を決める → `PlaytestUploader`（既存）→ 受け口。`PlaytestUploadRunner` は送信状況の唯一の書き手で、`PauseMenuTopic` は読み手。

---

### Task 1: 受け口に開発者トークンを足す

**Files:**
- Create: `tools/playtest-receiver/src/devTokens/devTokenStore.ts`
- Create: `tools/playtest-receiver/src/devTokens/developerSubject.ts`
- Create: `tools/playtest-receiver/src/routes/developerSession.ts`
- Create: `tools/playtest-receiver/src/routes/adminDevTokens.ts`
- Modify: `tools/playtest-receiver/src/index.ts`（経路の連鎖に2つ足す）
- Modify: `tools/playtest-receiver/src/routes/uploadsPrepare.ts`、`tools/playtest-receiver/src/routes/uploadsComplete.ts`（`authorizeUpload` の直後に進行記録の拒否を入れる）
- Modify: `tools/playtest-receiver/README.md`（エンドポイント表と curl 確認）
- Test: `tools/playtest-receiver/test/devTokens/developerSession.test.ts`、`tools/playtest-receiver/test/devTokens/adminDevTokens.test.ts`、`tools/playtest-receiver/test/devTokens/developerProgress.test.ts`

**Interfaces:**
- Produces（HTTP）:
  - `POST /v1/session/developer` body `{"token":"mdt_<64hex>"}` → 200 `{steamId:"dev-<machine>", allowed:true, token, expiresAt}` ／ 400 `bad-request` ／ 401 `invalid-developer-token` ／ 500 `server-misconfigured`
  - `POST /v1/admin/dev-tokens` body `{"machine":"<name>"}` → 201 `{machine, token}` ／ 400 `bad-machine` ／ 409 `machine-already-has-token`
  - `GET /v1/admin/dev-tokens` → 200 `{machines:[{machine, createdAt}]}`（machine の昇順）
  - `DELETE /v1/admin/dev-tokens/<machine>` → 200 `{revoked:"<machine>"}` ／ 404 `not-found`
  - 管理系はどれも、`x-admin-key` が無いか違えば、経路の形やメソッドより先に 401 `unauthorized`
  - prepare・complete: sub が `dev-` で kind=progress → 403 `developer-progress-not-accepted`
- Produces（TS）: `DEVELOPER_SUBJECT_PREFIX = "dev-"`、`isDeveloperSubject(sub: string): boolean`、`developerSubjectOf(machine: string): string`

- [x] **Step 1: 台帳と判定を書く**

`src/devTokens/developerSubject.ts`:
```ts
import type { PlaytestKind } from "../keys";

// 開発機のセッションはsubの接頭辞で見分ける。SteamIDは数字だけなので衝突しない
// Developer sessions are told apart by the sub prefix; Steam IDs are digits only, so they never collide
export const DEVELOPER_SUBJECT_PREFIX = "dev-";
export const MACHINE_PATTERN = /^[a-z0-9][a-z0-9-]{0,31}$/;

export function developerSubjectOf(machine: string): string {
  return `${DEVELOPER_SUBJECT_PREFIX}${machine}`;
}

export function isDeveloperSubject(sub: string): boolean {
  return sub.startsWith(DEVELOPER_SUBJECT_PREFIX);
}

// 開発機は進行記録を送らない（テスターの集計に混ぜない）。拒否理由はwarnに残す
// Developer machines never send progress records (kept out of tester stats); the refusal is warned
export function refusesDeveloperProgress(kind: PlaytestKind, sub: string): boolean {
  if (kind !== "progress" || !isDeveloperSubject(sub)) return false;
  console.warn(`[uploads] refused: progress records are not accepted from developer machine ${sub}`);
  return true;
}
```

`src/devTokens/devTokenStore.ts`（R2 の2系統のキーを常に対で書き、消す）:
```ts
import type { Env } from "../env";

const BY_HASH = "devtokens/by-hash/";
const BY_MACHINE = "devtokens/by-machine/";
export const DEV_TOKEN_PATTERN = /^mdt_[0-9a-f]{64}$/;

export type IssueResult = { kind: "issued"; token: string } | { kind: "exists" };

// 生のトークンは返すだけで保存しない。R2にはSHA-256だけを置く
// The raw token is only returned, never stored; R2 keeps just its SHA-256
export async function issueDevToken(env: Env, machine: string, now: Date): Promise<IssueResult> {
  if ((await env.BUCKET.head(BY_MACHINE + machine)) !== null) return { kind: "exists" };
  const token = `mdt_${randomHex(32)}`;
  const hash = await sha256Hex(token);
  const createdAt = now.toISOString();
  await env.BUCKET.put(BY_HASH + hash, JSON.stringify({ machine, createdAt }));
  await env.BUCKET.put(BY_MACHINE + machine, JSON.stringify({ hash, createdAt }));
  return { kind: "issued", token };
}

export async function findMachineByToken(env: Env, token: string): Promise<string | null> {
  const object = await env.BUCKET.get(BY_HASH + (await sha256Hex(token)));
  if (object === null) return null;
  const record = (await object.json()) as { machine?: unknown };
  return typeof record.machine === "string" ? record.machine : null;
}

export async function listDevTokens(env: Env): Promise<readonly { machine: string; createdAt: string }[]> {
  const listed = await env.BUCKET.list({ prefix: BY_MACHINE });
  const rows = await Promise.all(listed.objects.map(async (entry) => {
    const object = await env.BUCKET.get(entry.key);
    const record = object === null ? {} : ((await object.json()) as { createdAt?: string });
    return { machine: entry.key.slice(BY_MACHINE.length), createdAt: record.createdAt ?? "" };
  }));
  return rows.sort((a, b) => a.machine.localeCompare(b.machine));
}

export async function revokeDevToken(env: Env, machine: string): Promise<boolean> {
  const object = await env.BUCKET.get(BY_MACHINE + machine);
  if (object === null) return false;
  const record = (await object.json()) as { hash?: string };
  if (typeof record.hash === "string") await env.BUCKET.delete(BY_HASH + record.hash);
  await env.BUCKET.delete(BY_MACHINE + machine);
  return true;
}

function toHex(bytes: Uint8Array): string {
  return [...bytes].map((b) => b.toString(16).padStart(2, "0")).join("");
}

function randomHex(byteCount: number): string {
  return toHex(crypto.getRandomValues(new Uint8Array(byteCount)));
}

async function sha256Hex(text: string): Promise<string> {
  return toHex(new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(text))));
}
```
R2 の JSON 読み（`get`→null 判定→`json()`）は `findMachineByToken`・`listDevTokens`・`revokeDevToken` の3か所で同じなので、`readJson<T>(env, key): Promise<T | null>` を1本作ってそれを使う（裁定: 弱指摘③）。`listDevTokens` で by-machine の読み出しが null になるのは、一覧と失効の競合だけ。その行は createdAt を空にして返す（silent-ok: 一覧は表示用。管理者は次の一覧で消えたことに気づく）。この旨をコメントで書く。

- [x] **Step 2: 経路を書く**

`src/routes/developerSession.ts`:
- 形は `routes/session.ts` に合わせる。
- `segments` が `["v1","session","developer"]` のときだけ扱い、それ以外は null を返す。メソッドは POST だけ。
- body のパースは外部入力境界として `try/catch` で 400 にし、warn する。`token` が `DEV_TOKEN_PATTERN` に合わなければ 400 `bad-request`。
- `findMachineByToken` が null なら warn して 401 `invalid-developer-token`。
- `signToken(env.SESSION_HMAC_SECRET, developerSubjectOf(machine), now)` が null なら 500 `server-misconfigured`。
- 応答は `json({ steamId: sub, allowed: true, token, expiresAt })`。

`src/routes/adminDevTokens.ts`:
- `segments[0]==="v1" && segments[1]==="admin" && segments[2]==="dev-tokens"` で一致させる。
- 一致したら、まず `requireAdmin` を通す。
- そのあと `segments.length===3` なら GET（一覧）／POST（発行）、`segments.length===4` なら DELETE（失効）。それ以外は 404 か `requireMethod` の 405。
- 発行の machine は `MACHINE_PATTERN` で検証する。外れたら 400 `bad-machine`。
- 発行で `exists` が返ったら 409 `machine-already-has-token`（再発行は失効のあとに限る）。

`src/index.ts`: `routeSession` の直後に `routeDeveloperSession` を、`routeAdmin` の直前に `routeAdminDevTokens` を差す。

`uploadsPrepare.ts`・`uploadsComplete.ts` では、`const steamId = await authorizeUpload(...)` の直後に次を入れる:
```ts
if (refusesDeveloperProgress(kind, steamId)) return fail("developer-progress-not-accepted", 403);
if (await isRevokedDeveloper(env, steamId)) return fail("developer-token-revoked", 401);
```
`isRevokedDeveloper(env, sub)` は `devTokenStore.ts` に置く。`isDeveloperSubject(sub)` が偽なら false。真なら `env.BUCKET.head("devtokens/by-machine/" + sub.slice(4))` が null のとき warn して true を返す。クライアントは 401 を既存どおり「セッション拒否」と扱い、1回だけセッションを取り直す。取り直しも交換で 401 になるので、走行は `SessionRefused` で止まる（画面は「受け口に拒否されました」）。

- [x] **Step 3: テストを書く**

`test/devTokens/developerSession.test.ts` の検査:
- 発行（管理API）→ 交換が 200 で、4キーの形と `steamId==="dev-macbook"`。`verifyToken` が sub `dev-macbook` を返す。
- 交換で得た token で `prepare("report", ...)` → `complete` すると、`reports/dev-macbook/<id>/READY` ができる（`test/support/uploadsFixture.ts` の `prepare`/`complete` に bearer を渡す）。
- 失効後の交換が 401 `invalid-developer-token`。
- 形の違うトークン（`mdt_` 無し、63桁、大文字）が 400。

`test/devTokens/adminDevTokens.test.ts` の検査:
- キー無しで GET・POST・DELETE が全部 401。
- `bad machine`（`"Mac Book"`, `""`, `"-a"`, 33文字）が 400。
- 同名の二重発行が 409。
- 一覧が昇順で createdAt を持つ。
- 存在しない機材の失効が 404。
- R2 に生のトークン文字列が含まれない（`env.BUCKET.list()` の全オブジェクトの本文を読み、`token` を含まないこと）。

`test/devTokens/developerProgress.test.ts` の検査:
- dev の token で `prepare("progress", ...)` と `complete("progress", ...)` が 403 `developer-progress-not-accepted` になり、warn が出る。
- 発行済みセッションの token で、失効後の `prepare("report", ...)` と `complete("report", ...)` が 401 `developer-token-revoked` になる。
- Steam の sub（`STEAM_ID`）の progress は 200 のまま。

各テストの `beforeEach` で `clean()` を呼ぶ。

- [x] **Step 4: テストと型検査を通す**

Run: `cd tools/playtest-receiver && pnpm test && pnpm typecheck`
Expected: 全 PASS、型エラー0。

- [x] **Step 5: README を更新してコミットする**

エンドポイント表に4行を足し、「開発者トークン」の節に発行→Editor 設定→失効の手順を書く。

```bash
git add tools/playtest-receiver
git commit -m "feat(receiver): 機材ごとの開発者トークンでセッションを発行する"
```

### Task 2: Mac mini の開発者トークン操作と取り込みの表示名

**Files:**
- Create: `scripts/playtest/dev-token.sh`
- Modify: `scripts/playtest/lib/receiver-api.sh`（`receiver_dev_token_issue`・`receiver_dev_token_list`・`receiver_dev_token_revoke`）
- Modify: `scripts/playtest/lib/steam-persona.sh`（`dev-*` は Steam を引かない）
- Modify: `scripts/playtest/lib/steam_persona.py`（サブコマンド `developer`）
- Modify: `scripts/playtest/README.md`、`scripts/playtest/README-macmini.md`
- Test: `scripts/playtest/tests/test-dev-token.sh`、`scripts/playtest/tests/test_digest_reporters.py`（dev の表示名の1ケースを追加）

**Interfaces:**
- Consumes: Task 1 の管理 API。
- Produces:
  - `dev-token.sh issue <machine>`: 標準出力にトークン1行だけを出す。
  - `dev-token.sh list`: `machine<TAB>createdAt`。
  - `dev-token.sh revoke <machine>`。
  - 終了コードは成功0・失敗1。
  - `steam_persona.py developer <steamId> <out>`: `{"steamPersonaName":"開発機 <machine>","steamProfileUrl":"","steamPersonaMissing":""}`

- [x] **Step 1: API ラッパを足す**

`receiver-api.sh` の `receiver_curl` は余分な curl 引数をそのまま通すので、次のように書く:
```bash
# 開発者トークンの発行。応答JSON（machine, token）を out へ書く
# Issues a developer token; the response JSON (machine, token) is written to out
receiver_dev_token_issue() {
  local machine="$1" out="$2" body
  body="$(python3 -c 'import json,sys;print(json.dumps({"machine":sys.argv[1]}))' "$machine")"
  receiver_curl "$out" -X POST -H "content-type: application/json" --data "$body" "$RECEIVER_BASE/v1/admin/dev-tokens"
}
receiver_dev_token_list() { receiver_curl "$1" "$RECEIVER_BASE/v1/admin/dev-tokens"; }
receiver_dev_token_revoke() {
  local machine="$1" out="$2"
  receiver_curl "$out" -X DELETE "$RECEIVER_BASE/v1/admin/dev-tokens/$(url_encode_path "$machine")"
}
```
呼び方は `receiver_dev_token_list <out>`。

- [x] **Step 2: dev-token.sh を書く**

- env の読み込み順は `ingest.sh` L15-29 と同じにする（`PLAYTEST_ENV_FILE` か `$REPO/../../services/playtest/env.sh` → `lib/receiver-api.sh`）。
- 機材名は `^[a-z0-9][a-z0-9-]{0,31}$` で先に検証し、外れたら理由を stderr に出して1で終わる。
- issue はトークンだけを標準出力へ出し、案内（Editor のメニュー名）は stderr へ出す。

- [x] **Step 3: 表示名を開発機に切り替える**

`steam_persona_resolve` の先頭（キャッシュ確認より前）に足す:
```bash
  # 開発機はSteamアカウントではない。Steamへ問い合わせず開発機名を表示名にする
  # A developer machine is not a Steam account; skip Steam and label it with the machine name
  case "$steam_id" in
    dev-*)
      python3 "$HERE/lib/steam_persona.py" developer "$steam_id" "$out" \
        || { log "ERROR: 開発機の表示名を書けない: $steam_id"; return 1; }
      return 0 ;;
  esac
```
`steam_persona.py` には `developer(steam_id)` を足し、`{"steamPersonaName": f"開発機 {steam_id[4:]}", "steamProfileUrl": "", "steamPersonaMissing": ""}` を返す。CLI の分岐にも `developer` を足す。

- [x] **Step 4: テストを書いて通す**

`tests/test-dev-token.sh`:
- 既存の `test-ingest.sh` と同じく `CURL_CMD` を偽物に差し替える。
- issue・list・revoke がそれぞれ正しいメソッドと URL と `X-Admin-Key` を渡すこと。
- 不正な機材名で curl を呼ばずに1で終わること。
- issue の標準出力がトークン1行だけであること。

`test_digest_reporters.py` には、`steamPersonaName="開発機 macbook"` の報告者表記が `開発機 macbook（SteamID dev-macbook）` になるケースを足す。

Run: `bash scripts/playtest/tests/test-dev-token.sh && bash scripts/playtest/tests/test-ingest.sh && python3 -m pytest scripts/playtest/tests/test_digest_reporters.py -q`
Expected: 全 PASS。

- [x] **Step 5: README を更新してコミットする**

```bash
git add scripts/playtest
git commit -m "feat(playtest): 開発者トークンの発行・失効コマンドと開発機の表示名"
```

### Task 3: クライアントの資格情報を差し替え可能にし、開発者トークンを読む

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Credentials/IPlaytestSessionCredential.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Credentials/SteamTicketSessionCredential.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Credentials/DeveloperTokenSessionCredential.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Credentials/PlaytestCredentialPost.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Credentials/PlaytestDeveloperTokenFile.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/PlaytestSession.cs`（コンストラクタを `(IPlaytestReceiverApi api, IPlaytestSessionCredential credential)` にし、L55-68 を資格情報へ委ねる）
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/IPlaytestReceiverApi.cs`、`moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/PlaytestReceiverClient.cs`（`PostDeveloperSessionAsync`）
- Modify: `moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs`（`PlaytestDeveloperTokenFilePath`）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestReceiverFakes.cs`（`FakeApi`・`FakeUploadApi` に新メソッド）
- Modify: `PlaytestSession` を作っている全テスト（`git grep -n "new PlaytestSession(" moorestech_client` で列挙して置き換える）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Credentials/PlaytestDeveloperTokenFileTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Credentials/DeveloperTokenSessionCredentialTest.cs`

**Interfaces:**
- Produces:
```csharp
public interface IPlaytestSessionCredential
{
    // 受け口へセッションを求める。送る前に失敗したら Failure を持つ
    // Asks the receiver for a session; carries Failure when it fails before sending
    UniTask<PlaytestCredentialPost> PostSessionAsync(IPlaytestReceiverApi api, CancellationToken token);
}

// 送ったか・送る前に失敗したかの2状態。種別ごとに要るものだけを持つ（判別 union）
// Two states: sent, or failed before sending; each subtype carries only what it needs
public abstract class PlaytestCredentialPost { }
public sealed class PlaytestCredentialSent : PlaytestCredentialPost
{
    public readonly PlaytestApiResult Response;
    public PlaytestCredentialSent(PlaytestApiResult response);
}
public sealed class PlaytestCredentialNotSent : PlaytestCredentialPost
{
    public readonly PlaytestSessionResult Failure;   // TicketUnavailable 等
    public PlaytestCredentialNotSent(PlaytestSessionResult failure);
}

// トークンファイルの読み取り結果（3状態）
// Result of reading the token file (three states)
public abstract class PlaytestDeveloperTokenRead { }
public sealed class PlaytestDeveloperTokenPresent : PlaytestDeveloperTokenRead { public readonly string Token; public PlaytestDeveloperTokenPresent(string token); }
public sealed class PlaytestDeveloperTokenAbsent : PlaytestDeveloperTokenRead { }
public sealed class PlaytestDeveloperTokenUnreadable : PlaytestDeveloperTokenRead { public readonly string Reason; public PlaytestDeveloperTokenUnreadable(string reason); }
```
  - 判別は C# のパターンマッチ（`switch (post) { case PlaytestCredentialSent sent: ... }`）で行う。派生クラスは上の2つ・3つだけで、外に増やさない（sealed）。
  - `SteamTicketSessionCredential` はコンストラクタで `IPlaytestSteamTicketProvider` を受け取る。既存 L55-68 の「チケット取得→null なら `new PlaytestCredentialNotSent(Failed(TicketUnavailable,...))`→`PostSessionAsync`→finally で Release」をそのまま移す。
  - `DeveloperTokenSessionCredential` はコンストラクタで `string developerToken` を受け取り、 `api.PostDeveloperSessionAsync(developerToken, token)`。
  - `IPlaytestReceiverApi.PostDeveloperSessionAsync(string developerToken, CancellationToken token)` → `POST {base}/v1/session/developer` body `{"token":"..."}`。JSON は `JsonConvert.SerializeObject(new { token = developerToken })` で組み、文字列連結しない。
  - `GameSystemPaths.PlaytestDeveloperTokenFilePath => Path.Combine(GameSystemDirectory, "PlaytestReceiver", "developer-token.txt")`。
  - `PlaytestDeveloperTokenFile`:
    - `PlaytestDeveloperTokenRead Read(string path)`。上の3派生のどれかを返す。
    - `void Write(string path, string token)` と `void Delete(string path)`。
    - `public const string TokenPattern = "^mdt_[0-9a-f]{64}$"`。
    - 形が `TokenPattern` に合わない中身は `PlaytestDeveloperTokenUnreadable("トークンの形が違う")` にする。前後の空白と改行は trim する。
    - ファイル読み書きはディスクIO境界なので、`IOException`・`UnauthorizedAccessException` を catch し、`PlaytestDeveloperTokenUnreadable(reason)` と `Debug.LogWarning` に変える。
- 既存の `PlaytestSession.AuthenticateOnceAsync` は、`var post = await _credential.PostSessionAsync(_api, token); if (post is PlaytestCredentialNotSent notSent) return notSent.Failure; var response = ((PlaytestCredentialSent)post).Response;`（switch 式で書いてもよい） に置き換える。以降（401→TicketRejected、200以外→Unreachable、Parse）は変えない。

- [x] **Step 1: テストを書く**

`PlaytestDeveloperTokenFileTest`（一時ディレクトリで行う）:
- Write→Read が Present で同じ値になる。
- ファイルが無いと Absent。
- `"hello"` を書くと Unreadable（理由に「形」を含む）。
- 末尾改行つきの正しい値は Present。
- Delete の後は Absent。

`DeveloperTokenSessionCredentialTest`:
- `FakeApi` に `DeveloperSessionResponses` を1件積み、`new PlaytestSession(api, new DeveloperTokenSessionCredential("mdt_"+new string('a',64)))` の `AuthenticateAsync` が Authenticated になる。
- 送られたトークンが記録されている。
- 401 なら `TicketRejected`。

- [x] **Step 2: 実装を書く**（上の Interfaces どおり）

- [x] **Step 3: コンパイルしテストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.PlaytestReceiver"`
Expected: ErrorCount 0 / 全 PASS（既存の PlaytestSession・Uploader 系も含む）。

- [x] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.PlaytestReceiver moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs
git commit -m "feat(playtest): セッションの資格情報を差し替え可能にし開発者トークンを読む"
```
（Unity が生成した .meta も一緒に add する）

### Task 4: 送信経路の決定と送信状況、MainGame 起動時の押し出し

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Delivery/PlaytestDeliveryRoute.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Delivery/PlaytestDeliveryRouteResolver.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Delivery/PlaytestDeliveryStatus.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Delivery/PlaytestDeliveryStatusBoard.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestUploadRunSummary.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Playtest/PlaytestBootUploadTrigger.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestUploadRunner.cs`（配布版ゲートを経路の決定に置き換える）
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestUploader.cs`（戻り値を `PlaytestUploadRunSummary` に。範囲 `PlaytestOutboxScope` を受け取る）
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestOutboxScanner.cs`（`ScanPending(directories, scope)`）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（`PlaytestBootUploadTrigger` を `IStartable` で登録）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Delivery/PlaytestDeliveryRouteResolverTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Upload/PlaytestUploadRunnerTest.cs`（開発者モードのケースを書き換える）、`moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Upload/PlaytestOutboxScannerTest.cs`（範囲の1ケースを追加）

**Interfaces:**
- Consumes: Task 3 の `IPlaytestSessionCredential`、`SteamTicketSessionCredential`、`DeveloperTokenSessionCredential`、`PlaytestDeveloperTokenFile.Read`、`GameSystemPaths.PlaytestDeveloperTokenFilePath`。
- Produces:
```csharp
public enum PlaytestOutboxScope { ReportsAndProgress, ReportsOnly }
public enum PlaytestLocalOnlyReason { DeveloperTokenMissing, DeveloperTokenUnreadable, SteamNotRunning }

// 送信経路（判別 union）。経路ごとに要るものだけを持つ
// Delivery route as a discriminated union; each route carries only what it needs
public abstract class PlaytestDeliveryRoute { public abstract PlaytestOutboxScope Scope { get; } }
public sealed class PlaytestSteamRoute : PlaytestDeliveryRoute { public override PlaytestOutboxScope Scope => PlaytestOutboxScope.ReportsAndProgress; }
public sealed class PlaytestDeveloperTokenRoute : PlaytestDeliveryRoute
{
    public readonly string DeveloperToken;
    public PlaytestDeveloperTokenRoute(string developerToken);
    public override PlaytestOutboxScope Scope => PlaytestOutboxScope.ReportsOnly;
}
public sealed class PlaytestLocalOnlyRoute : PlaytestDeliveryRoute
{
    public readonly PlaytestLocalOnlyReason Reason;
    public readonly string Detail;          // ログ用の理由
    private readonly PlaytestOutboxScope _scope;
    public PlaytestLocalOnlyRoute(PlaytestLocalOnlyReason reason, PlaytestOutboxScope scope, string detail);
    public override PlaytestOutboxScope Scope => _scope;
}

public static class PlaytestDeliveryRouteResolver
{
    // 起動形態とトークンの読み取り結果から経路を決める唯一の場所。起動形態は評価済みの2状態だけを受ける
    // The single place that picks the route; the launch kind arrives already evaluated as two states
    public static PlaytestDeliveryRoute Resolve(bool isDistribution, PlaytestDeveloperTokenRead tokenRead);
    public static PlaytestDeliveryRoute ResolveCurrent();     // Resolve(PlaytestLaunchProfile.Resolve() == Distribution, PlaytestDeveloperTokenFile.Read(GameSystemPaths.PlaytestDeveloperTokenFilePath))
}

// 画面に出す送信状況（判別 union）
// Delivery status shown on screen (discriminated union)
public abstract class PlaytestDeliveryStatus { }
public sealed class PlaytestDeliveryLocalOnly : PlaytestDeliveryStatus { public readonly PlaytestLocalOnlyReason Reason; public readonly int PendingCount; public PlaytestDeliveryLocalOnly(PlaytestLocalOnlyReason reason, int pendingCount); }
public sealed class PlaytestDeliveryPending : PlaytestDeliveryStatus { public readonly int PendingCount; public PlaytestDeliveryPending(int pendingCount); }
public sealed class PlaytestDeliveryRefused : PlaytestDeliveryStatus { public readonly int PendingCount; public PlaytestDeliveryRefused(int pendingCount); }
public sealed class PlaytestDeliveryAllSent : PlaytestDeliveryStatus { public static readonly PlaytestDeliveryAllSent Instance = new(); }

public static class PlaytestDeliveryStatusBoard
{
    private static readonly ReactiveProperty<PlaytestDeliveryStatus> _current = new(new PlaytestDeliveryPending(0));
    public static IReadOnlyReactiveProperty<PlaytestDeliveryStatus> Current => _current;
    internal static void Set(PlaytestDeliveryStatus status);   // 書き手は PlaytestUploadRunner だけ（internal で限定。裁定: このままでよい）
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    internal static void ResetOnPlayMode();                     // Pending(0) に戻す
}

// 1走行の結果。止まった理由を種類で返す
// Result of one upload run; why it stopped is returned as a kind
public enum PlaytestUploadRunEnd { Finished, SessionRefused }
public sealed class PlaytestUploadRunSummary
{
    public readonly PlaytestUploadRunEnd End;
    public readonly int Sent;
    public PlaytestUploadRunSummary(PlaytestUploadRunEnd end, int sent);
}
```
`Resolve` の規則:
- `#if UNITY_EDITOR || DEVELOPMENT_BUILD` の中では `isDistribution` を見ない（開発ビルドは build-info.json を持ち、Steam 起動中は Distribution と判定されるため。開発ビルドで Steam 経路に入ると進行記録がテスター集計に混ざる）。
- `#if` の外（配布ビルド）では、`isDistribution` なら `PlaytestSteamRoute`。
- `#if UNITY_EDITOR || DEVELOPMENT_BUILD` のとき（isDistribution に関係なく）:
  - Present なら `PlaytestDeveloperTokenRoute(token)`。
  - Absent なら `PlaytestLocalOnlyRoute(DeveloperTokenMissing, ReportsOnly, "...")`。
  - Unreadable なら `PlaytestLocalOnlyRoute(DeveloperTokenUnreadable, ReportsOnly, reason)`。
- 配布ビルドで `isDistribution` が偽（build-info はあるが Steam が起動していない）なら `PlaytestLocalOnlyRoute(SteamNotRunning, ReportsAndProgress, ...)`。
- `#if` は Resolve のこの分岐だけに置く。経路の型そのものは囲まない（テストから使えるように。裁定: 弱指摘④）。

ランナーの変更:
- コンストラクタは `PlaytestUploadRunner(IPlaytestReceiverApi api, PlaytestOutboxDirectories directories, IPlaytestSteamTicketProvider ticketProvider)` のまま。
- 静的な `_sharedSession` を「Steam 用の1本」と「開発者トークン用（トークン文字列ごとに作り直す）」に分けて持つ。後者は `_developerSession` と `_developerSessionToken` の静的フィールドにし、どちらも `ResetOnPlayMode` で消す。
- `RequestUpload()` の流れ:
  1. `var route = PlaytestDeliveryRouteResolver.ResolveCurrent();`
  2. `route is PlaytestLocalOnlyRoute local` なら、`PlaytestOutboxScanner.ScanPending(_directories, local.Scope).Count` を数えて `Set(new PlaytestDeliveryLocalOnly(local.Reason, count))` し、`Debug.Log($"[PlaytestReceiver] boxes stay local: {local.Detail}")` を出して return する。
  3. それ以外は既存の再入防止（`_running`/`_rerunRequested`）を通し、`RunAsync(route)` を走らせる。
- `RunAsync`:
  1. `Set(new PlaytestDeliveryPending(scanCount))`。セッションは `PlaytestSteamRoute` なら Steam 用の1本、`PlaytestDeveloperTokenRoute` ならトークンごとの1本を使う（switch で分ける）
  2. `var summary = await uploader.UploadPendingAsync(token);`
  3. 残数を数え直す。`summary.End == SessionRefused` なら `Set(new PlaytestDeliveryRefused(remaining))`、残数0なら `Set(PlaytestDeliveryAllSent.Instance)`、それ以外は `Set(new PlaytestDeliveryPending(remaining))`。
- `PlaytestUploader` のコンストラクタは `PlaytestOutboxScope scope` を受け取る。`UploadPendingAsync` は `ScanPending(_directories, _scope)` を使い、`SessionRefused` で止めたときは `End=SessionRefused`、最後まで回ったら `End=Finished` を返す。
- `PlaytestOutboxScanner.ScanPending(directories, scope)`: `ReportsOnly` のとき progress outbox を走査しない。
- `PlaytestBootUploadTrigger : IStartable`: `Start()` で `_uploadRequester.RequestUpload()` を1回呼ぶ。コメントに「MainGame 起動で溜まった箱を送る（Editor の直接再生にはタイトルの押し場が無い）」と書く。

- [x] **Step 1: テストを書く**

`PlaytestDeliveryRouteResolverTest`: 上の規則の各行を1ケースずつ。Editor（テストは Editor で走る）では `isDistribution=true` でも開発者経路になること（開発ビルドの Steam 起動中を表す）も含める。配布ビルド側の分岐は #if の外なので EditMode では通らない。これは Task 10 の配布ビルド smoke（既存）で確かめる旨をテストのコメントに書く。

`PlaytestUploadRunnerTest`:
- 既存の「配布版は送信時に認証し開発者モードは送らない」を2つに分ける。
- 開発者モード＋トークン無し → API 呼び出し0、`PlaytestDeliveryStatusBoard.Current.Value` が `PlaytestDeliveryLocalOnly` で `PendingCount==1`。
- 開発者モード＋トークン有り（一時ファイルにトークンを書く。経路はテストから差し込めるよう、ランナーに `internal void RequestUploadWith(PlaytestDeliveryRoute route)` を足し、`RequestUpload()` は `RequestUploadWith(ResolveCurrent())` とする）→ `PostDeveloperSessionAsync` が1回、報告箱1つが送られる。progress outbox に置いた箱は送られず、`UPLOADED` が付かない。状態は `PlaytestDeliveryAllSent`。
- 開発者トークンが 401 → `PlaytestDeliveryRefused` で `PendingCount==1`。

`PlaytestOutboxScannerTest`: `ReportsOnly` で progress の箱が出ない。

- [x] **Step 2: 実装を書く**（上の Interfaces どおり。ランナーのコンストラクタは変えないので、`PlaytestTitleGateView.cs` は触らない）

- [x] **Step 3: コンパイルしテストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client.Tests.PlaytestReceiver|PlaytestTitleGate"`
Expected: ErrorCount 0 / 全 PASS。

- [x] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.PlaytestReceiver moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver
git commit -m "feat(playtest): 送信経路を1か所で決め、開発機は報告だけを開発者トークンで送る"
```

### Task 5: 開発者トークンを設定するエディタ拡張

**Files:**
- Create: `moorestech_client/Assets/Scripts/Editor/Playtest/PlaytestDeveloperTokenWindow.cs`

**Interfaces:**
- Consumes: `PlaytestDeveloperTokenFile.Read/Write/Delete`、`GameSystemPaths.PlaytestDeveloperTokenFilePath`、`PlaytestReceiverClient(PlaytestReceiverConfig.BaseUrl).PostDeveloperSessionAsync`、`PlaytestSessionResponse.Parse`（internal なので、ウィンドウからは応答 JSON の `steamId` を `JObject.Parse` で読む。パース失敗は外部入力境界として catch し、表示する）。
- Produces: メニュー `moorestech/Playtest/Developer Token`。

画面:
- 現在の状態を `EditorGUILayout.HelpBox` で出す。表示は「未設定」「設定済み（mdt_…末尾6桁）」「読めない: 理由」のどれか。
- 貼り付け欄（`EditorGUILayout.PasswordField`）と「保存」ボタン。形が違えば保存せず HelpBox で理由を出す。
- 「削除」ボタン。
- 「接続確認」ボタン。押すと `PostDeveloperSessionAsync` を呼び、200 なら「受け口に dev-<機材名> として認識されました」、401 なら「受け口に拒否されました（失効か打ち間違い）」、到達不能なら詳細を出す。結果は `Debug.Log` にも残す。
- 発行手順の1行案内（Mac mini で `scripts/playtest/dev-token.sh issue <機材名>`）を出す。
- 非同期は `UniTask` を `.Forget()` で投げ、結果はフィールドに書いて `Repaint()` する。

- [x] **Step 1: 実装を書く**（前例 `Scripts/Editor/MapAuthoring/MapAuthoringWindow.cs` の `ShowWindow` 形）
- [x] **Step 2: コンパイルする**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0。

- [x] **Step 3: 動作を確かめる**

`uloop execute-dynamic-code` で `EditorWindow.GetWindow` によりウィンドウを開き、スクリーンショットで描画を確かめる（未設定の表示）。保存・削除はファイルの往復で確かめる。

- [x] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Editor/Playtest
git commit -m "feat(editor): 開発者トークンを貼り付け・接続確認するウィンドウ"
```

### Task 6: バグ報告画面に送信状況を出す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/PauseMenuTopic.cs`（`Delivery` を DTO に足し、`PlaytestDeliveryStatusBoard.Current` の変化で Publish する）
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/PlaytestDeliveryDto.cs`（`Kind`・`PendingCount`・`Reason`。`PlaytestDeliveryStatus` の派生を switch で kind 文字列と reason 文字列へ写す）
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`（`PauseMenuDataSchema.delivery`）
- Modify: `moorestech_web/webui/src/features/pauseMenu/BugReportForm.tsx`（送信状況の1行 `data-testid="bug-report-delivery"`）
- Modify: `Localization/localization.csv`（6キー×4言語）→ `cd moorestech_web/webui && npm run gen:i18n`
- Modify: `moorestech_web/webui/src/features/pauseMenu/BugReportFormTests/testSupport.ts`（Status 型と辞書）
- Modify（delivery 必須化の波及。`bugReport:` を持つ PauseMenu データ全部）: `moorestech_web/webui/src/features/pauseMenu/PauseMenuPanel.test.ts`、`moorestech_web/webui/src/bridge/transport/webSocketClient.test.ts`、`moorestech_web/webui/e2e/mock-host/topics/topicFixtures.ts`、`moorestech_web/webui/e2e/mock-host/topics/topicControls.ts`。着手時に `git grep -n "bugReport:" moorestech_web/webui` で取りこぼしが無いか確かめる
- Test: `moorestech_web/webui/src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts`、`moorestech_web/webui/src/bridge/contract/schemas/__tests__/ui.test.ts`（delivery の受理・拒否）、`moorestech_client/Assets/Scripts/Client.Tests/WebUiHost/PauseMenuTopicDeliveryTest.cs`（DTO の JSON 形。既存の WebUiHost テストの置き場に合わせる）

**Interfaces:**
- Consumes: Task 4 の `PlaytestDeliveryStatusBoard.Current`、`PlaytestDeliveryStatus`、`PlaytestLocalOnlyReason`。
- Produces（wire）:
```ts
const DeliveryStatusSchema = z.discriminatedUnion("kind", [
  z.object({ kind: z.literal("localOnly"), reason: z.enum(["developerTokenMissing", "developerTokenUnreadable", "steamNotRunning"]), pendingCount: z.number().int().nonnegative() }).strict(),
  z.object({ kind: z.literal("pending"), pendingCount: z.number().int().nonnegative() }).strict(),
  z.object({ kind: z.literal("refused"), pendingCount: z.number().int().nonnegative() }).strict(),
  z.object({ kind: z.literal("allSent") }).strict(),
]);
// PauseMenuDataSchema に delivery: DeliveryStatusSchema を足す（必須）
```
C# 側の DTO は kind ごとに要るフィールドだけを出す。`WebUiJson` の null 省略設定を確かめ、`allSent` に `pendingCount` が載らないようにする（strict で落ちるため）。既存 DTO の出し方に合わせ、kind ごとの DTO クラスを分けるか、null 省略で揃える。

文言（key / english / japanese。german・korean も同じ意味で埋める）:
- `ui.bugReport.deliveryLocalOnlyTokenMissing`: "Not sent: no developer token is set ({count} kept on this machine)" / "未送信: 開発者トークンが未設定です（この機材に{count}件保存）"
- `ui.bugReport.deliveryLocalOnlyTokenUnreadable`: "Not sent: the developer token cannot be read ({count} kept on this machine)" / "未送信: 開発者トークンを読めません（この機材に{count}件保存）"
- `ui.bugReport.deliveryLocalOnlySteamNotRunning`: "Not sent: Steam is not running ({count} kept on this machine)" / "未送信: Steamが起動していません（この機材に{count}件保存）"
- `ui.bugReport.deliveryPending`: "{count} report(s) waiting to be sent" / "送信待ち {count}件"
- `ui.bugReport.deliveryRefused`: "The receiver refused this machine; the token may be revoked ({count} waiting)" / "受け口に拒否されました。トークン失効の可能性があります（送信待ち{count}件）"
- `ui.bugReport.deliveryAllSent`: "All reports have been sent" / "すべての報告を送信しました"（裁定「送信待ちと送信完了を区別して見せる」に従い、完了も1行で出す）

BugReportForm: 既存の `describeStatus() ?? describeMissing()` の行とは別に、`describeDelivery()` の行を1本足す（送信可否と送信状況は独立なので混ぜない）。

- [x] **Step 1: テストを書く**

`deliveryStatus.test.ts`:
- 各 kind で `bug-report-delivery` の文言が日本語辞書どおりになる。件数が埋まる。
- `allSent` のとき「すべての報告を送信しました」が出る。
- 送信可否の行と同時に出る。

`ui.test.ts`:
- `allSent` に `pendingCount` があると拒否する。
- 未知の reason を拒否する。
- delivery 欠落を拒否する。

C# の `PauseMenuTopicDeliveryTest`: `PlaytestDeliveryStatusBoard.Set(...)` の各値で `GetSnapshotJsonAsync()` の `delivery` が上の形になる。`ResetOnPlayMode` で後始末する。

- [x] **Step 2: 実装を書く**
- [x] **Step 3: テストを通す**

Run: `cd moorestech_web/webui && npm run gen:i18n && npx vitest run`（全体。transport と mock-host の波及も見る） → `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PauseMenuTopic"`
Expected: 全 PASS / ErrorCount 0。

- [x] **Step 4: コミットする**

```bash
git add Localization/localization.csv moorestech_web/webui moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests
git commit -m "feat(webui): バグ報告画面に受け口への送信状況を出す"
```

### Task 7: 報告の確保時に git bundle と停滞診断を箱へ入れる

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Repository/BugReportCommitBundles.cs`
- Move: `BugReportRepositoryFiles.cs` と `RepositoryStateProbe.cs` を `Client.Game/InGame/BugReport/Repository/` へ移す（BugReport 直下は既に10ファイルを超えているため、新設ファイルの置き場を作る。移動は Unity の .meta を保つため `uloop execute-dynamic-code` で `AssetDatabase.MoveAsset` を使う）。
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Repository/BugReportRepositoryFiles.cs`（`WriteWorkingTreeState` の末尾で bundle を作る。引数に `bundleId` を足し、呼び出し元 `BugReportBundleWriter.cs:88` と `LastSession/CrashBundleWriter.cs:91` を更新）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Repository/RepositoryStateProbe.cs`（`TryGit` を internal にし、「作業ツリーを書き換えない」注記に例外 `refs/bugreport/*` の一時 ref を明記）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Diagnostics/TrainSyncDiagnosticsCopier.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Train/Network/Diagnostics/TrainSynchronizationDiagnosticDirectory.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameInteractionRegistration.cs:188`（同じ定義を使う）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportBundleWriter.cs`（L55-58 で停滞診断の置き場と再生開始時刻を主スレッドで読み、thread pool 内で copier を呼ぶ）
- Modify: `moorestech_server/Assets/Scripts/Game.Paths/BugReportBundleLayout.cs`（`CommitsBundleFileName="commits.bundle"`・`MasterCommitsBundleFileName="master-commits.bundle"`・`BundleStatusFileName="bundle-status.txt"`・`TrainSyncDiagnosticsDirectoryName="train-sync"`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/Repository/BugReportCommitBundlesTest.cs`、`moorestech_client/Assets/Scripts/Client.Tests/BugReport/Diagnostics/TrainSyncDiagnosticsCopierTest.cs`

**Interfaces:**
- Produces:
```csharp
public static class BugReportCommitBundles
{
    // 未pushコミットをbundleにし状態を1行ずつ書く。書式は受け側 prepare-run.sh と bug-report-auto-fix が読む契約
    // Bundles unpushed commits and writes one status line each; the format is the contract read by prepare-run.sh and bug-report-auto-fix
    public static void Write(string repoDirectory, string bundleId, BugReportManifest manifest, string repositoryRoot, string masterDataRoot);
}
public static class TrainSyncDiagnosticsCopier
{
    public const int MaxFiles = 8;
    public static void Copy(string sourceDirectory, DateTime playStartedAtUtc, string bundleDirectory, BugReportManifest manifest);
}
public static class TrainSynchronizationDiagnosticDirectory
{
    public static string Resolve();          // Path.Combine(Application.persistentDataPath, "Logs", "TrainSynchronization")。主スレッド専用
    public static DateTime PlayStartedAtUtc { get; private set; }  // SubsystemRegistration で DateTime.UtcNow を記録（実世界の日時の記録なので DateTime 可）
}
```
`BugReportCommitBundles.Write` は、ラベル `commits.bundle`（repo=repositoryRoot, commit=manifest.Repository?.Commit）と `master-commits.bundle`（repo=masterDataRoot, commit=manifest.MasterData?.Commit）について、`ship-outbox.sh` の `attach_bundle` と同じ順序で判定して1行ずつ書く:
1. commit が空 → `skipped-no-commit`
2. repo ディレクトリが無い → `skipped-no-repo (<path>)`
3. `for-each-ref --contains <commit> refs/remotes/origin/` が成功しorigin追跡refを返す → `not-needed-remote-has-commit`。受信側はbundle取込後も不足するSHAをoriginから直接取得する。別remoteだけの公開では省略せず自己完結bundleを生成する。
4. `cat-file -e <commit>^{commit}` が非0 → `failed-commit-not-found (<commit>)`
5. `update-ref refs/bugreport/<id> <commit>`
6. `bundle create <out> origin/master..refs/bugreport/<id>` が0 → `created`。非0なら out を消して `failed-bundle-create`。
7. 最後に `update-ref -d refs/bugreport/<id>`。どの経路でも消す。

補足:
- git の生 SHA では `bundle create` が「empty bundle」で拒否するので、一時 ref は要る。
- `failed-*` は `manifest.AddMissing("repo/<label>", 理由)` にも残す。
- `bundle-status.txt` の書き込みはディスクIO境界なので、`IsDiskFailure` で catch して AddMissing する。

`TrainSyncDiagnosticsCopier.Copy`:
- `sourceDirectory` が無ければ何もしない（silent-ok: 停滞が起きていない再生では置き場自体が無い / 箱を読む人は train-sync の不在を「停滞なし」と読む）。
- `stall-*.json` のうち `LastWriteTimeUtc >= playStartedAtUtc` を新しい順に最大8件、`logs/train-sync/` へコピーする。
- 9件目以降は `manifest.AddMissing("logs/train-sync", "停滞診断が多すぎるため新しい8件だけ入れた（全N件）")`。
- コピー失敗は AddMissing する。

- [x] **Step 1: テストを書く**

`BugReportCommitBundlesTest`:
- 一時ディレクトリに `git init --bare origin.git` と、それを clone した作業 repo を作る。初回コミットを push し、2つ目を push しない。
- その commit で Write すると `commits.bundle: created`。bundle を別の空 repo へ `git fetch <bundle> '+refs/bugreport/*:refs/bugreport/*'` すると commit が取れる。
- push 済みの commit なら `not-needed-remote-has-commit`。
- manifest の commit が null なら `skipped-no-commit`。
- 存在しない SHA なら `failed-commit-not-found` で、manifest.Missing に1件入る。
- 実行後に `refs/bugreport/*` が残っていない（`git for-each-ref refs/bugreport` が空）。
- git の起動はテスト内でも `ProcessStartInfo` で行う。

`TrainSyncDiagnosticsCopierTest`:
- 古い mtime の1件、新しい10件を置くと、新しい8件だけが入り、Missing が1件。
- 置き場が無ければ何も起きない。

- [x] **Step 2: 実装を書く**
- [x] **Step 3: コンパイルしテストを通す**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "BugReportCommitBundles|TrainSyncDiagnosticsCopier|BuildInfoReaderTest|BugReportBundleWriterIdentityTest|CrashBundleWriterTest"` → `bash scripts/bugreport/tests/test-prepare-run.sh`
Expected: 全 PASS。

- [x] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.Tests moorestech_server/Assets/Scripts/Game.Paths
git commit -m "feat(bugreport): 報告の確保時に未pushコミットのbundleと列車同期の停滞診断を箱へ入れる"
```

### Task 8: 運搬スクリプトを削除し、記述を新経路に合わせる

**Files:**
- Delete: `scripts/bugreport/ship-outbox.sh`、`scripts/bugreport/launchd/com.moorestech.bugreport-shipper.plist`、`scripts/bugreport/tests/test-ship-outbox.sh`
- Modify:
  - `scripts/bugreport/README.md`（運搬の設定手順を消す。開発機の設定は「Mac mini で `dev-token.sh issue <機材名>` → Editor の `moorestech/Playtest/Developer Token` に貼る」の2行にし、Task 9 の道具の使い方を載せる）
  - `scripts/bugreport/inbox-poller.sh:29-30`（「ship-outbox の原子性と対」→「enqueue-autofix.sh の原子的 mv と対」）
  - `scripts/playtest/enqueue-autofix.sh:53-54,95-99`（ship-outbox を前例に挙げたコメントを、前例の実体（このファイル内の同じ処理）に書き換える）
  - `moorestech_client/Assets/Scripts/Client.Tests/BugReport/LastSession/BundleManifestContract.cs:12-13`
  - `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Playtest/PlaytestReportAndProgressTest.cs:105`
  - `.agents/skills/bug-report-auto-fix/SKILL.md:27,80,94`（bundle-status.txt の書き手を「報告の確保時に Editor」にする）
  - `docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md`（状態行に「運搬と自動投入は ADR 0081 で置き換え」）
  - `docs/adr/0061-steam-closed-playtest-report-receiver-and-save-compat.md:18`（「開発者は rsync 経路」→「開発者は開発者トークンで同じ受け口へ（ADR 0081）」）
  - `docs/adr/0064-playtest-uploads-go-direct-to-r2-and-omit-generated-world-files.md:64`
  - `docs/bug-report-system-open-questions.md:42`

- [x] **Step 1: 削除と書き換えを行う**
- [x] **Step 2: 残りを確かめる**

Run: `git grep -n "ship-outbox\|bugreport-shipper\|rsync path\|SHIP_BLOCKED" -- ':!docs/superpowers/plans'`
Expected: 0件。

Run: `bash scripts/bugreport/tests/test-inbox-poller.sh && bash scripts/bugreport/tests/test-prepare-run.sh && bash scripts/playtest/tests/test-enqueue-autofix.sh`
Expected: 全 PASS。

- [x] **Step 3: コミットする**

```bash
git add -A scripts/bugreport scripts/playtest .agents/skills/bug-report-auto-fix docs moorestech_client/Assets/Scripts/Client.Tests
git commit -m "chore(bugreport): rsync運搬スクリプトを廃止し記述を受け口経路に合わせる"
```

### Task 9: トリアージスクリプトと報告の所在一覧

**Files:**
- Create: `scripts/bugreport/triage.py`（入口。引数解析と節の順序だけ）
- Create: `scripts/bugreport/triage_lib/__init__.py`、`scripts/bugreport/triage_lib/manifest_summary.py`、`scripts/bugreport/triage_lib/log_scan.py`、`scripts/bugreport/triage_lib/train_timeline.py`、`scripts/bugreport/triage_lib/rail_edges.py`、`scripts/bugreport/triage_lib/contact_sheet.py`
- Create: `scripts/playtest/find-reports.sh`、`scripts/playtest/lib/find_reports.py`
- Modify: `.agents/skills/ref-train-system/SKILL.md`（調査の入口に1行:「列車が見た目だけ止まるときは、まず unity.log の `[TrainSynchronization] Permanently waiting` と箱の `logs/train-sync/` を見る」）
- Modify: `.agents/skills/bug-report-auto-fix/SKILL.md`（箱を読む最初の一手として `python3 scripts/bugreport/triage.py <箱> --out <dir>` を1行足す）
- Test: `scripts/bugreport/tests/test_triage.py`、`scripts/playtest/tests/test-find-reports.sh`

**Interfaces:**
- `python3 scripts/bugreport/triage.py <box_dir> --out <dir>`: `<dir>/triage.md` と `<dir>/contact-sheet.png`（ffmpeg があれば）を書き、triage.md の内容を標準出力にも出す。箱そのものには書かない（箱は取り込み済みの記録だから）。
- 節の順と中身:
  1. **manifest**: kind, createdAt, description, steamId（無ければ missing の理由）, isEditor, platform, reportTick, repository.commit/branch/dirty, masterData.commit, buildInfo, missing 全件。
  2. **ログ**: `logs/unity.log` のうち `(?i)\b(error|exception|warning)\b` に当たる行を、同文の連続をまとめて件数つきで出す（最大200行）。さらに `[TrainSynchronization]` の行は全件出す。
  3. **列車**: `snapshots/tick_*.json` を tick 順に読み、`trainUnits` ごとに `tick, IsAutoRun, speed, distance, Diagram の要約, railPositionSaveData の先頭ノード` の表を出す。
     - speed と distance は `CurrentSpeedBits`/`AccumulatedDistanceBits` を `struct.unpack('<d', struct.pack('<q', bits))` で復号する。値の型を実データで確かめ、float32 なら `'<f'`/`'<i'` にする。
     - Diagram は登録ノード数と現在の index 程度。キー名は実データを読んで決める。
  4. **レール**: 最新スナップショットの `railSegments` を `A → B (Length, RailTypeGuid 先頭8桁, IsDrawable)` で一覧にし、件数を出す。
  5. **停滞診断**: `logs/train-sync/*.json` があればファイル名と `reason` を出す。
  6. **動画**: `ffmpeg -y -i video.mp4 -vf "fps=1/5,scale=320:-1,tile=4x4" -frames:v 1 <out>/contact-sheet.png`。ffmpeg が無い・動画が無いときは理由を1行出す。
- 実データでの確認には、この会話で調べた箱の写し `/private/tmp/claude-501/-Users-sakastudio-hermes-agent-data-repos-moorestech/cad84adf-2f91-49ab-82a5-13a01017adac/scratchpad/editor-boxes/20261015_152024_e771bf62` を使う。消えていれば `moorestech_logs/harness/playtest/reports/*/` の最新の箱を使う。
- `scripts/playtest/find-reports.sh [--limit N]`（既定 N=10）。3節を出す:
  1. **受け口の未取り込み**: `receiver_inbox_page` を全ページ読み、`kind steamId id readyAt`。
  2. **取り込み済み**: `moorestech_logs/harness/playtest/reports/*/*/` を mtime の新しい順に N 件。`id`、報告者（`ingest.json` の steamPersonaName か steamId）、`manifest.kind`、description の先頭40字、`AUTOFIX_QUEUED` の有無。
  3. **自動修正**: `harness/bug-report/inbox/` の箱と、`harness/bug-report/runs/` の新しい N 件の状態（ラン記録の結果ファイルがあればその値）。
  - 受け口に届かないときはその節に理由を出し、残りの節は続けて出す。

- [x] **Step 1: テストを書く**

`test_triage.py`:
- 一時ディレクトリに最小の箱を作る。manifest、`logs/unity.log` に Error 2行（同文）と Warning 1行と `[TrainSynchronization] Permanently waiting` 1行、`snapshots/tick_100.json` と `tick_200.json` に trainUnits 1件と railSegments 2件。
- triage.md に各節の見出しがある。Error は「×2」とまとまる。列車表が2行、レール2件。
- 動画無しの理由行が出る。
- 箱の中に新しいファイルが増えていない。

`test-find-reports.sh`:
- `CURL_CMD` を偽にして受け口が1件を返すようにする。偽の `LOGS` に2件の報告と1件の inbox を置く。3節が出て、新しい順になっている。
- 受け口が500のとき、理由行と残り2節が出る。

- [x] **Step 2: 実装を書く**（各ファイル200行未満）
- [x] **Step 3: テストと実データ確認を行う**

Run: `python3 -m pytest scripts/bugreport/tests/test_triage.py -q && bash scripts/playtest/tests/test-find-reports.sh && python3 scripts/bugreport/triage.py <上の実箱> --out "$TMPDIR/triage-check"`
Expected: PASS。実箱で列車表に `IsAutoRun` の True→False の変化が見え、`[TrainSynchronization] Permanently waiting` の行が出る。

- [x] **Step 4: コミットする**

```bash
git add scripts/bugreport scripts/playtest .agents/skills/ref-train-system .agents/skills/bug-report-auto-fix
git commit -m "feat(bugreport): Unity不要のトリアージと報告の所在一覧"
```

### Task 10: 受け口を反映し、Mac と Windows の Editor で通す

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/RealReceiver/DeveloperReportRealReceiverCheck.cs`（[Explicit] の実機確認テスト）
- 記録は `../moorestech_logs/harness/sdd/<branch>/verification.md` に書き、PR の報告に載せる。

- [x] **Step 1: 受け口を反映する**

1. `~/hermes-agent/data/HERMES-OPS.md` と `tools/playtest-receiver/README.md` の反映手順を読む。
2. その手順どおり `cd tools/playtest-receiver && pnpm test && pnpm run deploy` を行う（wrangler の認証プロファイルは README の手順に従う）。
3. 反映後に確かめる:
   - `curl -s -o /dev/null -w '%{http_code}' -X POST https://playtest.moores.tech/v1/session/developer -d '{"token":"x"}'` が 400。
   - 管理キーつきの `GET /v1/admin/dev-tokens` が 200。

- [x] **Step 2: トークンを発行する**

`scripts/playtest/dev-token.sh issue macmini` と `scripts/playtest/dev-token.sh issue winbuild` を実行する。値は会話・ログ・コミットに残さない。Mac mini のトークンは検証後も常設する（裁定）。Mac mini 上の全 worktree の Editor が共通の outbox から本番へ送る前提を verification.md に書く。Mac への受け渡しは一時ファイル経由で行い、ウィンドウへ貼り付けたら一時ファイルは消す。

- [ ] **Step 3: Mac の Editor で通す（unityプレイ録画テストを兼ねる）**

この worktree の Editor で、`unity-playmode-recorded-playtest` スキルにより録画つきで次を行う:
1. トークン無しで再生 → ポーズ → バグ報告画面に「未送信: 開発者トークンが未設定です（この機材にN件保存）」が出る。
2. 再生を止め、メニュー `moorestech/Playtest/Developer Token` のウィンドウからトークンを貼り付けて保存する（`uloop execute-dynamic-code` でウィンドウのフィールドへ値を入れ、保存ボタンと同じ処理を呼ぶ。保存後のスクリーンショットを残す）。続けて「接続確認」で `dev-macmini` が表示されることを確かめる。
3. 再生 → バグ報告を1件送る → 画面が「送信待ち」から「すべての報告を送信しました」になる。
4. `scripts/playtest/ingest.sh` を1回手で回す（常駐の5分周期を待ってもよい）。
5. `moorestech_logs/harness/playtest/reports/dev-macmini/<id>/` に manifest、`repo/bundle-status.txt`、`repo/commits.bundle`（このブランチは未 push のコミットを持つので created）、`logs/` があることを確かめる。`ingest.json` の steamPersonaName が「開発機 macmini」であること。
6. 進行記録の outbox に箱があっても、受け口の inbox に progress の dev 行が出ないこと。

- [ ] **Step 4: Windows の Editor で通す**

1. `moorestech_logs/claude/-Users-katsumi-moorestech/memory/windows-build-machine-winbuild.md` の手順で winbuild を起こし、`C:\dev\moorestech` をこのブランチの push 済みコミットへ合わせる（master ピン・PersonalAssets ピンも一致させる）。
2. トークンを `%APPDATA%\.moorestech\PlaytestReceiver\developer-token.txt` に置く。
3. Editor をバッチモードで起動し、EditMode テスト `DeveloperReportRealReceiverCheck` を `-runTests -testFilter` で回す。この [Explicit] テストは本 Task で `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/RealReceiver/DeveloperReportRealReceiverCheck.cs` に作り、Mac でも同じものを先に1回回す。中身は次の3つ:
   - `BugReportBundleWriter.WriteAsync` で実 outbox に箱を1つ書く。Editor 起源なので git bundle も作られる。
   - `PlaytestUploadRunner` の開発者経路で本番の受け口へ送る。
   - 箱に `UPLOADED` が付くことを確かめる。
4. Mac mini で ingest を回し、`reports/dev-winbuild/<id>/` に届いたこと、`repo/bundle-status.txt` があることを確かめる。
5. 終わったら winbuild のトークンファイルを残すかどうかを検証記録に書く（残してよい。漏れたら `dev-token.sh revoke winbuild`）。

- [ ] **Step 5: 記録してコミットする**

テストファイルと検証記録の場所を報告に書く。

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/RealReceiver
git commit -m "test(playtest): 本番受け口への開発者経路の実機確認テスト（Explicit）"
```

### Task 11: 全ブランチレビュー

- [ ] 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。レビュー指摘の反映が判定経路・条件式・評価時点に触れたら、Task 10 Step 3 のunityプレイ録画テストを反映後のバイナリで再実施する。

### Task 12: PR を作り、閉じられる状態にする

- [ ] セッション終了可能状態にすること。
  - pr-create スキルで PR を作成する。
  - master とのコンフリクトがあれば master をマージして解消し、コンパイルを確認して push する（解消の実作業は pr-create 経由で opus subagent に委譲される）。
  - 全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態であることを確認して終える。
  - 全実装タスク完了＝完了ではない。PR 未作成のまま終わるのは plan 未完了である。
  - PR 作成後に `moores-wt rm dev-bugreport-via-receiver` で worktree と Editor を畳む。

---

## 判断記録（ADR）

- 設計 ADR: `docs/adr/0081-developer-bug-reports-via-receiver-with-per-machine-tokens.md`。裁定は `.decisions/2026-10-16-開発者*.md` の5本。
- 出所: ユーザー裁定 2026-10-16「CFへ一本化しrsync廃止」「開発者トークンを機材ごとに発行」＋原文「エディタ拡張でセットできるようにして」「テスター報告と同じく人が選ぶ」、過去引用「それで」（提示7点の逐語原文は未保全のため引用単独の含意検査不能。現在の採用根拠は `.decisions/2026-10-16-開発者報告の実装planを共通制約として無人実行する.md`）、「Mac・Windows両方」「次回再生時に再送」。
- 出所: シミュレーター予測（確信高・拒否権つき前提宣言）2026-10-16。異常終了箱も開発者トークンで送る（Requirements 6）。
- 出所: agent前提（winbuild に Unity 6000.3.8f1 と `C:\dev\moorestech` が実在。判事が ssh で実測）。Windows の確認は winbuild の Editor のバッチモードで、[Explicit] テストを使って行う。GUI 操作は ssh 越しに難しいので、画面の確認は Mac の録画で行う。
- 出所: agent前提（ADR 0070 で manifest の steamId はローカルの値）。開発機の manifest の steamId は空のままにし、識別は受け口の置き場が担う。
- 出所: agent前提（git の実測: 生 SHA の `git bundle create` は「Refusing to create empty bundle」）。一時 ref `refs/bugreport/<id>` を使う。`RepositoryStateProbe` の「書き換えない」規則の例外として明記する。
- 出所: agent前提（前例 `Client.RemoteExec/Access/RemoteExecAccessFile.cs`）。トークンは EditorPrefs ではなく GameSystemPaths 配下のファイルに置く。開発ビルドも同じファイルを読める。
- 出所: agent前提。開発機の押し場は「MainGame 起動」と「報告送信」の2つ。タイトルゲートの `uploadsEnabled` は配布版の同意ゲートなので変えない。
- 出所: agent前提。送信状況は既存の `bugReport`（送信可否）とは別のフィールド・別の行にする。送信可否と送信状況は独立に変わるため。
- 出所: ユーザー裁定 2026-10-16 選択「失効を即時に効かせる」。`dev-` の prepare・complete ごとに台帳を head し、無ければ 401 にする。
- 出所: ユーザー裁定 2026-10-16 選択「置いたままにする」。Mac mini の開発者トークン（機材名 macmini）は検証後も常設する。
- 出所: agent前提。`BugReport/` 直下が10ファイルを超えているので、新設の bundle 処理は `BugReport/Repository/` に置き、既存の repository 系2ファイルも移す。
- unityプレイ録画テスト: 含める（Task 10 Step 3）。バグ報告画面の表示変化（ローカル保存のみ→送信待ち→すべて送信済み）はランタイムの UI なので録画で確かめる。
- 出所: シミュレーター予測（確信高・拒否権つき前提宣言）2026-10-16。Phase 2.6 強: `PlaytestCredentialPost`・`PlaytestDeveloperTokenRead`・`PlaytestDeliveryRoute`・`PlaytestDeliveryStatus` を sealed 派生の判別 union にする／`Resolve` は評価済みの `bool isDistribution` を受ける（NotEvaluated の例外を消す）／受け口の `refusesDeveloperProgress` は `PlaytestKind` 型を受ける。
- 出所: 検査7（plan と裁定の食い違い）。裁定「送信待ちと送信完了を区別して見せる」が優先なので、`allSent` も「すべての報告を送信しました」を1行出す（質問せず plan を裁定へ合わせた）。
- 出所: ユーザー裁定 2026-10-16 質問「送信状況の置き場は読む口と書く口を別の interface に分けるか」→ 選択「このままでよい」（書く口は internal で限定）。
- 出所: ユーザー裁定 2026-10-16 質問「弱い指摘11件のうち推奨4件だけ反映でよいか」→ 選択「推奨の4件だけ拾う」。①走行結果を `PlaytestUploadRunEnd` の種類で返す ②トークン一覧を readonly で返す ③16進変換と R2 の JSON 読みを1関数ずつにする ④制約文を「経路の選択を #if で閉じる」に直す。残り7件（`dev-` 接頭辞の文字列判定、`Pending(0)` 初期値、主スレッド前提、押し場3口、契約型の置き場、受け口の dev 分岐、bash の `dev-*`）はこのまま。

## 本PR外のリファクタ提案（Phase 2.6 第3バケツ。plan のタスクにはしない）

- `Client.Starter/Playtest/TitleGates/PlaytestTitleGates.cs:60,158` の `uploadsEnabled = distributionBuild && ...` ↔ 新設 `PlaytestDeliveryRouteResolver`。送信可否の判定がタイトルゲート側にも残る。
- `scripts/playtest/dashboard/collect_reports.py`・`digest_candidates.py:18 load_candidate_reports`・`digest_reporter.py:26 reporter_label` ↔ 新設 `scripts/playtest/lib/find_reports.py`。取り込み済みの列挙と報告者表示が重なる。実装時に既存関数を import して使えるなら使う（使えない理由があれば PR に書く）。
- `scripts/bugreport/read-manifest.py`・`scripts/playtest/digest_schema.py` ↔ 新設 `triage_lib/manifest_summary.py`。manifest の読みが重なる。


### 最終レビューでの実装既定

ADR 0081の「既存要件を満たす実装上の既定」に従い、JWTを現ハッシュへ束縛、失敗件数の表示、再生IDの診断分離、旧Editor箱の元SHA補完、失効競合409を反映する。人による投入後のinbox監視は継続する。重要ログはTask 9の最大200件を維持し、反復集約・エラー優先・末尾選択と元位置を加える。

大容量コミット資料は既存PlaytestReceiverConfigの上限（単体100MiB・箱256MiB・128ファイル）を参照し、本体/masterのbundleを子転送へ分割する。転送計画の各断片を先行送信し、親の公開時にWorkerが存在・長さを照合する。ingestとprepare-run/prepare-masterは長さ・SHA256を検証し元bundleへ復元する。未push資料は空repoへ復元できる自己完結bundleとして生成する。この実装既定は既存の資料運搬要求を満たすためのもので、新しいユーザー裁定ではない。合成入力による分割・再送・復元検査と、本番大容量送信・2Editor並行の実測を区別する。Windows実送信はライセンス阻害のまま未完了とする。

### 実行・検証の現在地

全ブランチレビューは60/60系統、最終bug-passは20/20系統と外部監査1本を回収し、修正後RefixのCriticalは0件。Unity関連440件とSaveLoad235件が成功、規約confirmed・比較候補は0件。最新Workerは本番へ反映済み。

最新の自己完結コミットbundleは4,461,955,168bytes・43断片。ローカル復元後のSHA256と空repoのbundle検証は成功。本番R2は現在約8.44GB使用しており、追加課金が生じ得るため上限0.30USDの承認待ちで大容量実送信を保留する。過去c53745の小箱Mac実送信・録画成功を最新分割経路の実測には数えない。Windowsはライセンス未認証exit198で未確認。Task10とTask11の反映後録画・本番一貫検証、Task12の終了可能状態は未達としてチェックを残す。
