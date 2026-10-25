# 遠隔実行の待受を Web UI の Kestrel から切り離す Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** 遠隔実行（`/api/remote-exec`）を Web UI の Kestrel から外し、`System.Net.HttpListener` の専用待受で開く。Web ホストの起動に失敗しても遠隔実行は開く。

**Architecture:** `RemoteExecEndpoint` を `HttpContext` 非依存の「要求 DTO → 応答 DTO」の関数にし、`HttpListener` を包む `RemoteExecHttpListener`（待受の開閉・受付ループ）と `RemoteExecHttpExchange`（`HttpListenerContext` ⇔ DTO の変換）を `Client.RemoteExec/Listen/` に置く。要求・応答の DTO は入口の契約なので `Client.RemoteExec` 直下に置き、`Listen` → 直下の一方向の依存にする。起動配線は `InitializeScenePipeline` で Web UI の起動より前（起動印の確定直後）へ移し、`KestrelPort` の受け渡しを消す。トークン・`access.json`・起動オプション `--remoteExec`・結果の 7 種・送信側 CLI は変えない。

**Tech Stack:** Unity 6000.3.8f1 / Mono / API 互換 .NET Standard 2.1、`System.Net.HttpListener`、UniTask、Newtonsoft.Json、NUnit（EditMode／EditModeInPlayingTest）、bash＋PowerShell（検証機）

## Requirements

- R1: 遠隔実行の待受は `Client.RemoteExec` が所有する `HttpListener`（`127.0.0.1` のみ）で開く。受け入れ: `Client.RemoteExec` 配下の `.cs` に `Microsoft.AspNetCore` の `using` が 0 件（Task 5 で (b) に切り替えた場合は `Listen/` の 2 ファイルだけに限る）、`WebUiEndpoints.cs` に `RemoteExec` の文字列が 0 件（Task 1・Task 2）。
- R2: `RemoteExecEndpoint` は `HttpContext` に依存しない。受け入れ: `RemoteExecEndpoint.HandleAsync(RemoteExecHttpRequest, CancellationToken)` が `UniTask<RemoteExecHttpResponse>` を返し、`RemoteExecEndpointFailureTest` が `DefaultHttpContext` を使わずに通る（Task 1）。
- R3: 起動の Web 依存を外す。`InitializeScenePipeline` は `ResolveFromCommandLine` → `BeginCurrentSessionMarks` → `ActivateIfRequested()` → `WebUiStartup.StartAsync` の順に呼び、`WebUiHost.KestrelPort` を読まない。受け入れ: `PlaytestBootUnityCallbackBindingTest` の順序検査が通る。`KestrelPort` は使い手が無くなるので削除する（Task 2）。
- R4: 入口の契約は不変。トークン（`X-Remote-Exec-Token`、起動毎に 32 バイト乱数）・`access.json`（`port`・`token`・`processId`、置き場 `GameSystemPaths.RemoteExecDirectory`）・`--remoteExec`・パス `/api/remote-exec`・結果 7 種（`Cancelled`・`ServerUnavailable`・`Unauthorized`・`BadRequest`・`CompileFailed`・`RuntimeException`・`Succeeded`）・HTTP ステータス（404/403/400/200）・拒否ログの文言は変えない。ただし無効な起動は待受そのものを開かないので、要求は接続拒否になり入口の拒否ログは出ない（起動ログ `起動オプションが無いため遠隔実行は無効です` が残る）。受け入れ: `RemoteExecEndpointTest` の拒否・成功・台帳の検査が同じ期待値で通る（Task 1・Task 3）。
- R5: `--remoteExec` が無い起動では待受を開かない。受け入れ: 無効起動で `ActivateIfRequested()` を呼んでも `access.json` が作られず、有効→無効へ切り替えると直前の待受ポートへの接続が拒否される（Task 3）。検証機でも確かめる（Task 4）。
- R6: 待受を開けない（20 ポートすべて使用中、または `HttpListener.IsSupported` が false）ときは Harmony を読まず `access.json` も書かず、`Debug.LogError` で理由を残す（Task 1）。
- R7: `scripts/playtest/remote-exec.sh` は `access.json` 経由のまま、変更なしで動く。受け入れ: `scripts/playtest/tests/test-remote-exec.sh`・`test-remote-exec-offline.sh` が PASS し、検証機で `--windows` が `Succeeded` を返す（Task 4）。
- R8: 完了条件（検証機）: Windows 検証機の配布ビルドで (1) `--remoteExec` 付きなら `remote-exec.sh --windows` が `Succeeded` を返し、Harmony の差し込みも通る (2) 付けなければ遠隔実行の待受が開かない (3) `moorestech_Data/StreamingAssets/WebUi` を外して Web ホストの起動を失敗させても遠隔実行は開く。受け入れ: Task 4 の記録 `docs/research/2026-10-26-remote-exec-own-listener-windows.md`。
- R9: 配布ビルド（Mono・`managedStrippingLevel` 既定）で `HttpListener` が動かなければ (b) 遠隔実行専用の最小 Kestrel に切り替える（Task 5、条件付き）。
- R10: ADR 0072 を同じ PR で改訂する（待受だけを変え、トークン・起動オプション・結果 7 種は変えない旨と、出所が agent 既定であること）（Task 2）。
- やらないこと: Kestrel と AspNetCore 系 NuGet の撤去（段階 5 の 5d。Web UI がまだ使う）／`WebUiStartup.cs` の Editor 限定スイッチ（albof.3）／送信側 CLI・`access.json` の形式変更／Steam（playtest-staging 含む）への配布・Discord への投稿／遠隔実行の機能追加（タイムアウト・切断検知など）。

## Global Constraints

- ファイルは 200 行未満。1 ディレクトリ 10 ファイルまで（`Client.RemoteExec/Listen/` は新設 3 ファイル、`Client.RemoteExec/` 直下は 4→6 ファイル）。
- 主要処理に日本語→英語の 2 行コメント（各 1 行）。`#region Internal` はメソッド内ローカル関数の囲いにだけ使う。
- try-catch は外部境界（ネットワーク送受信・外部入力 JSON・ディスク IO・送信コードの実行境界）だけ。境界である根拠をコメントに書き、catch した失敗は `Debug.LogError`／`Debug.LogWarning` に残す。意図的な無音の続行には `silent-ok: <理由> / <誰がどこで気づくか>` を書く。
- `Func<>` 禁止・partial 禁止・デフォルト引数禁止・単純 getter/setter プロパティ禁止（`{ get; }` の不変プロパティは `RemoteExecResult` の前例どおり可）。
- `.meta` は手で作らない（Unity が生成したものをコミットする）。asmdef は JSON なので Edit で編集してよい。
- `.cs` を変えたら必ず `uloop compile --project-path ./moorestech_client` を通す。
- Unity Editor は Mac mini 全体で同時 3 本まで。起動前に `moores-wt status` で本数を確かめ、3 本以上なら Editor の要らない作業（ADR・README・スクリプトテスト）を先に進める。自分の Editor は `uloop launch --project-path ./moorestech_client` で起動し、止めるときは `moores-wt status` で自分の pid を確かめて `kill <pid>`（`pkill` 禁止）。
- 長い処理はバックグラウンドのポーリングで待たず、前景で実行する。区切り（タスク）ごとにコミットする。
- 他レーンの持ち物（albof.1/.3/.4、第 2 波の .5/.6/.7）には触らない。本 plan で例外的に触る albof.3 の持ち物は `WebUiStartup.cs` のログ文 1 行と `WebUiHost.cs` の `KestrelPort` 削除だけで、触ったら `bd note moorestech-albof.2 "…"` を残す。
- Steam・Discord など外へ出す操作はしない。検証機への持ち込みは scp。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 未実施
- Phase 2.6（型閉包・重複・ADR矛盾）: 未実施

---

## File Structure

| ファイル | 責務 | 前例 |
|---|---|---|
| Create `moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecHttpRequest.cs` | 要求 DTO（経路・メソッド・Origin の有無・トークンヘッダ・本文ストリーム） | `Run/RemoteExecResult.cs`（`{ get; }` の不変 DTO） |
| Create `moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecHttpResponse.cs` | 応答 DTO（ステータス＋`RemoteExecResult`）と JSON 直列化 | 現 `RemoteExecEndpoint.ResponseSettings` を移設 |
| Create `moorestech_client/Assets/Scripts/Client.RemoteExec/Listen/RemoteExecHttpListener.cs` | 待受の開閉・ポート探索・受付ループ | `Client.WebUiHost/Boot/KestrelServer.cs`（基準ポートから上へ bind を試す） |
| Create `moorestech_client/Assets/Scripts/Client.RemoteExec/Listen/RemoteExecHttpExchange.cs` | `HttpListenerContext` ⇔ DTO の変換と応答の送信 | 現 `RemoteExecEndpoint.WriteResultAsync` |
| Create `moorestech_client/Assets/Scripts/Client.RemoteExec/Listen/RemoteExecListenerEditorCleanup.cs` | Editor 専用: Play 終了・ドメインリロード・Editor 終了で待受を閉じる | `Client.WebUiHost/Boot/WebUiHostEditorCleanup.cs`（同じ 3 契機） |
| Modify `moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecEndpoint.cs` | DTO を受けて DTO を返す判定と実行 | 現行の判定順をそのまま保つ |
| Modify `moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecActivation.cs` | 引数なしの有効化（待受→Harmony→接続情報）と撤去（待受停止＋接続情報撤去） | 現行の「Harmony を読む前に入口を開けるか決める」順 |
| Modify `moorestech_client/Assets/Scripts/Client.RemoteExec/Access/RemoteExecAccessFile.cs` | `Issue(int port)`（null 分岐を削除） | — |
| Modify `moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecLaunchOption.cs` | 無効な起動で `RemoteExecActivation.Deactivate()`（待受停止＋接続情報撤去）を呼ぶ | 現行の「無効な起動で前回の入口を撤去」 |
| Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs` | remote-exec 分岐を削除 | — |
| Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef` | `Client.RemoteExec` 参照を削除 | — |
| Modify `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs` | `KestrelPort` を削除（使い手が消えるため） | — |
| Modify `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs` | 有効化を Web UI 起動より前へ移す | — |
| Modify `moorestech_client/Assets/Scripts/Client.Starter/Initialization/Boot/WebUiStartup.cs` | ログ文「遠隔実行も開きません」を削る（1 行） | — |
| Modify `moorestech_client/Assets/Scripts/Client.Tests/RemoteExec/RemoteExecEndpointFailureTest.cs` | DTO で同じ 3 検査＋待受を開けない検査 | — |
| Create `moorestech_client/Assets/Scripts/Client.Tests/RemoteExec/RemoteExecHttpListenerTest.cs` | 待受のポート探索・停止・経路違い 404 | — |
| Modify `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/RemoteExec/RemoteExecEndpointTest.cs` | Kestrel を立てず本番の有効化経路で通しの検査 | — |
| Modify `moorestech_client/Assets/Scripts/Client.Tests/Playtest/PlaytestBootUnityCallbackBindingTest.cs` | 起動順序の検査を新しい順に | — |
| Modify `docs/adr/0072-remote-exec-in-distributed-builds.md` | 待受の改訂 | — |
| Modify `scripts/playtest/README.md` | 遠隔実行節に「Web UI が起動しなくても使える」 | — |
| Create `docs/research/2026-10-26-remote-exec-own-listener-windows.md` | 検証機の実機確認の記録 | `docs/research/2026-09-27-remote-exec-e2e.md` |

## 配置と前例（spec-architecture-review）

データフロー: 起動引数 → `RemoteExecLaunchOption.IsEnabled` → `RemoteExecActivation`（待受を開く→Harmony→`access.json`）→ `remote-exec.sh` が `access.json` の port を読む → `RemoteExecHttpListener` → `RemoteExecHttpExchange` → `RemoteExecEndpoint` → `RemoteExecRunner`。立ち位置は置き換え（`WebUiEndpoints` の分岐 → 専用の待受）で、共有モデルに書く既存パイプラインには乗らない。

| # | 項目 | 配置 | 機構 | 前例 |
|---|---|---|---|---|
| 1 | `RemoteExecHttpRequest`・`RemoteExecHttpResponse`（internal） | `Client.RemoteExec` 直下 | `{ get; }` の不変 DTO・camelCase 直列化 | `Client.RemoteExec/Run/RemoteExecResult.cs`、現 `RemoteExecEndpoint.ResponseSettings` |
| 2 | `RemoteExecHttpListener`（internal static） | `Client.RemoteExec/Listen/` | `System.Net.HttpListener`・基準ポートから上へ 20 個探索・`UniTaskVoid` の受付ループ | 探索: `Client.WebUiHost/Boot/KestrelServer.cs`／static の状態: `Client.RemoteExec/Access/RemoteExecAccessFile.cs`（HttpListener はリポジトリ初出＝新規パターン） |
| 3 | `RemoteExecHttpExchange`（internal） | `Client.RemoteExec/Listen/` | Context ⇔ DTO、送信境界だけ try-catch | 現 `RemoteExecEndpoint.WriteResultAsync` |
| 4 | `RemoteExecListenerEditorCleanup` | `Client.RemoteExec/Listen/`（全体 `#if UNITY_EDITOR`） | Play 終了・ドメインリロード・Editor 終了 | `Client.WebUiHost/Boot/WebUiHostEditorCleanup.cs` |
| 5 | `RemoteExecEndpoint.HandleAsync(DTO)`（internal） | `Client.RemoteExec` 直下 | 判定順は現行のまま | 現 `RemoteExecEndpoint.cs` |
| 6 | `ActivateIfRequested()`・`Deactivate()`（撤去の呼び口を 1 つに） | `Client.RemoteExec` 直下 | 「Harmony を読む前に開けるか決める」順 | 現 `RemoteExecActivation.cs` |
| 7 | 起動順 `Resolve → BeginCurrentSessionMarks → ActivateIfRequested → WebUiStartup` | `Client.Starter/InitializeScenePipeline.cs` | パイプラインからの明示呼び出し・終了は `OnGameShutdown` の購読 | 現 `InitializeScenePipeline.cs:62-79`、`WebUiHost.cs` の `OnGameShutdown.Subscribe(_ => Stop())` |
| 8 | ポート帯 25100〜25119 | `RemoteExecHttpListener` の const | — | `Client.WebUiHost/Common/WebUiPortConfig.cs`（Kestrel 25050〜25069・Vite 25173〜。重ならない） |
| 9 | ADR 0072 のその場改訂 | `docs/adr` | 「状態: 採択（改訂）」＋追記段落 | `docs/adr/0059`・`0061`・`0062`・`0064`・`0085` |

新規パターン（PR 本文の注目点）: リポジトリ初の `HttpListener` で、HTTP サーバーが 2 本になる（Web UI の Kestrel と遠隔実行の待受）。既存の Kestrel 相乗りを残す案は「Web ホストが失敗すると遠隔実行も開かない」で棄却（ADR 0072 改訂の棄却案）。

死活表（Phase 2.5。遠隔実行と Web UI の起動経路にぶら下がる操作）:

| 現行の操作 | plan 後 | 根拠 |
|---|---|---|
| `remote-exec.sh`（Mac）・`--windows` で client/server 実行・Harmony | 生きる | 送信側は `access.json` の port を読むだけ（`scripts/playtest/remote-exec.sh:49-53`・`windows/remote-exec.ps1:14`） |
| 403・400 の拒否と理由ログ | 生きる | 判定順と文言が同じ |
| 無効起動での秘匿 | 生きる（形が変わる） | 待受を開かないので接続拒否（以前の 404＋拒否ログは無くなり、起動ログが残る） |
| 正常終了・無効起動での `access.json` 撤去 | 生きる | `Deactivate` に待受の停止が加わるだけ |
| バグ報告への台帳同梱・前回台帳の退避 | 生きる | 待受に依らない（`RemoteExecBundleMark`・`PreviousSessionRemoteExecLedgers`） |
| Web UI（CEF・`/ws`・`/api/ping`・アイコン・マスタ JSON・dist） | 生きる | 消すのは remote-exec の分岐だけ |
| Web UI の起動失敗時の遠隔実行 | 改善 | 有効化が Web UI より前 |
| `--playtestSmoke`・起動ゲート・起動印 | 生きる | 動くのは `BeginCurrentSessionMarks` より後ろだけ |
| Kestrel の `/api/remote-exec` へ直接 POST する外部ツール | 死ぬ（意図どおり） | scripts に直書きは無い（stub・sh・ps1 はどれも `access.json` の port） |

---

### Task 1: 入口を DTO 化し、HttpListener の待受を足す

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecHttpRequest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecHttpResponse.cs`
- Create: `moorestech_client/Assets/Scripts/Client.RemoteExec/Listen/RemoteExecHttpListener.cs`
- Create: `moorestech_client/Assets/Scripts/Client.RemoteExec/Listen/RemoteExecHttpExchange.cs`
- Create: `moorestech_client/Assets/Scripts/Client.RemoteExec/Listen/RemoteExecListenerEditorCleanup.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecEndpoint.cs`（全面書き換え）
- Modify: `moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecActivation.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.RemoteExec/Access/RemoteExecAccessFile.cs:19-28`
- Modify: `moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecLaunchOption.cs:17-24`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/RemoteExec/RemoteExecEndpointFailureTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/RemoteExec/RemoteExecHttpListenerTest.cs`

**Interfaces:**
- Consumes: 既存 `RemoteExecRunner.RunAsync(string body, RemoteExecTarget target, CancellationToken)`（internal、`UniTask<RemoteExecResult>`）、`RemoteExecResult.Unauthorized/BadRequest(string)`、`RemoteExecTargetWireName.TryParse`、`RemoteExecAccessFile.HeaderName/Token`、`RemoteExecHarmonyLoader.Load()`
- Produces:
  - `internal sealed class Client.RemoteExec.RemoteExecHttpRequest(string path, string method, bool hasOriginHeader, string tokenHeader, Stream body)`。プロパティ `Path`・`Method`・`HasOriginHeader`・`TokenHeader`（ヘッダが無ければ null）・`Body`
  - `internal sealed class Client.RemoteExec.RemoteExecHttpResponse(int statusCode, RemoteExecResult result)`。プロパティ `StatusCode`・`Result`、メソッド `string SerializeBody()`（camelCase JSON）
  - `internal static UniTask<RemoteExecHttpResponse> Client.RemoteExec.RemoteExecEndpoint.HandleAsync(RemoteExecHttpRequest request, CancellationToken cancellationToken)`。`internal const string Path = "/api/remote-exec"`（WebUiEndpoints の参照が消え、使い手は同アセンブリとテスト（`InternalsVisibleTo("Client.Tests")`）だけになるので public から internal へ落とす）
  - `internal static class Client.RemoteExec.Listen.RemoteExecHttpListener`: `internal const int BasePort = 25100`、`internal const int PortSearchRange = 20`、`internal static int? Start()`（開けたポート、開けなければ null）、`internal static void Stop()`（冪等）
  - `public static void Client.RemoteExec.RemoteExecActivation.ActivateIfRequested()`（引数なし）、`public static void Deactivate()`（待受停止＋`access.json` 撤去）
  - `internal static void RemoteExecAccessFile.Issue(int port)`（`int?` から `int` へ）

- [x] **Step 1: 要求・応答 DTO を書く**

`moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecHttpRequest.cs`:

```csharp
using System.IO;

namespace Client.RemoteExec
{
    // 待受の種類に依らない要求の形。入口の判定はこの値だけを見る
    // A listener-independent request shape; the entry decides from these values alone
    internal sealed class RemoteExecHttpRequest
    {
        public string Path { get; }
        public string Method { get; }
        public bool HasOriginHeader { get; }

        // トークンヘッダが無い要求は null
        // null when the request carries no token header
        public string TokenHeader { get; }
        public Stream Body { get; }

        internal RemoteExecHttpRequest(string path, string method, bool hasOriginHeader, string tokenHeader, Stream body)
        {
            Path = path;
            Method = method;
            HasOriginHeader = hasOriginHeader;
            TokenHeader = tokenHeader;
            Body = body;
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecHttpResponse.cs`:

```csharp
using Client.RemoteExec.Run;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Client.RemoteExec
{
    // 入口が返す応答。拒否も成功も同じRemoteExecResultの形で返す
    // The entry's response; refusals and successes share the RemoteExecResult shape
    internal sealed class RemoteExecHttpResponse
    {
        private static readonly JsonSerializerSettings BodySettings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        public int StatusCode { get; }
        public RemoteExecResult Result { get; }

        internal RemoteExecHttpResponse(int statusCode, RemoteExecResult result)
        {
            StatusCode = statusCode;
            Result = result;
        }

        internal string SerializeBody()
        {
            return JsonConvert.SerializeObject(Result, BodySettings);
        }
    }
}
```

- [x] **Step 2: `RemoteExecEndpoint` を DTO の関数に書き換える**

`moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecEndpoint.cs` を次の内容で置き換える（判定の順・ステータス・拒否文は現行と同じ。経路違いの 404 だけが新しい）:

```csharp
using System;
using System.IO;
using System.Threading;
using Client.RemoteExec.Access;
using Client.RemoteExec.Run;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.RemoteExec
{
    // 認証済みの要求を実行し、結果を応答の形で返す。待受の種類は知らない
    // Run authenticated requests and return the result as a response, knowing nothing of the listener kind
    internal static class RemoteExecEndpoint
    {
        internal const string Path = "/api/remote-exec";

        internal static async UniTask<RemoteExecHttpResponse> HandleAsync(RemoteExecHttpRequest request, CancellationToken cancellationToken)
        {
            // 専用待受は遠隔実行だけを受ける。他の経路は理由付きで隠す
            // The dedicated listener serves remote exec only; any other route is hidden with a reason
            if (request.Path != Path) return Reject(404, RemoteExecResult.BadRequest($"未知の経路です: {request.Path}"));

            // 無効な起動では経路を隠し、理由を開発者ログへ残す
            // Hide the route on disabled boots and log the reason for developers
            if (!RemoteExecLaunchOption.IsEnabled) return Reject(404, RemoteExecResult.Unauthorized("起動オプションが無いため要求を404で拒否しました"));

            // 有効な起動では各要求を認証する
            // Authenticate each request on enabled boots
            if (!IsAuthorized(out var reason)) return Reject(403, RemoteExecResult.Unauthorized(reason));

            // 外部入力のJSON・ストリーム境界で読み取り失敗を隔離する
            // Isolate read failures at the external JSON and network stream boundary
            JObject body;
            try
            {
                using var reader = new StreamReader(request.Body);
                body = JObject.Parse(await reader.ReadToEndAsync());
            }
            catch (Exception e) when (e is JsonException || e is IOException)
            {
                return Reject(400, RemoteExecResult.BadRequest($"要求本文を読めませんでした: {e.Message}"));
            }

            var codeValue = body["code"];
            var targetValue = body["target"];
            if (codeValue?.Type != JTokenType.String || targetValue?.Type != JTokenType.String)
                return Reject(400, RemoteExecResult.BadRequest("code と target は文字列で指定してください"));
            var targetName = (string)targetValue;
            if (!RemoteExecTargetWireName.TryParse(targetName, out var target))
                return Reject(400, RemoteExecResult.BadRequest($"未知の実行先です: {targetName}"));

            var result = await RemoteExecRunner.RunAsync((string)codeValue, target, cancellationToken);
            return new RemoteExecHttpResponse(200, result);

            #region Internal

            bool IsAuthorized(out string authReason)
            {
                authReason = null;
                if (request.Method != "POST") authReason = "POST 以外";
                else if (request.HasOriginHeader) authReason = "Origin ヘッダ付き（ブラウザ由来）";
                else if (RemoteExecAccessFile.Token == null) authReason = "トークン未発行";
                else if (request.TokenHeader != RemoteExecAccessFile.Token) authReason = "トークン不一致";
                return authReason == null;
            }

            // 入口の拒否も実行結果と同じ形で返す。送信側は成功も拒否も1つの型として読める
            // Entry rejections come back in the same shape as a result, so the sender reads success and refusal through one type
            static RemoteExecHttpResponse Reject(int status, RemoteExecResult rejection)
            {
                Debug.LogWarning($"[RemoteExec] 要求を拒否しました: {rejection.RejectionReason}");
                return new RemoteExecHttpResponse(status, rejection);
            }

            #endregion
        }
    }
}
```

- [x] **Step 3: 待受（`RemoteExecHttpListener`）を書く**

`moorestech_client/Assets/Scripts/Client.RemoteExec/Listen/RemoteExecHttpListener.cs`:

```csharp
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.RemoteExec.Listen
{
    // 遠隔実行専用の待受。Web UIのホストとは独立に開閉する（ADR 0072 2026-10-26改訂）
    // The remote-exec-only listener, opened and closed independently of the Web UI host (ADR 0072, revised 2026-10-26)
    internal static class RemoteExecHttpListener
    {
        // Web UIのKestrel(25050〜)・Vite(25173〜)と重ならない帯
        // A band that overlaps neither the Web UI Kestrel (25050+) nor Vite (25173+)
        internal const int BasePort = 25100;
        internal const int PortSearchRange = 20;

        private static HttpListener _listener;
        private static CancellationTokenSource _lifetime;

        internal static int? Start()
        {
            // Editorの再生し直しで前回の待受が残っていても二重に開かない
            // Never open twice even if an Editor replay left the previous listener open
            Stop();

            // 実行環境がHttpListenerを持たなければポートを変えても開けない。理由を残して諦める
            // Without HttpListener support no port will open, so give up with a reason
            if (!HttpListener.IsSupported)
            {
                Debug.LogError("[RemoteExec] この実行環境は HttpListener に対応していないため待受を開けません（遠隔実行は使えません）");
                return null;
            }

            // 基準ポートから上へbindを試し、最初に開けたポートを採る（KestrelServerと同じ探索）
            // Probe upward from the base port and adopt the first that binds (the same search as KestrelServer)
            for (var port = BasePort; port < BasePort + PortSearchRange; port++)
            {
                var listener = new HttpListener();
                listener.Prefixes.Add($"http://127.0.0.1:{port}/");
                // OSのネットワーク境界。使用中ポートのbind失敗だけを隔離して次を試す
                // OS network boundary; only an occupied-port bind failure is isolated before trying the next port
                try
                {
                    listener.Start();
                }
                catch (Exception e) when (e is HttpListenerException || e is SocketException)
                {
                    listener.Close();
                    Debug.Log($"[RemoteExec] ポート {port} は使えないため次を試します: {e.Message}");
                    continue;
                }

                _listener = listener;
                _lifetime = new CancellationTokenSource();
                AcceptLoopAsync(listener, _lifetime.Token).Forget();
                Debug.Log($"[RemoteExec] 待受を開きました http://127.0.0.1:{port}");
                return port;
            }

            Debug.LogError($"[RemoteExec] 空きポートが無いため待受を開けません（遠隔実行は使えません） {BasePort}..{BasePort + PortSearchRange - 1}");
            return null;
        }

        internal static void Stop()
        {
            if (_listener == null) return;
            _lifetime.Cancel();
            _listener.Close();
            _listener = null;
            _lifetime = null;
            Debug.Log("[RemoteExec] 待受を閉じました");
        }

        // 受付はスレッドプールで回す。メインスレッドが止まっていてもserver実行の要求は受け取れる
        // Accept on the thread pool so server-target requests still arrive while the main thread is stalled
        private static async UniTaskVoid AcceptLoopAsync(HttpListener listener, CancellationToken lifetime)
        {
            while (!lifetime.IsCancellationRequested)
            {
                HttpListenerContext context;
                // OSのネットワーク境界。停止による中断と想定外の故障を分けて扱う
                // OS network boundary; a stop-induced interruption is told apart from an unexpected fault
                try
                {
                    context = await listener.GetContextAsync().ConfigureAwait(false);
                }
                catch (Exception e) when (e is HttpListenerException || e is ObjectDisposedException || e is InvalidOperationException)
                {
                    // silent-ok: Stop() による中断で、閉じたことは Stop() が「待受を閉じました」でログ済み / 開発者が Player.log で気づく
                    if (lifetime.IsCancellationRequested) return;
                    Debug.LogError($"[RemoteExec] 待受が要求を受け取れなくなりました（遠隔実行は使えません。再起動で復旧）: {e}");
                    return;
                }
                RemoteExecHttpExchange.HandleAsync(context, lifetime).Forget(e => Debug.LogError($"[RemoteExec] 要求の処理に失敗しました: {e}"));
            }
        }
    }
}
```

- [x] **Step 4: 変換と送信（`RemoteExecHttpExchange`）を書く**

`moorestech_client/Assets/Scripts/Client.RemoteExec/Listen/RemoteExecHttpExchange.cs`:

```csharp
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using Client.RemoteExec.Access;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.RemoteExec.Listen
{
    // HttpListenerの要求をDTOへ写し、入口の応答を書き戻す
    // Map an HttpListener request into the DTO and write the entry's response back
    internal static class RemoteExecHttpExchange
    {
        internal static async UniTask HandleAsync(HttpListenerContext context, CancellationToken lifetime)
        {
            // ヘッダは値が空でも「付いている」とみなす（空のOriginもブラウザ由来として拒否する）
            // A header counts as present even when empty, so an empty Origin is still refused as browser-originated
            var source = context.Request;
            var request = new RemoteExecHttpRequest(source.Url.AbsolutePath, source.HttpMethod,
                source.Headers.Get("Origin") != null, source.Headers.Get(RemoteExecAccessFile.HeaderName), source.InputStream);
            var response = await RemoteExecEndpoint.HandleAsync(request, lifetime);
            var bytes = Encoding.UTF8.GetBytes(response.SerializeBody());

            // HTTP送信境界の切断を記録し、実行結果が未達だったことを残す
            // Record disconnects at the HTTP send boundary so undelivered results remain visible
            try
            {
                context.Response.StatusCode = response.StatusCode;
                context.Response.ContentType = "application/json; charset=utf-8";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes, 0, bytes.Length, lifetime);
                context.Response.Close();
            }
            catch (Exception e) when (e is IOException || e is HttpListenerException || e is ObjectDisposedException || e is OperationCanceledException)
            {
                Debug.LogWarning($"[RemoteExec] 応答を届けられませんでした: {e.Message}");
                context.Response.Abort();
            }
        }
    }
}
```

- [x] **Step 4b: Editor の後片付けを書く**

置き換え元の Kestrel は `WebUiHostEditorCleanup` が Play 終了・ドメインリロード・Editor 終了で止めている。待受も同じ 3 契機で閉じる（Play 中の再コンパイルで古いドメインのソケットが残らないように）。`moorestech_client/Assets/Scripts/Client.RemoteExec/Listen/RemoteExecListenerEditorCleanup.cs`:

```csharp
#if UNITY_EDITOR
using UnityEditor;

namespace Client.RemoteExec.Listen
{
    // Editor専用: Play終了・ドメインリロード・Editor終了のいずれでも待受を閉じる（WebUiHostEditorCleanupと同じ契機）
    // Editor-only: close the listener on play-mode exit, domain reload or editor quit (the same triggers as WebUiHostEditorCleanup)
    internal static class RemoteExecListenerEditorCleanup
    {
        [InitializeOnLoadMethod]
        private static void RegisterCleanupHooks()
        {
            AssemblyReloadEvents.beforeAssemblyReload += RemoteExecHttpListener.Stop;
            EditorApplication.quitting += RemoteExecHttpListener.Stop;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        // Reload Domain=offではbeforeAssemblyReloadが来ないため、Play終了の直前にも閉じる
        // With Reload Domain off beforeAssemblyReload never fires, so also close right before leaving play mode
        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) RemoteExecHttpListener.Stop();
        }
    }
}
#endif
```

（`access.json` の撤去は既存どおり `Application.quitting` → `OnGameShutdown` → `Deactivate` が担う。ここは待受のソケットだけを閉じる）

- [x] **Step 5: 有効化・撤去・接続情報を書き換える**

`moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecActivation.cs` を置き換える:

```csharp
using Client.RemoteExec.Access;
using Client.RemoteExec.Listen;
using Client.RemoteExec.Loading;
using UnityEngine;

namespace Client.RemoteExec
{
    // 有効な起動だけで専用の待受・Harmony・接続情報を用意する。Web UIの起動結果には依らない
    // Prepare the dedicated listener, Harmony and access details only on enabled boots, independent of the Web UI startup
    public static class RemoteExecActivation
    {
        public static void ActivateIfRequested()
        {
            if (!RemoteExecLaunchOption.IsEnabled)
            {
                Debug.Log("[RemoteExec] 起動オプションが無いため遠隔実行は無効です");
                return;
            }
            // 入口を開けるかはHarmonyを読む前に決める。読んだ後で断ると、パッチ能力だけ得て入口が永久に閉じた状態が残る
            // Whether the entry can open is decided before loading Harmony; refusing afterwards would leave patching enabled with the entry shut forever
            var port = RemoteExecHttpListener.Start();
            // silent-ok: 開けない理由は Start() が LogError 済み / 開発者が Player.log と remote-exec.sh の接続失敗で気づく
            if (port == null) return;

            // DLLを読み接続情報を発行
            // Load the DLL and publish access details
            RemoteExecHarmonyLoader.Load();
            RemoteExecAccessFile.Issue(port.Value);
        }

        // 入口（待受と接続情報）の撤去はここ1つに集める。呼ぶのは終了イベントの購読者と無効な起動の判定。Editorの後片付けは待受のソケットだけを閉じる
        // Withdrawing the entry (listener and access details) lives here alone, called by the shutdown subscriber and the disabled-boot resolution; the Editor cleanup closes only the listener socket
        public static void Deactivate()
        {
            RemoteExecHttpListener.Stop();
            RemoteExecAccessFile.Remove();
        }
    }
}
```

`moorestech_client/Assets/Scripts/Client.RemoteExec/Access/RemoteExecAccessFile.cs` の `Issue` 冒頭（現 19〜28 行）を次に置き換える（null 分岐とそのコメントを削除。以降の乱数生成・書き込みは現行のまま。`port.Value` は `port` に直す）:

```csharp
        // 待受を開けたポートだけを受け取る。開けなかった起動では呼ばれない
        // Receives only a port the listener actually opened; a boot that failed to open never calls this
        internal static void Issue(int port)
        {
            Token = null;
            var bytes = new byte[32];
```

同ファイル内の `["port"] = port.Value` を `["port"] = port`、`$"[RemoteExec] 入口を開きました port:{port.Value} access:{path}"` を `port:{port}` に直す。

`moorestech_client/Assets/Scripts/Client.RemoteExec/RemoteExecLaunchOption.cs` の無効分岐（現 19〜24 行）を次にする:

```csharp
            // 無効な起動では前回の入口（待受と接続情報）を撤去する。残すと死んだプロセスのトークンが有効な入口として読まれる
            // A disabled boot withdraws the previous entry (listener and access details); leaving it lets a dead process's token read as a live one
            if (!IsEnabled)
            {
                RemoteExecActivation.Deactivate();
                return;
            }
```

- [x] **Step 6: `RemoteExecEndpointFailureTest` を DTO で書き直す**

`moorestech_client/Assets/Scripts/Client.Tests/RemoteExec/RemoteExecEndpointFailureTest.cs` の変更:
- `using Microsoft.AspNetCore.Http;` を削除し、`using Client.RemoteExec.Listen;`・`using System.Net.Sockets;`・`using System.Net;`・`using System.Collections.Generic;` を足す
- `TearDown` の先頭に `RemoteExecActivation.Deactivate();` を足す
- テスト `WebUi起動失敗ではポート値に関係なく接続情報を発行しない` を削除し、次で置き換える:

```csharp
        [Test]
        public void 待受を開けなければHarmonyも接続情報も用意しない()
        {
            // 探索帯の全ポートを塞ぎ、待受を開けない起動を作る
            // Occupy every port in the search band to create a boot that cannot open the listener
            var blockers = new List<TcpListener>();
            for (var port = RemoteExecHttpListener.BasePort; port < RemoteExecHttpListener.BasePort + RemoteExecHttpListener.PortSearchRange; port++)
            {
                var blocker = new TcpListener(IPAddress.Loopback, port);
                // OSのネットワーク境界。他プロセスが既に使っているポートは塞がっているのでそのまま数える
                // OS network boundary; a port another process already holds is blocked anyway and counts as such
                try { blocker.Start(); blockers.Add(blocker); }
                catch (SocketException) { Debug.Log($"[Test] ポート {port} は既に使用中"); }
            }
            LogAssert.Expect(LogType.Warning, new Regex("--remoteExec が指定された"));
            RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "--remoteExec" });

            LogAssert.Expect(LogType.Error, new Regex("空きポートが無いため待受を開けません"));
            RemoteExecActivation.ActivateIfRequested();

            foreach (var blocker in blockers) blocker.Stop();
            Assert.IsNull(RemoteExecAccessFile.Token);
            Assert.IsFalse(File.Exists(Path.Combine(RemoteExecAccessFile.DirectoryPath, "access.json")));
        }
```

（`LogAssert.ignoreFailingMessages` は使わない。使用中ポートの `Debug.Log` は通常ログなので LogAssert を乱さない）

- 残り 3 テストの `DefaultHttpContext` を DTO に置き換える。ヘルパーをクラス末尾に足す:

```csharp
        private static RemoteExecHttpRequest PostRequest(string token, string body)
        {
            return new RemoteExecHttpRequest(RemoteExecEndpoint.Path, "POST", false, token, new MemoryStream(Encoding.UTF8.GetBytes(body)));
        }
```

  - `無効起動では要求を404で隠して理由をログに残す`: `var response = await RemoteExecEndpoint.HandleAsync(PostRequest(null, "{}"), CancellationToken.None);` → `Assert.AreEqual(404, response.StatusCode);`（台帳 0 件の検査は残す）
  - `送信コードの実行時例外を失敗応答と台帳へ残す`: `RemoteExecAccessFile.Issue(0);` はそのまま（`int` 引数）。`var response = await RemoteExecEndpoint.HandleAsync(PostRequest(RemoteExecAccessFile.Token, "{\"code\":\"throw new System.InvalidOperationException(\\\"execution failure\\\");\",\"target\":\"client\"}"), CancellationToken.None);` → `var json = JObject.Parse(response.SerializeBody());` で `Assert.AreEqual(200, response.StatusCode)`・`outcome`=`RuntimeException`・`exception` に `execution failure`（台帳の 4 検査は現行のまま）
  - `入口の拒否も実行結果と同じJSONで返す`: `var json = JObject.Parse((await RemoteExecEndpoint.HandleAsync(PostRequest(null, "{}"), CancellationToken.None)).SerializeBody());` → `outcome`=`Unauthorized`、`rejectionReason` に `起動オプションが無い`

- [x] **Step 7: `RemoteExecHttpListenerTest` を書く**

`moorestech_client/Assets/Scripts/Client.Tests/RemoteExec/RemoteExecHttpListenerTest.cs`:

```csharp
using System;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Client.RemoteExec;
using Client.RemoteExec.Listen;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.RemoteExec
{
    public sealed class RemoteExecHttpListenerTest
    {
        [TearDown]
        public void TearDown()
        {
            RemoteExecHttpListener.Stop();
            RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
        }

        [Test]
        public void 探索帯がWebUiのKestrel帯とVite帯に重ならない()
        {
            // ポート帯の台帳は2アセンブリに割れるので、重ならないことを機械で固定する
            // The port bands live in two assemblies, so their disjointness is pinned mechanically
            var start = RemoteExecHttpListener.BasePort;
            var end = start + RemoteExecHttpListener.PortSearchRange;
            foreach (var otherStart in new[] { Client.WebUiHost.Common.WebUiPortConfig.KestrelBasePort, Client.WebUiHost.Common.WebUiPortConfig.ViteBasePort })
            {
                var otherEnd = otherStart + Client.WebUiHost.Common.WebUiPortConfig.PortSearchRange;
                Assert.IsTrue(end <= otherStart || otherEnd <= start, $"遠隔実行の帯 {start}..{end - 1} が {otherStart}..{otherEnd - 1} と重なる");
            }
        }

        [Test]
        public void 基準ポートが使用中なら次のポートで開く()
        {
            var blocker = new TcpListener(IPAddress.Loopback, RemoteExecHttpListener.BasePort);
            // OSのネットワーク境界。他プロセスが既に基準ポートを使っていても「使用中」の前提は同じ
            // OS network boundary; if another process already holds the base port the "occupied" premise still holds
            try { blocker.Start(); }
            catch (SocketException) { Debug.Log("[Test] 基準ポートは既に使用中"); }
            var port = RemoteExecHttpListener.Start();
            blocker.Stop();

            Assert.IsNotNull(port);
            Assert.Greater(port.Value, RemoteExecHttpListener.BasePort);
            Assert.Less(port.Value, RemoteExecHttpListener.BasePort + RemoteExecHttpListener.PortSearchRange);
        }

        [Test]
        public async Task 停止後は同じポートへ接続できない()
        {
            var port = RemoteExecHttpListener.Start();
            Assert.IsNotNull(port);
            RemoteExecHttpListener.Stop();

            // 接続拒否はOSのネットワーク境界の例外として観測する
            // A refused connection is observed as an exception at the OS network boundary
            var refused = false;
            using var client = new TcpClient();
            try { await client.ConnectAsync(IPAddress.Loopback, port.Value); }
            catch (SocketException) { refused = true; }
            Assert.IsTrue(refused);
        }

        [Test]
        public async Task 遠隔実行以外の経路は404で理由をログに残す()
        {
            var port = RemoteExecHttpListener.Start();
            Assert.IsNotNull(port);
            using var client = new HttpClient(new HttpClientHandler { UseProxy = false });

            LogAssert.Expect(LogType.Warning, new Regex("未知の経路です: /api/ping"));
            using var response = await client.PostAsync($"http://127.0.0.1:{port.Value}/api/ping", new StringContent("{}", Encoding.UTF8, "application/json"));

            Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
            StringAssert.Contains("\"outcome\":\"BadRequest\"", await response.Content.ReadAsStringAsync());
        }
    }
}
```

- [x] **Step 8: コンパイルする（Task 2 の呼び出し側修正前なので、エラーは呼び出し側の 3 箇所だけのはず）**

Run: `uloop compile --project-path ./moorestech_client`
Expected: エラーは `WebUiEndpoints.cs`（`HandleAsync(HttpContext)` 不一致）・`InitializeScenePipeline.cs`（`ActivateIfRequested` の引数）・`RemoteExecEndpointTest.cs`／`PlaytestBootUnityCallbackBindingTest.cs` のみ。`Client.RemoteExec` 内のエラーは 0。ここではコミットせず Task 2 へ進む（Task 1 と Task 2 は 1 コミットにまとめる。コンパイルが通らないコミットを作らないため）。

### Task 2: 呼び出し側を新しい入口へ付け替え、ADR を改訂する

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiEndpoints.cs:97-103`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Client.WebUiHost.asmdef`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Boot/WebUiHost.cs:28-30`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs:62-79`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/Boot/WebUiStartup.cs:19`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Playtest/PlaytestBootUnityCallbackBindingTest.cs:51-65`
- Modify: `docs/adr/0072-remote-exec-in-distributed-builds.md`
- Modify: `scripts/playtest/README.md`（「遠隔実行（開発者向け）」節）

**Interfaces:**
- Consumes: Task 1 の `RemoteExecActivation.ActivateIfRequested()`・`Deactivate()`
- Produces: 起動順 `ResolveFromCommandLine` → `BeginCurrentSessionMarks` → `ActivateIfRequested()` → `WebUiStartup.StartAsync`。`WebUiHost.KestrelPort` は存在しない

- [x] **Step 1: Web UI 側の相乗りを外す**

`WebUiEndpoints.cs` の次のブロック（現 97〜103 行）を丸ごと削除する:

```csharp
                if (path == Client.RemoteExec.RemoteExecEndpoint.Path)
                {
                    // 有効判定と拒否理由はエンドポイントに集約する
                    // Keep activation checks and rejection reasons in the endpoint
                    await Client.RemoteExec.RemoteExecEndpoint.HandleAsync(context);
                    return;
                }
```

`Client.WebUiHost.asmdef` の `references` から `"Client.RemoteExec",` の行を削除する（削除後に `grep -rn "Client.RemoteExec" moorestech_client/Assets/Scripts/Client.WebUiHost` が 0 件であること）。

`WebUiHost.cs` の次の 3 行（現 28〜30 行）を削除する:

```csharp
        // 未起動はnull。0は実ポート域外の値で、書けば「届かない入口」を正常系の数字として名乗る
        // Not started is null; 0 is outside the real port range and would pose as a normal value for an unreachable entry
        public static int? KestrelPort => _kestrel?.ActualPort;
```

（削除前に `grep -rn "KestrelPort" moorestech_client/Assets moorestech_server/Assets --include='*.cs'` で、使い手が `InitializeScenePipeline.cs` と `PlaytestBootUnityCallbackBindingTest.cs` だけであることを確かめる。他に居たら削除せず残し、bd note に理由を書く）

- [x] **Step 2: 起動配線を Web UI より前へ移す**

`InitializeScenePipeline.cs` の現 62〜79 行（`ResolveFromCommandLine` から `OnGameShutdown.Subscribe(... Deactivate())` まで）を次にする:

```csharp
            // 前回セッションの出所印より先に起動オプションを確定する
            // Resolve launch arguments before capturing the current session origin
            Client.RemoteExec.RemoteExecLaunchOption.ResolveFromCommandLine(Environment.GetCommandLineArgs());
            Playtest.PreviousSessionStartupTasks.BeginCurrentSessionMarks();
            // 遠隔実行はWeb UIより先に専用の待受で開く。Webホストが起動に失敗してもCEFの不具合を調べられるようにする
            // Remote exec opens on its own listener before the Web UI, so a failed Web host still leaves CEF faults diagnosable
            Client.RemoteExec.RemoteExecActivation.ActivateIfRequested();
            // 正常終了で遠隔実行の入口を撤去する。終了イベントを知らないClient.RemoteExecへここから配線する
            // A clean exit withdraws the remote-exec entry; Client.RemoteExec does not know the shutdown event, so it is wired from here
            // 無効な起動は入口を持たないので購読もしない（同じ置き場を使う他プロセスの入口に触る理由が無い）
            // A disabled boot owns no entry and does not subscribe, having no reason to touch another process's entry in the shared location
            if (Client.RemoteExec.RemoteExecLaunchOption.IsEnabled)
                GameShutdownEvent.OnGameShutdown.Subscribe(_ => Client.RemoteExec.RemoteExecActivation.Deactivate());
            // Play終了で各await継続を打ち切る。Task系境界の継続がEditModeで再開しシーンを汚すのを防ぐ
            // Play-mode exit cancels every await so Task-based continuations never resume in EditMode and dirty the scene
            var exitToken = Application.exitCancellationToken;
            await Initialization.Boot.WebUiStartup.StartAsync(exitToken);
```

（`webUiStarted` 変数は他で使われていないことを `grep -n webUiStarted` で確かめてから消す）

`WebUiStartup.cs:19` のログ文を次にする（albof.3 の持ち物。触るのはこの 1 行だけ）:

```csharp
                if (!started) Debug.LogWarning("[WebUiHost] 起動が完了しなかったため、Web UIなしで続行します");
```

触ったら: `bd note moorestech-albof.2 "albof.3の持ち物 WebUiStartup.cs:19 のログ文から『遠隔実行も開きません』を削った（遠隔実行がWebから独立したため）。WebUiHost.KestrelPort も使い手が消えたので削除。マージ時に衝突したら albof.3 側の構造に合わせてこの2点だけ再適用する"`

- [x] **Step 3: 起動順の検査を書き換える**

`PlaytestBootUnityCallbackBindingTest.cs` のテスト `InitializeScenePipeline_WebUi起動後の実ポートで遠隔実行を有効化する`（現 51〜65 行）を次で置き換え、`using Client.WebUiHost.Boot;` が不要になれば削除する:

```csharp
        [Test]
        public void InitializeScenePipeline_起動印の後かつWebUi起動より前に遠隔実行を有効化する()
        {
            // Webホストの成否に依らず遠隔実行が開く順を、本番の非同期入口で固定する
            // Pin, in the production async entry, the order that opens remote exec regardless of the Web host's outcome
            var moveNext = FindInitializeMoveNext();
            var beginMarks = typeof(PreviousSessionStartupTasks).GetMethod(nameof(PreviousSessionStartupTasks.BeginCurrentSessionMarks));
            var activate = typeof(RemoteExecActivation).GetMethod(nameof(RemoteExecActivation.ActivateIfRequested));
            var startWebUi = typeof(WebUiStartup).GetMethod(nameof(WebUiStartup.StartAsync), BindingFlags.Static | BindingFlags.NonPublic);

            Assert.That(startWebUi, Is.Not.Null);
            Assert.That(MethodCallInspector.CallsInOrder(moveNext, beginMarks, activate), Is.True);
            Assert.That(MethodCallInspector.CallsInOrder(moveNext, activate, startWebUi), Is.True);
        }
```

- [x] **Step 4: ADR 0072 を改訂する**

`docs/adr/0072-remote-exec-in-distributed-builds.md`:
- 2 行目 `状態: 採択` を `状態: 採択（2026-10-26 改訂: 入口の待受）` にする
- Decision の「**入口は既存の Web UI サーバー（Kestrel、`127.0.0.1`）に1本足す。**」の項の末尾に 1 行足す: `  2026-10-26 改訂で待受を置き換えた（下の Consequences の追記が正）。トークン・起動オプション・拒否の扱いはこの項のまま。`
- agent 前提の「入口はゲーム開始の初期化（Web UI サーバーの起動）から終了まで使える。」の項の末尾に 1 行足す: `  2026-10-26 改訂: 開くのは起動印の確定直後（Web UI の起動より前）。`
- Consequences の末尾に次の段落を足す:

```markdown
2026-10-26 改訂（入口の待受）: 遠隔実行の入口を Web UI の Kestrel から外し、`Client.RemoteExec` が持つ専用の `System.Net.HttpListener`（`127.0.0.1`、25100 から上へ 20 ポートを探索）で開く。Web ホストの起動に失敗しても遠隔実行は開き、検証機で CEF の不具合を調べる手段が残る。開く時機は起動印の確定直後で、Web UI の起動より前。変えたのは待受だけで、トークン・`access.json`（`port`・`token`・`processId`）・`--remoteExec`・パス `/api/remote-exec`・結果の 7 種・送信側 CLI は変えない。無効な起動では待受そのものを開かず、要求は接続拒否になる（以前の 404 と入口の拒否ログは出ず、起動ログ「起動オプションが無いため遠隔実行は無効です」が残る）。送信者の切断は検知しない（`HttpListener` に要求ごとの中断通知が無いため。実行は最後まで走り、届かなかった応答はログに残る）。配布ビルド（Mono・stripping 既定）で `HttpListener` が動くことは検証機で確かめた（`docs/research/2026-10-26-remote-exec-own-listener-windows.md`）。
  出所: agent 既定（ユーザー未裁定）。UI Toolkit 移行計画 `docs/uitk-migration/plan.md` 裁定 3 の推奨 (a)、2026-10-26 ユーザー指示「動く前提で並列で開始して」により推奨を agent 既定として進めた。
  棄却案: (b) 遠隔実行専用の最小 Kestrel（Kestrel と AspNetCore 系 NuGet が遠隔実行のためだけに残る。`HttpListener` が配布ビルドで動かない場合の代替）／(c) 専用の TCP 待受（HTTP の解釈を自前で持つことになる）／現状維持（Web ホストの起動失敗で遠隔実行も開かない）
```

（Task 5 で (b) に切り替えた場合は、この段落を Task 5 Step 4 の文面に差し替える）

- [x] **Step 5: README を直す**

`scripts/playtest/README.md` の「## 遠隔実行（開発者向け）」節の「ゲーム開始後に、C# のメソッド本体をファイルまたは標準入力から送る。」を次の 2 行にする:

```markdown
ゲーム開始の初期化（ワールドに入る直前）から、C# のメソッド本体をファイルまたは標準入力から送れる。
入口は Web UI とは別の専用の待受（`127.0.0.1:25100` から上）で、Web UI が起動に失敗しても使える。ポートは `access.json` に書かれる。
```

- [x] **Step 6: コンパイルし、EditMode テストを回す**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

Run: `grep -rn "Microsoft.AspNetCore" moorestech_client/Assets/Scripts/Client.RemoteExec; grep -rn "RemoteExec" moorestech_client/Assets/Scripts/Client.WebUiHost`
Expected: どちらも 0 件

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.RemoteExec\.|PlaytestBootUnityCallbackBindingTest"`
Expected: 全 PASS（`RemoteExecEndpointFailureTest` 4 件・`RemoteExecHttpListenerTest` 4 件・既存の `RemoteExecCompilerTest`/`RemoteExecHarmonyTest`/`RemoteExecLedgerTest`/`RemoteExecRunnerTest` 等・`PlaytestBootUnityCallbackBindingTest`）

- [x] **Step 7: コミットする（Task 1 と合わせて 1 コミット）**

```bash
git add moorestech_client/Assets/Scripts/Client.RemoteExec moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.Tests/RemoteExec moorestech_client/Assets/Scripts/Client.Tests/Playtest/PlaytestBootUnityCallbackBindingTest.cs docs/adr/0072-remote-exec-in-distributed-builds.md scripts/playtest/README.md
git commit -m "feat(remote-exec): 遠隔実行の待受をWeb UIのKestrelから外しHttpListenerの専用待受で開く"
```

（Unity が生成した `Listen/*.cs.meta`・`Listen.meta`・`RemoteExecHttpListenerTest.cs.meta` も `git status` で確かめて含める）

### Task 3: 通しの EditModeInPlayingTest を新しい待受で直す

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/RemoteExec/RemoteExecEndpointTest.cs`

**Interfaces:**
- Consumes: `RemoteExecActivation.ActivateIfRequested()`・`Deactivate()`、`RemoteExecHttpListener.BasePort`・`PortSearchRange`、`RemoteExecEndpoint.Path`、`RemoteExecAccessFile.HeaderName/DirectoryPath`
- Produces: なし（テストのみ）

- [x] **Step 1: テストを書き換える**

変更点（拒否・不正入力・成功 2 回・コンパイル失敗・台帳 6 行の検査はすべて現行の期待値のまま残す）:
- `using Client.WebUiHost.Boot;`・`using Client.WebUiHost.Common;` を削除し、`using Client.RemoteExec.Listen;`・`using System.Net.Sockets;` を足す
- `var kestrel = new KestrelServer();` と `await kestrel.StartAsync(new WebSocketHub());` を削除
- 無効起動の部分を次にする:

```csharp
                // 無効な起動では待受も接続情報も作らない
                // A disabled boot opens no listener and writes no access details
                RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
                RemoteExecActivation.ActivateIfRequested();
                Assert.IsFalse(File.Exists(Path.Combine(RemoteExecAccessFile.DirectoryPath, "access.json")));
```

- 有効起動の部分を次にする（`url` はポート確定後に組み立てる）:

```csharp
                // 本番経路でトークン/ポート取得
                // Get token/port through the production path
                RemoteExecLaunchOption.ResolveFromCommandLine(new[] { "--remoteExec" });
                RemoteExecActivation.ActivateIfRequested();
                var access = JObject.Parse(File.ReadAllText(Path.Combine(RemoteExecAccessFile.DirectoryPath, "access.json")));
                var token = access.Value<string>("token");
                var port = access.Value<int>("port");
                Assert.GreaterOrEqual(port, RemoteExecHttpListener.BasePort);
                Assert.Less(port, RemoteExecHttpListener.BasePort + RemoteExecHttpListener.PortSearchRange);
                Assert.AreEqual(64, token.Length);
                var url = $"http://127.0.0.1:{port}{RemoteExecEndpoint.Path}";
                var before = files.CountLedgerLines();
```

- 台帳の検査ループの後（`try` の末尾）に、無効化で待受が閉じる検査を足す:

```csharp
                // 無効な起動へ切り替えると待受が閉じ、同じポートへ接続できない
                // Switching to a disabled boot closes the listener, so the same port refuses connections
                RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
                var refused = false;
                using (var probe = new TcpClient())
                {
                    // 接続拒否はOSのネットワーク境界の例外として観測する
                    // A refused connection is observed as an exception at the OS network boundary
                    try { await probe.ConnectAsync(System.Net.IPAddress.Loopback, port); }
                    catch (SocketException) { refused = true; }
                }
                Assert.IsTrue(refused);
```

- `finally` を次にする:

```csharp
            finally
            {
                // 失敗時も状態を復元
                // Restore state even on failure
                RemoteExecActivation.Deactivate();
                RemoteExecLaunchOption.ResolveFromCommandLine(Array.Empty<string>());
                files.Restore();
            }
```

- [x] **Step 2: コンパイルし、PlayMode 込みの遠隔実行テストを回す**

Run: `uloop compile --project-path ./moorestech_client`
Expected: ErrorCount 0

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "EditModeInPlayingTest\.RemoteExec\." --timeout-seconds 900`
Expected: `RemoteExecEndpointTest`・`RemoteExecServerTargetTest` が PASS（ドメインリロード中のエラーは 45 秒待って再実行）

Run: `bash scripts/playtest/tests/test-remote-exec.sh && bash scripts/playtest/tests/test-remote-exec-offline.sh`
Expected: どちらも `OK`／PASS（送信側 CLI は無変更で通る）

- [x] **Step 3: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/RemoteExec/RemoteExecEndpointTest.cs
git commit -m "test(remote-exec): 通しの検査をKestrelなしの専用待受で行う"
bd note moorestech-albof.2 "Task1-3 完了: HttpListener専用待受・起動配線をWeb UIより前へ・ADR 0072改訂。Editor上のテスト全PASS。次は検証機の配布ビルド確認"
```

### Task 4: Windows 検証機の配布ビルドで確かめる（完了条件 R8）

Task 4 実行時判断（agent 既定・ユーザー未裁定）: 外部uploadを起動しないため、ケース2/3もphase1を使う。必要なら送信した本番remote snippetでsmokeの終了を一時保留する。実機側は一意の専用パスとPID/絶対exeパスの一致で所有を確かめ、他プロセスの名前killや既存フォルダの一括削除をしない。各ケースのワールドは既存の `MOORESTECH_DEFAULT_WORLD_DIRECTORY` で専用ディレクトリへ隔離する。Harmonyの本文サイズだけで `Expect: 100-continue` を断定せず、明示送信の実際のHTTP traceで確認する。

実測: 2026-10-26、コード57bd4b4709のWindows配布ビルドでR7/R8全3ケースPASS。既存CLIで2・Harmony20,777777,20・client/server別スレッド、2597 bytesの実Expectヘッダと100 Continue→200 OKを確認。Task5条件不成立。最終build-infoはdirty=Trueであり、詳細とログ全体の残差は `docs/research/2026-10-26-remote-exec-own-listener-windows.md` を参照。

**Files:**
- Create: `docs/research/2026-10-26-remote-exec-own-listener-windows.md`（記録）

**Interfaces:**
- Consumes: Task 1〜3 のコミット済みブランチ
- Produces: 記録ファイル（合否と出力抜粋）。`HttpListener` が配布ビルドで動かなければ Task 5 へ

前例: `docs/research/2026-09-27-remote-exec-e2e.md`。検証機は自動ログイン後にロック画面になるので、「ゲーム開始後」は入力を要しない `--playtestSmoke` に `--remoteExec` を足して作り、その間に要求を送る。Steam 初期化のため作業フォルダに `steam_appid.txt`（`1958160`）を置く。Steam（playtest-staging）には上げない。

- [x] **Step 1: Windows の配布ビルドを焼く**

batchmode は同じプロジェクトを開いた Editor と同時に走れない。`moores-wt status` で自分の worktree（`uitk-s0-remote-exec`）の Editor pid を確かめて `kill <pid>` で止めてから:

```bash
W="$(git rev-parse --show-toplevel)"
RUN=$HOME/hermes-agent/data/services/playtest/runs/remote-exec-listener-check
PIN=$(python3 -c 'import json;print(next(r["commitHash"] for r in json.load(open("'"$W"'/.moorestech-external-revisions.json"))["repositories"] if r["key"]=="moorestech_master")[:8])')
mkdir -p "$RUN"
MOORESTECH_BUILD_OUTPUT="$RUN/build-windows" MOORESTECH_STEAM_BUILD_LABEL=remote-exec-listener-check \
MOORESTECH_BUILD_BRANCH=feature/uitk-s0-remote-exec \
MOORESTECH_MASTER_DATA_ROOT="$HOME/hermes-agent/data/worktrees/moorestech_master/pin-$PIN" \
/Applications/Unity/Hub/Editor/6000.3.8f1/Unity.app/Contents/MacOS/Unity -batchmode -nographics \
  -projectPath "$W/moorestech_client" -executeMethod Client.Editor.Build.SteamPlaytestBuildCli.WindowsSteamPlaytestBuild \
  -logFile "$RUN/unity-build-windows.log"
```

Expected: 終了コード 0、`$RUN/build-windows/moorestech.exe`・`moorestech_Data/RemoteExec/0Harmony.dll`・`moorestech_Data/StreamingAssets/WebUi/dist` が在る。pin worktree が無ければ `moores-wt status` の表示に従って用意する（ピンは `.moorestech-external-revisions.json` が正）。

- [x] **Step 2: 検証機を起こして作業フォルダへ持ち込む**

```bash
. $HOME/hermes-agent/data/services/playtest/env.sh
. "$W/scripts/playtest/lib/verify-ssh.sh"
wakeonlan "$MOORESTECH_VERIFY_MAC" || true
until verify_ssh -o ConnectTimeout=5 "$MOORESTECH_VERIFY_USER@$MOORESTECH_VERIFY_HOST" "echo ok"; do sleep 10; done
verify_ssh_remote "powershell -NoProfile -Command \"Remove-Item -Recurse -Force 'C:/moorestech-remote-exec-listener' -ErrorAction SilentlyContinue; New-Item -ItemType Directory -Force -Path 'C:/moorestech-remote-exec-listener' | Out-Null\""
scp -o BatchMode=yes -r "$RUN/build-windows" "$MOORESTECH_VERIFY_USER@$MOORESTECH_VERIFY_HOST:C:/moorestech-remote-exec-listener/build"
verify_ssh_remote "powershell -NoProfile -Command \"Set-Content -NoNewline -Path 'C:/moorestech-remote-exec-listener/build/steam_appid.txt' -Value 1958160\""
```

（WoL 待ちは前景で回す。600 秒で到達しなければ止めて記録に書く）

- [x] **Step 3: 検証機側の起動スクリプトを置く**

`$RUN/launch.ps1`（リポジトリには入れない。検証用の使い捨て）を書いて scp する。`run-smoke.ps1` の `Start-SteamInInteractiveSession` と同じく使い捨てのスケジュールタスクで対話セッションに起動する:

```powershell
param([string]$Arguments)
$ErrorActionPreference = 'Stop'
$exe = 'C:\moorestech-remote-exec-listener\build\moorestech.exe'
Get-Process -Name moorestech -ErrorAction SilentlyContinue | Stop-Process -Force
Remove-Item "$env:APPDATA\.moorestech\RemoteExec\access.json" -ErrorAction SilentlyContinue
$taskName = "moorestech-remote-exec-check-" + [Guid]::NewGuid().ToString("N").Substring(0, 8)
$action = New-ScheduledTaskAction -Execute "cmd.exe" -Argument "/c start `"`" `"$exe`" $Arguments" -WorkingDirectory 'C:\moorestech-remote-exec-listener\build'
$principal = New-ScheduledTaskPrincipal -UserId (whoami) -LogonType Interactive
Register-ScheduledTask -TaskName $taskName -Action $action -Principal $principal -Force | Out-Null
Start-ScheduledTask -TaskName $taskName
Start-Sleep -Seconds 5
Unregister-ScheduledTask -TaskName $taskName -Confirm:$false
```

```bash
scp -o BatchMode=yes "$RUN/launch.ps1" "$MOORESTECH_VERIFY_USER@$MOORESTECH_VERIFY_HOST:C:/moorestech-remote-exec-listener/launch.ps1"
```

起動後の状態確認用 `$RUN/probe.ps1`（各ケースで使う。ゲームの pid・待受ポート・access.json・Player.log の `[RemoteExec]`/`[WebUiHost]` 行を出す。Player.log は `%USERPROFILE%\AppData\LocalLow\sakastudio\moorestech\Player.log`。`ProjectSettings.asset` の `companyName: sakastudio`）:

```powershell
$p = Get-Process -Name moorestech -ErrorAction SilentlyContinue
if ($p) { "pid=$($p.Id)"; Get-NetTCPConnection -State Listen -OwningProcess $p.Id -ErrorAction SilentlyContinue | ForEach-Object { "listen=$($_.LocalPort)" } }
$access = "$env:APPDATA\.moorestech\RemoteExec\access.json"
if (Test-Path $access) { Get-Content -Raw $access } else { 'access.json=none' }
$log = "$env:USERPROFILE\AppData\LocalLow\sakastudio\moorestech\Player.log"
if (Test-Path $log) { Select-String -Path $log -Pattern '\[RemoteExec\]|\[WebUiHost\]' | Select-Object -Last 30 | ForEach-Object { $_.Line } }
```

```bash
scp -o BatchMode=yes "$RUN/probe.ps1" "$MOORESTECH_VERIFY_USER@$MOORESTECH_VERIFY_HOST:C:/moorestech-remote-exec-listener/probe.ps1"
probe() { verify_ssh_remote "powershell -NoProfile -ExecutionPolicy Bypass -File C:/moorestech-remote-exec-listener/probe.ps1"; }
```

- [x] **Step 4: ケース 1 — `--remoteExec` なしでは待受が開かない（R8-2）**

```bash
verify_ssh_remote "powershell -NoProfile -ExecutionPolicy Bypass -File C:/moorestech-remote-exec-listener/launch.ps1 -Arguments \"--playtestSmoke --smokePhase phase1 --smokeResultDirectory C:/moorestech-remote-exec-listener/r1\""
sleep 40; probe
```

Expected: `access.json=none`、Listen ポートに 25100〜25119 が無い（Kestrel の 25050 帯だけ）、Player.log に `[RemoteExec] 起動オプションが無いため遠隔実行は無効です`、`[RemoteExec] 待受を開きました` が無い。さらに Kestrel の `/api/remote-exec` へ POST して 404（経路自体が消えた）を確かめる:

```bash
verify_ssh_remote "powershell -NoProfile -Command \"try { Invoke-WebRequest -UseBasicParsing -Method Post -Uri http://127.0.0.1:25050/api/remote-exec -Body '{}' } catch { \$_.Exception.Response.StatusCode.value__ }\""
```

Expected: `404`（Kestrel のポートが 25050 でなければ probe の Listen ポートのうち 25050 帯の値を使う）

- [x] **Step 5: ケース 2 — `--remoteExec` 付きで `Succeeded` と Harmony（R8-1・R7）**

phase1 のゲームが自分で終わるのを待つ（`probe` で `pid=` が消えるまで 10 秒おきに前景で確認、最長 300 秒）。その後:

```bash
verify_ssh_remote "powershell -NoProfile -ExecutionPolicy Bypass -File C:/moorestech-remote-exec-listener/launch.ps1 -Arguments \"--remoteExec --playtestSmoke --smokePhase phase2 --smokeResultDirectory C:/moorestech-remote-exec-listener/r2\""
until probe | grep -q '"port"'; do sleep 3; done; probe
printf 'return 1 + 1;' | bash "$W/scripts/playtest/remote-exec.sh" --windows -
echo $?
cat > "$RUN/harmony.cs" <<'CS'
using HarmonyLib;
using System.Reflection;
var original = typeof(Core.Update.GameUpdater).GetMethod("SecondsToTicks");
var postfix = typeof(RemoteExecSnippet).GetMethods(BindingFlags.NonPublic | BindingFlags.Static).Single(method => method.Name.Contains("g__Postfix|"));
var harmony = new Harmony("remote-exec-listener-check");
var before = Core.Update.GameUpdater.SecondsToTicks(1);
var during = 0u;
try { harmony.Patch(original, postfix: new HarmonyMethod(postfix)); during = (uint)Core.Update.GameUpdater.SecondsToTicks(1); }
finally { harmony.UnpatchAll("remote-exec-listener-check"); }
var after = Core.Update.GameUpdater.SecondsToTicks(1);
return $"{before},{during},{after}";

static void Postfix(ref uint __result) { __result = 777777; }
CS
bash "$W/scripts/playtest/remote-exec.sh" --windows "$RUN/harmony.cs"
printf 'return System.Threading.Thread.CurrentThread.ManagedThreadId;' | bash "$W/scripts/playtest/remote-exec.sh" --windows --target server -
```

Expected: `return 1 + 1;` → stdout の JSON が `"outcome":"Succeeded"`・`"result":"2"`、exit 0。access.json の `port` が 25100〜25119 で、probe の Listen ポートにその値が在る。Harmony → `"result":"<before>,777777,<before>"`。server → ワールドロード前なら `ServerUnavailable`（exit 2・設計どおり）、ロード後に再送して `Succeeded`。

`GameUpdater.SecondsToTicks(double)` の戻り値は `uint`（`moorestech_server/Assets/Scripts/Core.Update/GameUpdater.cs:47`）なので `during` と `Postfix` の `ref` は `uint` にしてある。前回記録の結果 `20,777777,20` と同じ形になる。

ゲームが smoke を終えて自分で閉じるまでに送り切れなかったら（`Connection refused`・access.json の pid 消失）、phase1（新規ワールド生成で長い）に `--remoteExec` を付けて同じ 3 本を送り直す。

- [x] **Step 6: ケース 3 — Web ホストを失敗させても遠隔実行は開く（R8-3）**

ゲームが閉じたのを probe で確かめてから:

```bash
verify_ssh_remote "powershell -NoProfile -Command \"Rename-Item 'C:/moorestech-remote-exec-listener/build/moorestech_Data/StreamingAssets/WebUi' 'WebUi.disabled'\""
verify_ssh_remote "powershell -NoProfile -ExecutionPolicy Bypass -File C:/moorestech-remote-exec-listener/launch.ps1 -Arguments \"--remoteExec --playtestSmoke --smokePhase phase2 --smokeResultDirectory C:/moorestech-remote-exec-listener/r3\""
until probe | grep -q '"port"'; do sleep 3; done; probe
printf 'return 1 + 1;' | bash "$W/scripts/playtest/remote-exec.sh" --windows -
echo $?
verify_ssh_remote "powershell -NoProfile -Command \"Rename-Item 'C:/moorestech-remote-exec-listener/build/moorestech_Data/StreamingAssets/WebUi.disabled' 'WebUi'\""
```

Expected: Player.log に `[WebUiHost] production artifact rejected` と `[RemoteExec] 待受を開きました`、Listen ポートに 25050 帯（Kestrel）が無く 25100 帯が在る、`return 1 + 1;` が `Succeeded`・exit 0。

- [x] **Step 7: 警告語を確かめ、記録を書いてコミットする**

各ケースの Player.log の `[RemoteExec]`・`[WebUiHost]` 行を `拒否|refus|Exception|Error|書けません|開けません|failed|失敗` で grep し、意図したもの（ケース 3 の `production artifact rejected`）以外が 0 件（ケース 1 の Kestrel の 404 は経路が無いだけでログを出さない）であることを確かめる。合否は肯定行の有無ではなくこの警告語ゼロで判定する。

`docs/research/2026-10-26-remote-exec-own-listener-windows.md` に、対象コミット・ビルド方法・3 ケースの出力抜粋（ケース 2 の Harmony 送信は本文が 1KB を超え `Expect: 100-continue` 付きの要求になるので、「本文の大きい要求」の実測を兼ねたと明記する）（JSON・exit・probe の Listen ポート・Player.log 行）・警告語 grep の結果・未検証の残差を前例 `docs/research/2026-09-27-remote-exec-e2e.md` の節立て（実施方法と plan からの差分／結果／未検証・残差）で書く。

```bash
git add docs/research/2026-10-26-remote-exec-own-listener-windows.md
git commit -m "docs(remote-exec): 専用待受の検証機での実機確認の記録"
bd note moorestech-albof.2 "検証機の配布ビルドで HttpListener 待受を確認（--remoteExec でSucceeded・Harmony、無しで待受なし、WebUi除去でも開く）。記録 docs/research/2026-10-26-remote-exec-own-listener-windows.md"
```

`HttpListener` が配布ビルドで動かなかった（ケース 2 で `[RemoteExec] 待受を開きました` が出ない・例外・接続できない）場合は、症状（Player.log の該当行・例外型）を記録に書いてコミットし、Task 5 へ進む。動いた場合 Task 5 は飛ばす。

### Task 5（条件付き）: `HttpListener` が配布ビルドで動かないときだけ、遠隔実行専用の最小 Kestrel に切り替える

Task 4 で `HttpListener` が動かなかった場合だけ実行する。動いた場合は Step 0 に「不要（Task 4 で動作確認済み）」とチェックして飛ばす。

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.RemoteExec/Listen/RemoteExecHttpListener.cs`（中身を Kestrel に差し替え。`Start(): int?`・`Stop()` と定数は変えない）
- Modify: `moorestech_client/Assets/Scripts/Client.RemoteExec/Listen/RemoteExecHttpExchange.cs`（`HttpContext` ⇔ DTO の変換に差し替え）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/RemoteExec/RemoteExecHttpListenerTest.cs`（必要なら）
- Modify: `docs/adr/0072-remote-exec-in-distributed-builds.md`（Task 2 Step 4 の段落を差し替え）
- Modify: `docs/research/2026-10-26-remote-exec-own-listener-windows.md`

（asmdef の変更は不要。`Client.RemoteExec.asmdef` は `overrideReferences: false` で、AspNetCore の DLL は自動参照される。本 PR 前の `RemoteExecEndpoint` も asmdef の参照なしで `Microsoft.AspNetCore.Http` を使っていた）

**Interfaces:**
- Consumes: Task 1 の DTO と `RemoteExecEndpoint.HandleAsync`
- Produces: Task 1 と同じ `RemoteExecHttpListener.Start()`／`Stop()`（呼び出し側・テストの期待値は不変）

- [ ] **Step 0: 要否を判定する**（Task 4 の結果で「不要」ならここで終わる）

- [ ] **Step 1: 待受を Kestrel で書き直す**

`KestrelServer.StartAsync` と同じく基準ポートから上へ bind を試す。`Start()` は同期の入口のまま保つため、bind を `webHost.StartAsync(CancellationToken.None).GetAwaiter().GetResult()` で待つ（Kestrel の起動は bind 完了で返り、メインスレッドを長く止めない。前例との差はここだけで、判断記録に書く）:

```csharp
// 置き換え後の RemoteExecHttpListener.Start の for ループ本体
var url = $"http://127.0.0.1:{port}";
var lifetime = new CancellationTokenSource();
var webHost = new WebHostBuilder()
    .UseKestrel()
    .UseUrls(url)
    .Configure(app => app.Run(context => RemoteExecHttpExchange.HandleAsync(context, lifetime.Token).AsTask()))
    .Build();
// ポート使用中の bind 失敗は IOException で通知される。OS ネットワーク境界の隔離のためここに限り try-catch を使用
// A bind on an occupied port surfaces as IOException; try-catch here only isolates the OS network boundary
try { webHost.StartAsync(CancellationToken.None).GetAwaiter().GetResult(); }
catch (IOException e) { webHost.Dispose(); lifetime.Dispose(); Debug.Log($"[RemoteExec] ポート {port} は使えないため次を試します: {e.Message}"); continue; }
_webHost = webHost; _lifetime = lifetime;
Debug.Log($"[RemoteExec] 待受を開きました {url}");
return port;
```

`Stop()` は `_lifetime.Cancel(); _webHost.StopAsync().ContinueWith(t => Debug.LogError($"[RemoteExec] 待受の停止に失敗しました: {t.Exception}"), TaskContinuationOptions.OnlyOnFaulted); _webHost.Dispose();` に置き換え、フィールドを null に戻して `待受を閉じました` をログに出す。

`RemoteExecHttpExchange.HandleAsync` を `HttpContext` 版に置き換える（`using Microsoft.AspNetCore.Http;` を足し、`System.Net`・`System.Text` の using を外す）:

```csharp
        internal static async UniTask HandleAsync(HttpContext context, CancellationToken lifetime)
        {
            // 送信者の切断と待受の停止のどちらでも実行を取り消す
            // Cancel the run on either a sender disconnect or a listener stop
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime, context.RequestAborted);
            var headers = context.Request.Headers;
            var request = new RemoteExecHttpRequest(context.Request.Path.Value, context.Request.Method,
                headers.ContainsKey("Origin"), headers.ContainsKey(RemoteExecAccessFile.HeaderName) ? headers[RemoteExecAccessFile.HeaderName].ToString() : null, context.Request.Body);
            var response = await RemoteExecEndpoint.HandleAsync(request, linked.Token);

            // HTTP送信境界の切断を記録し、実行結果が未達だったことを残す
            // Record disconnects at the HTTP send boundary so undelivered results remain visible
            try
            {
                context.Response.StatusCode = response.StatusCode;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(response.SerializeBody(), linked.Token);
            }
            catch (Exception e) when (e is IOException || e is OperationCanceledException)
            {
                Debug.LogWarning($"[RemoteExec] 応答を届けられませんでした: {e.Message}");
            }
        }
```

- [ ] **Step 2: コンパイルし Task 2 Step 6・Task 3 Step 2 のテストを回す**（期待値は同じ。全 PASS）
- [ ] **Step 3: Task 4 の Step 1〜7 をやり直す**（記録に「(b) に切り替えた理由」と再確認の結果を追記）
- [ ] **Step 4: ADR 0072 の 2026-10-26 改訂段落を (b) の文面に差し替える**: 「`Client.RemoteExec` が持つ遠隔実行専用の最小 Kestrel」「`HttpListener` は配布ビルドで動かなかった（症状: 記録へのリンク）」「Kestrel と AspNetCore 系 NuGet は遠隔実行のためにも残る（段階 5 の 5d で Web 用を消しても、遠隔実行の分は残す）」。棄却案に (a) を移す。出所は同じ agent 既定
- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.RemoteExec moorestech_client/Assets/Scripts/Client.Tests/RemoteExec docs/adr/0072-remote-exec-in-distributed-builds.md docs/research/2026-10-26-remote-exec-own-listener-windows.md
git commit -m "feat(remote-exec): 配布ビルドでHttpListenerが動かないため遠隔実行専用の最小Kestrelへ切り替える"
bd note moorestech-albof.2 "HttpListenerが配布ビルドで動かず(b)最小Kestrelへ切替。症状と再確認は docs/research/2026-10-26-remote-exec-own-listener-windows.md。段階5の5dはKestrel/AspNetCoreを遠隔実行分だけ残す前提に変わる"
```

### Task 6: 全ブランチレビュー（省略不可）

- [ ] **Step 1: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。** Codex ホストでは `references/codex-host.md` の runner で実行する。
- [ ] **Step 2: レビュー指摘の反映が判定経路・条件式・評価時点（入口の許可判定・有効化の順序・待受の開閉）に触れたら、反映後のコードで Task 3 Step 2 の EditModeInPlayingTest を再実施し、さらに Task 4（検証機）を焼き直して再実施してから完了とする。** ユニットテスト通過・ログ無音は代替にならない。
- [ ] **Step 3: 残課題の起票。** plan・記録・進捗台帳に書いた「未検証」「残差」を 1 件ずつ `bd create --parent moorestech-albof.2` し、記録の末尾に issue id を列挙する。

### Task 7: PR を作り、セッションを閉じられる状態にする（省略不可）

- [ ] **Step 1: セッション終了可能状態にすること: pr-create スキルで PR を作成し、master とのコンフリクトがあれば master をマージして解消・コンパイル確認のうえ push する（解消の実作業は pr-create 経由で opus subagent に委譲される）。** マージ方式はマージコミット（Squash しない）。PR は作成までで止め、マージしない（moorestech の規約）。PR 本文に次の 2 節を入れる:
  - 「ユーザーに見てほしい判断」: 本 plan の `## 判断記録（ADR）` のうち出所が「agent 既定（ユーザー未裁定）」の項目をすべて列挙する
  - 「ユーザーに見てほしい判断」の先頭には、ADR 0072 の元のユーザー裁定（入口は既存の Web UI サーバーに足す・専用の待ち受けは棄却）を agent 既定で覆したことを置く
  - 「他レーンへの影響」: albof.3 の持ち物 `WebUiStartup.cs:19`（ログ文）と `WebUiHost.KestrelPort`（削除）を触ったこと
  - 「本 PR 外のリファクタ提案」: 既存側 `Client.WebUiHost/Boot/KestrelServer.cs`／新設側 `Client.RemoteExec/Listen/RemoteExecHttpListener.cs`／同じ「基準ポートから上へ bind を試す」探索（統合するなら両アセンブリの下に共通層が要る。段階 5 で Web 用 Kestrel を消すなら統合は不要）
- [ ] **Step 2: 全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認して終える。** `bd note moorestech-albof.2 "PR #<番号> 作成"` を残す。worktree の片付け（`moores-wt rm`）はこの環境の規約どおり PR 作成後に行う。

---

## Self-Review 記録

- Requirements coverage: R1→T1/T2、R2→T1、R3→T2、R4→T1/T3、R5→T3/T4、R6→T1、R7→T3/T4、R8→T4、R9→T5、R10→T2。
- 保留・縮退経路（解消条件と最小構成）:

| 状態 | 起きる時機 | 誰が気づくか | 解消 |
|---|---|---|---|
| 探索帯 20 ポートが全部使用中 | 有効起動の初期化 | Player.log の `空きポートが無いため待受を開けません`（ERROR）・`remote-exec.sh` は access.json 無しで失敗 | 再起動（ポートを空けて）。最小構成: 他プロセスが帯を占有。Harmony も access.json も作らないので、パッチ能力だけ残る状態にはならない |
| 受付ループの想定外の故障 | 実行中 | Player.log の `待受が要求を受け取れなくなりました`（ERROR）・送信側は接続失敗 | 再起動 |
| access.json を書けない | 有効起動の初期化 | 既存どおり ERROR、要求は `トークン未発行` で 403 | 再起動（環境を直す） |
| 送信者の切断 | 実行中 | 応答の送信失敗が `応答を届けられませんでした`（WARNING） | 実行は最後まで走る（切断で取り消さない。判断記録参照） |
| Editor の再生し直しで前回の待受が残る | Editor | `Start()` が先に `Stop()` するので二重に開かない | 自動 |

- 「解かない」側と対の経路: 無効な起動で待受を開かない（Task 3 の無効→`access.json` 無し、有効→無効で接続拒否）と、有効な起動で開く（同テストの成功 2 回と台帳）を同じテストで固定している。
- 共有状態の持ち主: 待受（`RemoteExecHttpListener` の static、起動ごとに 1 つ）、トークン（`RemoteExecAccessFile`、有効化で 1 回）。待受の寿命 = 有効化から `Deactivate`（正常終了）または無効な起動の `ResolveFromCommandLine` まで。異常終了では OS がソケットを閉じ、access.json の残骸は既存の `processId` 生存確認で弾かれる。

## 判断記録（ADR）

- 設計の正本は `docs/adr/0072-remote-exec-in-distributed-builds.md`（本 PR で改訂）。上位の計画は `docs/uitk-migration/plan.md` §2.1 の 0-3。
- 待受を `HttpListener` にする（裁定 3 の (a)）。動かなければ (b) 遠隔実行専用の最小 Kestrel（Task 5）。出所: agent 既定（ユーザー未裁定）。計画 `docs/uitk-migration/plan.md` 裁定 3 の推奨、2026-10-26 ユーザー指示「動く前提で並列で開始して」。
- 待受のポートは Kestrel の前例（`KestrelServer.StartAsync` の基準ポートから上へ bind を試す）に合わせ、25100 から 20 ポートを探索する。OS 自動採番（ポート 0）にしないのは、`HttpListener` に bind した実ポートを返す API が無いため（ポート 0 のプレフィックスを Mono が受け付けるかは未確認＝推測を避けて前例に寄せた）。出所: agent 判断。
- 送信者の切断で実行を取り消さない。Kestrel では `RequestAborted` が切断を伝えたが、`HttpListener` には要求ごとの中断通知が無い。取り消しのトークンは待受の寿命（`Stop()` で取り消し）だけにする。実行は最後まで走り、届かない応答は `応答を届けられませんでした`（WARNING）に残る。結果の 7 種（`Cancelled` を含む）は変えない。出所: agent 判断（ADR 0072 の 2026-10-26 改訂に明記）。
- 遠隔実行を開く時機を Web UI の起動より前（起動印の確定直後）へ移す。Web ホストの起動待ち（Vite の疎通確認など）で止まっていても遠隔実行が使えるようにするため。台帳（`RemoteExecLedger`）は `BeginCurrentSessionMarks` の中で初期化されるので、この順で間に合う。出所: agent 判断。
- 受付ループは `ConfigureAwait(false)` でスレッドプールに置く。メインスレッドが止まっているときでも server 実行の要求を受け取れるようにするため（client/server 両対象とも `RemoteExecRunner` がコンパイル等の準備でメインスレッドへ切り替えるため、メインが止まっていれば実行準備は待つ。HTTP 受付の継続とコード実行の進行は別であり、これは現行と同じ）。出所: agent 判断。
- `RemoteExecEndpoint` は DTO（`RemoteExecHttpRequest` → `RemoteExecHttpResponse`）の関数にし、待受の種類（`HttpListener`／Kestrel）は `RemoteExecHttpExchange` だけが知る。(b) へ切り替えても入口の判定とテストが変わらないようにするため。interface にせず具象 DTO にしたのは、実装が 1 つで差し替える場面が無いため（YAGNI）。出所: agent 判断。
- 専用待受は `/api/remote-exec` 以外の経路を 404（`BadRequest`・理由ログ付き）で返す。待受を遠隔実行の専用にしたので、他の経路を受ける理由が無いため。出所: agent 判断。
- `WebUiHost.KestrelPort` を削除し、`WebUiStartup.cs:19` のログ文を直す（どちらも albof.3 の持ち物）。前者は使い手が本 PR で消えるため（使われない public を残さない規約）、後者は「遠隔実行も開きません」が誤りになるため。どちらも数行で、衝突したら albof.3 側の構造に合わせて再適用する。出所: agent 判断（bd note で albof.3 側へ知らせる）。
- unityプレイ録画テストは入れない。変更は開発者専用ツールの待受と起動配線で、ゲームプレイ・入力・カメラ・UI・エンティティ表示には触れない。起動配線（`InitializeScenePipeline`）の実挙動は既存の EditModeInPlayingTest `RemoteExecEndpointTest`（PlayMode で本番の起動を通す）と、検証機の配布ビルドでの実機確認（Task 4）で確かめる。出所: agent 判断。
- 検証機の確認は前回（`docs/research/2026-09-27-remote-exec-e2e.md`）と同じく、`--playtestSmoke` に `--remoteExec` を足して「ゲーム開始後」を作り、その間に要求を送る。検証機は自動ログイン後にロック画面になり、タイトルから手で開始できないため。Steam 初期化のため作業フォルダに `steam_appid.txt` を置く。Steam 配布は更新しない。出所: agent 判断（前例どおり）。
- Web ホストを失敗させる手段は、ビルドの `moorestech_Data/StreamingAssets/WebUi` を改名して `WebUiArtifactValidator` に成果物を拒否させる。製品コードに失敗注入の口を足さずに済み、検証後に名前を戻せば元のビルドに戻るため。出所: agent 判断（ユーザー指示の例どおり）。
- Phase 2.6 強（検査7・ADR 矛盾）: 本 plan は ADR 0072 の Decision「入口は既存の Web UI サーバーに1本足す（棄却案: 専用の待ち受けを新設）」を反転する。→ このままでよい（ADR を同じ PR で改訂する）。依頼文が「ADR 0072 … 本 PR で改訂する」「待受は agent 既定 (a) HttpListener」と明示しており、上位計画の裁定 3 も「どれを選んでも改訂が要る」としている。ユーザーの元の裁定を覆すので、PR 本文の「ユーザーに見てほしい判断」の先頭に置く。出所: agent 既定（ユーザー未裁定。スキルが聞けと定める問いを、ユーザー指示「質問しない」により推奨案で決めた）。
- Phase 2.6 強（検査6・第 3 バケツ）: `RemoteExecHttpListener.Start()` のポート探索は `KestrelServer.StartAsync` と同じ役割。→ plan のタスクには足さず、PR 本文の「本 PR 外のリファクタ提案」節に「既存側 `Client.WebUiHost/Boot/KestrelServer.cs`／新設側 `Client.RemoteExec/Listen/RemoteExecHttpListener.cs`／同じ『基準ポートから上へ bind を試す』探索。統合するなら `Client.RemoteExec` と `Client.WebUiHost` の下の共通層が要る（段階 5 で Web 用 Kestrel を消すなら統合自体が不要になる）」と 1 行で書く。出所: agent 既定（ユーザー未裁定）。
- Phase 2.6 弱（1 問にまとめて推奨どおり採る）: (1) 撤去の呼び口が `ResolveFromCommandLine` と `Deactivate` の 2 口に分かれる → 採る。無効分岐は `RemoteExecActivation.Deactivate()` を呼び、撤去の本体は `Deactivate` 1 つにした（`Start()` 冒頭の `Stop()` は再生し直しの二重起動防止で、撤去ではないので残す）。(2) `IsEnabled` の判定が 4 箇所 → 採らない。`RemoteExecEndpoint` の判定は待受が残った場合の防御（テストで固定済み）、`InitializeScenePipeline` の購読条件は「入口を持つ起動だけが撤去を購読する」で、`Client.RemoteExec` は終了イベントを知らないため呼び出し側に置く既存の形。(3) Task 5 の `Path.Value ?? ""` → 採る。null のまま渡し、入口の経路判定で 404 にする。出所: agent 既定（ユーザー未裁定）。
- 実行環境が `HttpListener` を持たない場合は `HttpListener.IsSupported` の分岐で理由を残して諦める。型の読み込み失敗（stripping で型が消えた等）は try-catch で包まない（AGENTS.md の外部境界の列挙に当たらないため）。その場合は `--remoteExec` 付きの起動だけが初期化で例外を出して止まり、Task 4 のケース 2 で必ず表に出て Task 5 へ進む判断材料になる。出所: agent 判断（再検査の注目点を受けて）。
- `RemoteExecLaunchOption.ResolveFromCommandLine` の無効分岐は `RemoteExecActivation.Deactivate()` を呼ぶ（同じアセンブリ内で判定役が撤去を呼び返す形になる）。撤去の手順を 1 か所に保つことを優先した。無効起動の撤去を `ActivateIfRequested` 側へ移す案は、撤去が `BeginCurrentSessionMarks` より後ろへずれるので採らない。出所: agent 判断。
- Phase 2.6 再検査の弱 2 件（`Stop()` の呼び口が Editor の後片付けを含めて多い → `Deactivate` のコメントに「Editor の後片付けはソケットだけ」と書いて字面を合わせる／PR 本文の節の波及 → Task 7 Step 1 に「リファクタ提案」節と並び順を足す）は両方採った。出所: agent 既定（ユーザー未裁定）。
- user-simulator review（Fable 判事）: 適用推奨 0・Warning 3・要裁定 0。Warning は 3 件とも反映した。(1) ポート帯の台帳が 2 アセンブリに割れる → `RemoteExecHttpListenerTest` に Kestrel 帯・Vite 帯と重ならない検査を足した（`Client.Tests` は両方を参照できる）。(2) 探索帯 20 ポートを塞ぐ `待受を開けなければHarmonyも接続情報も用意しない` は、別 worktree の Editor が同じ瞬間に `RemoteExecEndpointTest` を回すと相手側が「空きポートが無い」で落ちうる（塞ぐのはミリ秒単位。再実行で解消）。(3) 検証機の Harmony 送信は `Expect: 100-continue` 付きの大きい要求の実測を兼ねると記録に書く。出所: シミュレーター予測（agent 既定で反映。ユーザー未承認）。

- Task 1–2 実装時のコンパイル依存: Task 3 の `RemoteExecEndpointTest` は `ActivateIfRequested()` の引数削除だけ先行して同一コミットに含め、待受・検査の変更は Task 3 の別コミットにする。出所: agent 既定（ユーザー未裁定）。Task 1 Step 8 の一時的なエラー観測は省き、Task 1–2 をまとめた通常 compile で ErrorCount 0 を確認した。
