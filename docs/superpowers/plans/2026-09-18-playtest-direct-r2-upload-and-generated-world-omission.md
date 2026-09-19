# プレイテスト受け口の R2 直接アップロードと生成ワールド地形の省略 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** プレイテストの報告・進行記録の箱を、Worker を経由せず署名付き URL で R2 へ直接アップロードし、生成ワールドの報告からは `map.json` と地形を省いて、Workers 無料プランのまま検証機 smoke の phase2 が通る状態にする。

**Architecture:** Worker（`tools/playtest-receiver`）は認証・許可リスト照合のあと、箱の宣言（相対パスと長さ）を受けてファイルごとの署名付き R2 PUT URL を発行し（`prepare`）、`complete` で R2 の実オブジェクトを宣言と照合してから READY を書く。バイト列は Worker を通らない。クライアント（`Client.PlaytestReceiver`）は prepare → 署名付き URL へ PUT（アイドル期限）→ complete の順で送り、Retryable の失敗は同一走行内で有限回再試行する。バンドル作成（`Client.Game/InGame/BugReport`）は生成ワールドなら `world.json` だけを入れ、再生ツール（`Server.Boot/Replay`）は `world.json` から worldId を算出して同梱スナップショット／共有キャッシュから地形を引き当てる。

**Tech Stack:** Cloudflare Workers（TypeScript, vitest + `@cloudflare/vitest-pool-workers`, miniflare R2）、R2 S3 互換 API の SigV4 presign（`aws4fetch`）、Unity C#（UniTask, Newtonsoft.Json, NUnit）。

## Requirements

設計ADR: `docs/adr/0064-playtest-uploads-go-direct-to-r2-and-omit-generated-world-files.md`（裁定の出所は同ADRの出所欄）。

- R1. Worker はアップロードのバイト列を中継しない。`PUT /v1/uploads/{kind}/{id}/{path}` は削除し、代わりに `POST /v1/uploads/{kind}/{id}/prepare`（Bearer）が宣言 `{files:[{path,bytes}]}` を受けてファイルごとの署名付き R2 PUT URL を返す。受入: `putUpload` と `FixedLengthStream` の使用が `src/` から消え、prepare のテストが署名付き URL を返す。
- R2. 署名はファイルごとの Content-Length を署名ヘッダに含めて発行し、1ファイル `maxFileBytes`（100MiB）の上限を維持する。受入: 発行 URL の `X-Amz-SignedHeaders` に `content-length` が含まれ、100MiB 超の宣言は prepare が 413 `too-large` で拒否する。
- R3. prepare は箱全体のファイル数（`maxBundleFiles` = 128）と合計バイト（`maxBundleBytes` = 256MiB）を検査し、超過は 413 で拒否して理由を `console.warn` に出す。受入: 129 ファイルの宣言と合計 256MiB+1 の宣言がそれぞれ 413 になり、warn が出る。
- R4. `complete` は R2 の実オブジェクトを列挙し、prepare が保存した宣言（`DECLARED`）と照合（存在と長さ）してから READY を書く。欠けや長さ違いがあれば 409 `incomplete` で欠損一覧を返し READY を書かない。READY の `files` は Worker が照合して確定した一覧（クライアント申告を使わない）。受入: 宣言どおり置いた箱は READY・索引が揃い、1ファイル欠けた箱は 409 で READY が無い。
- R5. Worker の署名用アクセスキーはシークレット `R2_ACCESS_KEY_ID` / `R2_SECRET_ACCESS_KEY`、アカウント ID は `[vars] R2_ACCOUNT_ID`。テスターへ渡るのは署名付き URL だけ。受入: `Env` と README と `wrangler.toml` に載り、テストは固定値で動く。
- R6. 署名付き URL の有効期限は `uploadUrlTtlSeconds` = 3600（トークンと同じ）。受入: URL の `X-Amz-Expires=3600`。
- R7. クライアントの `PlaytestUploader` は prepare → 署名付き URL へ PUT（Bearer なし、`Content-Length` 明示）→ complete の順で送る。進行記録も同じ経路。受入: `PlaytestUploaderTest` がこの順序をフェイクで固定する。
- R8. PUT の期限はサイズ比例をやめ、`uploadIdleTimeoutSeconds` = 60 秒バイトが進まないときだけ切るアイドル期限にする。受入: `PlaytestReceiverConfig.UploadTimeout(long)` / `UploadBytesPerSecondBudget` / `MaxUploadTimeoutSeconds` が消え、`UploadIdleTimeoutSeconds` が contract.json と一致するテストがある。
- R9. Retryable（回線断・5xx・408/429・SessionUnavailable・LocalUnreadableFile）の失敗は同一走行内で 3 回まで、10s/30s/60s 待って再試行し、送れたファイルは飛ばして続きから送る。上限超過で従来どおり `LogRetryable` して次回起動へ持ち越す（回数は数えない）。complete の 409 `incomplete` も Retryable として prepare からやり直す。受入: フェイク API が 2 回 503 → 3 回目 200 を返すと箱が Sent になり、4 回連続 503 で BoxDeferred になるテスト。待ち時間はテストで 0 に差し替えられる。
- R10. 生成ワールド（`world.json` の `mapMode` が `generated`）のバグ報告バンドルは `world/world.json` だけを入れ、`map.json` と `terrain/` を入れない。manifest に `worldDefinition: "generated-world-json-only"` を書く。手作りワールドは従来どおり全部入れ `worldDefinition: "full"`。受入: `BugReportWorldFilesCopierTest` に両ケース。
- R11. 再生ツール `BugReportBundleTools.ReplayCheck` は、箱の `world/` に `map.json` が無く `world.json` が generated なら、`WorldIdentity.CalculateGenerated(seed, generationMasterFingerprint, generatorVersion)` で worldId を出し、`WorldDataDirectory.ForBundledSnapshot(serverDataDirectory, worldId)` → `ForWorldCache(worldId)` の順で地形を引き当てる。`placementLedgerDigest` が一致しなければ理由付きで Reject。受入: `BugReportBundleWorldResolverTest`（一致・不一致・無い）。
- R12. `contract.json` に `maxBundleFiles` / `maxBundleBytes` / `uploadUrlTtlSeconds` / `uploadIdleTimeoutSeconds` を足し、Worker の `contract.ts` とクライアントの `PlaytestReceiverConfig` の定数がテストで一致する。
- R13. Mac mini 側の取り込み（`scripts/playtest/ingest.sh`）と検証（`verify-on-windows.sh`）は READY の `files` を読むだけなので変更しない。README（`tools/playtest-receiver/README.md`・`scripts/playtest/README.md`）の手順に R2 API トークンの作成とシークレット投入を足す。
- やらないこと: Workers の有料化、テスターごとの発行頻度制限、R2 のライフサイクル規則、クラッシュ報告経路の変更、`UPLOAD_FAILED`（恒久失敗 5 回）の扱いの変更、取り込み側の再現（自動修正ラン）での地形引き当て（`ReplayCheck` 以外の消費者は現状無い）。

## Global Constraints

- 1ファイル 200 行以下、`partial` 禁止、`Func<>` 禁止、try-catch は外部境界（HTTP・ファイル I/O・外部 JSON パース）に限り理由をコメントで明記。
- コメントは日本語1行 → 英語1行の対。自明なコメントは書かない。
- fail-closed の経路（拒否・保留・見送り）は必ず理由をログ（`console.warn` / `Debug.LogWarning`）へ出す。
- 受け口の契約値は `tools/playtest-receiver/contract.json` が正本。Worker 側 `src/contract.ts` とクライアント側 `PlaytestReceiverConfig` はテストで一致を固定する。
- R2 キーの安全性は `keys.ts` の `isSafeSegment` / `joinSafePath` だけを入口にする。
- 取り込み済み（ACKED）の箱への prepare / complete は書かずに冪等の成功を返す（plan D の裁定を維持）。
- 生成ワールドの判定は `WorldMetaJson.MapMode == "generated"`（大文字小文字無視）。同梱スナップショットの有無では判定しない。
- コミットは `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>` で終える。
- `.cs` を変えたタスクは `uloop compile --project-path ./moorestech_client` を通す。

---

## File Structure

### Worker（`tools/playtest-receiver`）

- Modify `src/env.ts` — `R2_ACCOUNT_ID` / `R2_ACCESS_KEY_ID` / `R2_SECRET_ACCESS_KEY` / `R2_BUCKET_NAME` を追加。
- Modify `src/contract.ts` — `MAX_BUNDLE_FILES` / `MAX_BUNDLE_BYTES` / `UPLOAD_URL_TTL_SECONDS` を contract.json から取り出す。
- Modify `contract.json` — 4 定数を追加。
- Create `src/presign.ts` — `aws4fetch` で R2 の PUT を presign する。
- Create `src/bundleDeclaration.ts` — 宣言の検証（パス・長さ・件数・総量）と `DECLARED` オブジェクトの読み書き。
- Modify `src/bundleMarkers.ts` — `DECLARED_MARKER` を追加。
- Modify `src/routes/uploads.ts` — `putUpload` を削除し `prepareUpload` を追加、`completeUpload` を照合型に書き換える（分割: 照合は `src/routes/uploadsVerify.ts` へ）。
- Create `src/routes/uploadsVerify.ts` — R2 列挙と宣言の照合、READY 要約の組み立て。
- Modify `wrangler.toml` — `[vars] R2_ACCOUNT_ID` と `R2_BUCKET_NAME`。
- Modify `vitest.config.ts` — テスト用の固定シークレット。
- Modify `package.json` — `aws4fetch` を dependencies へ。
- Modify `test/support/uploadsFixture.ts` — 宣言と直接 PUT の模擬ヘルパー。
- Delete `test/uploads/integrity.test.ts` / Modify `test/uploads/uploads.test.ts` / `test/uploads/validation.test.ts` — prepare / complete の契約に書き換え。
- Create `test/presign.test.ts`、`test/bundleDeclaration.test.ts`。
- Modify `test/contract.test.ts` — 新定数。
- Modify `README.md` — R2 API トークンの作成とシークレット投入。

### クライアント（`moorestech_client/Assets/Scripts`）

- Modify `Client.PlaytestReceiver/PlaytestReceiverConfig.cs` — サイズ比例の期限を消し `UploadIdleTimeoutSeconds` / `MaxBundleFiles` / `MaxBundleBytes` を足す。
- Modify `Client.PlaytestReceiver/Http/IPlaytestReceiverApi.cs` — `PutFileAsync` を `PostPrepareAsync` と `PutToSignedUrlAsync` に置き換える。
- Create `Client.PlaytestReceiver/Http/PlaytestPrepareResponse.cs` — prepare 応答の DTO とパース。
- Create `Client.PlaytestReceiver/Http/PlaytestDeclaredFile.cs` — 宣言1件（path/bytes）。
- Create `Client.PlaytestReceiver/Http/IdleTimeoutStream.cs` — 読み出しのたびにアイドル期限を延ばすストリーム。
- Modify `Client.PlaytestReceiver/Http/PlaytestReceiverClient.cs` — prepare / 署名付き URL への PUT / complete。
- Modify `Client.PlaytestReceiver/Upload/PlaytestAuthorizedCalls.cs` — `PlaytestPrepareCall` を追加、`PlaytestPutFileCall` を削除。
- Create `Client.PlaytestReceiver/Upload/PlaytestUploadRetrySchedule.cs` — 再試行の回数と待ち時間（テストで 0 秒に差し替え可能）。
- Create `Client.PlaytestReceiver/Upload/PlaytestBoxDeclaration.cs` — 箱の走査から宣言（送る／見送る）を作る。
- Modify `Client.PlaytestReceiver/Upload/PlaytestUploader.cs` — prepare → PUT → complete と再試行（200 行に収めるため宣言と要約は上記2ファイルへ出す）。
- Modify `Client.PlaytestReceiver/Upload/PlaytestUploadFailurePolicy.cs` — 409 `incomplete` を Retryable に分類。
- Modify `Client.PlaytestReceiver/Upload/PlaytestUploadRunner.cs` — `PlaytestUploader` の生成に既定の再試行表を渡す。
- Modify `Client.Game/InGame/BugReport/BugReportWorldFilesCopier.cs` — 生成ワールドは `world.json` のみ。
- Modify `Client.Game/InGame/BugReport/BugReportManifest.cs` — `WorldDefinition` フィールド。
- Modify `Client.Tests/PlaytestReceiver/PlaytestReceiverConfigTest.cs` / `PlaytestReceiverContractTest.cs` / `Upload/PlaytestUploaderTest.cs` / `PlaytestReceiverFakes.cs`、`Client.Tests/BugReport/Bundle/BugReportWorldFilesCopierTest.cs`。

### サーバー（`moorestech_server/Assets/Scripts`）

- Create `Server.Boot/Replay/BugReportBundleWorldResolver.cs` — 箱の `world/` から再生に使うワールドディレクトリを解決する。
- Modify `Server.Boot/Replay/BugReportBundleTools.cs` — `ReplayCheck` が resolver を使う。
- Modify `Game.Paths/BugReportBundleLayout.cs` — `WorldDefinitionFull` / `WorldDefinitionGeneratedWorldJsonOnly` 定数。
- Create `moorestech_server/Assets/Scripts/Tests/Server.Boot/Replay/BugReportBundleWorldResolverTest.cs`（既存の Server.Boot テスト置き場に合わせる。無ければ `Tests.Module/...` の Replay テストと同じディレクトリ）。

### 運用文書

- Modify `scripts/playtest/README.md` — 受け口の準備に R2 API トークンの作成を追加。

---

## Task 1: 契約値と Worker の環境（contract.json / contract.ts / env.ts / wrangler.toml / vitest.config.ts / package.json）

**Files:**
- Modify: `tools/playtest-receiver/contract.json`
- Modify: `tools/playtest-receiver/src/contract.ts`
- Modify: `tools/playtest-receiver/src/env.ts`
- Modify: `tools/playtest-receiver/wrangler.toml`
- Modify: `tools/playtest-receiver/vitest.config.ts`
- Modify: `tools/playtest-receiver/package.json`
- Modify: `tools/playtest-receiver/test/contract.test.ts`

**Interfaces:**
- Produces: `MAX_BUNDLE_FILES: number`（128）、`MAX_BUNDLE_BYTES: number`（268435456）、`UPLOAD_URL_TTL_SECONDS: number`（3600）、`UPLOAD_IDLE_TIMEOUT_SECONDS: number`（60）を `src/contract.ts` から export。`Env` に `R2_ACCOUNT_ID: string; R2_BUCKET_NAME: string; R2_ACCESS_KEY_ID: string; R2_SECRET_ACCESS_KEY: string;`。

- [ ] **Step 1: contract.json に4定数を足す**

```json
{
  "steamIdentity": "moorestech-playtest",
  "maxFileBytes": 104857600,
  "maxBundleFiles": 128,
  "maxBundleBytes": 268435456,
  "uploadUrlTtlSeconds": 3600,
  "uploadIdleTimeoutSeconds": 60,
  "reservedUploadSegments": ["READY", "ACKED", "DECLARED", "complete", "prepare"],
  "kinds": ["report", "progress"],
  "tokenTtlSeconds": 3600
}
```

- [ ] **Step 2: contract.ts で取り出す**

```ts
import contract from "../contract.json";

// 共有契約の値はcontract.jsonが正本。Worker側の定数はここから取り出すだけで、値をコードに書かない
// contract.json is the single source of the shared contract; the Worker only reads its constants here and never restates the values
export const STEAM_IDENTITY: string = contract.steamIdentity;
export const MAX_FILE_BYTES: number = contract.maxFileBytes;
export const MAX_BUNDLE_FILES: number = contract.maxBundleFiles;
export const MAX_BUNDLE_BYTES: number = contract.maxBundleBytes;
export const UPLOAD_URL_TTL_SECONDS: number = contract.uploadUrlTtlSeconds;
export const UPLOAD_IDLE_TIMEOUT_SECONDS: number = contract.uploadIdleTimeoutSeconds;
export const RESERVED_UPLOAD_SEGMENTS: ReadonlySet<string> = new Set(contract.reservedUploadSegments);
export const TOKEN_TTL_SECONDS: number = contract.tokenTtlSeconds;
```

（既存の `contract.ts` に無い export 名があればそのまま残す。既存の名前を変えない。）

- [ ] **Step 3: env.ts / wrangler.toml / vitest.config.ts / package.json**

```ts
// Workerのbindings。secretsはCLIで設定
// Bindings the Worker receives; secrets are provisioned with `wrangler secret put`
export interface Env {
  BUCKET: R2Bucket;
  STEAM_APP_ID: string;
  STEAM_WEB_API_KEY: string;
  SESSION_HMAC_SECRET: string;
  ADMIN_KEY: string;
  // 署名付きURLはS3互換APIで作る。アカウントIDとバケット名はvars、アクセスキーはsecrets
  // Presigned URLs come from the S3-compatible API; account id and bucket name are vars, the access key pair is secrets
  R2_ACCOUNT_ID: string;
  R2_BUCKET_NAME: string;
  R2_ACCESS_KEY_ID: string;
  R2_SECRET_ACCESS_KEY: string;
}
```

`wrangler.toml` の `[vars]` に足す（`STEAM_APP_ID` の下）:

```toml
[vars]
STEAM_APP_ID = "1958160"
# 署名付きURLのホスト（<account>.r2.cloudflarestorage.com）と、URLへ埋めるバケット名。バケットのbindingとは別に文字列で要る
# Host of the presigned URL (<account>.r2.cloudflarestorage.com) and the bucket name embedded in it; needed as strings apart from the binding
R2_ACCOUNT_ID = "a60ba10bddf2fc760d75cc378137e8d4"
R2_BUCKET_NAME = "moorestech-playtest"
```

`vitest.config.ts` の `miniflare.bindings` に足す:

```ts
        bindings: {
          STEAM_WEB_API_KEY: "test-steam-key",
          SESSION_HMAC_SECRET: "test-hmac-secret",
          ADMIN_KEY: "test-admin-key",
          R2_ACCESS_KEY_ID: "test-access-key-id",
          R2_SECRET_ACCESS_KEY: "test-secret-access-key",
        },
```

`package.json` に `"dependencies": { "aws4fetch": "^1.0.20" }` を足し `pnpm install` を実行する（lockfile をコミットに含める）。

- [ ] **Step 4: contract.test.ts に4定数の一致を足す**

```ts
import { describe, expect, it } from "vitest";
import contract from "../contract.json";
import { MAX_BUNDLE_BYTES, MAX_BUNDLE_FILES, UPLOAD_IDLE_TIMEOUT_SECONDS, UPLOAD_URL_TTL_SECONDS } from "../src/contract";

describe("contract constants", () => {
  it("箱単位の上限とURL期限とアイドル期限はcontract.jsonと一致する", () => {
    expect(MAX_BUNDLE_FILES).toBe(contract.maxBundleFiles);
    expect(MAX_BUNDLE_BYTES).toBe(contract.maxBundleBytes);
    expect(UPLOAD_URL_TTL_SECONDS).toBe(contract.uploadUrlTtlSeconds);
    expect(UPLOAD_IDLE_TIMEOUT_SECONDS).toBe(contract.uploadIdleTimeoutSeconds);
    expect(contract.maxBundleBytes).toBeGreaterThanOrEqual(contract.maxFileBytes);
  });
});
```

- [ ] **Step 5: テストを実行する**

Run: `cd tools/playtest-receiver && pnpm install && pnpm -s test`
Expected: 既存 96 件 + 新規が PASS（この時点では `routes/uploads.ts` は未変更なので全件通る）。

- [ ] **Step 6: コミットする**

```bash
git add tools/playtest-receiver/contract.json tools/playtest-receiver/src/contract.ts tools/playtest-receiver/src/env.ts tools/playtest-receiver/wrangler.toml tools/playtest-receiver/vitest.config.ts tools/playtest-receiver/package.json tools/playtest-receiver/pnpm-lock.yaml tools/playtest-receiver/test/contract.test.ts
git commit -m "feat(playtest-receiver): 直接アップロードの契約値とR2署名用の環境を足す (ADR 0064)"
```

---

## Task 2: 署名付き URL の発行（presign.ts）

**Files:**
- Create: `tools/playtest-receiver/src/presign.ts`
- Test: `tools/playtest-receiver/test/presign.test.ts`

**Interfaces:**
- Produces: `presignPut(env: Env, key: string, contentLength: number, now: Date): Promise<string>` — R2 S3 互換 API への PUT 用署名付き URL（`X-Amz-Expires=UPLOAD_URL_TTL_SECONDS`、`X-Amz-SignedHeaders=content-length;host`）。

- [ ] **Step 1: テストを書く**

```ts
import { describe, expect, it } from "vitest";
import { UPLOAD_URL_TTL_SECONDS } from "../src/contract";
import { presignPut } from "../src/presign";
import { workerEnv } from "./support/uploadsFixture";

describe("presignPut", () => {
  it("バケットとキーを含むPUT用の署名付きURLをTTLと署名ヘッダ付きで返す", async () => {
    const url = new URL(await presignPut(workerEnv, "reports/7656/20260913_120000_aaaa1111/frames/frame 1.jpg", 1234, new Date("2026-09-18T00:00:00Z")));
    expect(url.host).toBe(`${workerEnv.R2_ACCOUNT_ID}.r2.cloudflarestorage.com`);
    expect(decodeURIComponent(url.pathname)).toBe(`/${workerEnv.R2_BUCKET_NAME}/reports/7656/20260913_120000_aaaa1111/frames/frame 1.jpg`);
    expect(url.searchParams.get("X-Amz-Expires")).toBe(String(UPLOAD_URL_TTL_SECONDS));
    expect(url.searchParams.get("X-Amz-SignedHeaders")).toBe("content-length;host");
    expect(url.searchParams.get("X-Amz-Date")).toBe("20260918T000000Z");
    expect(url.searchParams.get("X-Amz-Signature")).toMatch(/^[0-9a-f]{64}$/);
  });

  it("Content-Lengthが違えば署名も違う", async () => {
    const a = await presignPut(workerEnv, "reports/7656/x/a.bin", 1, new Date("2026-09-18T00:00:00Z"));
    const b = await presignPut(workerEnv, "reports/7656/x/a.bin", 2, new Date("2026-09-18T00:00:00Z"));
    expect(new URL(a).searchParams.get("X-Amz-Signature")).not.toBe(new URL(b).searchParams.get("X-Amz-Signature"));
  });
});
```

- [ ] **Step 2: 実装する**

```ts
import { AwsClient } from "aws4fetch";
import { UPLOAD_URL_TTL_SECONDS } from "./contract";
import type { Env } from "./env";

// R2のS3互換APIに対するPUT用の署名付きURL。Content-Lengthを署名ヘッダに含め、宣言と違う長さのPUTをR2に拒否させる
// A presigned PUT for R2's S3-compatible API; content-length is a signed header so R2 rejects a PUT whose length differs from the declaration
export async function presignPut(env: Env, key: string, contentLength: number, now: Date): Promise<string> {
  const client = new AwsClient({
    accessKeyId: env.R2_ACCESS_KEY_ID,
    secretAccessKey: env.R2_SECRET_ACCESS_KEY,
    service: "s3",
    region: "auto",
  });
  const encodedKey = key.split("/").map(encodeURIComponent).join("/");
  const url = new URL(`https://${env.R2_ACCOUNT_ID}.r2.cloudflarestorage.com/${env.R2_BUCKET_NAME}/${encodedKey}`);
  url.searchParams.set("X-Amz-Expires", String(UPLOAD_URL_TTL_SECONDS));
  const signed = await client.sign(
    new Request(url.toString(), { method: "PUT", headers: { "content-length": String(contentLength) } }),
    { aws: { signQuery: true, datetime: toAmzDate(now), allHeaders: true } },
  );
  return signed.url;
}

// SigV4のX-Amz-Date形式（YYYYMMDDTHHMMSSZ）。テストで固定時刻を渡せるよう引数で受ける
// SigV4's X-Amz-Date form (YYYYMMDDTHHMMSSZ); the instant is a parameter so tests can pin it
function toAmzDate(now: Date): string {
  return now.toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "Z");
}
```

`aws4fetch` の `sign` は `signQuery: true` で query 署名を作り、`allHeaders: true` で `content-length` を `X-Amz-SignedHeaders` に含める。`datetime` は `YYYYMMDDTHHMMSSZ` 文字列。実装時に `X-Amz-SignedHeaders` の実際の値をテストで確かめ、`host` だけになる場合は `headers` に `host` を明示して両方が含まれる形へ寄せる。

- [ ] **Step 3: テストを実行する**

Run: `cd tools/playtest-receiver && pnpm -s test -- presign`
Expected: 2 件 PASS。

- [ ] **Step 4: コミットする**

```bash
git add tools/playtest-receiver/src/presign.ts tools/playtest-receiver/test/presign.test.ts
git commit -m "feat(playtest-receiver): R2 PUT の署名付きURLを Content-Length 込みで発行する"
```

---

## Task 3: 箱の宣言（bundleDeclaration.ts / bundleMarkers.ts）

**Files:**
- Create: `tools/playtest-receiver/src/bundleDeclaration.ts`
- Modify: `tools/playtest-receiver/src/bundleMarkers.ts`
- Test: `tools/playtest-receiver/test/bundleDeclaration.test.ts`

**Interfaces:**
- Produces:
  - `DECLARED_MARKER = "DECLARED"`、`declaredMarkerKey(kind, steamId, id): string`（`bundleMarkers.ts`）
  - `interface DeclaredFile { path: string; bytes: number }`
  - `type DeclarationCheck = { ok: true; files: DeclaredFile[] } | { ok: false; status: 400 | 413; error: string; detail: string }`
  - `parseDeclaration(body: unknown): DeclarationCheck` — 形・パス安全性・予約名・1ファイル上限・件数・総量を検査する（重複パスは 400）。
  - `writeDeclaration(bucket: R2Bucket, kind, steamId, id, files: DeclaredFile[]): Promise<void>` / `readDeclaration(bucket, kind, steamId, id): Promise<DeclaredFile[] | null>`

- [ ] **Step 1: bundleMarkers.ts に DECLARED を足す**

```ts
export const READY_MARKER = "READY";
export const ACKED_MARKER = "ACKED";
export const DECLARED_MARKER = "DECLARED";

export function declaredMarkerKey(kind: PlaytestKind, steamId: string, id: string): string {
  return `${bundlePrefix(kind, steamId, id)}/${DECLARED_MARKER}`;
}
```

- [ ] **Step 2: テストを書く**

```ts
import { describe, expect, it } from "vitest";
import { MAX_BUNDLE_BYTES, MAX_BUNDLE_FILES, MAX_FILE_BYTES } from "../src/contract";
import { parseDeclaration } from "../src/bundleDeclaration";

function files(count: number, bytes = 1) {
  return { files: Array.from({ length: count }, (_, i) => ({ path: `frames/frame_${i}.jpg`, bytes })) };
}

describe("parseDeclaration", () => {
  it("正しい宣言はそのまま通る", () => {
    const result = parseDeclaration({ files: [{ path: "manifest.json", bytes: 10 }, { path: "frames/frame 1.jpg", bytes: 20 }] });
    expect(result).toEqual({ ok: true, files: [{ path: "manifest.json", bytes: 10 }, { path: "frames/frame 1.jpg", bytes: 20 }] });
  });
  it("配列でない・path/bytesが欠ける・bytesが整数でない宣言は400", () => {
    expect(parseDeclaration({}).ok).toBe(false);
    expect(parseDeclaration({ files: [{ path: "a" }] })).toMatchObject({ ok: false, status: 400 });
    expect(parseDeclaration({ files: [{ path: "a", bytes: 1.5 }] })).toMatchObject({ ok: false, status: 400 });
    expect(parseDeclaration({ files: [{ path: "a", bytes: -1 }] })).toMatchObject({ ok: false, status: 400 });
  });
  it("逸脱パス・予約名・重複は400", () => {
    expect(parseDeclaration({ files: [{ path: "../x", bytes: 1 }] })).toMatchObject({ ok: false, status: 400, error: "bad-path" });
    expect(parseDeclaration({ files: [{ path: "READY", bytes: 1 }] })).toMatchObject({ ok: false, status: 400, error: "reserved-name" });
    expect(parseDeclaration({ files: [{ path: "a", bytes: 1 }, { path: "a", bytes: 2 }] })).toMatchObject({ ok: false, status: 400, error: "duplicate-path" });
  });
  it("1ファイル上限超・件数超・総量超は413", () => {
    expect(parseDeclaration({ files: [{ path: "a", bytes: MAX_FILE_BYTES + 1 }] })).toMatchObject({ ok: false, status: 413, error: "too-large" });
    expect(parseDeclaration(files(MAX_BUNDLE_FILES + 1))).toMatchObject({ ok: false, status: 413, error: "too-many-files" });
    expect(parseDeclaration({ files: [{ path: "a", bytes: MAX_FILE_BYTES }, { path: "b", bytes: MAX_FILE_BYTES }, { path: "c", bytes: MAX_BUNDLE_BYTES - 2 * MAX_FILE_BYTES + 1 }] })).toMatchObject({ ok: false, status: 413, error: "bundle-too-large" });
  });
  it("空の宣言は400（何も送らない箱はcompleteできない）", () => {
    expect(parseDeclaration(files(0))).toMatchObject({ ok: false, status: 400, error: "empty-declaration" });
  });
});
```

- [ ] **Step 3: 実装する**

```ts
import { declaredMarkerKey } from "./bundleMarkers";
import { MAX_BUNDLE_BYTES, MAX_BUNDLE_FILES, MAX_FILE_BYTES, RESERVED_UPLOAD_SEGMENTS } from "./contract";
import { joinSafePath, type PlaytestKind } from "./keys";

export interface DeclaredFile {
  path: string;
  bytes: number;
}

export type DeclarationCheck =
  | { ok: true; files: DeclaredFile[] }
  | { ok: false; status: 400 | 413; error: string; detail: string };

// 宣言の検査はここ1箇所。パスの安全性はkeys.tsに委ね、上限は契約値だけを見る
// The single declaration check; path safety is delegated to keys.ts and every limit comes from the contract
export function parseDeclaration(body: unknown): DeclarationCheck {
  const files = (body as { files?: unknown })?.files;
  if (!Array.isArray(files)) return reject(400, "bad-request", "files is not an array");
  if (files.length === 0) return reject(400, "empty-declaration", "no files declared");
  if (files.length > MAX_BUNDLE_FILES) return reject(413, "too-many-files", `${files.length} files exceeds ${MAX_BUNDLE_FILES}`);

  const seen = new Set<string>();
  const accepted: DeclaredFile[] = [];
  let total = 0;
  for (const entry of files) {
    const path = (entry as { path?: unknown })?.path;
    const bytes = (entry as { bytes?: unknown })?.bytes;
    if (typeof path !== "string" || typeof bytes !== "number" || !Number.isInteger(bytes) || bytes < 0) {
      return reject(400, "bad-request", `malformed entry: ${JSON.stringify(entry)}`);
    }
    const segments = path.split("/");
    if (joinSafePath(segments) === null) return reject(400, "bad-path", path);
    if (RESERVED_UPLOAD_SEGMENTS.has(segments[0] as string)) return reject(400, "reserved-name", path);
    if (seen.has(path)) return reject(400, "duplicate-path", path);
    if (bytes > MAX_FILE_BYTES) return reject(413, "too-large", `${path}: ${bytes} bytes exceeds ${MAX_FILE_BYTES}`);
    seen.add(path);
    total += bytes;
    if (total > MAX_BUNDLE_BYTES) return reject(413, "bundle-too-large", `${total} bytes exceeds ${MAX_BUNDLE_BYTES}`);
    accepted.push({ path, bytes });
  }
  return { ok: true, files: accepted };
}

function reject(status: 400 | 413, error: string, detail: string): DeclarationCheck {
  return { ok: false, status, error, detail };
}

// 宣言はcompleteの照合元としてR2に置く。クライアントの再申告を信じないための唯一の記録
// The declaration is stored in R2 as the reference complete verifies against; it is the only record, so the client's re-statement is never trusted
export async function writeDeclaration(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string, files: DeclaredFile[]): Promise<void> {
  await bucket.put(declaredMarkerKey(kind, steamId, id), JSON.stringify({ files }), { httpMetadata: { contentType: "application/json" } });
}

export async function readDeclaration(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string): Promise<DeclaredFile[] | null> {
  const object = await bucket.get(declaredMarkerKey(kind, steamId, id));
  if (object === null) return null;
  // 自分で書いたJSONだが、R2上のオブジェクトは外部入力として扱い、壊れていれば無かったことにして再prepareを促す
  // Although we wrote it, an R2 object is treated as external input; a broken one counts as absent so the client re-prepares
  try {
    const parsed = parseDeclaration(await object.json());
    return parsed.ok ? parsed.files : null;
  } catch {
    console.warn(`[upload] DECLARED of ${steamId}/${id} is not readable JSON; treating it as absent`);
    return null;
  }
}
```

- [ ] **Step 4: テストを実行する**

Run: `cd tools/playtest-receiver && pnpm -s test -- bundleDeclaration`
Expected: 5 件 PASS。

- [ ] **Step 5: コミットする**

```bash
git add tools/playtest-receiver/src/bundleDeclaration.ts tools/playtest-receiver/src/bundleMarkers.ts tools/playtest-receiver/test/bundleDeclaration.test.ts
git commit -m "feat(playtest-receiver): 箱の宣言の検査と DECLARED マーカーを足す"
```

---

## Task 4: prepare / complete の経路（uploads.ts / uploadsVerify.ts とテスト）

**Files:**
- Modify: `tools/playtest-receiver/src/routes/uploads.ts`
- Create: `tools/playtest-receiver/src/routes/uploadsVerify.ts`
- Modify: `tools/playtest-receiver/test/support/uploadsFixture.ts`
- Modify: `tools/playtest-receiver/test/uploads/uploads.test.ts`
- Modify: `tools/playtest-receiver/test/uploads/validation.test.ts`
- Delete: `tools/playtest-receiver/test/uploads/integrity.test.ts`（PUT 中継の長さ整合テスト。役割は R2 の署名と complete の照合へ移る）
- Modify: `tools/playtest-receiver/test/router.test.ts`（PUT が 410 になる行があれば更新）

**Interfaces:**
- Consumes: `presignPut`（Task 2）、`parseDeclaration` / `writeDeclaration` / `readDeclaration` / `DeclaredFile`（Task 3）。
- Produces（HTTP 契約）:
  - `POST /v1/uploads/{kind}/{id}/prepare`（Bearer、JSON `{files:[{path,bytes}]}`）→ 200 `{ outcome: "prepared", uploads: [{ path, url, bytes }], expiresInSeconds: 3600 }`。ACKED 済みなら 200 `{ outcome: "acked" }`（判別は `outcome` 一本。空配列や bool で表さない）。400/413 は `parseDeclaration` の `error`。
  - `POST /v1/uploads/{kind}/{id}/complete`（Bearer、JSON `{ manifest: string|null, skipped: [...] }`）→ 200 `{ ready: true, fileCount }`。宣言が無ければ 409 `{ error: "not-prepared" }`。欠け・長さ違いがあれば 409 `{ error: "incomplete", missing: [{path, expectedBytes, actualBytes|null}] }` で READY を書かない。ACKED 済みなら 200 `{ ready: true }`（書かない）。
  - `PUT /v1/uploads/...` → 410 `{ error: "direct-upload-required" }`（warn 付き）。
  - READY の本文: `{ kind, id, fileCount, files: string[], skipped, manifest }`（`files` は Worker が照合した一覧。取り込み側の読み方は変えない）。
- `verifyDeclaredObjects(bucket, kind, steamId, id, declared: DeclaredFile[]): Promise<{ present: string[]; missing: { path: string; expectedBytes: number; actualBytes: number | null }[] }>`（`uploadsVerify.ts`）。

- [ ] **Step 1: フィクスチャに宣言と直接 PUT の模擬を足す**

`test/support/uploadsFixture.ts` に追記:

```ts
import { bundlePrefix, type PlaytestKind } from "../../src/keys";

// 署名付きURLへの直接PUTは、テストではR2へ同じキーで置くことで模擬する（S3 APIはminiflareに無い）
// A direct PUT to the presigned URL is simulated by putting the same key into R2 (miniflare has no S3 API)
export async function putDirect(kind: PlaytestKind, steamId: string, id: string, path: string, body: string): Promise<void> {
  await workerEnv.BUCKET.put(`${bundlePrefix(kind, steamId, id)}/${path}`, body);
}

export function declaration(entries: Record<string, number>): string {
  return JSON.stringify({ files: Object.entries(entries).map(([path, bytes]) => ({ path, bytes })) });
}

export async function prepare(kind: PlaytestKind, id: string, body: string, steamId = STEAM_ID): Promise<Response> {
  return handle(new Request(`https://x/v1/uploads/${kind}/${id}/prepare`, { method: "POST", headers: { authorization: await bearer(steamId), "content-type": "application/json" }, body }), workerEnv, noNetwork);
}

export async function complete(kind: PlaytestKind, id: string, body = "{}", steamId = STEAM_ID): Promise<Response> {
  return handle(new Request(`https://x/v1/uploads/${kind}/${id}/complete`, { method: "POST", headers: { authorization: await bearer(steamId), "content-type": "application/json" }, body }), workerEnv, noNetwork);
}
```

（`handle` は `../../src/index` から import する。フィクスチャは現状 `handle` を export していないので、ここで `export { handle }` まで含めて足す。）

- [ ] **Step 2: uploads.test.ts を prepare/complete の契約に書き換える**

```ts
import { afterEach, describe, expect, it } from "vitest";
import { ackedMarkerKey, declaredMarkerKey, READY_MARKER } from "../../src/bundleMarkers";
import { bundlePrefix, pendingIndexKey } from "../../src/keys";
import { clean, complete, declaration, ID, prepare, putDirect, STEAM_ID, workerEnv } from "../support/uploadsFixture";

afterEach(clean);

describe("prepare", () => {
  it("宣言どおりのファイルごとに署名付きURLを返し、DECLAREDを置く", async () => {
    const response = await prepare("report", ID, declaration({ "manifest.json": 10, "frames/frame 1.jpg": 20 }));
    expect(response.status).toBe(200);
    const body = await response.json() as { outcome: string; uploads: { path: string; url: string; bytes: number }[]; expiresInSeconds: number };
    expect(body.outcome).toBe("prepared");
    expect(body.expiresInSeconds).toBe(3600);
    expect(body.uploads.map((u) => u.path)).toEqual(["manifest.json", "frames/frame 1.jpg"]);
    expect(new URL(body.uploads[1]!.url).host).toBe(`${workerEnv.R2_ACCOUNT_ID}.r2.cloudflarestorage.com`);
    expect(await workerEnv.BUCKET.get(declaredMarkerKey("report", STEAM_ID, ID))).not.toBeNull();
  });
  it("ACKED済みの箱へのprepareはURLを発行せずackedを返す", async () => {
    await workerEnv.BUCKET.put(ackedMarkerKey("report", STEAM_ID, ID), "");
    const response = await prepare("report", ID, declaration({ "a": 1 }));
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ outcome: "acked" });
    expect(await workerEnv.BUCKET.get(declaredMarkerKey("report", STEAM_ID, ID))).toBeNull();
  });
  it("Bearer無しは401", async () => {
    const response = await handle(new Request(`https://x/v1/uploads/report/${ID}/prepare`, { method: "POST", body: declaration({ a: 1 }) }), workerEnv, noNetwork);
    expect(response.status).toBe(401);
  });
});

describe("complete", () => {
  it("宣言した全ファイルが宣言どおりの長さで揃えばREADYと索引を書き、filesはWorkerが数えた一覧になる", async () => {
    await prepare("report", ID, declaration({ "manifest.json": 2, "a.bin": 3 }));
    await putDirect("report", STEAM_ID, ID, "manifest.json", "{}");
    await putDirect("report", STEAM_ID, ID, "a.bin", "abc");
    const response = await complete("report", ID, JSON.stringify({ manifest: "{}", skipped: [{ path: "x", reason: "too-large" }] }));
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ ready: true, fileCount: 2 });
    const ready = await workerEnv.BUCKET.get(`${bundlePrefix("report", STEAM_ID, ID)}/${READY_MARKER}`);
    expect(await ready!.json()).toEqual({ kind: "report", id: ID, fileCount: 2, files: ["a.bin", "manifest.json"], skipped: [{ path: "x", reason: "too-large" }], manifest: "{}" });
    expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, ID))).not.toBeNull();
  });
  it("欠けたファイルがあれば409 incompleteで欠損を返し、READYを書かない", async () => {
    await prepare("report", ID, declaration({ "manifest.json": 2, "a.bin": 3 }));
    await putDirect("report", STEAM_ID, ID, "manifest.json", "{}");
    const response = await complete("report", ID);
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ error: "incomplete", missing: [{ path: "a.bin", expectedBytes: 3, actualBytes: null }] });
    expect(await workerEnv.BUCKET.get(`${bundlePrefix("report", STEAM_ID, ID)}/${READY_MARKER}`)).toBeNull();
  });
  it("長さが宣言と違うファイルは欠損として返す", async () => {
    await prepare("report", ID, declaration({ "a.bin": 3 }));
    await putDirect("report", STEAM_ID, ID, "a.bin", "abcd");
    const response = await complete("report", ID);
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ error: "incomplete", missing: [{ path: "a.bin", expectedBytes: 3, actualBytes: 4 }] });
  });
  it("prepareしていない箱のcompleteは409 not-prepared", async () => {
    const response = await complete("report", ID);
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ error: "not-prepared" });
  });
  it("ACKED済みの箱のcompleteは書かずに冪等成功", async () => {
    await workerEnv.BUCKET.put(ackedMarkerKey("report", STEAM_ID, ID), "");
    const response = await complete("report", ID);
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ ready: true });
    expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, ID))).toBeNull();
  });
  it("宣言に無いオブジェクトはREADYのfilesに載らない", async () => {
    await prepare("report", ID, declaration({ "a.bin": 1 }));
    await putDirect("report", STEAM_ID, ID, "a.bin", "a");
    await putDirect("report", STEAM_ID, ID, "stray.bin", "zz");
    await complete("report", ID);
    const ready = await workerEnv.BUCKET.get(`${bundlePrefix("report", STEAM_ID, ID)}/${READY_MARKER}`);
    expect((await ready!.json() as { files: string[] }).files).toEqual(["a.bin"]);
  });
});

describe("legacy PUT", () => {
  it("PUT中継は410 direct-upload-required", async () => {
    const response = await handle(new Request(`https://x/v1/uploads/report/${ID}/a.bin`, { method: "PUT", headers: { authorization: await bearer(), "content-length": "1" }, body: "a" }), workerEnv, noNetwork);
    expect(response.status).toBe(410);
    expect(await response.json()).toEqual({ error: "direct-upload-required" });
  });
});
```

（`handle`・`bearer`・`noNetwork` の import を足す。既存の `uploads.test.ts` にある session トークン期限や 401 のテストのうち prepare/complete に読み替えられるものは残し、PUT 中継固有のもの（Content-Length 411/400/413、body 不一致）は削除する。`validation.test.ts` の「予約名」「逸脱パス」「不正 id」「未知 kind」は prepare 経由に読み替える: 例 `prepare("report", "..", ...)` → 400、`declaration({"READY": 1})` → 400 `reserved-name`。）

- [ ] **Step 3: uploadsVerify.ts を実装する**

```ts
import type { DeclaredFile } from "../bundleDeclaration";
import { ACKED_MARKER, DECLARED_MARKER, READY_MARKER } from "../bundleMarkers";
import { bundlePrefix, type PlaytestKind } from "../keys";

export interface MissingObject {
  path: string;
  expectedBytes: number;
  actualBytes: number | null;
}

export interface VerifiedObjects {
  present: string[];
  missing: MissingObject[];
}

// 宣言と実オブジェクトの照合。存在と長さだけを見る（内容の検査は取り込み側の仕事）。宣言に無いキーは無視する
// Verifies the declaration against real objects by presence and size only (content checks belong to ingest); undeclared keys are ignored
export async function verifyDeclaredObjects(bucket: R2Bucket, kind: PlaytestKind, steamId: string, id: string, declared: DeclaredFile[]): Promise<VerifiedObjects> {
  const prefix = `${bundlePrefix(kind, steamId, id)}/`;
  const sizes = new Map<string, number>();
  let cursor: string | undefined;
  do {
    const page = await bucket.list({ prefix, cursor, limit: 1000 });
    for (const object of page.objects) {
      const relative = object.key.slice(prefix.length);
      if (relative === READY_MARKER || relative === ACKED_MARKER || relative === DECLARED_MARKER) continue;
      sizes.set(relative, object.size);
    }
    cursor = page.truncated ? page.cursor : undefined;
  } while (cursor !== undefined);

  const present: string[] = [];
  const missing: MissingObject[] = [];
  for (const file of declared) {
    const actual = sizes.get(file.path);
    if (actual === file.bytes) present.push(file.path);
    else missing.push({ path: file.path, expectedBytes: file.bytes, actualBytes: actual ?? null });
  }
  present.sort();
  return { present, missing };
}
```

- [ ] **Step 4: uploads.ts を書き換える**

`routeUploads` のディスパッチを次に変える（`putUpload` と `FixedLengthStream` の使用は削除する）:

```ts
  const rest = segments.slice(4);
  if (rest.length === 1 && rest[0] === "prepare") {
    const denied = requireMethod(request, ["POST"], "upload prepare");
    if (denied !== null) return denied;
    return prepareUpload(request, env, kind, id);
  }
  if (rest.length === 1 && rest[0] === "complete") {
    const denied = requireMethod(request, ["POST"], "upload complete");
    if (denied !== null) return denied;
    return completeUpload(request, env, kind, id);
  }
  // 旧クライアントのPUT中継。バイト列はもう受けない（ADR 0064）。理由を返して再ビルドを促す
  // The old client's relayed PUT; bytes are no longer accepted (ADR 0064), so answer with the reason
  console.warn(`[upload] rejected a relayed PUT (direct upload required): /${segments.join("/")}`);
  return fail("direct-upload-required", 410);
```

`prepareUpload`:

```ts
async function prepareUpload(request: Request, env: Env, kind: PlaytestKind, id: string): Promise<Response> {
  const steamId = await authorize(request, env);
  if (typeof steamId !== "string") return steamId;

  if (await isAcked(env.BUCKET, kind, steamId, id)) {
    console.warn(`[upload] ignored prepare for an already acked bundle: ${steamId}/${id}`);
    return json({ outcome: "acked" });
  }

  // 本文は外部入力のJSON。壊れていれば400（理由はwarn）
  // The body is external JSON; a broken one is a 400 with the reason warned
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    console.warn(`[upload] prepare body of ${steamId}/${id} is not JSON`);
    return fail("bad-request", 400);
  }
  const declaration = parseDeclaration(body);
  if (!declaration.ok) {
    console.warn(`[upload] rejected the declaration of ${steamId}/${id}: ${declaration.error} (${declaration.detail})`);
    return fail(declaration.error, declaration.status);
  }

  await writeDeclaration(env.BUCKET, kind, steamId, id, declaration.files);
  const now = new Date();
  const uploads = [];
  for (const file of declaration.files) {
    const key = `${bundlePrefix(kind, steamId, id)}/${file.path}`;
    uploads.push({ path: file.path, bytes: file.bytes, url: await presignPut(env, key, file.bytes, now) });
  }
  return json({ outcome: "prepared", uploads, expiresInSeconds: UPLOAD_URL_TTL_SECONDS });
}
```

`completeUpload`:

```ts
async function completeUpload(request: Request, env: Env, kind: PlaytestKind, id: string): Promise<Response> {
  const steamId = await authorize(request, env);
  if (typeof steamId !== "string") return steamId;

  if (await isAcked(env.BUCKET, kind, steamId, id)) {
    console.warn(`[upload] ignored complete for an already acked bundle: ${steamId}/${id}`);
    return json({ ready: true });
  }
  const declared = await readDeclaration(env.BUCKET, kind, steamId, id);
  if (declared === null) {
    console.warn(`[upload] complete without a declaration: ${steamId}/${id}`);
    return fail("not-prepared", 409);
  }
  const verified = await verifyDeclaredObjects(env.BUCKET, kind, steamId, id, declared);
  if (verified.missing.length > 0) {
    console.warn(`[upload] ${steamId}/${id} is incomplete: ${verified.missing.map((m) => `${m.path}(${m.actualBytes ?? "absent"}/${m.expectedBytes})`).join(", ")}`);
    return json({ error: "incomplete", missing: verified.missing }, 409);
  }

  // 本文はクライアントの補足（manifest原文とskipped）。ファイル一覧は照合済みの側を使い、申告は使わない
  // The body is the client's supplement (raw manifest and skipped); the file list comes from the verified side, never from the claim
  let supplement: { manifest?: unknown; skipped?: unknown } = {};
  try {
    supplement = (await request.json()) as typeof supplement;
  } catch {
    console.warn(`[upload] complete body of ${steamId}/${id} is not JSON; storing READY without manifest/skipped`);
  }
  const summary = JSON.stringify({
    kind,
    id,
    fileCount: verified.present.length,
    files: verified.present,
    skipped: Array.isArray(supplement.skipped) ? supplement.skipped : [],
    manifest: typeof supplement.manifest === "string" ? supplement.manifest : null,
  });
  await env.BUCKET.put(`${bundlePrefix(kind, steamId, id)}/${READY_MARKER}`, summary, { httpMetadata: { contentType: "application/json" } });
  await env.BUCKET.put(pendingIndexKey(kind, steamId, id), "");
  return json({ ready: true, fileCount: verified.present.length });
}
```

`http.ts` の `json` が第2引数に status を取らない場合は `json(body, status = 200)` に拡張する（既存の呼び出しはそのまま）。import を整理し、`MAX_FILE_BYTES` と `RESERVED_UPLOAD_SEGMENTS` の import は uploads.ts から消える（bundleDeclaration.ts へ移る）。

- [ ] **Step 5: 全テストを実行する**

Run: `cd tools/playtest-receiver && pnpm -s test && pnpm -s exec tsc --noEmit`
Expected: 全件 PASS、型エラー 0。`grep -rn "FixedLengthStream\|putUpload" src/` が空。

- [ ] **Step 6: コミットする**

```bash
git add tools/playtest-receiver/src tools/playtest-receiver/test
git commit -m "feat(playtest-receiver): PUT中継を prepare/complete の直接アップロード契約に置き換える (ADR 0064)"
```

---

## Task 5: Worker の README と運用手順（R2 API トークン）

**Files:**
- Modify: `tools/playtest-receiver/README.md`
- Modify: `scripts/playtest/README.md`

- [ ] **Step 1: README に手順を足す**

`tools/playtest-receiver/README.md` の secrets 手順（手順2）に次を追記する:

```markdown
2b. R2 の署名付き URL 用に API トークンを作る（Cloudflare ダッシュボード → R2 → Manage R2 API Tokens → Create API token）。
    権限は「Object Read & Write」、対象バケットは `moorestech-playtest` だけに限定する。表示される Access Key ID と Secret Access Key を secrets に入れる:
    ```bash
    pnpm exec wrangler secret put R2_ACCESS_KEY_ID
    pnpm exec wrangler secret put R2_SECRET_ACCESS_KEY
    ```
    アカウント ID とバケット名は `wrangler.toml` の `[vars]`（`R2_ACCOUNT_ID` / `R2_BUCKET_NAME`）。
    クライアントは Worker が返す署名付き URL（`https://<account>.r2.cloudflarestorage.com/<bucket>/<key>?X-Amz-...`）へ直接 PUT する。
    Worker はバイト列を中継しない（Workers 無料プランの CPU 上限のため。ADR 0064）。
```

HTTP 契約の節を `prepare` / `complete` / PUT 410 に書き換える（Task 4 の Interfaces を逐語で載せる）。

`scripts/playtest/README.md` の「受け口」節に「R2 API トークン（Object Read & Write、バケット限定）を作り Worker の secrets へ入れる」の1行と、`verify-on-windows.sh` の READY 確認が変わらないことを1行足す。

- [ ] **Step 2: コミットする**

```bash
git add tools/playtest-receiver/README.md scripts/playtest/README.md
git commit -m "docs(playtest): 直接アップロードの契約と R2 API トークンの手順を書く"
```

---

## Task 6: クライアントの契約値とアイドル期限ストリーム

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/PlaytestReceiverConfig.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestOutboxScanner.cs:24`（`ReservedUploadSegments` を `{ "READY", "ACKED", "DECLARED", "complete", "prepare" }` へ。`PlaytestReceiverContractTest` の `AreEquivalent` が contract.json と突き合わせる）
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/IdleTimeoutStream.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestReceiverConfigTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestReceiverContractTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Http/IdleTimeoutStreamTest.cs`

**Interfaces:**
- Produces:
  - `PlaytestReceiverConfig.UploadIdleTimeoutSeconds = 60`、`MaxBundleFiles = 128`、`MaxBundleBytes = 256L * 1024 * 1024`、`UploadUrlTtlSeconds = 3600`。`UploadTimeout(long)` / `UploadBytesPerSecondBudget` / `MaxUploadTimeoutSeconds` は削除。
  - `sealed class IdleTimeoutStream : Stream`（`IdleTimeoutStream(Stream inner, CancellationTokenSource idle, TimeSpan idleTimeout)`）— `Read`/`ReadAsync` のたびに `idle.CancelAfter(idleTimeout)` を呼び直す読み取り専用ラッパ。

- [ ] **Step 1: Config を書き換える**

`PlaytestReceiverConfig` から `UploadBytesPerSecondBudget`・`MaxUploadTimeoutSeconds`・`UploadTimeout` を削除し、次を足す:

```csharp
        // 署名付きURLへのPUTは経過時間で切らず、バイトが進まない時間で切る（低速回線を殺さない。ADR 0064）
        // A PUT to the presigned URL is cut by stalled bytes, not elapsed time, so slow lines survive (ADR 0064)
        public const int UploadIdleTimeoutSeconds = 60;
        public const int UploadUrlTtlSeconds = 3600;
        public const int MaxBundleFiles = 128;
        public const long MaxBundleBytes = 256L * 1024 * 1024;
```

- [ ] **Step 2: IdleTimeoutStream を書く**

```csharp
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Client.PlaytestReceiver.Http
{
    // 読み出しが進むたびにアイドル期限を延ばす読み取り専用ストリーム。送信本文に噛ませて「進んでいる限り切らない」を実現する
    // A read-only stream that extends the idle deadline on every read; wrapped around the request body so a moving upload is never cut
    public sealed class IdleTimeoutStream : Stream
    {
        private readonly Stream _inner;
        private readonly CancellationTokenSource _idle;
        private readonly TimeSpan _idleTimeout;

        public IdleTimeoutStream(Stream inner, CancellationTokenSource idle, TimeSpan idleTimeout)
        {
            _inner = inner;
            _idle = idle;
            _idleTimeout = idleTimeout;
            _idle.CancelAfter(_idleTimeout);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            var read = _inner.Read(buffer, offset, count);
            if (read > 0) _idle.CancelAfter(_idleTimeout);
            return read;
        }

        public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            var read = await _inner.ReadAsync(buffer, offset, count, cancellationToken);
            if (read > 0) _idle.CancelAfter(_idleTimeout);
            return read;
        }

        public override bool CanRead => _inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
        public override void SetLength(long value) { throw new NotSupportedException(); }
        public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }

        protected override void Dispose(bool disposing)
        {
            if (disposing) _inner.Dispose();
            base.Dispose(disposing);
        }
    }
}
```

- [ ] **Step 3: テストを書く**

`PlaytestReceiverConfigTest.cs` の `UploadTimeout` に関するテスト（サイズ比例・上限 900 秒）を削除し、次に置き換える:

```csharp
        [Test]
        public void アイドル期限はトークン寿命より短く0より大きい()
        {
            Assert.Greater(PlaytestReceiverConfig.UploadIdleTimeoutSeconds, 0);
            Assert.Less(PlaytestReceiverConfig.UploadIdleTimeoutSeconds, PlaytestReceiverConfig.UploadUrlTtlSeconds);
        }
```

`PlaytestReceiverContractTest.cs`（contract.json を読んで比較している既存テストと同じ読み方）に足す:

```csharp
        [Test]
        public void 箱単位の上限とURL期限とアイドル期限が受け口と一致する()
        {
            var contract = ReadContract();
            Assert.AreEqual((int)contract["maxBundleFiles"], PlaytestReceiverConfig.MaxBundleFiles);
            Assert.AreEqual((long)contract["maxBundleBytes"], PlaytestReceiverConfig.MaxBundleBytes);
            Assert.AreEqual((int)contract["uploadUrlTtlSeconds"], PlaytestReceiverConfig.UploadUrlTtlSeconds);
            Assert.AreEqual((int)contract["uploadIdleTimeoutSeconds"], PlaytestReceiverConfig.UploadIdleTimeoutSeconds);
        }
```

（`ReadContract()` は既存テストが contract.json を `JObject` で読む補助の名前に合わせる。無ければ既存テストの読み方をそのまま繰り返す。）

`IdleTimeoutStreamTest.cs`:

```csharp
using System;
using System.IO;
using System.Threading;
using Client.PlaytestReceiver.Http;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Http
{
    public class IdleTimeoutStreamTest
    {
        [Test]
        public void 読み出しが進む間は期限が延び進まなければ切れる()
        {
            using var idle = new CancellationTokenSource();
            using var stream = new IdleTimeoutStream(new MemoryStream(new byte[64]), idle, TimeSpan.FromMilliseconds(150));
            var buffer = new byte[16];
            Thread.Sleep(100);
            Assert.AreEqual(16, stream.Read(buffer, 0, 16));
            Thread.Sleep(100);
            Assert.IsFalse(idle.IsCancellationRequested, "進んでいる間に切れてはいけない");
            Thread.Sleep(200);
            Assert.IsTrue(idle.IsCancellationRequested, "進まなくなったら切れる");
        }
    }
}
```

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client`（この時点で `PlaytestReceiverClient.cs` が `UploadTimeout` を参照してエラーになる。Task 7 で直すため、Task 6 と 7 は同じ worktree で連続して行い、コンパイルは Task 7 の末尾で通す）。

- [ ] **Step 5: コミットする**（Task 7 の後にまとめてよいが、ファイル単位で分けるならここ）

```bash
git add moorestech_client/Assets/Scripts/Client.PlaytestReceiver/PlaytestReceiverConfig.cs moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/IdleTimeoutStream.cs moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver
git commit -m "feat(playtest-receiver-client): 契約値を足しアップロード期限をアイドル期限にする (ADR 0064)"
```

---

## Task 7: クライアントの HTTP 面（prepare / 署名付き URL への PUT / complete）

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/IPlaytestReceiverApi.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/PlaytestDeclaredFile.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/PlaytestPrepareResponse.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http/PlaytestReceiverClient.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestAuthorizedCalls.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Http/PlaytestPrepareResponseTest.cs`

**Interfaces:**
- Produces:
  - `public sealed class PlaytestDeclaredFile { public readonly string Path; public readonly long Bytes; public readonly string AbsolutePath; }`（`AbsolutePath` はクライアント内部用でワイヤには乗せない）
  - `IPlaytestReceiverApi`:
    - `UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token)`（変更なし）
    - `UniTask<PlaytestApiResult> PostPrepareAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, IReadOnlyList<PlaytestDeclaredFile> files, CancellationToken token)`
    - `UniTask<PlaytestApiResult> PutToSignedUrlAsync(string signedUrl, string absoluteFilePath, long bytes, CancellationToken token)`
    - `UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, string supplementJson, CancellationToken token)`
  - `public enum PlaytestPrepareOutcome { Prepared, AlreadyAcked }`、`public sealed class PlaytestPrepareResponse { public readonly PlaytestPrepareOutcome Outcome; public readonly IReadOnlyList<PlaytestPreparedUpload> Uploads; public static bool TryParse(string body, out PlaytestPrepareResponse response, out string detail); }`、`public sealed class PlaytestPreparedUpload { public readonly string Path; public readonly string Url; public readonly long Bytes; }`
  - `internal sealed class PlaytestPrepareCall : IPlaytestAuthorizedCall`（`PlaytestPutFileCall` は削除）

- [ ] **Step 1: 型を書く**

`PlaytestDeclaredFile.cs`:

```csharp
namespace Client.PlaytestReceiver.Http
{
    // 箱の宣言1件。ワイヤに乗るのはPathとBytesで、AbsolutePathは送る側の手元だけで使う
    // One declared file; Path and Bytes go on the wire, AbsolutePath stays on the sending side
    public sealed class PlaytestDeclaredFile
    {
        public readonly string Path;
        public readonly long Bytes;
        public readonly string AbsolutePath;

        public PlaytestDeclaredFile(string path, long bytes, string absolutePath)
        {
            Path = path;
            Bytes = bytes;
            AbsolutePath = absolutePath;
        }
    }
}
```

`PlaytestPrepareResponse.cs`:

```csharp
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Client.PlaytestReceiver.Http
{
    public sealed class PlaytestPreparedUpload
    {
        public readonly string Path;
        public readonly string Url;
        public readonly long Bytes;

        public PlaytestPreparedUpload(string path, string url, long bytes)
        {
            Path = path;
            Url = url;
            Bytes = bytes;
        }
    }

    public enum PlaytestPrepareOutcome
    {
        Prepared,
        AlreadyAcked,
    }

    // prepare応答。判別は outcome 一本で、AlreadyAcked なら送るものは無く completeの冪等成功へ進む
    // The prepare response; the outcome alone discriminates, and AlreadyAcked means nothing to send before the idempotent complete
    public sealed class PlaytestPrepareResponse
    {
        public readonly PlaytestPrepareOutcome Outcome;
        public readonly IReadOnlyList<PlaytestPreparedUpload> Uploads;

        private PlaytestPrepareResponse(PlaytestPrepareOutcome outcome, IReadOnlyList<PlaytestPreparedUpload> uploads)
        {
            Outcome = outcome;
            Uploads = uploads;
        }

        // 受け口の応答は外部入力のJSON。形が違えば理由を返し、呼び出し側がRetryableとして扱う
        // The receiver's body is external JSON; a malformed one returns the reason so the caller treats it as retryable
        public static bool TryParse(string body, out PlaytestPrepareResponse response, out string detail)
        {
            response = null;
            detail = "";
            JObject root;
            try
            {
                root = JObject.Parse(body);
            }
            catch (Newtonsoft.Json.JsonException e)
            {
                detail = $"prepare response is not JSON: {e.Message}";
                return false;
            }
            var outcome = root.Value<string>("outcome");
            if (outcome == "acked")
            {
                response = new PlaytestPrepareResponse(PlaytestPrepareOutcome.AlreadyAcked, new List<PlaytestPreparedUpload>());
                return true;
            }
            if (outcome != "prepared" || !(root["uploads"] is JArray uploads))
            {
                detail = $"prepare response has an unknown outcome or no uploads array: {outcome}";
                return false;
            }
            var parsed = new List<PlaytestPreparedUpload>();
            foreach (var entry in uploads)
            {
                var path = entry.Value<string>("path");
                var url = entry.Value<string>("url");
                var bytes = entry.Value<long?>("bytes");
                if (path == null || url == null || bytes == null)
                {
                    detail = $"prepare response entry is malformed: {entry}";
                    return false;
                }
                parsed.Add(new PlaytestPreparedUpload(path, url, bytes.Value));
            }
            response = new PlaytestPrepareResponse(PlaytestPrepareOutcome.Prepared, parsed);
            return true;
        }
    }
}
```

- [ ] **Step 2: インターフェースと呼び出しを書き換える**

`IPlaytestReceiverApi.cs`:

```csharp
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Client.PlaytestReceiver.Http
{
    // 受け口への呼び出し面。バイト列は受け口ではなく署名付きURL（R2）へ送る
    // The receiver call surface; bytes go to the presigned URL (R2), not to the receiver
    public interface IPlaytestReceiverApi
    {
        UniTask<PlaytestApiResult> PostSessionAsync(string ticketHex, CancellationToken token);
        UniTask<PlaytestApiResult> PostPrepareAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, IReadOnlyList<PlaytestDeclaredFile> files, CancellationToken token);
        UniTask<PlaytestApiResult> PutToSignedUrlAsync(string signedUrl, string absoluteFilePath, long bytes, CancellationToken token);
        UniTask<PlaytestApiResult> PostCompleteAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, string supplementJson, CancellationToken token);
    }
}
```

`PlaytestAuthorizedCalls.cs`: `PlaytestPutFileCall` を削除し `PlaytestPrepareCall` を足す:

```csharp
    // 箱の宣言を送りURLを受ける。トークンの取り直しと再送はセッション側が行う
    // Sends the box declaration and receives the URLs; the session performs any token refresh and resend
    internal sealed class PlaytestPrepareCall : IPlaytestAuthorizedCall
    {
        private readonly IPlaytestReceiverApi _api;
        private readonly PlaytestOutboxBox _box;
        private readonly IReadOnlyList<PlaytestDeclaredFile> _files;

        public PlaytestPrepareCall(IPlaytestReceiverApi api, PlaytestOutboxBox box, IReadOnlyList<PlaytestDeclaredFile> files)
        {
            _api = api;
            _box = box;
            _files = files;
        }

        public UniTask<PlaytestApiResult> SendAsync(string bearerToken, CancellationToken token)
        {
            return _api.PostPrepareAsync(bearerToken, _box.Kind, _box.BundleId, _files, token);
        }
    }
```

`PlaytestReceiverClient.cs`: `PutFileAsync` を消し、次の2つを足す（`SendAsync(request, timeout, token)` は残し、署名付き URL への PUT だけアイドル期限版を使う）:

```csharp
        public UniTask<PlaytestApiResult> PostPrepareAsync(string bearerToken, PlaytestUploadKind kind, string bundleId, IReadOnlyList<PlaytestDeclaredFile> files, CancellationToken token)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/v1/uploads/{PlaytestUploadPath.ForPrepare(kind, bundleId)}")
            {
                Content = new StringContent(ComposeDeclarationBody(files), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            return SendAsync(request, TimeSpan.FromSeconds(PlaytestReceiverConfig.HttpTimeoutSeconds), token);
        }

        // 宣言のワイヤ表現。path と bytes だけを載せ、AbsolutePath は決して出さない（テストで固定）
        // The wire form of the declaration: only path and bytes, never AbsolutePath (pinned by a test)
        public static string ComposeDeclarationBody(IReadOnlyList<PlaytestDeclaredFile> files)
        {
            var declared = new JArray();
            foreach (var file in files) declared.Add(new JObject { ["path"] = file.Path, ["bytes"] = file.Bytes });
            return new JObject { ["files"] = declared }.ToString(Formatting.None);
        }

        // 署名付きURLへの直接PUT。Bearerは付けず（署名が権限）、Content-Lengthは署名に含まれるため必ず明示する
        // The direct PUT to the presigned URL: no bearer (the signature is the authority) and Content-Length is explicit because it is signed
        public UniTask<PlaytestApiResult> PutToSignedUrlAsync(string signedUrl, string absoluteFilePath, long bytes, CancellationToken token)
        {
            if (!File.Exists(absoluteFilePath))
            {
                Debug.LogWarning($"[PlaytestReceiver] {absoluteFilePath} disappeared before upload");
                return UniTask.FromResult(PlaytestApiResult.LocalUnreadableFile($"{absoluteFilePath} does not exist"));
            }
            return SendWithIdleTimeoutAsync(signedUrl, absoluteFilePath, bytes, token);
        }

        private static async UniTask<PlaytestApiResult> SendWithIdleTimeoutAsync(string signedUrl, string absoluteFilePath, long bytes, CancellationToken token)
        {
            using var idle = CancellationTokenSource.CreateLinkedTokenSource(token);
            var idleTimeout = TimeSpan.FromSeconds(PlaytestReceiverConfig.UploadIdleTimeoutSeconds);
            var content = new StreamContent(new IdleTimeoutStream(File.OpenRead(absoluteFilePath), idle, idleTimeout));
            content.Headers.ContentLength = bytes;
            content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            var request = new HttpRequestMessage(HttpMethod.Put, signedUrl) { Content = content };
            // ネットワーク送受信は外部境界。到達失敗とアイドル切れをTransportFailureへ隔離する
            // Network I/O is an external boundary; unreachability and an idle cut are isolated into TransportFailure
            try
            {
                using (request)
                using (var response = await Client.SendAsync(request, idle.Token))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    return PlaytestApiResult.Responded((int)response.StatusCode, body);
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (OperationCanceledException)
            {
                var message = $"no upload progress for {idleTimeout.TotalSeconds:0}s";
                Debug.LogWarning($"[PlaytestReceiver] PUT to R2 for {absoluteFilePath} {message}");
                return PlaytestApiResult.TransportFailure(message);
            }
            catch (Exception exception)
            {
                var message = $"{exception.GetType().Name}: {exception.GetBaseException().Message}";
                Debug.LogWarning($"[PlaytestReceiver] PUT to R2 for {absoluteFilePath} failed: {message}");
                return PlaytestApiResult.TransportFailure(message);
            }
        }
```

`PostCompleteAsync` は引数名を `supplementJson` にし本文をそのまま送る（挙動は変えない）。`PlaytestUploadPath` に `ForPrepare(kind, bundleId)`（`ForComplete` と同型で末尾が `prepare`）を足し、`ForFile` は削除する（`PlaytestUploadPathTest` の `ForFile` のテストは `ForPrepare` に読み替え、安全パス検査のテストは Task 8 の宣言側テストへ移す）。

- [ ] **Step 3: テストを書く（`PlaytestPrepareResponseTest.cs`）**

```csharp
using Client.PlaytestReceiver.Http;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Http
{
    public class PlaytestPrepareResponseTest
    {
        [Test]
        public void prepared応答はuploads配列をパースする()
        {
            var ok = PlaytestPrepareResponse.TryParse("{\"outcome\":\"prepared\",\"uploads\":[{\"path\":\"a/b.bin\",\"url\":\"https://r2/x\",\"bytes\":3}],\"expiresInSeconds\":3600}", out var response, out var detail);
            Assert.IsTrue(ok, detail);
            Assert.AreEqual(PlaytestPrepareOutcome.Prepared, response.Outcome);
            Assert.AreEqual(1, response.Uploads.Count);
            Assert.AreEqual("a/b.bin", response.Uploads[0].Path);
            Assert.AreEqual(3, response.Uploads[0].Bytes);
        }

        [Test]
        public void 宣言のワイヤ表現にAbsolutePathは乗らない()
        {
            var body = PlaytestReceiverClient.ComposeDeclarationBody(new[] { new PlaytestDeclaredFile("a/b.bin", 3, "C:/secret/a/b.bin") });
            Assert.AreEqual("{\"files\":[{\"path\":\"a/b.bin\",\"bytes\":3}]}", body);
            StringAssert.DoesNotContain("secret", body);
        }

        [Test]
        public void ackedの応答は空のuploadsで通る()
        {
            Assert.IsTrue(PlaytestPrepareResponse.TryParse("{\"outcome\":\"acked\"}", out var response, out _));
            Assert.AreEqual(PlaytestPrepareOutcome.AlreadyAcked, response.Outcome);
        }

        [Test]
        public void JSONでない_uploadsが無い_項目が欠ける応答は理由付きで失敗する()
        {
            Assert.IsFalse(PlaytestPrepareResponse.TryParse("not json", out _, out var d1)); StringAssert.Contains("not JSON", d1);
            Assert.IsFalse(PlaytestPrepareResponse.TryParse("{}", out _, out var d2)); StringAssert.Contains("outcome", d2);
            Assert.IsFalse(PlaytestPrepareResponse.TryParse("{\"outcome\":\"prepared\",\"uploads\":[{\"path\":\"a\"}]}", out _, out var d3)); StringAssert.Contains("malformed", d3);
        }
    }
}
```

- [ ] **Step 4: コンパイル**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PlaytestUploader.cs` と `PlaytestReceiverFakes`/`PlaytestUploaderTest` が旧 `PutFileAsync` を参照してエラー。Task 8 で直す（Task 6〜8 は同一 worktree で連続実行し、Task 8 末尾で ErrorCount 0 を確認する）。

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Http moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestAuthorizedCalls.cs moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Http
git commit -m "feat(playtest-receiver-client): prepare と署名付きURLへの直接PUTのHTTP面を足す"
```

---

## Task 8: アップローダーの流れと再試行（宣言・再試行表・Uploader・失敗分類・Runner・テスト）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestBoxDeclaration.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestUploadRetrySchedule.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestUploader.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestUploadFailurePolicy.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/PlaytestUploadRunner.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/PlaytestReceiverFakes.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Upload/PlaytestUploaderTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Upload/PlaytestUploadRunnerTest.cs`（コンストラクタ引数の追随のみ）

**Interfaces:**
- Consumes: Task 7 の `IPlaytestReceiverApi` / `PlaytestDeclaredFile` / `PlaytestPrepareResponse` / `PlaytestPrepareCall`。
- Produces:
  - `public sealed class PlaytestSkippedFile { public readonly string Path; public readonly string Reason; public readonly long Bytes; }`（JSON キー `path` / `reason` / `bytes`。`PlaytestBoxDeclaration.cs` と同じファイルに置く）、`public sealed class PlaytestBoxDeclaration { public readonly IReadOnlyList<PlaytestDeclaredFile> Files; public readonly IReadOnlyList<PlaytestSkippedFile> Skipped; public static PlaytestBoxDeclaration Build(PlaytestOutboxBox box); }` — 箱を走査し、予約名・安全でないパス・100MiB 超・件数超・総量超を見送り（`skipped` に理由）にして宣言を作る。
  - `public sealed class PlaytestUploadRetrySchedule { public readonly IReadOnlyList<TimeSpan> Delays; public static readonly PlaytestUploadRetrySchedule Default /* 10s,30s,60s */; public static readonly PlaytestUploadRetrySchedule Immediate /* 0,0,0 */; }`
  - `PlaytestUploader(IPlaytestReceiverApi api, PlaytestSession session, PlaytestOutboxDirectories directories, PlaytestUploadRetrySchedule retry)`
  - `PlaytestUploadFailurePolicy.Classify`: `Responded` の 409 は Retryable（complete の `incomplete` / `not-prepared` は prepare からやり直す）。

- [ ] **Step 1: 宣言と再試行表を書く**

`PlaytestBoxDeclaration.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Client.PlaytestReceiver.Http;
using Newtonsoft.Json;
using UnityEngine;

namespace Client.PlaytestReceiver.Upload
{
    // 見送ったファイル1件。completeの補足（skipped[]）にそのまま載る
    // One skipped file; rides in the complete supplement's skipped[] as is
    public sealed class PlaytestSkippedFile
    {
        [JsonProperty("path")] public readonly string Path;
        [JsonProperty("reason")] public readonly string Reason;
        [JsonProperty("bytes")] public readonly long Bytes;

        public PlaytestSkippedFile(string path, string reason, long bytes)
        {
            Path = path;
            Reason = reason;
            Bytes = bytes;
        }
    }

    // 箱の走査から宣言（送るもの）と見送り（送らないものと理由）を作る。送信前に分かる理由はすべてここで決まる
    // Builds the declaration (what to send) and the skips (what not to, with reasons) from the box; every reason knowable before sending is decided here
    public sealed class PlaytestBoxDeclaration
    {
        public readonly IReadOnlyList<PlaytestDeclaredFile> Files;
        public readonly IReadOnlyList<PlaytestSkippedFile> Skipped;

        private PlaytestBoxDeclaration(IReadOnlyList<PlaytestDeclaredFile> files, IReadOnlyList<PlaytestSkippedFile> skipped)
        {
            Files = files;
            Skipped = skipped;
        }

        public static PlaytestBoxDeclaration Build(PlaytestOutboxBox box)
        {
            var files = new List<PlaytestDeclaredFile>();
            var skipped = new List<PlaytestSkippedFile>();
            long total = 0;
            foreach (var absolute in PlaytestOutboxScanner.ListPayloadFiles(box.Directory))
            {
                var relative = PlaytestOutboxScanner.ToRelativePath(box.Directory, absolute);
                var reason = DescribeSkip(relative, absolute, files.Count, total, out var length);
                if (reason != null)
                {
                    Debug.LogWarning($"[PlaytestReceiver] skipping {relative}: {reason}");
                    skipped.Add(new PlaytestSkippedFile(relative, reason, length));
                    continue;
                }
                files.Add(new PlaytestDeclaredFile(relative, length, absolute));
                total += length;
            }
            return new PlaytestBoxDeclaration(files, skipped);

            #region Internal

            // 見送り理由の判定は PlaytestUploadPath.DescribeRejection 一本（受け口の parseDeclaration と同じ規則）。ここでは長さを測って渡すだけ
            // The rejection rules live in PlaytestUploadPath.DescribeRejection alone (mirroring the receiver's parseDeclaration); this only measures the length
            static string DescribeSkip(string relative, string absolute, int declaredCount, long declaredTotal, out long length)
            {
                length = new FileInfo(absolute).Length;
                return PlaytestUploadPath.DescribeRejection(relative, length, declaredCount, declaredTotal);
            }

            #endregion
        }
    }
}
```

`PlaytestUploadPath` に `public static string DescribeRejection(string relative, long bytes, int declaredCount, long declaredTotal)` を足す: 既存 `ForFile` が持っていた「各セグメントが安全か」の検査（`unsafe-path`）、`PlaytestOutboxScanner.ReservedUploadSegments` の先頭セグメント一致（`reserved-name`）、`MaxFileBytes`（`too-large`）、`MaxBundleFiles`（`too-many-files`）、`MaxBundleBytes`（`bundle-too-large`）を順に見て、拒否理由の文字列か null を返す。クライアント側の宣言規則の正本はこの1メソッドだけ（Worker 側の正本は `bundleDeclaration.ts`。言語が違うので片側ずつ）。`ForFile` は削除し、`PlaytestUploadPathTest` の安全パス検査は `DescribeRejection` のテスト（`unsafe-path` / `reserved-name` / `too-large` / `too-many-files` / `bundle-too-large` / null）に置き換える。

`PlaytestUploadRetrySchedule.cs`:

```csharp
using System;
using System.Collections.Generic;

namespace Client.PlaytestReceiver.Upload
{
    // 一過性の失敗を同一走行内で待って再試行する回数と間隔（ADR 0064）。テストは Immediate で待ちを消す
    // How many times and how long a transient failure is retried within one run (ADR 0064); tests use Immediate to remove the waits
    public sealed class PlaytestUploadRetrySchedule
    {
        public readonly IReadOnlyList<TimeSpan> Delays;

        public PlaytestUploadRetrySchedule(IReadOnlyList<TimeSpan> delays)
        {
            Delays = delays;
        }

        public static readonly PlaytestUploadRetrySchedule Default = new(new[] { TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(60) });
        public static readonly PlaytestUploadRetrySchedule Immediate = new(new[] { TimeSpan.Zero, TimeSpan.Zero, TimeSpan.Zero });
    }
}
```

- [ ] **Step 2: PlaytestUploader を書き換える**

`UploadOneAsync` を次に置き換える（`DescribeSkip` と `ComposeSummary` のローカル関数は宣言側へ移ったので消す。`UploadPendingAsync` と `UploadWithinBoxBoundaryAsync` はそのまま）:

```csharp
        private readonly PlaytestUploadRetrySchedule _retry;

        public PlaytestUploader(IPlaytestReceiverApi api, PlaytestSession session, PlaytestOutboxDirectories directories, PlaytestUploadRetrySchedule retry)
        {
            _api = api;
            _session = session;
            _directories = directories;
            _retry = retry;
        }

        // prepare→署名付きURLへPUT→completeを1試行とし、一過性の失敗は表の回数だけ待って同じ箱をやり直す。送れたファイルは次の試行で飛ばす
        // One attempt is prepare→PUT to the presigned URLs→complete; a transient failure waits per the schedule and retries the same box, skipping files already sent
        private async UniTask<BoxOutcome> UploadOneAsync(PlaytestOutboxBox box, CancellationToken token)
        {
            var declaration = PlaytestBoxDeclaration.Build(box);
            if (declaration.Files.Count == 0)
            {
                // 送るものが1つも無い箱は受け口へ行かずに恒久失敗として数える（偽のHTTP応答を合成しない）
                // A box with nothing sendable never reaches the receiver; it is counted as permanent without forging an HTTP response
                PlaytestUploadAttemptLog.Increment(box.Directory, $"nothing to send: every file was skipped ({declaration.Skipped.Count} skipped)");
                return BoxOutcome.BoxDeferred;
            }
            var sentPaths = new HashSet<string>();
            var attempt = 0;
            while (true)
            {
                var failure = await TryOnceAsync(box, declaration, sentPaths, token);
                if (failure == null)
                {
                    PlaytestUploadAttemptLog.MarkUploaded(box.Directory);
                    Debug.Log($"[PlaytestReceiver] uploaded {PlaytestUploadPath.KindSegment(box.Kind)}/{box.BundleId} ({sentPaths.Count} files, {declaration.Skipped.Count} skipped)");
                    return BoxOutcome.Sent;
                }
                var description = PlaytestUploadFailurePolicy.Describe(failure.What, failure.Result);
                if (PlaytestUploadFailurePolicy.Classify(failure.Result, failure.IsSignedPut) != PlaytestUploadFailureKind.Retryable)
                {
                    PlaytestUploadAttemptLog.Increment(box.Directory, description);
                    return BoxOutcome.BoxDeferred;
                }
                if (attempt >= _retry.Delays.Count)
                {
                    PlaytestUploadAttemptLog.LogRetryable(box.Directory, $"{description} (after {attempt} retries)");
                    return PlaytestUploadFailurePolicy.AbortsRun(failure.Result) ? BoxOutcome.RunAborted : BoxOutcome.BoxDeferred;
                }
                var delay = _retry.Delays[attempt];
                attempt++;
                Debug.LogWarning($"[PlaytestReceiver] {description}; retry {attempt}/{_retry.Delays.Count} in {delay.TotalSeconds:0}s");
                await UniTask.Delay(delay, DelayType.Realtime, PlayerLoopTiming.Update, token);
            }
        }

        private sealed class AttemptFailure
        {
            public readonly string What;
            public readonly PlaytestApiResult Result;
            public readonly bool IsSignedPut;

            public AttemptFailure(string what, PlaytestApiResult result) : this(what, result, false) { }

            public AttemptFailure(string what, PlaytestApiResult result, bool isSignedPut)
            {
                What = what;
                Result = result;
                IsSignedPut = isSignedPut;
            }
        }

        // 1試行。成功はnull、失敗は「どこで」と結果を返す。既に送れたファイルはprepareに再宣言しつつPUTだけ飛ばす（completeの照合は全宣言を見る）
        // One attempt: null on success, else where it failed and the result. Files already sent are re-declared to prepare but their PUT is skipped (complete verifies the whole declaration)
        private async UniTask<AttemptFailure> TryOnceAsync(PlaytestOutboxBox box, PlaytestBoxDeclaration declaration, HashSet<string> sentPaths, CancellationToken token)
        {
            var prepared = await _session.SendAuthorizedAsync(new PlaytestPrepareCall(_api, box, declaration.Files), token);
            if (!prepared.IsSuccess) return new AttemptFailure("prepare", prepared);
            if (!PlaytestPrepareResponse.TryParse(prepared.Body, out var response, out var detail))
            {
                return new AttemptFailure("prepare", PlaytestApiResult.TransportFailure(detail));
            }
            if (response.Outcome == PlaytestPrepareOutcome.Prepared)
            {
                foreach (var upload in response.Uploads)
                {
                    if (sentPaths.Contains(upload.Path)) continue;
                    var file = FindDeclared(declaration, upload.Path);
                    if (file == null)
                    {
                        Debug.LogWarning($"[PlaytestReceiver] the receiver returned an upload for an undeclared path: {upload.Path}");
                        return new AttemptFailure("prepare", PlaytestApiResult.TransportFailure($"undeclared path in prepare response: {upload.Path}"));
                    }
                    var put = await _api.PutToSignedUrlAsync(upload.Url, file.AbsolutePath, file.Bytes, token);
                    if (!put.IsSuccess) return new AttemptFailure(upload.Path, put, true);
                    sentPaths.Add(upload.Path);
                }
            }
            var completed = await _session.SendAuthorizedAsync(new PlaytestCompleteCall(_api, box, ComposeSupplement(box, declaration)), token);
            if (completed.IsSuccess) return null;
            // 409 は受け口が数えた欠損。欠けたパスを送信済みから外さないと次の試行で PUT が全部飛び同じ 409 を繰り返す（不明なら全部送り直す）
            // A 409 carries the receiver's count of what is missing; unless those paths leave the sent set, the next attempt skips every PUT and repeats the same 409 (when unknown, resend all)
            if (completed.Kind == PlaytestApiResultKind.Responded && completed.StatusCode == 409) ForgetMissing(completed.Body, sentPaths);
            return new AttemptFailure("complete", completed);
        }

        private static void ForgetMissing(string body, HashSet<string> sentPaths)
        {
            // 受け口の応答は外部入力のJSON。読めなければ全部送り直す側に倒す
            // The receiver's body is external JSON; when unreadable, fall back to resending everything
            try
            {
                var missing = JObject.Parse(body)["missing"] as JArray;
                if (missing == null) { sentPaths.Clear(); return; }
                foreach (var entry in missing) sentPaths.Remove(entry.Value<string>("path") ?? "");
            }
            catch (JsonException)
            {
                sentPaths.Clear();
            }
        }

        private static PlaytestDeclaredFile FindDeclared(PlaytestBoxDeclaration declaration, string path)
        {
            foreach (var file in declaration.Files)
            {
                if (file.Path == path) return file;
            }
            return null;
        }

        // completeの補足。manifest原文と見送り一覧だけを載せ、ファイル一覧は受け口が照合して決める
        // The complete supplement: only the raw manifest and the skips; the file list is settled by the receiver's verification
        private static string ComposeSupplement(PlaytestOutboxBox box, PlaytestBoxDeclaration declaration)
        {
            var manifestPath = Path.Combine(box.Directory, BugReportBundleLayout.ManifestFileName);
            return JsonConvert.SerializeObject(new
            {
                skipped = declaration.Skipped,
                manifest = File.Exists(manifestPath) ? File.ReadAllText(manifestPath) : null,
            });
        }
```

ファイルが 200 行を超える場合は `AttemptFailure` と `TryOnceAsync` を `PlaytestUploadAttempt.cs`（`internal sealed class PlaytestUploadAttempt`。コンストラクタで `IPlaytestReceiverApi`・`PlaytestSession` を受け、`UniTask<AttemptFailure> RunAsync(box, declaration, sentPaths, token)`）へ出す。

`PlaytestUploadFailurePolicy` に `Classify(PlaytestApiResult result, bool isSignedPut)` を足し（既存の `Classify(result)` は `Classify(result, false)` に委譲）、`ClassifyStatusCode(int statusCode, bool isSignedPut)` を次にする:

```csharp
                if (statusCode == 401 || (statusCode == 403 && !isSignedPut)) return PlaytestUploadFailureKind.Unauthorized;
                // 署名付きURLへのPUTの403は署名の期限切れか長さ不一致。prepareからやり直せば直るので一過性として扱う
                // A 403 from the presigned PUT means an expired signature or a length mismatch; redoing from prepare heals it, so it is transient
                if (statusCode == 403) return PlaytestUploadFailureKind.Retryable;
                // 409はcompleteの「揃っていない」「prepareが無い」。prepareからやり直せば直る
                // A 409 is complete's "incomplete" or "not-prepared"; redoing from prepare heals it
                if (statusCode == 408 || statusCode == 409 || statusCode == 429) return PlaytestUploadFailureKind.Retryable;
                if (400 <= statusCode && statusCode < 500) return PlaytestUploadFailureKind.PermanentForFile;
                return PlaytestUploadFailureKind.Retryable;
```

`PlaytestUploadRunner.RunAsync` の `new PlaytestUploader(_api, session, _directories)` を `new PlaytestUploader(_api, session, _directories, PlaytestUploadRetrySchedule.Default)` にする。

- [ ] **Step 3: フェイクとテストを書き換える**

`PlaytestReceiverFakes.cs` には `IPlaytestReceiverApi` のフェイクが **2つ**（L80 付近と L112 付近）ある。両方を新インターフェースに合わせ、`PutFileAsync` を消して `PostPrepareAsync` / `PutToSignedUrlAsync` を足す。呼び出し記録 `Calls`（`"prepare"` / `"put:<path>"` / `"complete"`）と、応答のキュー（`EnqueuePrepare(PlaytestApiResult)`、`EnqueuePut(string path, PlaytestApiResult)`、`EnqueueComplete(PlaytestApiResult)`。キューが空なら 200 の既定応答）を持たせる。prepare の既定応答本文は宣言から組み立てる:

```csharp
        private static string PrepareBodyFor(IReadOnlyList<PlaytestDeclaredFile> files)
        {
            var uploads = new JArray();
            foreach (var f in files) uploads.Add(new JObject { ["path"] = f.Path, ["url"] = $"https://r2.test/{f.Path}", ["bytes"] = f.Bytes });
            return new JObject { ["outcome"] = "prepared", ["uploads"] = uploads, ["expiresInSeconds"] = 3600 }.ToString(Formatting.None);
        }
```

`PlaytestUploaderTest.cs` を次の観点で書き直す（`PlaytestOutboxTestBoxes` で箱を作る既存ヘルパーを使う。`new PlaytestUploader(api, session, directories, PlaytestUploadRetrySchedule.Immediate)`）:

```csharp
        [Test]
        public async Task 箱はprepare_全ファイルPUT_completeの順で送られUPLOADEDが置かれる()
        {
            // 2ファイルの箱を作る（既存ヘルパー `PlaytestOutboxTestBoxes.Make(outbox, bundleId, params (Name, Content)[])`）
            var sent = await uploader.UploadPendingAsync(CancellationToken.None);
            Assert.AreEqual(1, sent);
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json", "put:a.bin", "complete" }, api.Calls);
            Assert.IsTrue(File.Exists(Path.Combine(box, "UPLOADED")));
        }

        [Test]
        public async Task 一過性の503は同一走行内で再試行され送れたファイルは飛ばされる()
        {
            api.EnqueuePut("a.bin", PlaytestApiResult.Responded(503, "{\"error\":\"1102\"}"));
            var sent = await uploader.UploadPendingAsync(CancellationToken.None);
            Assert.AreEqual(1, sent);
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json", "put:a.bin", "prepare", "put:a.bin", "complete" }, api.Calls);
        }

        [Test]
        public async Task 再試行の上限を超えた一過性失敗は数えずに次回へ持ち越す()
        {
            for (var i = 0; i < 4; i++) api.EnqueuePut("a.bin", PlaytestApiResult.Responded(503, ""));
            var sent = await uploader.UploadPendingAsync(CancellationToken.None);
            Assert.AreEqual(0, sent);
            Assert.IsFalse(File.Exists(Path.Combine(box, "UPLOADED")));
            Assert.IsFalse(File.Exists(Path.Combine(box, "UPLOAD_ATTEMPTS")));
            Assert.AreEqual(4, api.Calls.FindAll(c => c == "put:a.bin").Count);
        }

        [Test]
        public async Task completeのincompleteはprepareからやり直す()
        {
            api.EnqueueComplete(PlaytestApiResult.Responded(409, "{\"error\":\"incomplete\",\"missing\":[{\"path\":\"a.bin\",\"expectedBytes\":3,\"actualBytes\":null}]}"));
            var sent = await uploader.UploadPendingAsync(CancellationToken.None);
            Assert.AreEqual(1, sent);
            CollectionAssert.AreEqual(new[] { "prepare", "put:manifest.json", "put:a.bin", "complete", "prepare", "put:a.bin", "complete" }, api.Calls, "欠けたファイルだけ送り直す");
        }

        [Test]
        public async Task ackedの応答ならPUTせずcompleteだけ送る()
        {
            api.EnqueuePrepare(PlaytestApiResult.Responded(200, "{\"outcome\":\"acked\"}"));
            await uploader.UploadPendingAsync(CancellationToken.None);
            CollectionAssert.AreEqual(new[] { "prepare", "complete" }, api.Calls);
        }

        [Test]
        public async Task 到達不能は再試行後に走行ごと止める()
        {
            for (var i = 0; i < 4; i++) api.EnqueuePrepare(PlaytestApiResult.TransportFailure("unreachable"));
            // 箱を2つ作る
            var sent = await uploader.UploadPendingAsync(CancellationToken.None);
            Assert.AreEqual(0, sent);
            Assert.AreEqual(4, api.Calls.FindAll(c => c == "prepare").Count, "2箱目には進まない");
        }

        [Test]
        public async Task 100MiB超と予約名は宣言から外れcompleteのskippedに載る()
        {
            // "READY" という名のファイルと、FileInfo.Length を偽装できないため MaxFileBytes+1 のスパースファイル（FileStream.SetLength）を置く
            await uploader.UploadPendingAsync(CancellationToken.None);
            StringAssert.Contains("\"reason\":\"reserved-name\"", api.LastCompleteBody);
            StringAssert.Contains("\"reason\":\"too-large\"", api.LastCompleteBody);
            Assert.IsFalse(api.Calls.Contains("put:READY"));
        }
```

（既存テストのうち「LocalUnsafePath は PermanentForFile として数える」「恒久失敗を 5 回で UPLOAD_FAILED」は宣言側で見送りになるため、前者は削除、後者は `EnqueuePrepare(Responded(400, ...))` を 5 回で同じ検証に読み替える。）

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaytestReceiver\..*"`
Expected: 全件 PASS。

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.PlaytestReceiver moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver
git commit -m "feat(playtest-receiver-client): 箱を prepare→直接PUT→complete で送り一過性失敗を同一走行内で再試行する (ADR 0064)"
```

---

## Task 9: 生成ワールドのバンドルは world.json だけ（Copier / Manifest / Layout）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Paths/BugReportBundleLayout.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportManifest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportWorldFilesCopier.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/Bundle/BugReportWorldFilesCopierTest.cs`

**Interfaces:**
- Produces: `BugReportBundleLayout.WorldDefinitionFull = "full"`、`BugReportBundleLayout.WorldDefinitionGeneratedWorldJsonOnly = "generated-world-json-only"`、`BugReportManifest.WorldDefinition: string`（JSON キー `worldDefinition`）。

- [ ] **Step 1: 定数とフィールド**

`BugReportBundleLayout.cs` に足す:

```csharp
        // manifest.worldDefinition の値。生成ワールドは world.json だけを入れ、再現側は同梱スナップショットから地形を引き当てる（ADR 0064）
        // Values of manifest.worldDefinition; a generated world ships only world.json and the reproducer restores terrain from the bundled snapshot (ADR 0064)
        public const string WorldDefinitionFull = "full";
        public const string WorldDefinitionGeneratedWorldJsonOnly = "generated-world-json-only";
```

`BugReportManifest.cs` の `SnapshotTicks` の上に足す:

```csharp
        // world/ に何を入れたか。BugReportBundleLayout.WorldDefinition* のどれか
        // What world/ holds; one of BugReportBundleLayout.WorldDefinition*
        public string WorldDefinition;
```

- [ ] **Step 2: Copier を変える**

`CopyWorldDefinition` を次にする（`CopyTerrain` と `RequiresTerrain` は残し、`RequiresTerrain` を `IsGeneratedWorld` に改名して意味を合わせる）:

```csharp
        // 生成ワールドは world.json だけを入れる。地形は seed・指紋・生成器版から同じものが引き当てられる（ADR 0064）。手作りワールドは全部入れる
        // A generated world ships only world.json; its terrain is restored from seed, fingerprint and generator version (ADR 0064). A hand-made world ships everything
        private static void CopyWorldDefinition(BugReportCapturedData data, string bundleDirectory, BugReportManifest manifest)
        {
            if (string.IsNullOrEmpty(data.WorldRootDirectory))
            {
                manifest.AddMissing(BugReportBundleLayout.WorldDirectoryName, "記録時のワールドディレクトリが分からなかった");
                return;
            }

            var source = WorldDataDirectory.FromWorldRoot(data.WorldRootDirectory);
            var world = Path.Combine(bundleDirectory, BugReportBundleLayout.WorldDirectoryName);
            var destination = WorldDataDirectory.FromWorldRoot(world);
            Directory.CreateDirectory(world);
            CopyIfExists(source.WorldMetaFilePath, destination.WorldMetaFilePath, manifest);
            if (IsGeneratedWorld(source.WorldMetaFilePath, manifest))
            {
                manifest.WorldDefinition = BugReportBundleLayout.WorldDefinitionGeneratedWorldJsonOnly;
                return;
            }
            manifest.WorldDefinition = BugReportBundleLayout.WorldDefinitionFull;
            CopyIfExists(source.MapJsonFilePath, destination.MapJsonFilePath, manifest);
            CopyTerrain(source, destination, manifest);
        }
```

`IsGeneratedWorld` は旧 `RequiresTerrain` と同じ本体（world.json が無ければ false、読めなければ `AddMissing` して **false**（全部入れる側に倒す。地形を省く判断は読めた world.json だけに基づく））。mapMode の語は独自定数 `GeneratedMapMode` を消し、正本 `Game.MapGeneration.Transfer.WorldMapMode.Generated` を使う（`Client.Game` asmdef が `Game.MapGeneration` を参照済みか確認し、無ければ参照を足す）。`CopyTerrain` の `requiresTerrain` 引数は削除し、地形が無ければ `AddMissing(TerrainDirectoryName, "手作りワールドの地形ディレクトリが無かった")` にする。

- [ ] **Step 3: テスト**

`BugReportWorldFilesCopierTest.cs` に足す（既存の「生成ワールドの地形を全部コピーする」テストは「world.json だけ」に書き換える）:

```csharp
        [Test]
        public void 生成ワールドはworld_jsonだけを入れmanifestに省略を記録する()
        {
            WriteWorldMeta(worldRoot, "generated");
            File.WriteAllText(Path.Combine(worldRoot, "map.json"), "{}");
            Directory.CreateDirectory(Path.Combine(worldRoot, "terrain"));
            File.WriteAllBytes(Path.Combine(worldRoot, "terrain", "height_0_0.r16"), new byte[8]);

            BugReportWorldFilesCopier.Copy(DataFor(worldRoot), bundle, manifest);

            Assert.IsTrue(File.Exists(Path.Combine(bundle, "world", "world.json")));
            Assert.IsFalse(File.Exists(Path.Combine(bundle, "world", "map.json")));
            Assert.IsFalse(Directory.Exists(Path.Combine(bundle, "world", "terrain")));
            Assert.AreEqual(BugReportBundleLayout.WorldDefinitionGeneratedWorldJsonOnly, manifest.WorldDefinition);
            Assert.IsEmpty(manifest.Missing.FindAll(m => m.Item == "terrain" || m.Item == "map.json"));
        }

        [Test]
        public void 手作りワールドはmap_jsonと地形を全部入れる()
        {
            WriteWorldMeta(worldRoot, "template");
            File.WriteAllText(Path.Combine(worldRoot, "map.json"), "{}");
            Directory.CreateDirectory(Path.Combine(worldRoot, "terrain"));
            File.WriteAllBytes(Path.Combine(worldRoot, "terrain", "height_0_0.r16"), new byte[8]);

            BugReportWorldFilesCopier.Copy(DataFor(worldRoot), bundle, manifest);

            Assert.IsTrue(File.Exists(Path.Combine(bundle, "world", "map.json")));
            Assert.IsTrue(File.Exists(Path.Combine(bundle, "world", "terrain", "height_0_0.r16")));
            Assert.AreEqual(BugReportBundleLayout.WorldDefinitionFull, manifest.WorldDefinition);
        }

        [Test]
        public void world_jsonが読めなければ全部入れる側に倒し理由を残す()
        {
            File.WriteAllText(Path.Combine(worldRoot, "world.json"), "{broken");
            File.WriteAllText(Path.Combine(worldRoot, "map.json"), "{}");

            BugReportWorldFilesCopier.Copy(DataFor(worldRoot), bundle, manifest);

            Assert.IsTrue(File.Exists(Path.Combine(bundle, "world", "map.json")));
            Assert.AreEqual(BugReportBundleLayout.WorldDefinitionFull, manifest.WorldDefinition);
            Assert.IsNotEmpty(manifest.Missing.FindAll(m => m.Item == "world.json"));
        }
```

（`WriteWorldMeta(root, mapMode)` と `DataFor(root)` は既存テストのヘルパー名に合わせる。無ければ `world.json` に `{"seed":196,"mapMode":"<mode>","generatorVersion":"4.0.0","generationMasterFingerprint":"f"}` を書く小さな static を足す。）

- [ ] **Step 4: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.BugReport.Bundle.BugReportWorldFilesCopierTest"` → PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Paths/BugReportBundleLayout.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport moorestech_client/Assets/Scripts/Client.Tests/BugReport/Bundle/BugReportWorldFilesCopierTest.cs
git commit -m "feat(bug-report): 生成ワールドのバンドルは world.json だけを入れる (ADR 0064)"
```

---

## Task 10: 再生ツールが world.json から地形を引き当てる（BugReportBundleWorldResolver）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Boot/Replay/BugReportBundleWorldResolver.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Replay/BugReportBundleTools.cs:36-46`
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/Replay/BugReportBundleWorldResolverTest.cs`（namespace `Tests.CombinedTest.Server.Replay`、asmdef `Server.Tests`。既存の Replay テストと同じ置き場）

**Interfaces:**
- Consumes: `WorldIdentity.CalculateGenerated(int seed, string generationMasterFingerprint, string generatorVersion)`、`WorldDataDirectory.ForBundledSnapshot(serverDataDirectory, worldId)`、`WorldDataDirectory.ForWorldCache(worldId)`、`WorldMetaJson`。
- Produces: `public static class BugReportBundleWorldResolver { public static BugReportBundleWorldResolution Resolve(string bundleDirectory, string serverDataDirectory); }`、`public enum BugReportBundleWorldOutcome { Resolved, Rejected }`、`public abstract class BugReportBundleWorldResolution { public abstract BugReportBundleWorldOutcome Outcome { get; } }` とその2実装 `ResolvedWorld(WorldDataDirectory World)` / `RejectedWorld(string Reason)`

- [ ] **Step 1: テストを書く**

```csharp
using System.IO;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using Newtonsoft.Json;
using Server.Boot.Replay;

namespace Tests.CombinedTest.Server.Replay
{
    public class BugReportBundleWorldResolverTest
    {
        private string _root;

        [SetUp] public void SetUp() { _root = Path.Combine(Path.GetTempPath(), "bwr-" + Path.GetRandomFileName()); Directory.CreateDirectory(_root); }
        [TearDown] public void TearDown() { Directory.Delete(_root, true); }

        [Test]
        public void map_jsonがある箱はその箱のworldを使う()
        {
            var bundle = Bundle(meta: Meta("generated", "digest-a"), withMapJson: true);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Resolved, resolution.Outcome);
            Assert.AreEqual(Path.Combine(bundle, "world"), ((BugReportBundleWorldResolution.ResolvedWorld)resolution).World.Root);
        }

        [Test]
        public void 生成ワールドでmap_jsonが無ければ同梱スナップショットを引き当てる()
        {
            var meta = Meta("generated", "digest-a");
            var bundle = Bundle(meta, withMapJson: false);
            var serverData = ServerData();
            var worldId = WorldIdentity.CalculateGenerated(meta.Seed, meta.GenerationMasterFingerprint, meta.GeneratorVersion);
            var snapshot = WorldDataDirectory.ForBundledSnapshot(serverData, worldId);
            Directory.CreateDirectory(snapshot.Root);
            File.WriteAllText(snapshot.WorldMetaFilePath, JsonConvert.SerializeObject(meta));
            File.WriteAllText(snapshot.MapJsonFilePath, "{}");

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            Assert.AreEqual(BugReportBundleWorldOutcome.Resolved, resolution.Outcome);
            Assert.AreEqual(snapshot.Root, ((BugReportBundleWorldResolution.ResolvedWorld)resolution).World.Root);
        }

        [Test]
        public void 配置台帳ダイジェストが違うスナップショットは理由付きで拒む()
        {
            var meta = Meta("generated", "digest-a");
            var bundle = Bundle(meta, withMapJson: false);
            var serverData = ServerData();
            var snapshot = WorldDataDirectory.ForBundledSnapshot(serverData, WorldIdentity.CalculateGenerated(meta.Seed, meta.GenerationMasterFingerprint, meta.GeneratorVersion));
            Directory.CreateDirectory(snapshot.Root);
            File.WriteAllText(snapshot.WorldMetaFilePath, JsonConvert.SerializeObject(Meta("generated", "digest-b")));
            File.WriteAllText(snapshot.MapJsonFilePath, "{}");

            var resolution = BugReportBundleWorldResolver.Resolve(bundle, serverData);
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("placementLedgerDigest", ((BugReportBundleWorldResolution.RejectedWorld)resolution).Reason);
        }

        [Test]
        public void 同梱にも共有キャッシュにも無ければ理由付きで拒む()
        {
            var bundle = Bundle(Meta("generated", "digest-a"), withMapJson: false);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
            StringAssert.Contains("worldSnapshots", ((BugReportBundleWorldResolution.RejectedWorld)resolution).Reason);
        }

        [Test]
        public void 手作りワールドでmap_jsonが無い箱は拒む()
        {
            var bundle = Bundle(Meta("template", "digest-a"), withMapJson: false);
            var resolution = BugReportBundleWorldResolver.Resolve(bundle, ServerData());
            Assert.AreEqual(BugReportBundleWorldOutcome.Rejected, resolution.Outcome);
        }

        private static WorldMetaJson Meta(string mode, string digest)
        {
            return new WorldMetaJson { Seed = 196, GeneratorVersion = "4.0.0", Algorithm = "VanillaGenerator", MapMode = mode, GenerationMasterFingerprint = "fp", PlacementLedgerDigest = digest, TerrainResolution = 2049, TerrainTileCount = 9 };
        }

        private string Bundle(WorldMetaJson meta, bool withMapJson)
        {
            var bundle = Path.Combine(_root, "bundle");
            var world = WorldDataDirectory.FromWorldRoot(Path.Combine(bundle, "world"));
            Directory.CreateDirectory(world.Root);
            File.WriteAllText(world.WorldMetaFilePath, JsonConvert.SerializeObject(meta));
            if (withMapJson) File.WriteAllText(world.MapJsonFilePath, "{}");
            return bundle;
        }

        private string ServerData()
        {
            var dir = Path.Combine(_root, "serverData");
            Directory.CreateDirectory(dir);
            return dir;
        }
    }
}
```

（`ForWorldCache` は `GameSystemPaths` の共有キャッシュを指すため、テストでは同梱側だけを用意する。共有キャッシュへの分岐は「同梱に無ければ `ForWorldCache(worldId)` を同じ検査で見る」というコードで、テストは「両方無い」で拒否を固定する。）

- [ ] **Step 2: 実装する**

```csharp
using System.IO;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;

namespace Server.Boot.Replay
{
    public enum BugReportBundleWorldOutcome
    {
        Resolved,
        Rejected,
    }

    // 解決か拒否かは列挙型一本で判別する。World と RejectReason を null で使い分けない
    // Resolved vs rejected is told by the enum alone; World and RejectReason are never distinguished by null
    public abstract class BugReportBundleWorldResolution
    {
        public abstract BugReportBundleWorldOutcome Outcome { get; }

        public sealed class ResolvedWorld : BugReportBundleWorldResolution
        {
            public readonly WorldDataDirectory World;
            public ResolvedWorld(WorldDataDirectory world) { World = world; }
            public override BugReportBundleWorldOutcome Outcome => BugReportBundleWorldOutcome.Resolved;
        }

        public sealed class RejectedWorld : BugReportBundleWorldResolution
        {
            public readonly string Reason;
            public RejectedWorld(string reason) { Reason = reason; }
            public override BugReportBundleWorldOutcome Outcome => BugReportBundleWorldOutcome.Rejected;
        }

        public static BugReportBundleWorldResolution Resolved(WorldDataDirectory world) { return new ResolvedWorld(world); }
        public static BugReportBundleWorldResolution Rejected(string reason) { return new RejectedWorld(reason); }
    }

    // 箱の world/ から再生に使うワールドを決める。map.json があればその箱、無ければ生成ワールドに限り world.json から同梱スナップショット／共有キャッシュを引き当てる（ADR 0064）
    // Decides the world used for replay from the bundle's world/: the bundle itself when map.json is present, else for a generated world the bundled snapshot / shared cache located from world.json (ADR 0064)
    public static class BugReportBundleWorldResolver
    {
        public static BugReportBundleWorldResolution Resolve(string bundleDirectory, string serverDataDirectory)
        {
            var bundled = WorldDataDirectory.FromWorldRoot(Path.Combine(bundleDirectory, BugReportBundleLayout.WorldDirectoryName));
            if (!Directory.Exists(bundled.Root)) return BugReportBundleWorldResolution.Rejected($"バンドルに {BugReportBundleLayout.WorldDirectoryName}/ がありません bundle:{bundleDirectory}");
            if (File.Exists(bundled.MapJsonFilePath)) return BugReportBundleWorldResolution.Resolved(bundled);

            if (!File.Exists(bundled.WorldMetaFilePath)) return BugReportBundleWorldResolution.Rejected($"バンドルの world/ に map.json も world.json もありません bundle:{bundleDirectory}");
            var meta = ReadMeta(bundled.WorldMetaFilePath, out var readError);
            if (meta == null) return BugReportBundleWorldResolution.Rejected($"world.json を読めません: {readError}");
            if (!string.Equals(meta.MapMode, WorldMapMode.Generated, System.StringComparison.OrdinalIgnoreCase))
            {
                return BugReportBundleWorldResolution.Rejected($"手作りワールド（mapMode={meta.MapMode}）なのに map.json が無く、記録時のワールドを引き当てられません bundle:{bundleDirectory}");
            }

            var worldId = WorldIdentity.CalculateGenerated(meta.Seed, meta.GenerationMasterFingerprint, meta.GeneratorVersion);
            foreach (var candidate in new[] { WorldDataDirectory.ForBundledSnapshot(serverDataDirectory, worldId), WorldDataDirectory.ForWorldCache(worldId) })
            {
                if (!File.Exists(candidate.MapJsonFilePath) || !File.Exists(candidate.WorldMetaFilePath)) continue;
                var candidateMeta = ReadMeta(candidate.WorldMetaFilePath, out _);
                if (candidateMeta == null) continue;
                if (candidateMeta.PlacementLedgerDigest != meta.PlacementLedgerDigest)
                {
                    return BugReportBundleWorldResolution.Rejected($"同梱スナップショット {worldId} の placementLedgerDigest が箱の world.json と一致しません（箱:{meta.PlacementLedgerDigest} 候補:{candidateMeta.PlacementLedgerDigest}）");
                }
                return BugReportBundleWorldResolution.Resolved(candidate);
            }
            return BugReportBundleWorldResolution.Rejected($"生成ワールド {worldId} が worldSnapshots にも共有キャッシュにもありません（同じコミットの配布ビルドの game/ を serverDataDirectory に指定すること） serverData:{serverDataDirectory}");
        }

        // world.json は外部入力のJSON。読めない理由を返し、呼び出し側が拒否理由に載せる
        // world.json is external JSON; the read error is returned so the caller can put it in the rejection reason
        private static WorldMetaJson ReadMeta(string path, out string error)
        {
            error = null;
            try
            {
                return JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(path));
            }
            catch (JsonException e)
            {
                error = e.Message;
                return null;
            }
        }
    }
}
```

`BugReportBundleTools.ReplayCheck` の `worldRoot` の存在検査（39〜46 行付近）を次に置き換える:

```csharp
            var resolved = BugReportBundleWorldResolver.Resolve(bundleDirectory, serverDataDirectory);
            if (resolved is BugReportBundleWorldResolution.RejectedWorld rejected) return Reject(rejected.Reason);
            var sourceWorld = ((BugReportBundleWorldResolution.ResolvedWorld)resolved).World;
```

（以降の `var sourceWorld = WorldDataDirectory.FromWorldRoot(worldRoot);` は削除。`worldRoot` 変数も消す。）

- [ ] **Step 3: コンパイルとテスト**

Run: `uloop compile --project-path ./moorestech_client` → ErrorCount 0
Run: `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Tests.CombinedTest.Server.Replay.BugReportBundleWorldResolverTest"` → PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Server.Boot/Replay moorestech_server/Assets/Scripts/Tests
git commit -m "feat(replay): 箱の world.json から同梱スナップショットを引き当てて再生する (ADR 0064)"
```

---

## Task 11: 受け口の実デプロイと検証機 smoke の通し（人手工程を含む）

**Files:**
- Modify: `docs/superpowers/plans/2026-09-18-playtest-direct-r2-upload-and-generated-world-omission.md`（判断記録へ実出力を転記）

- [ ] **Step 1: R2 API トークン（ユーザー作業）**

ユーザーに依頼: Cloudflare ダッシュボード（moorestech アカウント）→ R2 → Manage R2 API Tokens → Create API token（Object Read & Write、バケット `moorestech-playtest` 限定）。Access Key ID と Secret Access Key を受け取ったら:

```bash
cd tools/playtest-receiver
unset CLOUDFLARE_API_TOKEN
pnpm exec wrangler secret put R2_ACCESS_KEY_ID --profile moorestech
pnpm exec wrangler secret put R2_SECRET_ACCESS_KEY --profile moorestech
pnpm exec wrangler deploy --profile moorestech
```

（値を出力・ログ・会話に出さない。）

- [ ] **Step 2: 署名の実機確認（R2 が Content-Length 不一致を拒むこと）**

R2 の公式文書は署名済み `content-length` の強制を明記していないため、実機で確かめる。Mac mini で `node` に `aws4fetch` を入れた一時スクリプト（値は env から読み、出力しない）で `presignPut` と同じ手順の URL を 10 バイト宣言で作り、`curl -X PUT --data-binary @<11バイトのファイル>` が **403** になり、10 バイトのファイルが **200** になることを確認する。403 にならなければ、`complete` の長さ照合だけが防衛線になる旨を『## 判断記録（ADR）』に書く（ADR 0064 の想定どおりの縮退で、実装は変えない）。あわせて `curl -sS -o /dev/null -w '%{http_code}\n' -X PUT https://playtest.moores.tech/v1/uploads/report/x/a.bin -H 'Authorization: Bearer x'` が 401 であることを見る。

- [ ] **Step 2b: 低速回線でアイドル期限が誤発火しないこと**

Unity/Mono の `HttpClient` が `StreamContent` をバッファせずストリーミング送信するかは静的に確認できない（バッファするとファイル読み出しはミリ秒で終わり、60 秒後に送信途中で切れる）。検証機（または Mac mini の Player ビルド）で、`MOORESTECH_PLAYTEST_RECEIVER_BASE` を向けた **開発ビルド**（Editor/Development の URL 上書き経路）から、帯域を 1Mbps 程度に絞った受け側（例: Mac mini の `pv -L 128k` を挟んだ簡易 HTTP sink、または Network Link Conditioner）へ 20MB を PUT し、idle cancel が起きず完了することを確認する。切れた場合は `IdleTimeoutStream` を「送信進捗」で延ばす形（本文を固定長のチャンクで手動送信し、各チャンクの送信完了で延ばす）へ変え、テストと判断記録を更新する。

- [ ] **Step 3: 検証機 smoke**

`fix/playtest-verify-remote-quoting`（PR #1370）がマージ済みの master へ本ブランチをマージしてから、`scripts/playtest/release-playtest.sh origin/<本ブランチ>`（`MOORESTECH_BUILD_BRANCH=<本ブランチ>`）で配布ビルド → steamcmd → 検証機 smoke を回す。phase2 の `report-uploaded` が 300 秒以内に通り、`verify-on-windows.sh` が受け口の READY を取得して ACK するところまでを確認する。READY の `files` に `world/map.json` と `world/terrain/*` が無く、`world/world.json` があることを `runs/<label>/verify/report-ready` で確かめる。

- [ ] **Step 4: 判断記録へ転記してコミット**

smoke の結果（ラベル・phase1/phase2・READY の fileCount）を本 plan の『## 判断記録（ADR）』へ書き、コミットする。

```bash
git add docs/superpowers/plans/2026-09-18-playtest-direct-r2-upload-and-generated-world-omission.md
git commit -m "docs(plan): 直接アップロードの実デプロイと検証機 smoke の結果を記録する"
```

---

## Task 12: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

- [ ] **Step 1:** moores-code-review スキルを全ブランチ差分に対して実行し、Critical を反映する。レビュー指摘の反映がアップローダーの判定経路（分類・再試行・照合）に触れたら、Task 8 のテストと Task 11 の smoke（phase2）を反映後のバイナリで再実施する。

## Task 13: セッション終了可能状態にすること

- [ ] **Step 1:** pr-create スキルで PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する。全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認して終える。PR 本文に「ユーザー作業: R2 API トークンの作成と secrets 投入（Task 11 Step 1）」を残す。

---

## 配置と前例

| 項目 | 配置 | 前例 |
|---|---|---|
| `presign.ts` / `bundleDeclaration.ts` / `uploadsVerify.ts` | Worker `src/`（`token.ts` / `keys.ts` と同層） | `token.ts`（HMAC 署名）、`keys.ts`（キー安全性の唯一の入口） |
| `DECLARED` マーカー | `bundleMarkers.ts` | `READY` / `ACKED` |
| `PlaytestDeclaredFile` / `PlaytestPrepareResponse` / `IdleTimeoutStream` | `Client.PlaytestReceiver/Http` | `PlaytestSessionResponse`（応答 DTO と TryParse） |
| `PlaytestBoxDeclaration` / `PlaytestUploadRetrySchedule` | `Client.PlaytestReceiver/Upload` | `PlaytestOutboxScanner`（箱の走査）、`PlaytestUploadFailurePolicy`（判定の単一地点） |
| `BugReportBundleWorldResolver` | `Server.Boot/Replay` | `BundleServerDataCheck`（箱と実データの照合） |
| `WorldDefinition*` 定数 | `Game.Paths/BugReportBundleLayout` | 同ファイルの `WorldDirectoryName` |

機構選択（検査4）: 既存の中継 PUT を「抑止」するのではなく削除し、経路を1本（直接 PUT）に置き換える。受動的統合案（中継を残して直接 PUT を併設）は経路が2本になり、無料プランで落ちる側が残るため採らない。

## 判断記録（ADR）

- 設計 ADR: `docs/adr/0064-playtest-uploads-go-direct-to-r2-and-omit-generated-world-files.md`（裁定の出所はすべてそこ）。
- **署名付き URL の発行は箱ごとに1回のバッチ（prepare）** — agent前提（ADR 0064 の agent 前提を踏襲。Worker リクエスト数を箱あたり定数に抑える）。
- **`complete` の照合は「存在と長さ」だけ** — agent前提。内容の検査（manifest の構造等）は取り込み側（plan H の read-manifest）に既にある。ETag/ハッシュ照合は Worker の CPU を使わずに済むが、クライアント側でハッシュを計算する追加負荷と時間が要るため見送る。
- **409 は Retryable** — agent前提。`incomplete` / `not-prepared` は prepare からやり直せば直る。plan D の分類表（408/429/5xx = Retryable、その他 4xx = PermanentForFile）に 409 を足す形。
- **再試行は 3 回、10s/30s/60s** — agent前提（ADR 0064 で plan 側に委ねられた具体値）。検証機 smoke の 300 秒（`report-uploaded`）に収まる合計 100 秒。
- **アイドル期限 60 秒** — agent前提（`HttpTimeoutSeconds` と同値。回線断の検出遅れを 1 分に抑える）。
- **`world.json` が読めないときは全部入れる側に倒す** — agent前提。地形を省く判断は読めた `world.json` だけに基づく（fail-closed は「省かない」）。
- **manifest の `schemaVersion` は 2 のまま、`worldDefinition` を追加** — agent前提。取り込み側は未知キーを無視する読み方（`read-manifest.py` は必要キーだけ読む）で、version を上げると取り込み側の同時改修が要る。
- **`ReplayCheck` 以外の `world/` 消費者は現状無い** — コード事実（`git grep BugReportBundleLayout.WorldDirectoryName`）。自動修正ランが将来箱を起動する場合は同じ resolver を使う。
- **unityプレイ録画テストは含めない** — 変更はネットワーク送信とバンドル作成・再生ツールで、ゲームプレイ・入力・UI に触れない。実機の通しは Task 11 の検証機 smoke（配布ビルドでの phase1/phase2）が担う。
- **`test/uploads/integrity.test.ts` の削除** — 中継 PUT の長さ整合（body と Content-Length の不一致）はもう Worker の責務でなく、R2 の署名（Content-Length 署名）と complete の長さ照合（Task 4 のテスト）に移る。
- **応答の判別は列挙型一本**（`BugReportBundleWorldOutcome` / `PlaytestPrepareOutcome`、Worker の prepare 応答は `outcome` キー） — ユーザー裁定 2026-09-18（型検査の強い指摘1 → 選択「畳む：結果は列挙型一本で判別する」）。
- **prepare は試行ごとに呼び直す。ADR 0064 の「箱ごとに1回」は「ファイルごとでなく箱ごとのバッチ発行」の意味に改める** — ユーザー裁定 2026-09-18（型検査の強い指摘2 → 選択「ADR の言葉を直す」）。URL の期限切れ（1時間）も再試行で自然に解ける。
- **型検査の弱い指摘は (a)(c)(d)(e) を拾い (b) は現状維持** — ユーザー裁定 2026-09-18（選択「(a)(c)(d)(e) を拾う。(b) はこのまま」）。(a) 空の宣言は偽の 400 を合成せず受け口へ行かずに恒久失敗として数える、(c) `ComposeDeclarationBody` のテストで AbsolutePath がワイヤに乗らないことを固定、(d) `PlaytestSkippedFile`、(e) mapMode は `WorldMapMode.Generated`、クライアントの宣言規則は `PlaytestUploadPath.DescribeRejection` 一本。(b) prepare 応答のパース失敗理由は文字列のまま（ログで足りる）。
- **complete の 409 で欠けたパスを送信済み集合から外す** — シミュレーター予測（判事）→ 適用。外さないと再試行が PUT を全部飛ばして同じ 409 を繰り返し、収束しない。
- **`PlaytestOutboxScanner.ReservedUploadSegments` の更新を Task 6 に含める** — シミュレーター予測（判事）→ 適用。contract.json の予約名にクライアント側ミラーがあり、契約テストが突き合わせている。
- **署名付き URL への PUT の 403 は Retryable** — シミュレーター予測（判事・Warning）→ 適用。署名の期限切れ・長さ不一致は prepare からやり直せば直る。受け口の 403 は従来どおり Unauthorized。
- **R2 の Content-Length 強制と Mono HttpClient のストリーミング送信は Task 11 で実機確認** — シミュレーター予測（判事・Warning）→ 検証ステップ化。静的に確認できない前提を smoke（高速 LAN）だけで済ませない。
- **本PR外のリファクタ提案（型検査 第3バケツ）**: `WorldSnapshotStore`（同梱→共有キャッシュの同順走査）と `TerrainTransferMetaReader`（world.json→worldId 導出）に同役割の既存実装がある。`BugReportBundleWorldResolver` はバンドルの world.json だけ（地形ファイル無し）から解決する点で入力が違うため新設するが、worldId 導出の共有化は別 PR の提案として PR 本文に載せる。
- **Task 6〜8 は同一 worktree で連続実行** — インターフェース差し替えの途中でコンパイルが通らない区間があるため、ErrorCount 0 の確認は Task 8 末尾で行う（各タスクのコミットは分ける）。
