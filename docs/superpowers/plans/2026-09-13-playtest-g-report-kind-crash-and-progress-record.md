# プレイテスト G: 報告種別・前回異常終了・進行記録・同意表示 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** ポーズメニューのプレイ報告に種別（バグ／感想）を足し、前回の異常終了をタイトルで検知して前回の記録を `kind=crash` の箱として送れるようにし、セッションの進行記録を購読で集めて終了時に outbox へ書き、初回起動時に「送られる内容」の同意表示を一度だけ出す（ADR 0058 のクライアント側 5 裁定）。

**Architecture:** (1) plan B の `BugReportManifest`・`BugReportBundleWriter`・`bug_report.submit` に `kind` を通し、webui は既存の共通 `ModeSwitch` でバグ／感想を選ばせる。(2) 正常終了の意図は `GameShutdownEvent.OnGameShutdown` の購読で `CLEAN_EXIT` マーカーを書き、起動時にマーカーの有無で前回異常終了を判定してから消す。マーカーが無ければ前回の録画リング・スナップショット・パケットログ・`Player-prev.log`・クラッシュダンプを `BugReports/last-session/` へ退避し、タイトル（ADR 0040 の言語選択ゲートと同型の全画面ゲート）で説明欄付きの送信確認を出す。(3) 進行記録は `ProgressRecorder` が `ProgressRecords/current/` に `header.json` と `events.jsonl` を追記し、終了時（`IGameShutdownParticipant`）に純関数 `ProgressRecordComposer` が `record.json` を組んで outbox へ書く。イベントはサーバーの既存イベントパケット購読・`UIStateControl.OnStateChanged` 購読・操作直後のプッシュだけで集め、`Update()` の毎tick判定は足さない。(4) 同意表示は既読フラグファイルで一度きり判定する同型のゲート。(5) `steamId` は plan D が差し替える `IPlaytestSessionIdentity` 経由、`buildInfo` は plan E が焼く `StreamingAssets/build-info.json` を読むだけで、どちらも未実装なら空文字／null で動く。

**Tech Stack:** Unity C#（`Client.Game`・`Client.Starter`・`Client.WebUiHost`・`Client.Tests`・`Server.Protocol`・`Game.Paths`）、VContainer、UniRx、UniTask、MessagePack、Newtonsoft.Json、React 18＋Mantine（webui）、vitest、Localization CSV。

## Requirements

- R1. 報告種別: ポーズメニューの報告フォームに「バグ／感想」の択一トグル（既定: バグ）を置き、`bug_report.submit` の payload に `kind` を足して `manifest.kind` へ書く。受入: vitest で既定が `bug`・切替後の送信 payload が `{ description, kind: "feedback" }`、EditMode 単体テストで `manifest.json` の `kind` が渡した値になる。
- R2. 種別の値域: `kind` は `"bug"|"feedback"|"crash"` のみ。`bug_report.submit` が範囲外を受けたら `ActionResult.Fail("invalid_kind")` で拒否し理由をログに出す。受入: EditMode 単体テストで `"crash"` と未知値の両方が action から拒否される（`crash` はゲート経由でしか作らない）。
- R3. 正常終了マーカー: `GameShutdownEvent.OnGameShutdown` の購読で `<GameSystemDirectory>/BugReports/last-session/CLEAN_EXIT` を書き、起動時に「有無を読んでから削除」する。受入: EditMode 単体テストで、マーカーがある起動は `WasPreviousExitClean()==true`・無い起動は false になり、どちらの場合も呼び出し後にマーカーが消えている。
- R4. 前回記録の退避: 起動時（内蔵サーバー開始前）に、前回異常終了なら `BugReports/recording/`（録画リング残骸）と `<ワールド>/snapshots/`（スナップショット・パケットログ）を `BugReports/last-session/` へ移動し、`Player-prev.log` とクラッシュダンプの実パスを解決する。正常終了なら録画リングを削除する。リングは終了時には消さない。受入: EditMode 単体テストで、不正終了フラグ有りなら退避先にファイルが移り、有りでも無しでも元ディレクトリが空になる。
- R5. 前回異常終了の確認ゲート: 前回異常終了のとき、タイトル（MainGame ロード完了後・オープニング前）で全画面ゲートを出し、説明欄と「送る」「送らない」を出す。「送る」で `kind=crash` の箱を outbox に書き、どちらでもゲートを閉じてゲームを始める。受入: EditMode 単体テストでゲートの待機・応答が1回だけ効き、送信時に `READY` と `manifest.kind=="crash"` が揃う。vitest でゲートの描画と2ボタンの dispatch を確認する。
- R6. ワールドのプレイ時間: `va:getWorldPlaySessionInfo` で `worldCreatedAt`・`totalPlaySeconds` をセッション開始時に取得する。受入: サーバー CombinedTest で応答が返り、`totalPlaySeconds` が 0 以上・`worldCreatedAt` が ISO 文字列。
- R7. 進行記録の器: セッション開始で `ProgressRecords/current/header.json` を書き、イベントを `events.jsonl` へ追記し、終了時に `record.json`（§3 の形）を `ProgressRecords/outbox/<id>/` に書いて `READY` を置く。受入: EditMode 単体テストで 0 件・1 件のイベントどちらでも `record.json` が書け、必須キーが全て出る。
- R8. 途中終了の回収: 起動時に `ProgressRecords/current/` が残っていたら必ず閉じて outbox へ出す。前回異常終了なら `endReason="crash-recovered"`、正常終了なら `endReason="quit"`。受入: EditMode 単体テストで、残骸あり×クリーン／非クリーンの2ケースがそれぞれの `endReason` で outbox に出て `current/` が空になる。
- R9. 進行記録のイベント: 研究完了・チャレンジ達成はサーバーの既存イベントパケット購読、UI遷移は `UIStateControl.OnStateChanged` 購読、ブロック設置は `PlaceBlockEventPacket` 購読、クラフトは `craft.execute` の送信直後プッシュ、報告送信は `bug_report.submit` の成功直後プッシュで記録する。`Update()` のポーリングは足さない。受入: EditMode 単体テストで各 push が `events.jsonl` に1行ずつ出る。
- R10. 集計値: `record.json` の `reachedChallenges`・`completedResearch` は初期ハンドシェイクの完了集合とセッション中の完了イベントの和、`placedBlockCount`・`craftCount` はイベント数、`lastUiState` は最後の `uiStateChanged`、`buildModeCancelled` は「PlaceBlock 滞在中に `blockPlaced` が1件も無いまま抜けた」ときに合成する。受入: 純関数テストで、設置あり／なしの PlaceBlock 滞在それぞれについて合成の有無が変わる。
- R11. 同意表示: 初回起動時のみ、タイトルで「送られる内容」を全画面で出し、了解ボタンでローカル既読フラグを書いて先へ進む。文言は日本語・英語・ドイツ語。受入: EditMode 単体テストでフラグ無し起動だけ待機し、フラグ有り起動は待機しない。vitest で本文と了解ボタンが描かれる。
- R12. `steamId`・`buildInfo`: プレイ報告 manifest と進行記録 record の両方に `steamId`（未取得は `""`）と `buildInfo`（`build-info.json` 不在なら `null`）を入れる。受入: EditMode 単体テストで、`build-info.json` 不在時に `buildInfo` が `null`・`steamId` が `""` で書け、JSON が壊れない。
- R13. ローカライズ: 追加文言（種別トグル・クラッシュ確認・同意表示）を `Localization/localization.csv` に ja/en/de で足し、`pnpm gen:i18n` と C# 生成を通す。受入: `L.ui.playtest.*` が TS/C# 両方から参照できる。
- やらないこと: 受け口へのアップロード（plan D）／`build-info.json` の生成と Windows 配布ビルド（plan E）／セーブ互換とマスタ欠損の除去（plan F）／Mac mini の取り込み・日次ダイジェスト（plan H）／報告ごとの添付選択UI（ADR 0058 で不要と裁定）／進行記録を断る設定トグル（同）／Unity CrashReport 等のクラッシュ自動検知ハンドラ（同）／起動時の許可リスト照合（plan D）。

## Global Constraints

- 作業ブランチ: `feature/playtest-client-report`。
- **依存: plan B（`docs/superpowers/plans/2026-09-11-bug-report-b-client-capture-and-report-ui.md`）と plan A（同 `-a-server-foundation.md`）は 2026-09-13 時点で未実装である。** 本planは plan A → plan B の完了コミットを土台に実行する。plan B の `BugReportManifest`・`BugReportOutbox`・`BugReportBundleWriter`・`BugReportCaptureSession`・`BugReportSubmitActionHandler`・`GameFrameRecorder`・`RepositoryStateProbe`・`BugReportForm`（webui）と、plan A の `WorldDataDirectory.SnapshotDirectory`・`WorldSnapshotRing` が存在しない状態では本planのどのタスクも開始しない。
- 型名・ファイル名は本plan記載のとおり（`PlaytestReportKind`・`BuildInfo`・`BuildInfoReader`・`IPlaytestSessionIdentity`・`EmptyPlaytestSessionIdentity`・`CleanExitMarker`・`CleanExitMarkWriter`・`PreviousSessionArtifacts`・`PreviousSessionSalvage`・`PlayerLogLocator`・`CrashDumpLocator`・`CrashBundleWriter`・`CrashReportGate`・`CrashReportGateTopic`・`CrashReportRespondActionHandler`・`PlaytestConsentFlag`・`PlaytestConsentGate`・`PlaytestConsentGateTopic`・`AcknowledgePlaytestConsentActionHandler`・`PlaytestGateBinder`・`PlaytestStartGates`・`GetWorldPlaySessionInfoProtocol`・`WorldPlaySessionInfo`・`ProgressRecordPaths`・`ProgressRecordHeader`・`ProgressEventEntry`・`ProgressEventType`・`ProgressRecordFiles`・`ProgressRecordComposer`・`ProgressRecorder`・`IPlaytestProgressSink`）。
- `.cs` を変更したら `uloop compile --project-path ./moorestech_client`。テストは `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "<正規表現>"`。EditModeInPlayingTest は `--test-mode EditMode --timeout-seconds 900`（Domain Reload エラーは45秒待って再実行）。webui は `cd moorestech_web/webui && pnpm test`・`pnpm lint`。
- webui は `.agents/skills/webui-design/SKILL.md` のホワイトリストに従う: 種別トグルは共通 `ModeSwitch`（`@/shared/ui`）、説明欄は素の `<textarea>`（plan B `BugReportForm` の `.description` を再利用）、ボタンは `PanelActionButton`、色は `tokens.css` の変数のみ、寸法は固定長トークン（%禁止）、`z-index` 直書き禁止（`--z-portal-*`）。vitest は `.test.ts` のみ（`.tsx` は include されない）。JSX は `createElement`＋`react-test-renderer`（前例 `features/settings/LanguageSelect.test.ts`）。
- WebUI⇄C#: 新アクションは `bridge/transport/actionContract.ts` の `ActionPayloads` と `ACTION_TYPES` の両方に追加。新トピックは `bridge/contract/schemas/ui.ts`（zod）→ `payloadTypes.ts` → `validators.ts` → `transport/protocol.ts`（`Topics` と `TopicPayloads`）の4箇所に追加し、`App.tsx` で描画する。C# 側 DTO は `WebUiJson.Serialize`（camelCase）。
- コメントは「// 日本語 → // English」2行セット。1ファイル200行以下、1ディレクトリ10ファイル以下。partial・`Func<>`・デフォルト引数・単純getter/setter禁止。イベントは UniRx。`Update()` ポーリング禁止。try-catch は外部境界（外部プロセス・ネットワーク送受信・外部入力JSONのパース）に限り、境界である根拠をコメントに書く。
- fail-closed 経路（マーカー不在・退避元不在・ダンプ不在・プロトコル失敗・JSONパース失敗）は必ず `Debug.LogWarning/LogError` と manifest／record の `missing` に理由を残す。無音の縮退禁止。
- 経過時間: クライアントの実世界時刻は `DateTime.UtcNow`（セッション開始・終了・累計プレイ時間の記録用途は AGENTS.md で許可）。サーバーtickは `Core.Update.GameUpdater.CurrentTick` を読むだけ。ゲームロジックの経過時間計測は本planに無い。

### 共有契約（`scratchpad/plans/shared-contracts.md` §2・§3 の逐語転記。変更禁止・矛盾禁止）

**§2. outbox（plan B `BugReportOutbox` を流用）**

- プレイ報告: `<GameSystemDirectory>/BugReports/outbox/<id>/` … plan B のファイル群 + `manifest.json` に追加フィールド `kind`（"bug"|"feedback"|"crash"）、`steamId`（string、Steam未起動なら ""）、`buildInfo`（§1、Editorなら null）。`READY` で完了。送信済みは `UPLOADED` マーカー（rsync経路は plan C の `SHIPPED`）。
- 進行記録: `<GameSystemDirectory>/ProgressRecords/outbox/<id>/record.json` + `READY` + 送信後 `UPLOADED`。
- 前回異常終了: `<GameSystemDirectory>/BugReports/last-session/` に「正常終了マーカー `CLEAN_EXIT`」。起動時に無ければ前回異常終了。常時記録のリング（`BugReports/recording/`・サーバースナップショットリング）は終了時に消さない。

**§3. 進行記録 record.json**

```json
{ "schemaVersion": 1, "steamId": "7656...", "buildInfo": {...§1}, "sessionStart": "<ISO>", "sessionEnd": "<ISO>", "endReason": "quit|crash-recovered",
  "playSeconds": 1234.5, "worldCreatedAt": "<ISO>", "totalPlaySeconds": 5678.9,
  "reachedChallenges": ["<guid>"], "completedResearch": ["<guid>"], "placedBlockCount": 120, "craftCount": 40,
  "lastUiState": "GameScreen", "events": [ { "t": "<ISO>", "tick": 12345, "type": "researchCompleted|challengeCompleted|uiStateChanged|buildModeCancelled|blockPlaced|reportSent", "data": {} } ] }
```

イベントは購読で取る（研究完了・チャレンジ達成はサーバーの既存イベントパケット購読、UI遷移は `UIStateControl.OnStateChanged`）。`Update()` ポーリング禁止。

**§1 の BuildInfo（plan E が生成、本planは読むだけ）** — `StreamingAssets/build-info.json`:

```json
{ "commit": "<40hex>", "branch": "master", "masterDataCommit": "<40hex>", "dirty": false,
  "steamBuildLabel": "playtest-20260913-1730", "builtAt": "2026-09-13T17:30:00+09:00", "target": "StandaloneWindows64" }
```

C# 型は `Client.Game/InGame/BugReport/BuildInfo.cs`（plan B の `BugReportManifest.Repository` の代替として `manifest.buildInfo` に埋める）。Editor実行時は `build-info.json` 不在なので plan B の git probe を使う。

### コミット

各タスク末尾でコミットする。コミットメッセージ末尾に以下を付ける:

```
Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
Claude-Session: https://claude.ai/code/session_01HtaDWGdhRRiNho39vPY5Li
```

---

### Task 1: 報告種別（`PlaytestReportKind`・`manifest.kind`・`bug_report.submit` の `kind`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Playtest/PlaytestReportKind.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportManifest.cs`（`Kind` フィールド追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportBundleWriter.cs`（`WriteAsync` に `kind` 引数）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/BugReportSubmitActionHandler.cs`（payload の `kind` を検証して渡す）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/PlaytestReportKindTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/BugReportSubmitKindTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/NullBugReportCaptureSources.cs`

**Interfaces:**
- Consumes: plan B `BugReportManifest`・`BugReportBundleWriter.WriteAsync(BugReportCapturedData, string)`・`BugReportCaptureSession`・`IBugReportCaptureSources`・`PauseMenuStateService`・`ActionResult`
- Produces:
  - `public static class PlaytestReportKind { public const string Bug = "bug"; public const string Feedback = "feedback"; public const string Crash = "crash"; public static bool IsSubmittableFromPauseMenu(string kind); public static bool IsKnown(string kind); }`
  - `BugReportManifest.Kind`（`public string Kind;` → JSON キー `kind`）
  - `BugReportBundleWriter.WriteAsync(BugReportCapturedData data, string description, string kind)`（plan B の2引数版を置き換える。呼び出し側は本タスクで全て更新する）
  - `public sealed class NullBugReportCaptureSources : IBugReportCaptureSources`（後続タスクのテストも使う）

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/BugReport/PlaytestReportKindTest.cs`:
```csharp
using Client.Game.InGame.BugReport.Playtest;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class PlaytestReportKindTest
    {
        [Test]
        public void ポーズメニューから送れるのはバグと感想だけ()
        {
            Assert.IsTrue(PlaytestReportKind.IsSubmittableFromPauseMenu(PlaytestReportKind.Bug));
            Assert.IsTrue(PlaytestReportKind.IsSubmittableFromPauseMenu(PlaytestReportKind.Feedback));
            Assert.IsFalse(PlaytestReportKind.IsSubmittableFromPauseMenu(PlaytestReportKind.Crash));
            Assert.IsFalse(PlaytestReportKind.IsSubmittableFromPauseMenu("bugs"));
            Assert.IsFalse(PlaytestReportKind.IsSubmittableFromPauseMenu(""));
            Assert.IsFalse(PlaytestReportKind.IsSubmittableFromPauseMenu(null));
        }

        [Test]
        public void クラッシュは既知の種別として扱う()
        {
            Assert.IsTrue(PlaytestReportKind.IsKnown(PlaytestReportKind.Crash));
            Assert.IsFalse(PlaytestReportKind.IsKnown("unknown"));
        }
    }
}
```

`Client.Tests/BugReport/NullBugReportCaptureSources.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Tests.BugReport
{
    // 確保セッションを空で組むためのテスト用取得元。全て既定値を返し何も記録しない
    // A test source that builds an empty capture session; returns defaults and records nothing
    public sealed class NullBugReportCaptureSources : IBugReportCaptureSources
    {
        public UniTask<long> RequestServerCapture() => UniTask.FromResult(0L);
        public void CutRecordingSegment() { }
        public IReadOnlyList<string> CompletedVideoSegments() => Array.Empty<string>();
        public string RecordingUnavailableReason() => "テスト用の取得元なので録画しない";
        public IReadOnlyList<(long unixMs, ulong tick)> FrameTicks() => Array.Empty<(long, ulong)>();
        public IReadOnlyList<UnityLogEntry> Logs() => Array.Empty<UnityLogEntry>();
        public ClientStateSnapshot ClientState() => new(Vector3.zero, Vector3.zero, Vector3.zero, "PauseMenu", 0);
        public UniTask<string> CaptureScreenshot() => UniTask.FromResult<string>(null);
    }
}
```

`Client.Tests/BugReport/BugReportSubmitKindTest.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Client.WebUiHost.Game.Actions;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class BugReportSubmitKindTest
    {
        [Test]
        public void manifestにkindが出る()
        {
            var manifest = new BugReportManifest
            {
                CreatedAt = "2026-09-13T12:00:00Z",
                Description = "説明",
                Platform = "OSXEditor",
                IsEditor = true,
                Kind = PlaytestReportKind.Feedback,
                SnapshotTicks = new List<ulong>(),
                SnapshotFiles = new List<string>(),
                PacketLogFiles = new List<string>(),
                Missing = new List<MissingItem>(),
            };
            var json = JObject.Parse(manifest.ToJson());
            Assert.AreEqual("feedback", (string)json["kind"]);
        }

        [Test]
        public void 範囲外のkindはactionが拒否する()
        {
            // kind 検証は確保セッション解決より前段なので、素で組んだハンドラで足りる
            // The kind check runs before the capture session is resolved, so a plainly built handler suffices
            var session = new BugReportCaptureSession(new NullBugReportCaptureSources());
            var handler = new BugReportSubmitActionHandler(new BugReportBundleWriter(), session, new PauseMenuStateService(session));

            var crash = handler.ExecuteAsync(new JObject { ["description"] = "説明", ["kind"] = "crash" }).GetAwaiter().GetResult();
            Assert.IsFalse(crash.Ok);
            Assert.AreEqual("invalid_kind", crash.Error);

            var unknown = handler.ExecuteAsync(new JObject { ["description"] = "説明", ["kind"] = "nope" }).GetAwaiter().GetResult();
            Assert.AreEqual("invalid_kind", unknown.Error);

            var missing = handler.ExecuteAsync(new JObject { ["description"] = "説明" }).GetAwaiter().GetResult();
            Assert.AreEqual("invalid_kind", missing.Error);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PlaytestReportKind` が無い／`BugReportManifest.Kind` が無いというコンパイルエラー

- [ ] **Step 3: `PlaytestReportKind` を実装する**

`Client.Game/InGame/BugReport/Playtest/PlaytestReportKind.cs`:
```csharp
namespace Client.Game.InGame.BugReport.Playtest
{
    // プレイ報告の種別。文字列は受け口・取り込み側と共有する契約値なのでここが正本（ADR 0058）
    // The play-report kind; these strings are the contract shared with the receiver and the ingest side (ADR 0058)
    public static class PlaytestReportKind
    {
        public const string Bug = "bug";
        public const string Feedback = "feedback";
        public const string Crash = "crash";

        // クラッシュは前回異常終了ゲートだけが作る。ポーズメニューからは選ばせない
        // Only the previous-crash gate creates the crash kind; the pause menu must not offer it
        public static bool IsSubmittableFromPauseMenu(string kind)
        {
            return kind == Bug || kind == Feedback;
        }

        public static bool IsKnown(string kind)
        {
            return kind == Bug || kind == Feedback || kind == Crash;
        }
    }
}
```

- [ ] **Step 4: manifest と書き出しに `kind` を通す**

`BugReportManifest.cs` の `public string Description;` の直後に足す:
```csharp
        // プレイ報告の種別。取り込み側は bug のときだけ自動修正ランを起動する（ADR 0058）
        // The report kind; the ingest side starts an auto-fix run only for bug (ADR 0058)
        public string Kind;
```

`BugReportBundleWriter.cs` の `WriteAsync` を3引数にし、manifest 初期化子に `Kind` を足す（以降の本文は plan B のまま）:
```csharp
        public async UniTask<BugReportBundleResult> WriteAsync(BugReportCapturedData data, string description, string kind)
        {
            var directory = BugReportOutbox.CreateBundleDirectory(DateTime.UtcNow, Guid.NewGuid().ToString("N").Substring(0, 8));
            var manifest = new BugReportManifest
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                Description = description,
                Kind = kind,
                Platform = Application.platform.ToString(),
                IsEditor = Application.isEditor,
                ReportTick = data.ReportTick,
                ClientState = data.ClientState,
                Missing = new List<MissingItem>(data.Missing),
            };
```

`BugReportSubmitActionHandler.ExecuteAsync` の説明文チェック直後（`TakeCapturedData()` より前）に足し、書き出し呼び出しへ渡す:
```csharp
            // 種別は webui のトグルが必ず載せる。載っていない・範囲外は壊れた要求として拒否する
            // The webui toggle always sends a kind; a missing or out-of-range value is a broken request
            var kind = payload?["kind"]?.ToString() ?? "";
            if (!PlaytestReportKind.IsSubmittableFromPauseMenu(kind))
            {
                Debug.LogWarning($"プレイ報告の種別が不正なため送信しません kind:{kind}");
                return ActionResult.Fail("invalid_kind");
            }
```
```csharp
            var result = await _writer.WriteAsync(data, description, kind);
```
（`using Client.Game.InGame.BugReport.Playtest;` を追加する）

- [ ] **Step 5: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.BugReport\.(PlaytestReportKindTest|BugReportSubmitKindTest)$"`
Expected: 全PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/BugReportSubmitActionHandler.cs moorestech_client/Assets/Scripts/Client.Tests/BugReport
git commit -m "feat(client): プレイ報告に種別を通しmanifestへ書く"
```

---

### Task 2: ポーズメニューの種別トグル（webui）とローカライズ

**Files:**
- Modify: `Localization/localization.csv`（3行追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`（`dummyText` を更新）
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts`（`bug_report.submit` の payload に `kind`）
- Modify: `moorestech_web/webui/src/features/pauseMenu/BugReportForm.tsx`
- Modify: `moorestech_web/webui/src/features/pauseMenu/BugReportForm.test.ts`

**Interfaces:**
- Consumes: Task 1 の `kind` 契約（`"bug"|"feedback"`）、plan B `BugReportForm`（props `{ status, onSent }`）と `style.module.css` の `.description`・`.status`、`@/shared/ui` の `ModeSwitch`
- Produces:
  - `bug_report.submit` の payload 型 `{ description: string; kind: string }`
  - ローカライズキー `ui.playtest.reportKind.label`・`ui.playtest.reportKind.bug`・`ui.playtest.reportKind.feedback`

- [ ] **Step 1: ローカライズ行を追加して生成する**

`Localization/localization.csv` 末尾に追加（列: key,Source,english,japanese,german）:
```
ui.playtest.reportKind.label,Report type,Report type,報告の種別,Art der Meldung
ui.playtest.reportKind.bug,Bug,Bug,バグ,Fehler
ui.playtest.reportKind.feedback,Impressions,Impressions,感想,Eindrücke
```
`_CompileRequester.cs` の `dummyText` を `uuidgen | tr a-z A-Z` の値へ変更する。

Run: `cd moorestech_web/webui && pnpm gen:i18n && cd ../.. && uloop compile --project-path ./moorestech_client`
Expected: `src/shared/i18n/generated/localizationKeys.ts` に `playtest.reportKind` が出て、C# 側 `LocalizationKeys.Ui.Playtest.ReportKind.Bug` が生成される

- [ ] **Step 2: 失敗するテストを書く**

`src/features/pauseMenu/BugReportForm.test.ts` の `dictionary` に3キーを足す:
```ts
const dictionary = {
  "ui.bugReport.placeholder": "何が起きた？",
  "ui.bugReport.send": "バグ報告を送信",
  "ui.bugReport.sent": "書き出しました",
  "ui.bugReport.capturePending": "記録を確保しています…",
  "ui.bugReport.missing": "欠けている項目: {items}",
  "ui.playtest.reportKind.label": "報告の種別",
  "ui.playtest.reportKind.bug": "バグ",
  "ui.playtest.reportKind.feedback": "感想",
};
```
既存の `vi.mock("@/shared/ui", ...)` に `ModeSwitch` のモックを足す:
```ts
vi.mock("@/shared/ui", () => ({
  PanelActionButton: ({ children, onClick, testId }: { children: unknown; onClick: () => void; testId?: string }) =>
    createElement("mock-button", { onClick, "data-testid": testId }, children as never),
  ModeSwitch: ({ value, onChange, testId }: { value: string; onChange: (v: string) => void; testId?: string }) =>
    createElement("mock-mode-switch", { value, onChange, "data-testid": testId }),
}));
```
既存の「入力後の送信で bug_report.submit を説明文付きで送りトーストを出す」の期待値を `{ description: "ベルトが止まる", kind: "bug" }` に直し、テストを1件足す:
```ts
  it("感想へ切り替えるとkindがfeedbackになる", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ hasSession: true, capturePending: false, missing: [] });
    act(() => renderer.root.findByProps({ "data-testid": "bug-report-kind" }).props.onChange("feedback"));
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "序盤が長い" } }));
    await act(async () => renderer.root.findByProps({ "data-testid": "bug-report-send" }).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", { description: "序盤が長い", kind: "feedback" });
    act(() => renderer.unmount());
  });
```

Run: `cd moorestech_web/webui && pnpm test -- BugReportForm`
Expected: FAIL（`bug-report-kind` が見つからない／payload に `kind` が無い）

- [ ] **Step 3: 契約とフォームを実装する**

`actionContract.ts` の `ActionPayloads` の `"bug_report.submit"` を差し替える:
```ts
  "bug_report.submit": { description: string; kind: string };
```

`BugReportForm.tsx` を差し替える:
```tsx
// ポーズメニュー直置きのプレイ報告欄。Escape時点の記録はC#側が確保済みで、ここは種別と説明文と送信だけを担う
// The play-report form in the pause menu; C# has secured the Escape-moment records, this adds only kind, text and send
import { useState } from "react";
import { dispatchAction, type PauseMenuData } from "@/bridge";
import { emitToast } from "@/features/toast";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch, PanelActionButton } from "@/shared/ui";
import styles from "./style.module.css";

type Props = {
  status: PauseMenuData["bugReport"];
  onSent: () => void;
};

// 契約値はC#の PlaytestReportKind と同じ文字列。既定はバグ（ADR 0058）
// The contract strings match C#'s PlaytestReportKind; bug is the default (ADR 0058)
const KindBug = "bug";
const KindFeedback = "feedback";

export function BugReportForm({ status, onSent }: Props) {
  const { t } = useI18n();
  const [description, setDescription] = useState("");
  const [kind, setKind] = useState<string>(KindBug);

  const send = async () => {
    const trimmed = description.trim();
    if (trimmed.length === 0) return;
    const ok = await dispatchAction("bug_report.submit", { description: trimmed, kind });
    if (!ok) return;
    emitToast(t(L.ui.bugReport.sent), "info");
    setDescription("");
    onSent();
  };

  return (
    <>
      <span className={styles.status}>{t(L.ui.playtest.reportKind.label)}</span>
      <ModeSwitch
        value={kind}
        options={[
          { value: KindBug, label: t(L.ui.playtest.reportKind.bug), testId: "bug-report-kind-bug" },
          { value: KindFeedback, label: t(L.ui.playtest.reportKind.feedback), testId: "bug-report-kind-feedback" },
        ]}
        onChange={setKind}
        testId="bug-report-kind"
      />
      <textarea
        className={styles.description}
        value={description}
        placeholder={t(L.ui.bugReport.placeholder)}
        onChange={(e) => setDescription(e.currentTarget.value)}
        data-testid="bug-report-description"
      />
      {status.capturePending && <span className={styles.status} data-testid="bug-report-status">{t(L.ui.bugReport.capturePending)}</span>}
      {status.missing.length > 0 && (
        <span className={styles.status} data-testid="bug-report-status">{t(L.ui.bugReport.missing, { items: status.missing.join(", ") })}</span>
      )}
      <PanelActionButton onClick={send} testId="bug-report-send">{t(L.ui.bugReport.send)}</PanelActionButton>
    </>
  );
}
```

- [ ] **Step 4: テストと lint**

Run: `cd moorestech_web/webui && pnpm test -- BugReportForm && pnpm lint`
Expected: PASS

- [ ] **Step 5: コミットする**

```bash
git add Localization/localization.csv moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs moorestech_web/webui/src
git commit -m "feat(webui): プレイ報告の種別トグルをポーズメニューに置く"
```

---

### Task 3: 正常終了マーカーと前回セッションの退避（`CleanExitMarker`・`PreviousSessionSalvage`）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs`（`BugReportLastSessionDirectory` を追加）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/CleanExitMarker.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/CleanExitMarkWriter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/PreviousSessionArtifacts.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/PreviousSessionSalvage.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/PlayerLogLocator.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/CrashDumpLocator.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Recording/GameFrameRecorder.cs`（`Initialize` の録画ディレクトリ削除をやめる）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/InitializeScenePipeline.cs`（起動直後に退避を走らせる）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（`CleanExitMarkWriter` を EntryPoint 登録）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/CleanExitMarkerTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/PreviousSessionSalvageTest.cs`

**Interfaces:**
- Consumes: plan A `WorldDataDirectory.SnapshotDirectory`、plan B `GameSystemPaths.BugReportDirectory`／`BugReportRecordingDirectory`／`GameFrameRecorder`、`Client.Game.Common.GameShutdownEvent.OnGameShutdown`
- Produces:
  - `GameSystemPaths.BugReportLastSessionDirectory`（`<GameSystemDirectory>/BugReports/last-session`）
  - `public static class CleanExitMarker { public const string FileName = "CLEAN_EXIT"; public static string FilePath { get; } public static bool ConsumePreviousExitCleanFlag(); public static void MarkCleanExit(); }`（`ConsumePreviousExitCleanFlag` は「読んでから消す」1回きりの判定）
  - `public sealed class CleanExitMarkWriter : IInitializable, IDisposable`（`GameShutdownEvent.OnGameShutdown` 購読で `MarkCleanExit()`）
  - `public sealed class PreviousSessionArtifacts { public bool PreviousExitWasClean; public string RecordingDirectory; public string SnapshotsDirectory; public string PlayerLogPath; public List<string> CrashDumpFiles; public List<MissingItem> Missing; public bool HasAnythingToSend { get; } }`
  - `public static class PreviousSessionSalvage { public static PreviousSessionArtifacts Artifacts { get; } public static PreviousSessionArtifacts RunAtStartup(string worldSnapshotDirectory); public static PreviousSessionArtifacts Salvage(bool previousExitWasClean, string recordingDirectory, string worldSnapshotDirectory, string lastSessionDirectory); }`
  - `public static class PlayerLogLocator { public static string PreviousSessionLogPath(); }`
  - `public static class CrashDumpLocator { public static List<string> FindDumpFiles(); public static IReadOnlyList<string> CandidateRoots(); }`

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/BugReport/CleanExitMarkerTest.cs`:
```csharp
using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class CleanExitMarkerTest
    {
        [SetUp]
        [TearDown]
        public void RemoveMarker()
        {
            if (File.Exists(CleanExitMarker.FilePath)) File.Delete(CleanExitMarker.FilePath);
        }

        [Test]
        public void マーカーがあれば前回は正常終了と判定し消える()
        {
            CleanExitMarker.MarkCleanExit();
            Assert.IsTrue(File.Exists(CleanExitMarker.FilePath));
            Assert.IsTrue(CleanExitMarker.ConsumePreviousExitCleanFlag());
            Assert.IsFalse(File.Exists(CleanExitMarker.FilePath));
        }

        [Test]
        public void マーカーが無ければ前回は異常終了と判定する()
        {
            Assert.IsFalse(CleanExitMarker.ConsumePreviousExitCleanFlag());
            Assert.IsFalse(File.Exists(CleanExitMarker.FilePath));
        }
    }
}
```

`Client.Tests/BugReport/PreviousSessionSalvageTest.cs`:
```csharp
using System;
using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class PreviousSessionSalvageTest
    {
        private string _root;
        private string _recording;
        private string _snapshots;
        private string _lastSession;

        [SetUp]
        public void CreateDirectories()
        {
            _root = Path.Combine(Path.GetTempPath(), $"moorestech-salvage-{Guid.NewGuid():N}");
            _recording = Path.Combine(_root, "recording");
            _snapshots = Path.Combine(_root, "snapshots");
            _lastSession = Path.Combine(_root, "last-session");
            Directory.CreateDirectory(_recording);
            Directory.CreateDirectory(_snapshots);
            File.WriteAllText(Path.Combine(_recording, "seg_00.mp4"), "video");
            File.WriteAllText(Path.Combine(_snapshots, "tick_600.json"), "{}");
            File.WriteAllText(Path.Combine(_snapshots, "packets_601.bin"), "b");
        }

        [TearDown]
        public void DeleteDirectories()
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, true);
        }

        [Test]
        public void 異常終了なら録画とスナップショットを退避する()
        {
            var artifacts = PreviousSessionSalvage.Salvage(false, _recording, _snapshots, _lastSession);

            Assert.IsFalse(artifacts.PreviousExitWasClean);
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.RecordingDirectory, "seg_00.mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.SnapshotsDirectory, "tick_600.json")));
            Assert.IsTrue(File.Exists(Path.Combine(artifacts.SnapshotsDirectory, "packets_601.bin")));
            Assert.IsTrue(artifacts.HasAnythingToSend);

            // 次のセッションのリングが前回分の上に書かないよう、元は空になっている
            // The originals are emptied so the next session's ring never writes on top of them
            Assert.AreEqual(0, Directory.GetFiles(_recording).Length);
            Assert.AreEqual(0, Directory.GetFiles(_snapshots).Length);
        }

        [Test]
        public void 正常終了なら退避せず元を消す()
        {
            var artifacts = PreviousSessionSalvage.Salvage(true, _recording, _snapshots, _lastSession);

            Assert.IsTrue(artifacts.PreviousExitWasClean);
            Assert.IsNull(artifacts.RecordingDirectory);
            Assert.IsNull(artifacts.SnapshotsDirectory);
            Assert.AreEqual(0, Directory.GetFiles(_recording).Length);

            // スナップショットはサーバーのリングが世代管理するので消さない
            // Snapshots stay because the server ring manages their generations
            Assert.AreEqual(2, Directory.GetFiles(_snapshots).Length);
        }

        [Test]
        public void 退避元が空でも欠損理由を残して続行する()
        {
            File.Delete(Path.Combine(_recording, "seg_00.mp4"));
            var artifacts = PreviousSessionSalvage.Salvage(false, _recording, "/nonexistent/snapshots", _lastSession);

            Assert.IsNull(artifacts.RecordingDirectory);
            Assert.IsNull(artifacts.SnapshotsDirectory);
            StringAssert.Contains("recording", string.Join(",", artifacts.Missing.ConvertAll(m => m.Item)));
            StringAssert.Contains("snapshots", string.Join(",", artifacts.Missing.ConvertAll(m => m.Item)));
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `CleanExitMarker`／`PreviousSessionSalvage` が無いというコンパイルエラー

- [ ] **Step 3: パスとマーカーを実装する**

`GameSystemPaths.cs` の `BugReportRecordingDirectory`（plan B が追加）の直後に:
```csharp
        // 前回セッションの正常終了マーカーと退避物の置き場。起動時にだけ読む
        // Holds the previous session's clean-exit marker and salvaged files; read only at boot
        public static string BugReportLastSessionDirectory => Path.Combine(BugReportDirectory, "last-session");
```

`LastSession/CleanExitMarker.cs`:
```csharp
using System;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 正常終了の意図が表明されたことだけを記録するマーカー。クラッシュは終了パイプラインに入れないので書かれない
    // A marker recording only that a graceful exit was intended; a crash never enters the shutdown pipeline, so it stays absent
    public static class CleanExitMarker
    {
        public const string FileName = "CLEAN_EXIT";

        public static string FilePath => Path.Combine(GameSystemPaths.BugReportLastSessionDirectory, FileName);

        // 起動時に1回だけ呼ぶ。読んだ時点で消して、次の終了で書き直させる
        // Call once at boot; the marker is removed on read so the next exit rewrites it
        public static bool ConsumePreviousExitCleanFlag()
        {
            var path = FilePath;
            var wasClean = File.Exists(path);
            if (wasClean) File.Delete(path);
            else Debug.LogWarning("前回の正常終了マーカーがありません。前回は異常終了として扱います");
            return wasClean;
        }

        public static void MarkCleanExit()
        {
            var path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        }
    }
}
```

`LastSession/CleanExitMarkWriter.cs`:
```csharp
using System;
using Client.Game.Common;
using UniRx;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 終了パイプラインの発火を購読してマーカーを書く。終了処理側にプレイテストの語彙を持ち込まないための購読
    // Subscribes to the shutdown pipeline and writes the marker, keeping playtest vocabulary out of the shutdown code
    public sealed class CleanExitMarkWriter : IInitializable, IDisposable
    {
        private IDisposable _subscription;

        public void Initialize()
        {
            // 参加者の書き出し完了を待たずに、意図が表明された時点で書く（強制終了されても正常終了として残す）
            // Written the moment the intent is declared, without awaiting participant flushes, so a forced kill still counts as graceful
            _subscription = GameShutdownEvent.OnGameShutdown.Subscribe(_ => CleanExitMarker.MarkCleanExit());
        }

        public void Dispose()
        {
            _subscription?.Dispose();
        }
    }
}
```

- [ ] **Step 4: 退避とログ・ダンプの解決を実装する**

`LastSession/PreviousSessionArtifacts.cs`:
```csharp
using System.Collections.Generic;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 前回セッションから拾えたものの一覧。拾えなかったものは Missing に理由付きで残す
    // What could be salvaged from the previous session; whatever could not be is listed in Missing with a reason
    public sealed class PreviousSessionArtifacts
    {
        public bool PreviousExitWasClean;
        public string RecordingDirectory;
        public string SnapshotsDirectory;
        public string PlayerLogPath;
        public List<string> CrashDumpFiles = new();
        public List<MissingItem> Missing = new();

        public bool HasAnythingToSend => RecordingDirectory != null || SnapshotsDirectory != null || PlayerLogPath != null || CrashDumpFiles.Count > 0;
    }
}
```

`LastSession/PreviousSessionSalvage.cs`:
```csharp
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 起動直後に前回の記録を退避する。内蔵サーバーのリングと録画リングが上書きを始める前に走らせること
    // Salvages the previous records right at boot, before the embedded server's ring and the recording ring start overwriting
    public static class PreviousSessionSalvage
    {
        public static PreviousSessionArtifacts Artifacts { get; private set; }

        public static PreviousSessionArtifacts RunAtStartup(string worldSnapshotDirectory)
        {
            var wasClean = CleanExitMarker.ConsumePreviousExitCleanFlag();
            Artifacts = Salvage(wasClean, GameSystemPaths.BugReportRecordingDirectory, worldSnapshotDirectory, GameSystemPaths.BugReportLastSessionDirectory);
            Debug.Log($"前回セッションの退避が終わりました clean:{Artifacts.PreviousExitWasClean} sendable:{Artifacts.HasAnythingToSend} missing:{Artifacts.Missing.Count}");
            return Artifacts;
        }

        public static PreviousSessionArtifacts Salvage(bool previousExitWasClean, string recordingDirectory, string worldSnapshotDirectory, string lastSessionDirectory)
        {
            var artifacts = new PreviousSessionArtifacts { PreviousExitWasClean = previousExitWasClean };
            Directory.CreateDirectory(lastSessionDirectory);

            // 正常終了なら前回分は要らない。録画だけ捨て、スナップショットはサーバーのリングに任せる
            // A clean exit needs nothing kept: drop the recording and leave the snapshots to the server's ring
            if (previousExitWasClean)
            {
                ClearDirectory(recordingDirectory);
                return artifacts;
            }

            artifacts.RecordingDirectory = MoveFilesInto(recordingDirectory, Path.Combine(lastSessionDirectory, "recording"), "recording", artifacts);
            artifacts.SnapshotsDirectory = MoveFilesInto(worldSnapshotDirectory, Path.Combine(lastSessionDirectory, "snapshots"), "snapshots", artifacts);

            artifacts.PlayerLogPath = PlayerLogLocator.PreviousSessionLogPath();
            if (artifacts.PlayerLogPath == null) artifacts.Missing.Add(new MissingItem { Item = "playerLog", Reason = "前回セッションのPlayer-prev.logが見つからない" });

            artifacts.CrashDumpFiles = CrashDumpLocator.FindDumpFiles();
            if (artifacts.CrashDumpFiles.Count == 0) artifacts.Missing.Add(new MissingItem { Item = "crashDump", Reason = $"クラッシュダンプが見つからない（探索先: {string.Join(", ", CrashDumpLocator.CandidateRoots())}）" });

            return artifacts;
        }

        // 中身のあるディレクトリだけを退避先へ移す。空・不在は欠損として残し、呼び出し側は null で受ける
        // Moves only a non-empty directory; empty or absent is recorded as missing and returned as null
        private static string MoveFilesInto(string source, string destination, string item, PreviousSessionArtifacts artifacts)
        {
            if (source == null || !Directory.Exists(source))
            {
                artifacts.Missing.Add(new MissingItem { Item = item, Reason = $"退避元が無い: {source}" });
                return null;
            }
            var files = Directory.GetFiles(source);
            if (files.Length == 0)
            {
                artifacts.Missing.Add(new MissingItem { Item = item, Reason = $"退避元が空: {source}" });
                return null;
            }

            ClearDirectory(destination);
            Directory.CreateDirectory(destination);
            foreach (var file in files) File.Move(file, Path.Combine(destination, Path.GetFileName(file)));
            return destination;
        }

        private static void ClearDirectory(string directory)
        {
            if (!Directory.Exists(directory)) return;
            foreach (var file in Directory.GetFiles(directory)) File.Delete(file);
        }
    }
}
```

`LastSession/PlayerLogLocator.cs`:
```csharp
using System.IO;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 前回セッションのUnityログ。Unityは起動時に前回のPlayer.logをPlayer-prev.logへ回すのでそれを拾う
    // The previous session's Unity log; Unity rotates the old Player.log to Player-prev.log at boot, so that is what we take
    public static class PlayerLogLocator
    {
        public const string PreviousLogFileName = "Player-prev.log";

        public static string PreviousSessionLogPath()
        {
            // Editorでは Editor-prev.log ではなく consoleLogPath の隣を見る。どちらの実行形態も同じ規則で解決する
            // In the Editor this looks next to consoleLogPath as well, resolving both run modes by the same rule
            var currentLogPath = Application.consoleLogPath;
            if (string.IsNullOrEmpty(currentLogPath)) return null;
            var directory = Path.GetDirectoryName(currentLogPath);
            if (directory == null) return null;
            var candidate = Path.Combine(directory, PreviousLogFileName);
            return File.Exists(candidate) ? candidate : null;
        }
    }
}
```

`LastSession/CrashDumpLocator.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;

namespace Client.Game.InGame.BugReport.LastSession
{
    // Unityが残すクラッシュダンプの置き場。Windowsは %LOCALAPPDATA%\Temp\<company>\<product>\Crashes が既定
    // Where Unity leaves crash dumps; on Windows the default is %LOCALAPPDATA%\Temp\<company>\<product>\Crashes
    public static class CrashDumpLocator
    {
        public const string CrashesFolderName = "Crashes";

        public static IReadOnlyList<string> CandidateRoots()
        {
            var roots = new List<string>();
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (!string.IsNullOrEmpty(localAppData))
            {
                roots.Add(Path.Combine(localAppData, "Temp", UnityEngine.Application.companyName, UnityEngine.Application.productName, CrashesFolderName));
                roots.Add(Path.Combine(localAppData, UnityEngine.Application.companyName, UnityEngine.Application.productName, CrashesFolderName));
            }

            // macOS はクラッシュレポータのdiagnosticsに残る。Windows検証機と同じ入口で拾えるよう並べておく
            // macOS keeps them in the crash reporter's diagnostics folder; listed here so one entry point covers both
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(home)) roots.Add(Path.Combine(home, "Library", "Logs", "DiagnosticReports"));
            return roots;
        }

        // 直近24時間ぶんだけを拾う。過去の無関係なダンプで箱を膨らませない
        // Takes only the last 24 hours so unrelated old dumps never inflate the box
        public static List<string> FindDumpFiles()
        {
            var since = DateTime.UtcNow.AddHours(-24);
            var found = new List<string>();
            foreach (var root in CandidateRoots())
            {
                if (!Directory.Exists(root)) continue;
                foreach (var file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                {
                    var info = new FileInfo(file);
                    if (info.LastWriteTimeUtc < since) continue;
                    if (!IsDumpLikeName(info.Name)) continue;
                    found.Add(file);
                }
            }
            return found;
        }

        private static bool IsDumpLikeName(string fileName)
        {
            var name = fileName.ToLowerInvariant();
            return name.EndsWith(".dmp") || name.EndsWith(".crash") || name.EndsWith(".ips") || name == "error.log";
        }
    }
}
```

- [ ] **Step 5: 録画リングの起動時削除をやめ、起動シーケンスへ組み込む**

`GameFrameRecorder.Initialize()` の次の1行を削除する（前回の残骸の所有者は `PreviousSessionSalvage` に一本化する）:
```csharp
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
```
削除した位置に理由を書く:
```csharp
            // 前回分の掃除は起動時の PreviousSessionSalvage が済ませている。ここで消すと異常終了の残骸を失う
            // PreviousSessionSalvage already cleaned the previous files at boot; deleting here would lose the crash remnants
```

`InitializeScenePipeline.Initialize()` の `var serverDirectory = args.ServerDataDirectory;` の直後に:
```csharp
            // 内蔵サーバーのスナップショットリングと録画リングが上書きを始める前に、前回セッションの記録を退避する
            // Salvage the previous session's records before the embedded snapshot ring and the recording ring start overwriting
            var previousWorldSnapshotDirectory = _proprieties.IsRemoteConnection ? null : WorldDataDirectory.FromWorldRoot(args.WorldDirectory).SnapshotDirectory;
            PreviousSessionSalvage.RunAtStartup(previousWorldSnapshotDirectory);
```
（`using Client.Game.InGame.BugReport.LastSession;` と `using Game.Paths;` を追加する）

`MainGameModelRegistration.Register` の plan B `builder.RegisterEntryPoint<UnityLogRing>().AsSelf();` の直後に:
```csharp
            builder.RegisterEntryPoint<CleanExitMarkWriter>();
```
（`using Client.Game.InGame.BugReport.LastSession;` を追加する）

- [ ] **Step 6: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.BugReport\.(CleanExitMarkerTest|PreviousSessionSalvageTest)$"`
Expected: 全PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport moorestech_client/Assets/Scripts/Client.Starter moorestech_client/Assets/Scripts/Client.Tests/BugReport
git commit -m "feat(client): 正常終了マーカーと前回セッション記録の起動時退避"
```

---

### Task 4: `steamId` と `buildInfo` の埋め込み（`IPlaytestSessionIdentity`・`BuildInfoReader`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BuildInfo.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Playtest/BuildInfoReader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Playtest/PlaytestSessionIdentity.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportManifest.cs`（`SteamId`・`BuildInfo` フィールド）
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportBundleWriter.cs`（ctor で identity を受け、manifest に載せる）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（`IPlaytestSessionIdentity` の既定実装を登録）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/BugReportSubmitKindTest.cs`（Task 1 で書いた `new BugReportBundleWriter()` を1引数版へ直す）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/BuildInfoReaderTest.cs`

**Interfaces:**
- Consumes: plan B `BugReportManifest`・`BugReportBundleWriter`、Task 1 の `WriteAsync(data, description, kind)`
- Produces:
  - `public sealed class BuildInfo { public string Commit; public string Branch; public string MasterDataCommit; public bool Dirty; public string SteamBuildLabel; public string BuiltAt; public string Target; }`（shared-contracts §1 の JSON と1対1。camelCase で往復する）
  - `public static class BuildInfoReader { public const string FileName = "build-info.json"; public static string FilePath { get; } public static BuildInfo Read(); public static BuildInfo Parse(string json); }`（不在・壊れたJSONは `null` と `LogWarning`）
  - `public interface IPlaytestSessionIdentity { string SteamId { get; } }`
  - `public sealed class EmptyPlaytestSessionIdentity : IPlaytestSessionIdentity`（`SteamId => ""`。plan D の `PlaytestSession` が DI 登録を差し替える差込口）
  - `BugReportManifest.SteamId`（`string`）・`BugReportManifest.BuildInfo`（`BuildInfo`）
  - `BugReportBundleWriter(IPlaytestSessionIdentity identity)`

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/BugReport/BuildInfoReaderTest.cs`:
```csharp
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.Playtest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class BuildInfoReaderTest
    {
        [Test]
        public void 配布ビルドのJSONを読める()
        {
            var json = @"{ ""commit"": ""abc"", ""branch"": ""master"", ""masterDataCommit"": ""def"", ""dirty"": false,
                           ""steamBuildLabel"": ""playtest-20260913-1730"", ""builtAt"": ""2026-09-13T17:30:00+09:00"", ""target"": ""StandaloneWindows64"" }";
            var info = BuildInfoReader.Parse(json);
            Assert.AreEqual("abc", info.Commit);
            Assert.AreEqual("def", info.MasterDataCommit);
            Assert.AreEqual("playtest-20260913-1730", info.SteamBuildLabel);
            Assert.IsFalse(info.Dirty);
        }

        [Test]
        public void 壊れたJSONはnullを返す()
        {
            Assert.IsNull(BuildInfoReader.Parse("{ not json"));
            Assert.IsNull(BuildInfoReader.Parse(""));
        }

        [Test]
        public void manifestはsteamIdとbuildInfoを持ちEditorではnullで書ける()
        {
            var manifest = new BugReportManifest
            {
                CreatedAt = "2026-09-13T12:00:00Z",
                Description = "説明",
                Kind = PlaytestReportKind.Bug,
                SteamId = "",
                BuildInfo = null,
            };
            var json = JObject.Parse(manifest.ToJson());
            Assert.AreEqual("", (string)json["steamId"]);
            Assert.IsTrue(json.ContainsKey("buildInfo"));
            Assert.AreEqual(JTokenType.Null, json["buildInfo"].Type);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `BuildInfoReader`／`BugReportManifest.SteamId` が無いというコンパイルエラー

- [ ] **Step 3: 型とリーダーを実装する**

`BugReport/BuildInfo.cs`:
```csharp
namespace Client.Game.InGame.BugReport
{
    // 配布ビルドの出所。plan E が StreamingAssets/build-info.json へ焼き、報告と進行記録が読むだけの契約（shared-contracts §1）
    // The distributed build's origin; plan E bakes StreamingAssets/build-info.json and reports/records only read it (shared-contracts §1)
    public sealed class BuildInfo
    {
        public string Commit;
        public string Branch;
        public string MasterDataCommit;
        public bool Dirty;
        public string SteamBuildLabel;
        public string BuiltAt;
        public string Target;
    }
}
```

`BugReport/Playtest/BuildInfoReader.cs`:
```csharp
using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace Client.Game.InGame.BugReport.Playtest
{
    // build-info.json を読むだけ。Editor実行や自作ビルドでは存在しないので null を返し、理由をログへ出す
    // Only reads build-info.json; it is absent in the Editor and in self-made builds, so this returns null and logs why
    public static class BuildInfoReader
    {
        public const string FileName = "build-info.json";

        public static string FilePath => Path.Combine(Application.streamingAssetsPath, FileName);

        public static BuildInfo Read()
        {
            var path = FilePath;
            if (!File.Exists(path))
            {
                Debug.LogWarning($"build-info.json が無いため buildInfo は null になります path:{path}");
                return null;
            }
            return Parse(File.ReadAllText(path));
        }

        public static BuildInfo Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;

            // 外部入力JSONのパースは境界。壊れた配布物でも報告経路を落とさないためここだけcatchする
            // Parsing externally supplied JSON is a boundary; only here we catch so a broken artifact never kills the report path
            try
            {
                return JsonConvert.DeserializeObject<BuildInfo>(json);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"build-info.json を解釈できません: {exception.GetBaseException().Message}");
                return null;
            }
        }
    }
}
```

`BugReport/Playtest/PlaytestSessionIdentity.cs`:
```csharp
namespace Client.Game.InGame.BugReport.Playtest
{
    // 報告に付けるテスター識別。実体は plan D の Steam 認証が入れ替える差込口で、既定は空文字（開発者のrsync経路）
    // The tester identity attached to reports; plan D's Steam auth replaces this seam, and the default is empty (developer rsync path)
    public interface IPlaytestSessionIdentity
    {
        string SteamId { get; }
    }

    public sealed class EmptyPlaytestSessionIdentity : IPlaytestSessionIdentity
    {
        public string SteamId => "";
    }
}
```

- [ ] **Step 4: manifest と書き出しへ通す**

`BugReportManifest.cs` の `Kind` の直後に:
```csharp
        // 送り手のSteamIDと配布ビルドの出所。Steam未起動なら空文字、Editorなら buildInfo は null（shared-contracts §2）
        // The sender's SteamID and the build origin; empty string without Steam, and null buildInfo in the Editor (shared-contracts §2)
        public string SteamId;
        public BuildInfo BuildInfo;
```

`BugReportBundleWriter.cs` に ctor を足し、manifest 初期化子へ2行足す:
```csharp
        private readonly IPlaytestSessionIdentity _identity;

        public BugReportBundleWriter(IPlaytestSessionIdentity identity)
        {
            _identity = identity;
        }
```
```csharp
                Kind = kind,
                SteamId = _identity.SteamId,
                BuildInfo = BuildInfoReader.Read(),
```
（`using Client.Game.InGame.BugReport.Playtest;` を追加する）

`MainGameModelRegistration.Register` の plan B `builder.Register<BugReportBundleWriter>(Lifetime.Singleton);` の直前に:
```csharp
            // plan D の Steam 認証が入るまでは空のSteamIDで動かす（登録の差し替えだけで切り替わる）
            // Runs with an empty SteamID until plan D's Steam auth arrives; swapping this registration is the whole switch
            builder.Register<IPlaytestSessionIdentity, EmptyPlaytestSessionIdentity>(Lifetime.Singleton);
```

Task 1 の `BugReportSubmitKindTest` の生成を直す:
```csharp
            var handler = new BugReportSubmitActionHandler(new BugReportBundleWriter(new EmptyPlaytestSessionIdentity()), session, new PauseMenuStateService(session));
```

- [ ] **Step 5: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.BugReport\.(BuildInfoReaderTest|BugReportSubmitKindTest)$"`
Expected: 全PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs moorestech_client/Assets/Scripts/Client.Tests/BugReport
git commit -m "feat(client): 報告manifestへsteamIdとbuildInfoを埋める"
```

---

### Task 5: 前回異常終了の確認ゲートと `kind=crash` の箱（`CrashReportGate`・`CrashBundleWriter`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/CrashBundleWriter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/CrashReportGate.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestGateBinder.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Playtest/CrashReportGateTopic.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Playtest/CrashReportGateActions.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Playtest/PlaytestStartGates.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Initialization/MainGameInitializationFinalizer.cs`（言語ゲートの直後にプレイテストゲートを await）
- Modify: `Localization/localization.csv`（5行追加）・`moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`・`payloadTypes.ts`・`validators.ts`・`transport/protocol.ts`・`transport/actionContract.ts`
- Create: `moorestech_web/webui/src/features/playtestGate/CrashReportGate.tsx`・`CrashReportGateBody.tsx`・`style.module.css`・`index.ts`・`CrashReportGate.test.ts`
- Modify: `moorestech_web/webui/src/app/App.tsx`（`<EventLanguageGate />` の直後に描画）
- Modify: `moorestech_web/webui/src/app/tokens.css`（`--z-portal-playtest-gate`・記述欄の固定長トークン）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/CrashReportGateTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/CrashBundleWriterTest.cs`

**Interfaces:**
- Consumes: Task 3 `PreviousSessionSalvage.Artifacts`／`PreviousSessionArtifacts`、Task 4 `IPlaytestSessionIdentity`・`BuildInfoReader`、Task 1 `PlaytestReportKind.Crash`、plan B `BugReportOutbox`・`BugReportManifest`・`MissingItem`、前例 `EventLanguageGate`／`EventLanguageGateTopic`／`EventLanguageGateBinder`／`EventModeStartGate`
- Produces:
  - `public sealed class CrashBundleWriter { public CrashBundleWriter(IPlaytestSessionIdentity identity); public string Write(PreviousSessionArtifacts artifacts, string description); }`（戻り値は箱のパス）
  - `public class CrashReportGate { public CrashReportGate(bool startsWaiting, CrashBundleWriter writer, PreviousSessionArtifacts artifacts); public bool IsWaitingSelection(); public IObservable<Unit> OnWaitingChanged { get; } public string LastWrittenBundleDirectory { get; } public UniTask WaitForResponseAsync(); public CrashReportResponseResult Respond(bool send, string description); }`（`Client.Tests` から internal は見えないため公開面は public。生成は `PlaytestGateBinder` 経由に限る規律はコメントで示す）
  - `public enum CrashReportResponseResult { Sent, Skipped, AlreadyResponded }`
  - `public static class PlaytestGateBinder { public static CrashReportGate BindCrashReportGate(WebSocketHub hub, PreviousSessionArtifacts artifacts, CrashBundleWriter writer); }`
  - `public static class PlaytestStartGates { public static UniTask WaitForPlaytestGatesAsync(); }`
  - トピック `playtest.crash_report_gate`: `{ waiting: boolean }`
  - アクション `playtest.crash_report.respond`: payload `{ send: boolean, description: string }`
  - ローカライズキー `ui.playtest.crashGate.title`・`.body`・`.placeholder`・`.send`・`.skip`

- [ ] **Step 1: 失敗するC#テストを書く**

`Client.Tests/BugReport/CrashBundleWriterTest.cs`:
```csharp
using System;
using System.IO;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class CrashBundleWriterTest
    {
        [Test]
        public void 退避物と説明文からcrashの箱を書く()
        {
            var source = Path.Combine(Path.GetTempPath(), $"moorestech-crash-{Guid.NewGuid():N}");
            var recording = Path.Combine(source, "recording");
            Directory.CreateDirectory(recording);
            File.WriteAllText(Path.Combine(recording, "seg_00.mp4"), "video");
            var playerLog = Path.Combine(source, "Player-prev.log");
            File.WriteAllText(playerLog, "log");

            var artifacts = new PreviousSessionArtifacts
            {
                PreviousExitWasClean = false,
                RecordingDirectory = recording,
                PlayerLogPath = playerLog,
            };

            var bundle = new CrashBundleWriter(new EmptyPlaytestSessionIdentity()).Write(artifacts, "落ちた");

            Assert.IsTrue(File.Exists(Path.Combine(bundle, BugReportOutbox.ReadyMarkerFileName)));
            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
            Assert.AreEqual("crash", (string)manifest["kind"]);
            Assert.AreEqual("落ちた", (string)manifest["description"]);
            Assert.IsTrue(File.Exists(Path.Combine(bundle, "recording", "seg_00.mp4")));
            Assert.IsTrue(File.Exists(Path.Combine(bundle, "logs", "Player-prev.log")));

            Directory.Delete(bundle, true);
            Directory.Delete(source, true);
        }

        [Test]
        public void 退避物が空でも説明文だけで箱になり欠損が残る()
        {
            var artifacts = new PreviousSessionArtifacts { PreviousExitWasClean = false };
            artifacts.Missing.Add(new MissingItem { Item = "recording", Reason = "退避元が空" });

            var bundle = new CrashBundleWriter(new EmptyPlaytestSessionIdentity()).Write(artifacts, "起動しない");

            var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
            Assert.AreEqual("crash", (string)manifest["kind"]);
            Assert.AreEqual(1, ((JArray)manifest["missing"]).Count);
            Directory.Delete(bundle, true);
        }
    }
}
```

`Client.Tests/BugReport/CrashReportGateTest.cs`:
```csharp
using System.IO;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.WebUiHost.Game.Playtest;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class CrashReportGateTest
    {
        private static CrashReportGate CreateWaitingGate(PreviousSessionArtifacts artifacts)
        {
            return new CrashReportGate(true, new CrashBundleWriter(new EmptyPlaytestSessionIdentity()), artifacts);
        }

        [Test]
        public void 待機しないゲートは即座に完了する()
        {
            var gate = new CrashReportGate(false, new CrashBundleWriter(new EmptyPlaytestSessionIdentity()), new PreviousSessionArtifacts());
            Assert.IsTrue(gate.WaitForResponseAsync().Status.IsCompleted());
        }

        [Test]
        public void 送らないを選ぶと箱を作らず待機が解ける()
        {
            var gate = CreateWaitingGate(new PreviousSessionArtifacts { PreviousExitWasClean = false });
            Assert.AreEqual(CrashReportResponseResult.Skipped, gate.Respond(false, ""));
            Assert.IsFalse(gate.IsWaitingSelection());
            Assert.IsTrue(gate.WaitForResponseAsync().Status.IsCompleted());
        }

        [Test]
        public void 送るを選ぶと箱ができ二度目の応答は弾かれる()
        {
            var gate = CreateWaitingGate(new PreviousSessionArtifacts { PreviousExitWasClean = false });
            Assert.AreEqual(CrashReportResponseResult.Sent, gate.Respond(true, "落ちた"));
            Assert.AreEqual(CrashReportResponseResult.AlreadyResponded, gate.Respond(true, "二重"));
            Assert.IsNotNull(gate.LastWrittenBundleDirectory);
            Directory.Delete(gate.LastWrittenBundleDirectory, true);
        }
    }
}
```
（`Client.Tests` は `Client.WebUiHost` の internal を見られないため、`CrashReportGate` の公開面は `public` にする。`EventLanguageGate` と同じ「Bind 経由でしか作らせない」規律はコメントで示す）

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `CrashBundleWriter`／`CrashReportGate` が無いというコンパイルエラー

- [ ] **Step 3: `CrashBundleWriter` を実装する**

`LastSession/CrashBundleWriter.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.BugReport.Playtest;
using UnityEngine;

namespace Client.Game.InGame.BugReport.LastSession
{
    // 退避物と説明文から kind=crash の箱を1つ書く。確保セッションが無い経路なので plan B の書き出しとは別物
    // Writes one kind=crash box from the salvaged files and the description; a path without a capture session, so it is separate from plan B's writer
    public sealed class CrashBundleWriter
    {
        private readonly IPlaytestSessionIdentity _identity;

        public CrashBundleWriter(IPlaytestSessionIdentity identity)
        {
            _identity = identity;
        }

        public string Write(PreviousSessionArtifacts artifacts, string description)
        {
            var directory = BugReportOutbox.CreateBundleDirectory(DateTime.UtcNow, Guid.NewGuid().ToString("N").Substring(0, 8));
            var manifest = new BugReportManifest
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                Description = description,
                Kind = PlaytestReportKind.Crash,
                SteamId = _identity.SteamId,
                BuildInfo = BuildInfoReader.Read(),
                Platform = Application.platform.ToString(),
                IsEditor = Application.isEditor,
                Missing = new List<MissingItem>(artifacts.Missing),
            };

            CopyDirectory(artifacts.RecordingDirectory, Path.Combine(directory, "recording"));
            CopySnapshots(artifacts.SnapshotsDirectory, directory, manifest);
            CopyFileInto(artifacts.PlayerLogPath, Path.Combine(directory, "logs"));
            foreach (var dump in artifacts.CrashDumpFiles) CopyFileInto(dump, Path.Combine(directory, "crashDumps"));

            File.WriteAllText(Path.Combine(directory, "manifest.json"), manifest.ToJson());
            BugReportOutbox.MarkReady(directory);
            Debug.Log($"前回異常終了の箱を書きました {directory} missing:{manifest.Missing.Count}");
            return directory;
        }

        // スナップショットとパケットログは plan B のプレイ報告と同じ snapshots/ 配下へ揃える（再現側の入口を1つに保つ）
        // Snapshots and packet logs land under the same snapshots/ as plan B's report, keeping one entry point for reproduction
        private static void CopySnapshots(string source, string bundleDirectory, BugReportManifest manifest)
        {
            if (source == null || !Directory.Exists(source)) return;
            var destination = Path.Combine(bundleDirectory, "snapshots");
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source))
            {
                var name = Path.GetFileName(file);
                File.Copy(file, Path.Combine(destination, name));
                if (name.StartsWith("tick_", StringComparison.Ordinal)) manifest.SnapshotFiles.Add(name);
                if (name.StartsWith("packets_", StringComparison.Ordinal)) manifest.PacketLogFiles.Add(name);
            }
        }

        private static void CopyDirectory(string source, string destination)
        {
            if (source == null || !Directory.Exists(source)) return;
            Directory.CreateDirectory(destination);
            foreach (var file in Directory.GetFiles(source)) File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));
        }

        private static void CopyFileInto(string sourceFile, string destinationDirectory)
        {
            if (sourceFile == null || !File.Exists(sourceFile)) return;
            Directory.CreateDirectory(destinationDirectory);
            File.Copy(sourceFile, Path.Combine(destinationDirectory, Path.GetFileName(sourceFile)), true);
        }
    }
}
```

- [ ] **Step 4: ゲート・トピック・アクションを実装する（`EventLanguageGate` と同型）**

`Client.WebUiHost/Game/Playtest/CrashReportGate.cs`:
```csharp
using System;
using Client.Game.InGame.BugReport.LastSession;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.WebUiHost.Game.Playtest
{
    // 前回異常終了の送信確認。応答があるまで開始を止める（ADR 0040 の言語選択ゲートと同型）
    // The previous-crash send confirmation; holds the start until answered (same shape as the ADR 0040 language gate)
    public class CrashReportGate
    {
        private readonly UniTaskCompletionSource _responseSource = new();
        private readonly Subject<Unit> _onWaitingChanged = new();
        private readonly CrashBundleWriter _writer;
        private readonly PreviousSessionArtifacts _artifacts;
        private bool _isWaitingResponse;

        public string LastWrittenBundleDirectory { get; private set; }
        public IObservable<Unit> OnWaitingChanged => _onWaitingChanged;

        // 登録は常に無条件、待つかどうかは初期状態で決める（未登録によるWeb側購読の固着を避ける）
        // Registration is always unconditional; whether to wait is decided by the initial state to avoid a stuck web subscription
        public CrashReportGate(bool startsWaiting, CrashBundleWriter writer, PreviousSessionArtifacts artifacts)
        {
            _writer = writer;
            _artifacts = artifacts;
            _isWaitingResponse = startsWaiting;
            if (!startsWaiting) _responseSource.TrySetResult();
        }

        public bool IsWaitingSelection()
        {
            return _isWaitingResponse;
        }

        public UniTask WaitForResponseAsync()
        {
            return _responseSource.Task;
        }

        // 応答は1回だけ効く。二重クリックと再送は「応答済み」として区別し、成功と一律に丸めない
        // Only the first answer takes effect; double clicks and resends are distinguished instead of folded into success
        public CrashReportResponseResult Respond(bool send, string description)
        {
            if (!_isWaitingResponse) return CrashReportResponseResult.AlreadyResponded;
            _isWaitingResponse = false;

            if (send) LastWrittenBundleDirectory = _writer.Write(_artifacts, description ?? "");
            else Debug.Log("前回異常終了の記録は送らないと選ばれました");

            _onWaitingChanged.OnNext(Unit.Default);
            _responseSource.TrySetResult();
            return send ? CrashReportResponseResult.Sent : CrashReportResponseResult.Skipped;
        }
    }

    public enum CrashReportResponseResult
    {
        Sent,
        Skipped,
        AlreadyResponded
    }
}
```

`Client.WebUiHost/Game/Topics/Playtest/CrashReportGateTopic.cs`:
```csharp
using System;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics.Playtest
{
    // 前回異常終了の確認待ちを snapshot と event で配信する
    // Publishes the previous-crash confirmation wait as a snapshot and events
    public class CrashReportGateTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "playtest.crash_report_gate";

        private readonly WebSocketHub _hub;
        private readonly CrashReportGate _gate;
        private readonly IDisposable _waitingSubscription;

        public CrashReportGateTopic(WebSocketHub hub, CrashReportGate gate)
        {
            _hub = hub;
            _gate = gate;
            _waitingSubscription = gate.OnWaitingChanged.Subscribe(_ => _hub.Publish(TopicName, BuildJson()));
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _waitingSubscription.Dispose();
        }

        private string BuildJson()
        {
            return WebUiJson.Serialize(new CrashReportGateData { Waiting = _gate.IsWaitingSelection() });
        }

        private class CrashReportGateData
        {
            public bool Waiting;
        }
    }
}
```

`Client.WebUiHost/Game/Actions/Playtest/CrashReportGateActions.cs`:
```csharp
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions.Playtest
{
    public static class CrashReportGateActions
    {
        public static void Register(WebSocketHub hub, CrashReportGate gate)
        {
            hub.RegisterAction(new CrashReportRespondActionHandler(gate));
        }
    }

    // テスターの応答をゲートへ渡し、判断はゲートに集約する
    // Hands the tester's answer to the gate, which owns the judgement
    public class CrashReportRespondActionHandler : IActionHandler
    {
        public string ActionType => "playtest.crash_report.respond";

        private readonly CrashReportGate _gate;

        public CrashReportRespondActionHandler(CrashReportGate gate)
        {
            _gate = gate;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var send = payload?["send"]?.Value<bool>() ?? false;
            var description = payload?["description"]?.ToString() ?? "";

            var result = _gate.Respond(send, description);
            return UniTask.FromResult(result switch
            {
                CrashReportResponseResult.Sent => ActionResult.Success(),
                CrashReportResponseResult.Skipped => ActionResult.Success(),
                // 二度目の応答は何も変えないので成功へ丸めない
                // A second answer changes nothing, so it is not folded into success
                CrashReportResponseResult.AlreadyResponded => ActionResult.Fail("already_responded"),
                _ => ActionResult.Fail("already_responded"),
            });
        }
    }
}
```

`Client.WebUiHost/Game/Playtest/PlaytestGateBinder.cs`:
```csharp
using Client.Game.InGame.BugReport.LastSession;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Actions.Playtest;
using Client.WebUiHost.Game.Topics.Playtest;

namespace Client.WebUiHost.Game.Playtest
{
    /// <summary>
    /// - プレイテストの開始ゲート（topicとaction）をHubへ束ねるfacade
    /// - WebUiGameBinderより前に呼ぶ
    /// - Binds the playtest start gates' topics and actions to the Hub
    /// - Must be called before WebUiGameBinder
    /// </summary>
    public static class PlaytestGateBinder
    {
        public static CrashReportGate BindCrashReportGate(WebSocketHub hub, PreviousSessionArtifacts artifacts, CrashBundleWriter writer)
        {
            // 異常終了なら常に確認する。退避物ゼロでも説明文だけの箱には価値があるので待機条件から外さない
            // Always ask after an unclean exit; a description-only box still has value, so an empty salvage does not skip the wait
            var startsWaiting = !artifacts.PreviousExitWasClean;
            var gate = new CrashReportGate(startsWaiting, writer, artifacts);
            hub.RegisterTopic(CrashReportGateTopic.TopicName, new CrashReportGateTopic(hub, gate));
            CrashReportGateActions.Register(hub, gate);
            return gate;
        }
    }
}
```

`Client.Starter/Playtest/PlaytestStartGates.cs`:
```csharp
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Starter.Playtest
{
    /// <summary>
    /// プレイテストの開始ゲート。前回異常終了の確認を出し、応答があるまで開始を止める。
    /// The playtest start gates: shows the previous-crash confirmation and holds the start until it is answered.
    /// </summary>
    public static class PlaytestStartGates
    {
        public static async UniTask WaitForPlaytestGatesAsync()
        {
            var hub = Client.WebUiHost.Boot.WebUiHost.Hub;
            var artifacts = PreviousSessionSalvage.Artifacts ?? new PreviousSessionArtifacts { PreviousExitWasClean = true };

            // 画面を出せないなら止めない方を採る。前回分は last-session に残るので次回起動で聞き直せる
            // With no screen to show, not blocking wins: the salvage stays in last-session and the next boot can ask again
            if (hub == null)
            {
                if (!artifacts.PreviousExitWasClean) Debug.LogError("PlaytestStartGates: WebUiHostが起動しておらず前回異常終了の確認を出せないため、確認せずに開始します");
                return;
            }

            var gate = PlaytestGateBinder.BindCrashReportGate(hub, artifacts, new CrashBundleWriter(new EmptyPlaytestSessionIdentity()));
            await gate.WaitForResponseAsync();

            // 応答の継続はaction処理スタックの中で走る。ここで手放さないと初期化の間WSの受信ループが止まる
            // The continuation resumes inside the action's stack, so yielding here keeps the WS receive loop alive during initialization
            await UniTask.Yield();
        }
    }
}
```
（`EmptyPlaytestSessionIdentity` を直接使うのは、このゲートが DI コンテナ生成前に走るため。plan D が Steam 認証を入れる際は同じ1行を差し替える）

`MainGameInitializationFinalizer.FinalizeAsync` の言語ゲート await の直後に:
```csharp
            // 前回異常終了の確認をタイトルで出す。オープニングとチュートリアルが走り出す前に挟む
            // Ask about the previous crash at the title, ahead of the opening skit and the tutorials
            await Playtest.PlaytestStartGates.WaitForPlaytestGatesAsync();
```

- [ ] **Step 5: ローカライズ行を追加して生成する**

`Localization/localization.csv` 末尾に追加（本文にカンマを含むため `"` で囲む。改行は `\n`）:
```
ui.playtest.crashGate.title,The game did not close normally last time,The game did not close normally last time,前回ゲームが正常に終了しませんでした,Das Spiel wurde letztes Mal nicht normal beendet
ui.playtest.crashGate.body,"Send the last session's recording, world snapshot, packet log, Unity log and crash dump to the developer?","Send the last session's recording, world snapshot, packet log, Unity log and crash dump to the developer?","前回セッションの録画・ワールドのスナップショット・パケットログ・Unityログ・クラッシュダンプを開発者へ送りますか？","Die Aufzeichnung, den Welt-Snapshot, das Paketprotokoll, das Unity-Log und den Absturzbericht der letzten Sitzung an den Entwickler senden?"
ui.playtest.crashGate.placeholder,What were you doing when it stopped? (optional),What were you doing when it stopped? (optional),止まったとき何をしていましたか？（任意）,Was hast du gemacht als es abstürzte? (optional)
ui.playtest.crashGate.send,Send,Send,送る,Senden
ui.playtest.crashGate.skip,Do not send,Do not send,送らない,Nicht senden
```
`_CompileRequester.cs` の `dummyText` を新しい値へ変更する。

Run: `cd moorestech_web/webui && pnpm gen:i18n && cd ../.. && uloop compile --project-path ./moorestech_client`
Expected: `L.ui.playtest.crashGate.send` が TS/C# 両方で生成される

- [ ] **Step 6: webui の失敗するテストを書く**

`src/features/playtestGate/CrashReportGate.test.ts`:
```ts
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({ dispatchAction: vi.fn(async () => true) }));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchAction: mocks.dispatchAction,
}));

import { CrashReportGateBody } from "./CrashReportGateBody";

const dictionary = {
  "ui.playtest.crashGate.title": "前回ゲームが正常に終了しませんでした",
  "ui.playtest.crashGate.body": "前回セッションの記録を送りますか？",
  "ui.playtest.crashGate.placeholder": "止まったとき何をしていましたか？",
  "ui.playtest.crashGate.send": "送る",
  "ui.playtest.crashGate.skip": "送らない",
};

afterEach(() => {
  vi.clearAllMocks();
});

async function render(): Promise<ReactTestRenderer> {
  let renderer: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(CrashReportGateBody));
  });
  return renderer!;
}

describe("CrashReportGateBody", () => {
  it("説明欄と2つのボタンを描く", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render();
    expect(renderer.root.findByProps({ "data-testid": "crash-report-description" })).toBeTruthy();
    expect(renderer.root.findByProps({ "data-testid": "crash-report-send" })).toBeTruthy();
    expect(renderer.root.findByProps({ "data-testid": "crash-report-skip" })).toBeTruthy();
    act(() => renderer.unmount());
  });

  it("送るで説明文付きのsend=trueを投げる", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render();
    const textarea = renderer.root.findByProps({ "data-testid": "crash-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "採掘中に固まった" } }));
    await act(async () => renderer.root.findByProps({ "data-testid": "crash-report-send" }).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("playtest.crash_report.respond", { send: true, description: "採掘中に固まった" });
    act(() => renderer.unmount());
  });

  it("送らないで空の説明文とsend=falseを投げる", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render();
    await act(async () => renderer.root.findByProps({ "data-testid": "crash-report-skip" }).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("playtest.crash_report.respond", { send: false, description: "" });
    act(() => renderer.unmount());
  });
});
```

Run: `cd moorestech_web/webui && pnpm test -- CrashReportGate`
Expected: FAIL（モジュールが無い）

- [ ] **Step 7: bridge 契約と webui を実装する**

`src/bridge/contract/schemas/ui.ts` の `EventLanguageGateDataSchema` の直後:
```ts
export const CrashReportGateDataSchema = z.object({ waiting: z.boolean() });
```
`payloadTypes.ts`: import に `CrashReportGateDataSchema` を足し `export type CrashReportGateData = z.infer<typeof CrashReportGateDataSchema>;`
`validators.ts`: import に足し `[Topics.crashReportGate]: CrashReportGateDataSchema,`
`transport/protocol.ts`: `Topics` に
```ts
  // 前回異常終了の送信確認。待機中は全画面で操作を塞ぐ
  // The previous-crash send confirmation; blocks all input full-screen while waiting
  crashReportGate: "playtest.crash_report_gate",
```
と `TopicPayloads` に `[Topics.crashReportGate]: CrashReportGateData;`（型 import も追加）
`transport/actionContract.ts`: `ActionPayloads` に `"playtest.crash_report.respond": { send: boolean; description: string };`、`ACTION_TYPES` に `"playtest.crash_report.respond",`

`src/app/tokens.css`（`--z-portal-event-language-gate` の直後、および記述欄の固定長）:
```css
  /* プレイテストの開始ゲート。言語選択ゲートと同じ層に置く */
  /* The playtest start gates sit on the same layer as the language gate */
  --z-portal-playtest-gate: 700;
  --playtest-gate-textarea-width: 480px;
  --playtest-gate-textarea-height: 96px;
```
（`700` は既存 `--z-portal-event-language-gate` と同値にする。実値は `zLayerTokens.test.ts` の期待に合わせ、既存トークンの値を読んで同じ数値を入れる）

`src/features/playtestGate/style.module.css`:
```css
.description {
  width: var(--playtest-gate-textarea-width);
  height: var(--playtest-gate-textarea-height);
  background: var(--gauge-track);
  border: var(--bevel-1) solid var(--bevel-c1);
  color: var(--text-default);
  padding: var(--mode-switch-padding-block) var(--mode-switch-padding-inline);
  font-family: inherit;
  resize: none;
}

.description::placeholder {
  color: var(--text-muted);
}

.description:focus-visible {
  outline: var(--bevel-1) solid var(--text-high-contrast);
  outline-offset: var(--bevel-1);
}
```

`src/features/playtestGate/CrashReportGateBody.tsx`:
```tsx
// 待機中だけマウントされる本体。説明文は任意で、どちらのボタンでも待機が解ける
// The body mounted only while waiting; the description is optional and either button releases the wait
import { useState } from "react";
import { Group, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { PanelActionButton } from "@/shared/ui";
import styles from "./style.module.css";

export function CrashReportGateBody() {
  const { t } = useI18n();
  const [description, setDescription] = useState("");
  const [pending, setPending] = useState(false);

  const respond = async (send: boolean) => {
    setPending(true);
    await dispatchAction("playtest.crash_report.respond", { send, description: send ? description.trim() : "" });
  };

  return (
    <Stack align="center" gap="md">
      <Text c="white">{t(L.ui.playtest.crashGate.body)}</Text>
      <textarea
        className={styles.description}
        value={description}
        placeholder={t(L.ui.playtest.crashGate.placeholder)}
        onChange={(e) => setDescription(e.currentTarget.value)}
        data-testid="crash-report-description"
      />
      <Group justify="center" gap="lg">
        <PanelActionButton onClick={() => void respond(true)} disabled={pending} testId="crash-report-send">
          {t(L.ui.playtest.crashGate.send)}
        </PanelActionButton>
        <PanelActionButton onClick={() => void respond(false)} disabled={pending} testId="crash-report-skip">
          {t(L.ui.playtest.crashGate.skip)}
        </PanelActionButton>
      </Group>
    </Stack>
  );
}
```
（`PanelActionButton` に `disabled` が無ければ、`shared/ui/PanelActionButton` の props に `disabled?: boolean` を足し `<button disabled={disabled} data-disabled={disabled || undefined}>` を通す。既存の使用箇所は省略時 `undefined` で従来どおり）

`src/features/playtestGate/CrashReportGate.tsx`:
```tsx
// 待機中は全画面で操作を塞ぐ外殻。本体は待機中だけマウントする（EventLanguageGate と同型）
// The shell that blocks input full-screen while waiting; the body mounts only while waiting (same shape as EventLanguageGate)
import { Overlay, Portal, Stack, Title } from "@mantine/core";
import { Topics, useTopicSelector } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { CrashReportGateBody } from "./CrashReportGateBody";

export function CrashReportGate() {
  const { t } = useI18n();
  const waiting = useTopicSelector(Topics.crashReportGate, (data) => data?.waiting ?? false);

  if (!waiting) return null;

  return (
    <Portal>
      <Overlay
        fixed
        center
        backgroundOpacity={1}
        color="var(--event-language-gate-face)"
        zIndex="var(--z-portal-playtest-gate)"
        data-testid="crash-report-gate"
      >
        <Stack align="center" gap="xl">
          <Title order={1} c="white">{t(L.ui.playtest.crashGate.title)}</Title>
          <CrashReportGateBody />
        </Stack>
      </Overlay>
    </Portal>
  );
}
```

`src/features/playtestGate/index.ts`:
```ts
export { CrashReportGate } from "./CrashReportGate";
```

`src/app/App.tsx`: import を足し、`<EventLanguageGate />` の直後に `<CrashReportGate />` を置く。

- [ ] **Step 8: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.BugReport\.(CrashBundleWriterTest|CrashReportGateTest)$"` → `cd moorestech_web/webui && pnpm test && pnpm lint`
Expected: 全PASS

- [ ] **Step 9: コミットする**

```bash
git add Localization/localization.csv moorestech_client/Assets/Scripts moorestech_web/webui/src
git commit -m "feat(client,webui): 前回異常終了をタイトルで確認しcrashの箱を書く"
```

---

### Task 6: ワールドのプレイ時間を取る（`va:getWorldPlaySessionInfo`）

**Files:**
- Create: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponse/GetWorldPlaySessionInfoProtocol.cs`
- Modify: `moorestech_server/Assets/Scripts/Server.Protocol/PacketResponseCreator.cs`（辞書登録1行）
- Modify: `moorestech_server/Assets/Scripts/Game.World.Interface/DataStore/IWorldSettingsDatastore.cs`（`WorldCreationDateTimeUtc` を追加）
- Modify: `moorestech_server/Assets/Scripts/Game.World/DataStore/WorldSettings/WorldSettingsDatastore.cs`（同プロパティを公開）
- Modify: `moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs`（`GetWorldPlaySessionInfo`）
- Test: `moorestech_server/Assets/Scripts/Tests/CombinedTest/Server/PacketTest/GetWorldPlaySessionInfoProtocolTest.cs`

**Interfaces:**
- Consumes: `IWorldSettingsDatastore.GetCurrentPlayTime()`、`PacketResponseCreator` の登録規約、`PacketExchangeManager.GetPacketResponse<T>`
- Produces:
  - `public class GetWorldPlaySessionInfoProtocol : IPacketResponse { public const string ProtocolTag = "va:getWorldPlaySessionInfo"; }`＋`RequestWorldPlaySessionInfoMessagePack`／`ResponseWorldPlaySessionInfoMessagePack { [Key(2)] public string WorldCreatedAt; [Key(3)] public double TotalPlaySeconds; }`
  - `IWorldSettingsDatastore.WorldCreationDateTimeUtc`（`DateTime`）
  - `VanillaApiWithResponse.GetWorldPlaySessionInfo(CancellationToken ct)` → `UniTask<GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack>`

- [ ] **Step 1: 失敗するテストを書く**

`Tests/CombinedTest/Server/PacketTest/GetWorldPlaySessionInfoProtocolTest.cs`:
```csharp
using System;
using Game.Map.Interface.Json;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class GetWorldPlaySessionInfoProtocolTest
    {
        [Test]
        public void ワールド作成日時と累計プレイ時間が返る()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            serviceProvider.GetService<IWorldSettingsDatastore>().Initialize(serviceProvider.GetService<MapInfoJson>());

            var request = MessagePackSerializer.Serialize(new GetWorldPlaySessionInfoProtocol.RequestWorldPlaySessionInfoMessagePack());
            var response = packet.GetPacketResponse(new System.Collections.Generic.List<byte>(request), new PacketResponseContext(null))[0];
            var info = MessagePackSerializer.Deserialize<GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack>(response);

            Assert.IsTrue(DateTime.TryParse(info.WorldCreatedAt, out _), $"ISO日時ではない: {info.WorldCreatedAt}");
            Assert.GreaterOrEqual(info.TotalPlaySeconds, 0);
        }
    }
}
```
（`GetPacketResponse` の引数型は同ディレクトリの既存テスト（`InitialHandshakeProtocolTest.GetHandshakePacket`）の戻り値に合わせる。`List<byte>` でない場合はそちらへ揃える）

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.CombinedTest\.Server\.PacketTest\.GetWorldPlaySessionInfoProtocolTest$"`
Expected: コンパイルエラー（プロトコルが無い）

- [ ] **Step 3: サーバー側を実装する**

`IWorldSettingsDatastore.cs` に追加:
```csharp
        // ワールドが作られた実世界日時。進行記録がセッションの外側の文脈として読む
        // The real-world time the world was created; the progress record reads it as context outside the session
        public DateTime WorldCreationDateTimeUtc { get; }
```
（`using System;` を追加する）

`WorldSettingsDatastore.cs`: `private DateTime _worldCreationDateTime;` を消し、`public DateTime WorldCreationDateTimeUtc { get; private set; }` にして、`Initialize`／`LoadSettingData`／`GetSaveJsonObject` の参照を全て `WorldCreationDateTimeUtc` へ置き換える。

`GetWorldPlaySessionInfoProtocol.cs`:
```csharp
using System;
using Game.World.Interface.DataStore;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;

namespace Server.Protocol.PacketResponse
{
    // ワールドの作成日時と累計プレイ時間を1回で返す。進行記録がセッション開始時に取得する
    // Returns the world creation time and the total play time in one call; the progress record fetches it at session start
    public class GetWorldPlaySessionInfoProtocol : IPacketResponse
    {
        public const string ProtocolTag = "va:getWorldPlaySessionInfo";
        private readonly IWorldSettingsDatastore _worldSettingsDatastore;

        public GetWorldPlaySessionInfoProtocol(ServiceProvider serviceProvider)
        {
            _worldSettingsDatastore = serviceProvider.GetService<IWorldSettingsDatastore>();
        }

        public ProtocolMessagePackBase GetResponse(byte[] payload, PacketResponseContext context)
        {
            var createdAt = _worldSettingsDatastore.WorldCreationDateTimeUtc.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            return new ResponseWorldPlaySessionInfoMessagePack(createdAt, _worldSettingsDatastore.GetCurrentPlayTime().TotalSeconds);
        }

        [MessagePackObject]
        public class RequestWorldPlaySessionInfoMessagePack : ProtocolMessagePackBase
        {
            public RequestWorldPlaySessionInfoMessagePack()
            {
                Tag = ProtocolTag;
            }
        }

        [MessagePackObject]
        public class ResponseWorldPlaySessionInfoMessagePack : ProtocolMessagePackBase
        {
            [Key(2)] public string WorldCreatedAt;
            [Key(3)] public double TotalPlaySeconds;

            [Obsolete("デシリアライズ用のコンストラクタです。基本的に使用しないでください。")]
            public ResponseWorldPlaySessionInfoMessagePack() { }

            public ResponseWorldPlaySessionInfoMessagePack(string worldCreatedAt, double totalPlaySeconds)
            {
                Tag = ProtocolTag;
                WorldCreatedAt = worldCreatedAt;
                TotalPlaySeconds = totalPlaySeconds;
            }
        }
    }
}
```

`PacketResponseCreator.cs` の `GetPlayedSkitIdsProtocol` 登録行の直後:
```csharp
            _packetResponseDictionary.Add(GetWorldPlaySessionInfoProtocol.ProtocolTag, new GetWorldPlaySessionInfoProtocol(serviceProvider));
```

- [ ] **Step 4: クライアントAPIを足す**

`VanillaApiWithResponse.cs` の `GetPlayedSkitIds` の直後:
```csharp
        // 進行記録がセッション開始時に1回だけ読む。可変状態の同期ではないので初期データ取得のみ
        // The progress record reads this once at session start; it syncs no mutable state, so a fetch is enough
        public async UniTask<GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack> GetWorldPlaySessionInfo(CancellationToken ct)
        {
            var request = new GetWorldPlaySessionInfoProtocol.RequestWorldPlaySessionInfoMessagePack();
            return await _packetExchangeManager.GetPacketResponse<GetWorldPlaySessionInfoProtocol.ResponseWorldPlaySessionInfoMessagePack>(request, ct);
        }
```

- [ ] **Step 5: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Tests\.CombinedTest\.Server\.PacketTest\.GetWorldPlaySessionInfoProtocolTest$"`
Expected: PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_server/Assets/Scripts moorestech_client/Assets/Scripts/Client.Network/API/VanillaApiWithResponse.cs
git commit -m "feat(server): ワールド作成日時と累計プレイ時間を返すプロトコル"
```

---

### Task 7: 進行記録の器（`ProgressRecordFiles`・`ProgressRecordComposer`）

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs`（進行記録の3パス）
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/ProgressRecordPaths.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/ProgressEventEntry.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/ProgressRecordHeader.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/ProgressRecordFiles.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/ProgressRecordComposer.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Playtest/ProgressRecordComposerTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Playtest/ProgressRecordFilesTest.cs`

**Interfaces:**
- Consumes: Task 4 `BuildInfo`、plan B `BugReportOutbox`（配置規則の前例）
- Produces:
  - `GameSystemPaths.ProgressRecordDirectory`（`<GameSystemDirectory>/ProgressRecords`）・`ProgressRecordOutboxDirectory`（`.../outbox`）・`ProgressRecordCurrentDirectory`（`.../current`）
  - `public static class ProgressRecordPaths { public const string HeaderFileName = "header.json"; public const string EventsFileName = "events.jsonl"; public const string RecordFileName = "record.json"; public const string ReadyMarkerFileName = "READY"; public static string CurrentHeaderPath { get; } public static string CurrentEventsPath { get; } public static string CreateOutboxDirectory(DateTime now, string shortId); }`
  - `public static class ProgressEventType { public const string ResearchCompleted = "researchCompleted"; public const string ChallengeCompleted = "challengeCompleted"; public const string UiStateChanged = "uiStateChanged"; public const string BuildModeCancelled = "buildModeCancelled"; public const string BlockPlaced = "blockPlaced"; public const string ReportSent = "reportSent"; public const string CraftExecuted = "craftExecuted"; }`
  - `public sealed class ProgressEventEntry { public string T; public ulong Tick; public string Type; public JObject Data; public static ProgressEventEntry Create(DateTime utc, ulong tick, string type, JObject data); public string ToJsonLine(); public JObject ToJObject(); public static ProgressEventEntry FromJsonLine(string line); }`
  - `public sealed class ProgressRecordHeader { public int SchemaVersion = 1; public string SteamId; public BuildInfo BuildInfo; public string SessionStart; public string WorldCreatedAt; public double TotalPlaySecondsAtStart; public List<string> BaselineChallenges = new(); public List<string> BaselineResearch = new(); public string ToJson(); public static ProgressRecordHeader FromJson(string json); }`
  - `public static class ProgressRecordFiles { public static void WriteHeader(ProgressRecordHeader header); public static void AppendEvent(ProgressEventEntry entry); public static bool HasCurrentSession(); public static ProgressRecordHeader ReadHeader(); public static List<ProgressEventEntry> ReadEvents(); public static string CloseCurrentInto(string endReason, DateTime sessionEndUtc); public static void ClearCurrent(); }`（`CloseCurrentInto` は outbox の箱パスを返し、`current/` を空にする）
  - `public static class ProgressRecordComposer { public static string Compose(ProgressRecordHeader header, IReadOnlyList<ProgressEventEntry> events, string endReason, DateTime sessionEndUtc); public static List<ProgressEventEntry> WithSynthesizedBuildModeCancel(IReadOnlyList<ProgressEventEntry> events); }`

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/Playtest/ProgressRecordComposerTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Playtest.Progress;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    public class ProgressRecordComposerTest
    {
        private static readonly DateTime Start = new(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);

        private static ProgressRecordHeader CreateHeader()
        {
            return new ProgressRecordHeader
            {
                SteamId = "",
                BuildInfo = null,
                SessionStart = Start.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                WorldCreatedAt = "2026-09-10T09:00:00Z",
                TotalPlaySecondsAtStart = 100,
                BaselineChallenges = new List<string> { "11111111-1111-1111-1111-111111111111" },
                BaselineResearch = new List<string>(),
            };
        }

        [Test]
        public void イベントが0件でも必須キーが揃う()
        {
            var json = JObject.Parse(ProgressRecordComposer.Compose(CreateHeader(), new List<ProgressEventEntry>(), "quit", Start.AddSeconds(60)));

            foreach (var key in new[] { "schemaVersion", "steamId", "buildInfo", "sessionStart", "sessionEnd", "endReason", "playSeconds", "worldCreatedAt", "totalPlaySeconds", "reachedChallenges", "completedResearch", "placedBlockCount", "craftCount", "lastUiState", "events" })
            {
                Assert.IsTrue(json.ContainsKey(key), $"キー {key} が無い");
            }
            Assert.AreEqual(60d, (double)json["playSeconds"]);
            Assert.AreEqual(160d, (double)json["totalPlaySeconds"]);
            Assert.AreEqual("quit", (string)json["endReason"]);
            Assert.AreEqual(0, (int)json["placedBlockCount"]);
            Assert.AreEqual(1, ((JArray)json["reachedChallenges"]).Count);
        }

        [Test]
        public void 集計値はイベント列から導出される()
        {
            var events = new List<ProgressEventEntry>
            {
                ProgressEventEntry.Create(Start.AddSeconds(1), 10, ProgressEventType.BlockPlaced, new JObject { ["blockGuid"] = "b1" }),
                ProgressEventEntry.Create(Start.AddSeconds(2), 20, ProgressEventType.BlockPlaced, new JObject { ["blockGuid"] = "b2" }),
                ProgressEventEntry.Create(Start.AddSeconds(3), 30, ProgressEventType.ChallengeCompleted, new JObject { ["challengeGuid"] = "22222222-2222-2222-2222-222222222222" }),
                ProgressEventEntry.Create(Start.AddSeconds(4), 40, ProgressEventType.ResearchCompleted, new JObject { ["researchGuid"] = "33333333-3333-3333-3333-333333333333" }),
                ProgressEventEntry.Create(Start.AddSeconds(5), 50, "craftExecuted", new JObject { ["recipeGuid"] = "r1" }),
                ProgressEventEntry.Create(Start.AddSeconds(6), 60, ProgressEventType.UiStateChanged, new JObject { ["state"] = "BuildMenu" }),
            };

            var json = JObject.Parse(ProgressRecordComposer.Compose(CreateHeader(), events, "quit", Start.AddSeconds(10)));

            Assert.AreEqual(2, (int)json["placedBlockCount"]);
            Assert.AreEqual(1, (int)json["craftCount"]);
            Assert.AreEqual(2, ((JArray)json["reachedChallenges"]).Count);
            Assert.AreEqual(1, ((JArray)json["completedResearch"]).Count);
            Assert.AreEqual("BuildMenu", (string)json["lastUiState"]);
        }

        [Test]
        public void 設置せずにビルドモードを抜けたときだけキャンセルを合成する()
        {
            var cancelled = new List<ProgressEventEntry>
            {
                ProgressEventEntry.Create(Start.AddSeconds(1), 10, ProgressEventType.UiStateChanged, new JObject { ["state"] = "PlaceBlock" }),
                ProgressEventEntry.Create(Start.AddSeconds(2), 20, ProgressEventType.UiStateChanged, new JObject { ["state"] = "GameScreen" }),
            };
            var placed = new List<ProgressEventEntry>
            {
                ProgressEventEntry.Create(Start.AddSeconds(1), 10, ProgressEventType.UiStateChanged, new JObject { ["state"] = "PlaceBlock" }),
                ProgressEventEntry.Create(Start.AddSeconds(2), 15, ProgressEventType.BlockPlaced, new JObject()),
                ProgressEventEntry.Create(Start.AddSeconds(3), 20, ProgressEventType.UiStateChanged, new JObject { ["state"] = "GameScreen" }),
            };

            Assert.AreEqual(1, CountCancel(ProgressRecordComposer.WithSynthesizedBuildModeCancel(cancelled)));
            Assert.AreEqual(0, CountCancel(ProgressRecordComposer.WithSynthesizedBuildModeCancel(placed)));

            // 抜けないまま終わった滞在は合成しない（終了は別のイベントで表現される）
            // A stay that never ends is not synthesized; the exit is expressed by another event
            var stillInside = new List<ProgressEventEntry>
            {
                ProgressEventEntry.Create(Start.AddSeconds(1), 10, ProgressEventType.UiStateChanged, new JObject { ["state"] = "PlaceBlock" }),
            };
            Assert.AreEqual(0, CountCancel(ProgressRecordComposer.WithSynthesizedBuildModeCancel(stillInside)));
        }

        private static int CountCancel(List<ProgressEventEntry> events)
        {
            return events.FindAll(e => e.Type == ProgressEventType.BuildModeCancelled).Count;
        }
    }
}
```

`Client.Tests/Playtest/ProgressRecordFilesTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.Playtest.Progress;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    public class ProgressRecordFilesTest
    {
        [SetUp]
        [TearDown]
        public void ClearCurrent()
        {
            ProgressRecordFiles.ClearCurrent();
        }

        [Test]
        public void ヘッダとイベントを書いて閉じるとoutboxに箱が出る()
        {
            var start = DateTime.UtcNow;
            ProgressRecordFiles.WriteHeader(new ProgressRecordHeader
            {
                SessionStart = start.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                WorldCreatedAt = "2026-09-10T09:00:00Z",
                SteamId = "",
                TotalPlaySecondsAtStart = 0,
            });
            Assert.IsTrue(ProgressRecordFiles.HasCurrentSession());

            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(start, 1, ProgressEventType.BlockPlaced, new JObject()));
            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(start, 2, ProgressEventType.BlockPlaced, new JObject()));
            Assert.AreEqual(2, ProgressRecordFiles.ReadEvents().Count);

            var bundle = ProgressRecordFiles.CloseCurrentInto("crash-recovered", start.AddSeconds(30));

            Assert.IsTrue(File.Exists(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.IsTrue(File.Exists(Path.Combine(bundle, "READY")));
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual("crash-recovered", (string)record["endReason"]);
            Assert.AreEqual(2, (int)record["placedBlockCount"]);
            Assert.IsFalse(ProgressRecordFiles.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        [Test]
        public void ヘッダが無いのに閉じようとしたらnullを返す()
        {
            Assert.IsNull(ProgressRecordFiles.CloseCurrentInto("quit", DateTime.UtcNow));
        }

        [Test]
        public void 壊れたイベント行は飛ばして残りを読む()
        {
            ProgressRecordFiles.WriteHeader(new ProgressRecordHeader { SessionStart = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") });
            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(DateTime.UtcNow, 1, ProgressEventType.BlockPlaced, new JObject()));
            File.AppendAllText(ProgressRecordPaths.CurrentEventsPath, "{ broken\n");
            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(DateTime.UtcNow, 2, ProgressEventType.BlockPlaced, new JObject()));

            var events = ProgressRecordFiles.ReadEvents();
            Assert.AreEqual(2, events.Count);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ProgressRecordFiles` などが無いというコンパイルエラー

- [ ] **Step 3: パスと型を実装する**

`GameSystemPaths.cs` の `BugReportLastSessionDirectory` の直後:
```csharp
        // 進行記録の作業中セッションとoutbox。プレイ報告とは別ツリーで持つ（shared-contracts §2）
        // The in-flight progress session and its outbox; kept in a tree separate from play reports (shared-contracts §2)
        public static string ProgressRecordDirectory => Path.Combine(GameSystemDirectory, "ProgressRecords");
        public static string ProgressRecordOutboxDirectory => Path.Combine(ProgressRecordDirectory, "outbox");
        public static string ProgressRecordCurrentDirectory => Path.Combine(ProgressRecordDirectory, "current");
```

`Playtest/Progress/ProgressRecordPaths.cs`:
```csharp
using System;
using System.IO;
using Game.Paths;

namespace Client.Game.InGame.Playtest.Progress
{
    // 進行記録の配置規則はここだけが持つ。READY は「record.json まで書き終えた」合図（plan B の outbox と同型）
    // Owns the progress record layout; READY signals record.json is written (same shape as plan B's outbox)
    public static class ProgressRecordPaths
    {
        public const string HeaderFileName = "header.json";
        public const string EventsFileName = "events.jsonl";
        public const string RecordFileName = "record.json";
        public const string ReadyMarkerFileName = "READY";

        public static string CurrentHeaderPath => Path.Combine(GameSystemPaths.ProgressRecordCurrentDirectory, HeaderFileName);
        public static string CurrentEventsPath => Path.Combine(GameSystemPaths.ProgressRecordCurrentDirectory, EventsFileName);

        public static string CreateOutboxDirectory(DateTime now, string shortId)
        {
            var directory = Path.Combine(GameSystemPaths.ProgressRecordOutboxDirectory, $"{now:yyyyMMdd_HHmmss}_{shortId}");
            Directory.CreateDirectory(directory);
            return directory;
        }
    }
}
```

`Playtest/Progress/ProgressEventEntry.cs`:
```csharp
using System;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // 進行記録のイベント種別。文字列は受け口・集計側と共有する契約値（shared-contracts §3）
    // Progress event types; these strings are the contract shared with the receiver and the digest (shared-contracts §3)
    public static class ProgressEventType
    {
        public const string ResearchCompleted = "researchCompleted";
        public const string ChallengeCompleted = "challengeCompleted";
        public const string UiStateChanged = "uiStateChanged";
        public const string BuildModeCancelled = "buildModeCancelled";
        public const string BlockPlaced = "blockPlaced";
        public const string ReportSent = "reportSent";
        public const string CraftExecuted = "craftExecuted";
    }

    public sealed class ProgressEventEntry
    {
        public string T;
        public ulong Tick;
        public string Type;
        public JObject Data;

        public static ProgressEventEntry Create(DateTime utc, ulong tick, string type, JObject data)
        {
            return new ProgressEventEntry { T = utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"), Tick = tick, Type = type, Data = data ?? new JObject() };
        }

        public string ToJsonLine()
        {
            return new JObject { ["t"] = T, ["tick"] = Tick, ["type"] = Type, ["data"] = Data }.ToString(Newtonsoft.Json.Formatting.None);
        }

        public JObject ToJObject()
        {
            return new JObject { ["t"] = T, ["tick"] = Tick, ["type"] = Type, ["data"] = Data };
        }

        // 追記中に落ちた行は壊れうる。読み側の境界なのでここだけcatchし、壊れた行は捨てて続行する
        // A line can be torn by a crash mid-append; this read boundary catches, drops the bad line and continues
        public static ProgressEventEntry FromJsonLine(string line)
        {
            try
            {
                var json = JObject.Parse(line);
                return new ProgressEventEntry
                {
                    T = (string)json["t"],
                    Tick = (ulong)json["tick"],
                    Type = (string)json["type"],
                    Data = json["data"] as JObject ?? new JObject(),
                };
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"進行記録のイベント行を読めないため飛ばします: {exception.GetBaseException().Message}");
                return null;
            }
        }
    }
}
```

`Playtest/Progress/ProgressRecordHeader.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // セッション開始時に確定する文脈。終了時に組む record.json の土台になる
    // The context fixed at session start; the base for the record.json composed at the end
    public sealed class ProgressRecordHeader
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
        };

        public int SchemaVersion = 1;
        public string SteamId = "";
        public BuildInfo BuildInfo;
        public string SessionStart;
        public string WorldCreatedAt = "";
        public double TotalPlaySecondsAtStart;
        public List<string> BaselineChallenges = new();
        public List<string> BaselineResearch = new();

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Settings);
        }

        // 前回セッションの残骸を読む境界。壊れていたら null を返し、呼び出し側が理由付きで捨てる
        // The boundary that reads a leftover session; returns null when broken so the caller drops it with a reason
        public static ProgressRecordHeader FromJson(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<ProgressRecordHeader>(json, Settings);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"進行記録のヘッダを読めません: {exception.GetBaseException().Message}");
                return null;
            }
        }
    }
}
```

- [ ] **Step 4: 合成器（純関数）を実装する**

`Playtest/Progress/ProgressRecordComposer.cs`:
```csharp
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Client.Game.InGame.Playtest.Progress
{
    // ヘッダとイベント列から record.json を組む純関数。集計もキャンセル合成もここだけで行う（shared-contracts §3）
    // A pure function composing record.json from the header and events; all aggregation and cancel synthesis live here (shared-contracts §3)
    public static class ProgressRecordComposer
    {
        public const string PlaceBlockStateName = "PlaceBlock";

        public static string Compose(ProgressRecordHeader header, IReadOnlyList<ProgressEventEntry> events, string endReason, DateTime sessionEndUtc)
        {
            var enriched = WithSynthesizedBuildModeCancel(events);
            var sessionStart = ParseUtc(header.SessionStart, sessionEndUtc);
            var playSeconds = Math.Max(0, (sessionEndUtc - sessionStart).TotalSeconds);

            var reachedChallenges = new List<string>(header.BaselineChallenges);
            var completedResearch = new List<string>(header.BaselineResearch);
            var placedBlockCount = 0;
            var craftCount = 0;
            var lastUiState = "";
            var eventArray = new JArray();

            foreach (var entry in enriched)
            {
                eventArray.Add(entry.ToJObject());
                switch (entry.Type)
                {
                    case ProgressEventType.BlockPlaced:
                        placedBlockCount++;
                        break;
                    case ProgressEventType.CraftExecuted:
                        craftCount++;
                        break;
                    case ProgressEventType.ChallengeCompleted:
                        AddDistinct(reachedChallenges, (string)entry.Data["challengeGuid"]);
                        break;
                    case ProgressEventType.ResearchCompleted:
                        AddDistinct(completedResearch, (string)entry.Data["researchGuid"]);
                        break;
                    case ProgressEventType.UiStateChanged:
                        lastUiState = (string)entry.Data["state"] ?? lastUiState;
                        break;
                }
            }

            var record = new JObject
            {
                ["schemaVersion"] = header.SchemaVersion,
                ["steamId"] = header.SteamId ?? "",
                ["buildInfo"] = header.BuildInfo == null ? JValue.CreateNull() : JObject.Parse(JsonConvert.SerializeObject(header.BuildInfo, new JsonSerializerSettings { ContractResolver = new Newtonsoft.Json.Serialization.CamelCasePropertyNamesContractResolver() })),
                ["sessionStart"] = header.SessionStart,
                ["sessionEnd"] = sessionEndUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                ["endReason"] = endReason,
                ["playSeconds"] = playSeconds,
                ["worldCreatedAt"] = header.WorldCreatedAt ?? "",
                ["totalPlaySeconds"] = header.TotalPlaySecondsAtStart + playSeconds,
                ["reachedChallenges"] = new JArray(reachedChallenges),
                ["completedResearch"] = new JArray(completedResearch),
                ["placedBlockCount"] = placedBlockCount,
                ["craftCount"] = craftCount,
                ["lastUiState"] = lastUiState,
                ["events"] = eventArray,
            };
            return record.ToString(Formatting.Indented);
        }

        // 「PlaceBlock に入って、1件も設置しないまま抜けた」滞在だけを離脱として合成する
        // Synthesizes a cancel only for a stay that entered PlaceBlock and left without a single placement
        public static List<ProgressEventEntry> WithSynthesizedBuildModeCancel(IReadOnlyList<ProgressEventEntry> events)
        {
            var result = new List<ProgressEventEntry>(events.Count + 4);
            var insideBuildMode = false;
            var placedInsideBuildMode = false;

            foreach (var entry in events)
            {
                if (insideBuildMode && entry.Type == ProgressEventType.BlockPlaced) placedInsideBuildMode = true;

                if (entry.Type == ProgressEventType.UiStateChanged)
                {
                    var state = (string)entry.Data["state"];
                    var entering = state == PlaceBlockStateName;
                    if (insideBuildMode && !entering && !placedInsideBuildMode)
                    {
                        result.Add(ProgressEventEntry.Create(ParseUtc(entry.T, DateTime.UtcNow), entry.Tick, ProgressEventType.BuildModeCancelled, new JObject { ["nextState"] = state }));
                    }
                    if (!insideBuildMode && entering) placedInsideBuildMode = false;
                    insideBuildMode = entering;
                }

                result.Add(entry);
            }
            return result;
        }

        private static void AddDistinct(List<string> values, string value)
        {
            if (string.IsNullOrEmpty(value) || values.Contains(value)) return;
            values.Add(value);
        }

        private static DateTime ParseUtc(string iso, DateTime fallback)
        {
            return DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : fallback;
        }
    }
}
```

- [ ] **Step 5: ファイル層を実装する**

`Playtest/Progress/ProgressRecordFiles.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // 進行中セッションを current/ に追記し、終了時に outbox の1箱へ畳む。追記形式なので落ちても直前まで残る
    // Appends the in-flight session under current/ and folds it into one outbox box at the end; append-only survives a crash
    public static class ProgressRecordFiles
    {
        public static void WriteHeader(ProgressRecordHeader header)
        {
            Directory.CreateDirectory(GameSystemPaths.ProgressRecordCurrentDirectory);
            File.WriteAllText(ProgressRecordPaths.CurrentHeaderPath, header.ToJson());
        }

        public static void AppendEvent(ProgressEventEntry entry)
        {
            Directory.CreateDirectory(GameSystemPaths.ProgressRecordCurrentDirectory);
            File.AppendAllText(ProgressRecordPaths.CurrentEventsPath, entry.ToJsonLine() + "\n");
        }

        public static bool HasCurrentSession()
        {
            return File.Exists(ProgressRecordPaths.CurrentHeaderPath);
        }

        public static ProgressRecordHeader ReadHeader()
        {
            if (!HasCurrentSession()) return null;
            return ProgressRecordHeader.FromJson(File.ReadAllText(ProgressRecordPaths.CurrentHeaderPath));
        }

        public static List<ProgressEventEntry> ReadEvents()
        {
            var entries = new List<ProgressEventEntry>();
            if (!File.Exists(ProgressRecordPaths.CurrentEventsPath)) return entries;
            foreach (var line in File.ReadAllLines(ProgressRecordPaths.CurrentEventsPath))
            {
                if (line.Length == 0) continue;
                var entry = ProgressEventEntry.FromJsonLine(line);
                if (entry != null) entries.Add(entry);
            }
            return entries;
        }

        // 閉じられなければ null。呼び出し側は理由をログへ出す（無音で消さない）
        // Returns null when it cannot close; the caller logs the reason and never drops it silently
        public static string CloseCurrentInto(string endReason, DateTime sessionEndUtc)
        {
            var header = ReadHeader();
            if (header == null)
            {
                if (HasCurrentSession()) Debug.LogWarning("進行記録のヘッダが壊れているためこのセッションは送れません");
                ClearCurrent();
                return null;
            }

            var directory = ProgressRecordPaths.CreateOutboxDirectory(DateTime.UtcNow, Guid.NewGuid().ToString("N").Substring(0, 8));
            File.WriteAllText(Path.Combine(directory, ProgressRecordPaths.RecordFileName), ProgressRecordComposer.Compose(header, ReadEvents(), endReason, sessionEndUtc));
            File.WriteAllText(Path.Combine(directory, ProgressRecordPaths.ReadyMarkerFileName), "");
            ClearCurrent();
            Debug.Log($"進行記録を書きました {directory} endReason:{endReason}");
            return directory;
        }

        public static void ClearCurrent()
        {
            if (File.Exists(ProgressRecordPaths.CurrentHeaderPath)) File.Delete(ProgressRecordPaths.CurrentHeaderPath);
            if (File.Exists(ProgressRecordPaths.CurrentEventsPath)) File.Delete(ProgressRecordPaths.CurrentEventsPath);
        }
    }
}
```

- [ ] **Step 6: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Playtest\.(ProgressRecordComposerTest|ProgressRecordFilesTest)$"`
Expected: 全PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest moorestech_client/Assets/Scripts/Client.Tests/Playtest
git commit -m "feat(client): 進行記録の追記ファイルと record.json 合成器"
```

---

### Task 8: 進行記録の購読と収集（`ProgressRecorder`・`ProgressSessionRecovery`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/IPlaytestProgressSink.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/ProgressBaseline.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/ProgressSessionRecovery.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/ProgressRecorder.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（`ProgressRecorder` を EntryPoint 登録）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/CraftActions.cs`（クラフト送信直後のプッシュ）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:182`（`CraftExecuteActionHandler` へ sink を渡す）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/BugReportSubmitActionHandler.cs`（送信成功直後のプッシュ）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Playtest/ProgressSessionRecoveryTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Playtest/ProgressBaselineTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/Playtest/RecordingProgressSink.cs`（Task 1 のテストが使うフェイク）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/BugReportSubmitKindTest.cs`（ハンドラの ctor が4引数になるため）

**Interfaces:**
- Consumes: Task 7 の全型、Task 6 `VanillaApiWithResponse.GetWorldPlaySessionInfo`、Task 4 `IPlaytestSessionIdentity`／`BuildInfoReader`、Task 3 `PreviousSessionSalvage.Artifacts`、`Client.Game.Common.GameShutdownEvent`／`IGameShutdownParticipant`／`ShutdownFlushResult`、`UIStateControl.OnStateChanged`、`ResearchCompleteEventPacket`／`CompletedChallengeEventPacket`／`PlaceBlockEventPacket`、`InitialHandshakeResponse`
- Produces:
  - `public interface IPlaytestProgressSink { void RecordCraftExecuted(Guid recipeGuid); void RecordReportSent(string kind); }`
  - `public static class ProgressBaseline { public static List<string> CompletedChallengeGuids(IEnumerable<Guid> completedChallenges); public static List<string> CompletedResearchGuids(IEnumerable<KeyValuePair<Guid, ResearchNodeState>> researchStates); }`
  - `public static class ProgressSessionRecovery { public static string RecoverLeftoverSession(bool previousExitWasClean); }`（残骸が無ければ `null`）
  - `public sealed class ProgressRecorder : IInitializable, IDisposable, IGameShutdownParticipant, IPlaytestProgressSink`
  - `CraftExecuteActionHandler(IGameUnlockStateData unlockStateData, IPlaytestProgressSink progressSink)`
  - `BugReportSubmitActionHandler(BugReportBundleWriter writer, BugReportCaptureSession session, PauseMenuStateService pauseMenuStateService, IPlaytestProgressSink progressSink)`

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/Playtest/ProgressBaselineTest.cs`:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Playtest.Progress;
using Game.Research;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    public class ProgressBaselineTest
    {
        [Test]
        public void 完了済みチャレンジと完了済み研究だけを文字列で拾う()
        {
            var challengeA = Guid.NewGuid();
            var researchDone = Guid.NewGuid();
            var researchOpen = Guid.NewGuid();

            var challenges = ProgressBaseline.CompletedChallengeGuids(new List<Guid> { challengeA, challengeA });
            var research = ProgressBaseline.CompletedResearchGuids(new Dictionary<Guid, ResearchNodeState>
            {
                { researchDone, ResearchNodeState.Completed },
                { researchOpen, ResearchNodeState.Researchable },
            });

            CollectionAssert.AreEqual(new[] { challengeA.ToString() }, challenges);
            CollectionAssert.AreEqual(new[] { researchDone.ToString() }, research);
        }

        [Test]
        public void 空入力でも空リストを返す()
        {
            Assert.AreEqual(0, ProgressBaseline.CompletedChallengeGuids(new List<Guid>()).Count);
            Assert.AreEqual(0, ProgressBaseline.CompletedResearchGuids(new Dictionary<Guid, ResearchNodeState>()).Count);
        }
    }
}
```

`Client.Tests/Playtest/ProgressSessionRecoveryTest.cs`:
```csharp
using System;
using System.IO;
using Client.Game.InGame.Playtest.Progress;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.Playtest
{
    public class ProgressSessionRecoveryTest
    {
        [SetUp]
        [TearDown]
        public void ClearCurrent()
        {
            ProgressRecordFiles.ClearCurrent();
        }

        private static void WriteLeftoverSession()
        {
            ProgressRecordFiles.WriteHeader(new ProgressRecordHeader
            {
                SessionStart = DateTime.UtcNow.AddMinutes(-5).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                WorldCreatedAt = "2026-09-10T09:00:00Z",
            });
            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(DateTime.UtcNow.AddMinutes(-4), 1, ProgressEventType.BlockPlaced, new JObject()));
        }

        [Test]
        public void 残骸が無ければ何もしない()
        {
            Assert.IsNull(ProgressSessionRecovery.RecoverLeftoverSession(true));
            Assert.IsNull(ProgressSessionRecovery.RecoverLeftoverSession(false));
        }

        [Test]
        public void 異常終了の残骸はcrash_recoveredで送る()
        {
            WriteLeftoverSession();
            var bundle = ProgressSessionRecovery.RecoverLeftoverSession(false);
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual("crash-recovered", (string)record["endReason"]);
            Assert.IsFalse(ProgressRecordFiles.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        [Test]
        public void 正常終了なのに残った残骸はquitで送る()
        {
            // マーカーは書けたが record を書き切る前に落ちた場合。恒久的に残さず必ず回収する
            // The marker was written but the record was not; this leftover is always recovered, never left behind
            WriteLeftoverSession();
            var bundle = ProgressSessionRecovery.RecoverLeftoverSession(true);
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual("quit", (string)record["endReason"]);
            Assert.IsFalse(ProgressRecordFiles.HasCurrentSession());
            Directory.Delete(bundle, true);
        }

        [Test]
        public void イベントが0件の残骸も送れる()
        {
            ProgressRecordFiles.WriteHeader(new ProgressRecordHeader { SessionStart = DateTime.UtcNow.AddMinutes(-1).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'") });
            var bundle = ProgressSessionRecovery.RecoverLeftoverSession(false);
            var record = JObject.Parse(File.ReadAllText(Path.Combine(bundle, ProgressRecordPaths.RecordFileName)));
            Assert.AreEqual(0, ((JArray)record["events"]).Count);
            Directory.Delete(bundle, true);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `ProgressBaseline`／`ProgressSessionRecovery` が無いというコンパイルエラー

- [ ] **Step 3: 基準値と残骸回収を実装する**

`Playtest/Progress/ProgressBaseline.cs`:
```csharp
using System;
using System.Collections.Generic;
using Game.Research;

namespace Client.Game.InGame.Playtest.Progress
{
    // 初期ハンドシェイクから「セッション開始時点で既に到達していた分」を作る。以後はイベントで足す
    // Builds what was already reached at session start from the initial handshake; events add to it afterwards
    public static class ProgressBaseline
    {
        public static List<string> CompletedChallengeGuids(IEnumerable<Guid> completedChallenges)
        {
            var result = new List<string>();
            foreach (var guid in completedChallenges)
            {
                var text = guid.ToString();
                if (!result.Contains(text)) result.Add(text);
            }
            return result;
        }

        public static List<string> CompletedResearchGuids(IEnumerable<KeyValuePair<Guid, ResearchNodeState>> researchStates)
        {
            var result = new List<string>();
            foreach (var state in researchStates)
            {
                if (state.Value != ResearchNodeState.Completed) continue;
                var text = state.Key.ToString();
                if (!result.Contains(text)) result.Add(text);
            }
            return result;
        }
    }
}
```

`Playtest/Progress/ProgressSessionRecovery.cs`:
```csharp
using System;
using UnityEngine;

namespace Client.Game.InGame.Playtest.Progress
{
    // 前回セッションの書きかけを起動時に必ず畳む。異常終了なら crash-recovered、正常終了でも残っていれば quit で送る
    // Always folds a half-written previous session at boot: crash-recovered after an unclean exit, quit if it lingered after a clean one
    public static class ProgressSessionRecovery
    {
        public static string RecoverLeftoverSession(bool previousExitWasClean)
        {
            if (!ProgressRecordFiles.HasCurrentSession()) return null;

            // 終了時刻は分からないので最後のイベント時刻を使う。イベントが無ければ開始時刻に潰れる
            // The exit time is unknown, so the last event's time is used; with no events it collapses to the session start
            var events = ProgressRecordFiles.ReadEvents();
            var header = ProgressRecordFiles.ReadHeader();
            var sessionEnd = ResolveSessionEnd(header, events);
            var endReason = previousExitWasClean ? "quit" : "crash-recovered";

            var bundle = ProgressRecordFiles.CloseCurrentInto(endReason, sessionEnd);
            if (bundle == null) Debug.LogWarning("前回の進行記録を閉じられませんでした（ヘッダが壊れています）");
            return bundle;
        }

        private static DateTime ResolveSessionEnd(ProgressRecordHeader header, System.Collections.Generic.List<ProgressEventEntry> events)
        {
            var last = events.Count > 0 ? events[events.Count - 1].T : header?.SessionStart;
            return DateTime.TryParse(last, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed) ? parsed : DateTime.UtcNow;
        }
    }
}
```

- [ ] **Step 4: `ProgressRecorder` を実装する**

`Playtest/Progress/IPlaytestProgressSink.cs`:
```csharp
using System;

namespace Client.Game.InGame.Playtest.Progress
{
    // 購読では取れない「操作そのもの」を記録側へプッシュする窓口。実装は ProgressRecorder 1つ
    // The window that pushes actions themselves, which no subscription observes; ProgressRecorder is the only implementation
    public interface IPlaytestProgressSink
    {
        void RecordCraftExecuted(Guid recipeGuid);
        void RecordReportSent(string kind);
    }
}
```

`Playtest/Progress/ProgressRecorder.cs`:
```csharp
using System;
using System.Linq;
using Client.Game.Common;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.BugReport.Playtest;
using Client.Game.InGame.Context;
using Client.Game.InGame.UI.UIState;
using Client.Network.API;
using Core.Update;
using Cysharp.Threading.Tasks;
using MessagePack;
using Newtonsoft.Json.Linq;
using Server.Event.EventReceive;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.Playtest.Progress
{
    // セッションの進行を購読で集めて追記し、終了時に1件の進行記録として書き出す（ADR 0058）
    // Collects the session's progress through subscriptions, appends it, and writes one progress record at shutdown (ADR 0058)
    public sealed class ProgressRecorder : IInitializable, IDisposable, IGameShutdownParticipant, IPlaytestProgressSink
    {
        private readonly InitialHandshakeResponse _handshake;
        private readonly UIStateControl _uiStateControl;
        private readonly IPlaytestSessionIdentity _identity;
        private readonly DateTime _sessionStartUtc = DateTime.UtcNow;
        private bool _closed;

        public ProgressRecorder(InitialHandshakeResponse handshake, UIStateControl uiStateControl, IPlaytestSessionIdentity identity)
        {
            _handshake = handshake;
            _uiStateControl = uiStateControl;
            _identity = identity;
        }

        public void Initialize()
        {
            // 前回の書きかけを先に畳んでから今回を開く。current/ は常に1セッションぶんしか持たない
            // Fold the previous half-written session first; current/ never holds more than one session
            var previousExitWasClean = PreviousSessionSalvage.Artifacts?.PreviousExitWasClean ?? true;
            ProgressSessionRecovery.RecoverLeftoverSession(previousExitWasClean);

            OpenSessionAsync().Forget(exception => Debug.LogError($"進行記録のヘッダを書けませんでした: {exception.GetBaseException().Message}"));

            _uiStateControl.OnStateChanged += OnUiStateChanged;
            ClientContext.VanillaApi.Event.SubscribeEventResponse(ResearchCompleteEventPacket.EventTag, OnResearchCompleted);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(CompletedChallengeEventPacket.EventTag, OnChallengeCompleted);
            ClientContext.VanillaApi.Event.SubscribeEventResponse(PlaceBlockEventPacket.EventTag, OnBlockPlaced);

            GameShutdownEvent.RegisterParticipant(this);
        }

        public void Dispose()
        {
            _uiStateControl.OnStateChanged -= OnUiStateChanged;
        }

        // 終了時に record.json を書く。書き出しは同期IOなので待ちは一瞬で終わる
        // Writes record.json at shutdown; the write is synchronous IO and finishes immediately
        public UniTask<ShutdownFlushResult> FlushOnShutdownAsync()
        {
            if (_closed) return UniTask.FromResult(ShutdownFlushResult.AlreadyShutdown);
            _closed = true;
            ProgressRecordFiles.CloseCurrentInto("quit", DateTime.UtcNow);
            return UniTask.FromResult(ShutdownFlushResult.Flushed);
        }

        public void RecordCraftExecuted(Guid recipeGuid)
        {
            Append(ProgressEventType.CraftExecuted, new JObject { ["recipeGuid"] = recipeGuid.ToString() });
        }

        public void RecordReportSent(string kind)
        {
            Append(ProgressEventType.ReportSent, new JObject { ["kind"] = kind });
        }

        private async UniTaskVoid OpenSessionAsync()
        {
            var header = new ProgressRecordHeader
            {
                SteamId = _identity.SteamId,
                BuildInfo = BuildInfoReader.Read(),
                SessionStart = _sessionStartUtc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                BaselineChallenges = ProgressBaseline.CompletedChallengeGuids(_handshake.Challenges.SelectMany(c => c.CompletedChallenges).Select(c => c.ChallengeGuid)),
                BaselineResearch = ProgressBaseline.CompletedResearchGuids(_handshake.ResearchNodeStates),
            };

            var info = await ClientContext.VanillaApi.Response.GetWorldPlaySessionInfo(default);
            if (info == null) Debug.LogWarning("ワールドのプレイ時間を取得できないため worldCreatedAt と totalPlaySeconds は空で記録します");
            header.WorldCreatedAt = info?.WorldCreatedAt ?? "";
            header.TotalPlaySecondsAtStart = info?.TotalPlaySeconds ?? 0;

            ProgressRecordFiles.WriteHeader(header);
        }

        private void OnUiStateChanged(UIStateEnum state)
        {
            Append(ProgressEventType.UiStateChanged, new JObject { ["state"] = state.ToString() });
        }

        private void OnResearchCompleted(byte[] payload)
        {
            var message = MessagePackSerializer.Deserialize<ResearchCompleteEventPacket.ResearchCompleteEventMessagePack>(payload);
            Append(ProgressEventType.ResearchCompleted, new JObject { ["researchGuid"] = message.ResearchGuidStr });
        }

        private void OnChallengeCompleted(byte[] payload)
        {
            var message = MessagePackSerializer.Deserialize<CompletedChallengeEventMessagePack>(payload);
            Append(ProgressEventType.ChallengeCompleted, new JObject { ["challengeGuid"] = message.CompletedChallengeGuidStr });
        }

        private void OnBlockPlaced(byte[] payload)
        {
            // 設置数だけが集計対象。BlockId はマスタのロード順で採番される揮発値なので記録に残さない
            // Only the count is aggregated; BlockId is a volatile value renumbered per master load, so it is not recorded
            Append(ProgressEventType.BlockPlaced, new JObject());
        }

        private void Append(string type, JObject data)
        {
            if (_closed) return;
            ProgressRecordFiles.AppendEvent(ProgressEventEntry.Create(DateTime.UtcNow, GameUpdater.CurrentTick, type, data));
        }
    }
}
```
（`OnBlockPlaced` は payload を読まないので `MessagePackSerializer` は不要。購読の型引数も要らず、`byte[]` を受け取って件数を1つ足すだけにする）

- [ ] **Step 5: DI 登録と操作直後プッシュを配線する**

`MainGameModelRegistration.Register` の `IPlaytestSessionIdentity` 登録の直後:
```csharp
            // 進行記録は購読で集める。UIStateControl はシーン上のcomponentなので既存の登録から解決される
            // The progress record collects through subscriptions; UIStateControl resolves from the existing scene component registration
            builder.RegisterEntryPoint<ProgressRecorder>().AsSelf().As<IPlaytestProgressSink>();
```
（`using Client.Game.InGame.Playtest.Progress;` を追加する。`UIStateControl` が `builder.RegisterComponent` されていない場合は `MainGameStarter.StartGame` の登録に合わせて解決元を揃える）

`CraftActions.cs`: `CraftExecuteActionHandler` に sink を持たせ、送信直後に push する:
```csharp
        private readonly IGameUnlockStateData _unlockStateData;
        private readonly IPlaytestProgressSink _progressSink;

        public CraftExecuteActionHandler(IGameUnlockStateData unlockStateData, IPlaytestProgressSink progressSink)
        {
            _unlockStateData = unlockStateData;
            _progressSink = progressSink;
        }
```
```csharp
            ClientContext.VanillaApi.SendOnly.Craft(recipeGuid);

            // クラフトは送信のみで応答が無いため、変化を起こした操作の直後にプッシュする
            // A craft has no response, so the progress push happens right after the operation that causes the change
            _progressSink.RecordCraftExecuted(recipeGuid);
            return UniTask.FromResult(ActionResult.Success());
```
（`using Client.Game.InGame.Playtest.Progress;` を追加する）

`WebUiGameBinder.cs:182` を差し替える:
```csharp
            hub.RegisterAction(new CraftExecuteActionHandler(unlockStateData, resolver.Resolve<IPlaytestProgressSink>()));
```
`BugReportSubmitActionHandler` の ctor を4引数（`BugReportBundleWriter writer, BugReportCaptureSession session, PauseMenuStateService pauseMenuStateService, IPlaytestProgressSink progressSink`）にし、書き出し成功直後（`RequestClose()` の直前）に:
```csharp
            // 送信は購読で観測できないので、成功した操作の直後にプッシュする
            // A send is not observable through any subscription, so it is pushed right after the successful operation
            _progressSink.RecordReportSent(kind);
```
`WebUiGameBinder` の `BugReportSubmitActionHandler` 登録行にも `resolver.Resolve<IPlaytestProgressSink>()` を足す。

**Task 1 で書いた `BugReportSubmitKindTest` の生成も4引数へ直す**（`Client.Tests/Playtest/RecordingProgressSink.cs` を新設し、押された種別を保持するだけのフェイクにする）:
```csharp
using System;
using System.Collections.Generic;
using Client.Game.InGame.Playtest.Progress;

namespace Client.Tests.Playtest
{
    // プッシュされた内容だけを覚えるテスト用の記録先
    // A test sink that only remembers what was pushed
    public sealed class RecordingProgressSink : IPlaytestProgressSink
    {
        public readonly List<Guid> CraftedRecipes = new();
        public readonly List<string> SentReportKinds = new();

        public void RecordCraftExecuted(Guid recipeGuid) => CraftedRecipes.Add(recipeGuid);
        public void RecordReportSent(string kind) => SentReportKinds.Add(kind);
    }
}
```
```csharp
            var handler = new BugReportSubmitActionHandler(new BugReportBundleWriter(new EmptyPlaytestSessionIdentity()), session, new PauseMenuStateService(session), new RecordingProgressSink());
```

- [ ] **Step 6: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.Playtest\.(ProgressBaselineTest|ProgressSessionRecoveryTest)$"`
Expected: 全PASS

- [ ] **Step 7: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests/Playtest
git commit -m "feat(client): 進行記録を購読で集め終了時に書き出す"
```

---

### Task 9: 初回起動の同意表示（`PlaytestConsentGate`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Playtest/PlaytestConsentFlag.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestConsentGate.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/Playtest/PlaytestConsentGateTopic.cs`
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/Playtest/PlaytestConsentGateActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Playtest/PlaytestGateBinder.cs`（同意ゲートの Bind を追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Playtest/PlaytestStartGates.cs`（同意 → クラッシュ確認の順で待つ）
- Modify: `Localization/localization.csv`（3行追加）・`_CompileRequester.cs`
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`・`payloadTypes.ts`・`validators.ts`・`transport/protocol.ts`・`transport/actionContract.ts`
- Create: `moorestech_web/webui/src/features/playtestGate/PlaytestConsentGate.tsx`・`PlaytestConsentGate.test.ts`
- Modify: `moorestech_web/webui/src/features/playtestGate/index.ts`・`moorestech_web/webui/src/app/App.tsx`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/PlaytestConsentGateTest.cs`

**Interfaces:**
- Consumes: Task 5 `PlaytestGateBinder`・`PlaytestStartGates`、前例 `EventLanguageGate`
- Produces:
  - `public static class PlaytestConsentFlag { public const string FileName = "consent-acknowledged-v1"; public static string FilePath { get; } public static bool IsAcknowledged(); public static void Acknowledge(); }`
  - `public class PlaytestConsentGate { public PlaytestConsentGate(bool startsWaiting); public bool IsWaitingAcknowledgement(); public IObservable<Unit> OnWaitingChanged { get; } public UniTask WaitForAcknowledgementAsync(); public PlaytestConsentResult Acknowledge(); }`
  - `public enum PlaytestConsentResult { Acknowledged, AlreadyAcknowledged }`
  - `PlaytestGateBinder.BindConsentGate(WebSocketHub hub, bool alreadyAcknowledged)`
  - トピック `playtest.consent_gate`: `{ waiting: boolean }`／アクション `playtest.consent.acknowledge`: payload `{}`
  - ローカライズキー `ui.playtest.consent.title`・`.body`・`.agree`

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/BugReport/PlaytestConsentGateTest.cs`:
```csharp
using System.IO;
using Client.Game.InGame.BugReport.Playtest;
using Client.WebUiHost.Game.Playtest;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class PlaytestConsentGateTest
    {
        [SetUp]
        [TearDown]
        public void RemoveFlag()
        {
            if (File.Exists(PlaytestConsentFlag.FilePath)) File.Delete(PlaytestConsentFlag.FilePath);
        }

        [Test]
        public void 既読フラグが無ければ待機し了解で解ける()
        {
            Assert.IsFalse(PlaytestConsentFlag.IsAcknowledged());
            var gate = new PlaytestConsentGate(!PlaytestConsentFlag.IsAcknowledged());
            Assert.IsTrue(gate.IsWaitingAcknowledgement());

            Assert.AreEqual(PlaytestConsentResult.Acknowledged, gate.Acknowledge());
            Assert.IsFalse(gate.IsWaitingAcknowledgement());
            Assert.IsTrue(gate.WaitForAcknowledgementAsync().Status.IsCompleted());
            Assert.IsTrue(PlaytestConsentFlag.IsAcknowledged());
        }

        [Test]
        public void 既読フラグがあれば待機しない()
        {
            PlaytestConsentFlag.Acknowledge();
            var gate = new PlaytestConsentGate(!PlaytestConsentFlag.IsAcknowledged());
            Assert.IsFalse(gate.IsWaitingAcknowledgement());
            Assert.IsTrue(gate.WaitForAcknowledgementAsync().Status.IsCompleted());
        }

        [Test]
        public void 二度目の了解は弾く()
        {
            var gate = new PlaytestConsentGate(true);
            Assert.AreEqual(PlaytestConsentResult.Acknowledged, gate.Acknowledge());
            Assert.AreEqual(PlaytestConsentResult.AlreadyAcknowledged, gate.Acknowledge());
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: `PlaytestConsentFlag`／`PlaytestConsentGate` が無いというコンパイルエラー

- [ ] **Step 3: フラグとゲートを実装する**

`BugReport/Playtest/PlaytestConsentFlag.cs`:
```csharp
using System;
using System.IO;
using Game.Paths;

namespace Client.Game.InGame.BugReport.Playtest
{
    // 同意表示の既読フラグ。同意そのものはキー配布時の案内で取っており、これは表示を1回に絞るためのローカル印
    // The consent screen's read flag; consent itself is taken at key handout, and this local mark only limits the display to once
    public static class PlaytestConsentFlag
    {
        public const string FileName = "consent-acknowledged-v1";

        public static string FilePath => Path.Combine(GameSystemPaths.BugReportDirectory, FileName);

        public static bool IsAcknowledged()
        {
            return File.Exists(FilePath);
        }

        public static void Acknowledge()
        {
            Directory.CreateDirectory(GameSystemPaths.BugReportDirectory);
            File.WriteAllText(FilePath, DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"));
        }
    }
}
```

`Client.WebUiHost/Game/Playtest/PlaytestConsentGate.cs`:
```csharp
using System;
using Client.Game.InGame.BugReport.Playtest;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Playtest
{
    // 初回起動だけ「送られる内容」を出して開始を止める（ADR 0040 の言語選択ゲートと同型）
    // Shows what will be sent and holds the start on the first boot only (same shape as the ADR 0040 language gate)
    public class PlaytestConsentGate
    {
        private readonly UniTaskCompletionSource _acknowledgeSource = new();
        private readonly Subject<Unit> _onWaitingChanged = new();
        private bool _isWaiting;

        public IObservable<Unit> OnWaitingChanged => _onWaitingChanged;

        public PlaytestConsentGate(bool startsWaiting)
        {
            _isWaiting = startsWaiting;
            if (!startsWaiting) _acknowledgeSource.TrySetResult();
        }

        public bool IsWaitingAcknowledgement()
        {
            return _isWaiting;
        }

        public UniTask WaitForAcknowledgementAsync()
        {
            return _acknowledgeSource.Task;
        }

        // 了解は1回だけ効く。既読フラグはここで書き、次回以降は待機せず素通りする
        // Only the first acknowledgement takes effect; the read flag is written here so later boots pass straight through
        public PlaytestConsentResult Acknowledge()
        {
            if (!_isWaiting) return PlaytestConsentResult.AlreadyAcknowledged;
            _isWaiting = false;
            PlaytestConsentFlag.Acknowledge();
            _onWaitingChanged.OnNext(Unit.Default);
            _acknowledgeSource.TrySetResult();
            return PlaytestConsentResult.Acknowledged;
        }
    }

    public enum PlaytestConsentResult
    {
        Acknowledged,
        AlreadyAcknowledged
    }
}
```

`Topics/Playtest/PlaytestConsentGateTopic.cs` は `CrashReportGateTopic` と同じ形（`TopicName = "playtest.consent_gate"`、`gate.OnWaitingChanged` を購読し `{ Waiting = gate.IsWaitingAcknowledgement() }` を配信、`Dispose` で購読破棄）で書く。

`Actions/Playtest/PlaytestConsentGateActions.cs`:
```csharp
using Client.WebUiHost.Boot;
using Client.WebUiHost.Game.Playtest;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions.Playtest
{
    public static class PlaytestConsentGateActions
    {
        public static void Register(WebSocketHub hub, PlaytestConsentGate gate)
        {
            hub.RegisterAction(new AcknowledgePlaytestConsentActionHandler(gate));
        }
    }

    public class AcknowledgePlaytestConsentActionHandler : IActionHandler
    {
        public string ActionType => "playtest.consent.acknowledge";

        private readonly PlaytestConsentGate _gate;

        public AcknowledgePlaytestConsentActionHandler(PlaytestConsentGate gate)
        {
            _gate = gate;
        }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var result = _gate.Acknowledge();
            return UniTask.FromResult(result switch
            {
                PlaytestConsentResult.Acknowledged => ActionResult.Success(),
                // 二度目の了解は何も変えないので成功へ丸めない
                // A second acknowledgement changes nothing, so it is not folded into success
                PlaytestConsentResult.AlreadyAcknowledged => ActionResult.Fail("already_acknowledged"),
                _ => ActionResult.Fail("already_acknowledged"),
            });
        }
    }
}
```

`PlaytestGateBinder.cs` に追加:
```csharp
        public static PlaytestConsentGate BindConsentGate(WebSocketHub hub, bool alreadyAcknowledged)
        {
            var gate = new PlaytestConsentGate(!alreadyAcknowledged);
            hub.RegisterTopic(PlaytestConsentGateTopic.TopicName, new PlaytestConsentGateTopic(hub, gate));
            PlaytestConsentGateActions.Register(hub, gate);
            return gate;
        }
```

`PlaytestStartGates.WaitForPlaytestGatesAsync` の `hub == null` チェックの直後、クラッシュ確認より前に:
```csharp
            // 同意表示 → 前回異常終了の確認 の順。何が送られるかを読む前に送信可否を聞かない
            // Consent first, then the previous-crash confirmation; never ask to send before showing what gets sent
            var consentGate = PlaytestGateBinder.BindConsentGate(hub, PlaytestConsentFlag.IsAcknowledged());
            await consentGate.WaitForAcknowledgementAsync();
            await UniTask.Yield();
```
（`using Client.Game.InGame.BugReport.Playtest;` を追加する）

- [ ] **Step 4: ローカライズと webui**

`Localization/localization.csv` 末尾に追加（本文にカンマを含むため `"` で囲む）:
```
ui.playtest.consent.title,About this playtest,About this playtest,このプレイテストについて,Über diesen Playtest
ui.playtest.consent.body,"When you send a report we upload your description, the last 2 minutes of gameplay video, a world snapshot, the packet log, the Unity log and a screenshot. A session summary and an event list are sent automatically when you quit. Nothing else leaves your PC.","When you send a report we upload your description, the last 2 minutes of gameplay video, a world snapshot, the packet log, the Unity log and a screenshot. A session summary and an event list are sent automatically when you quit. Nothing else leaves your PC.","報告を送ると、説明文・直前2分の録画・ワールドのスナップショット・パケットログ・Unityログ・スクリーンショットが送信されます。終了時にはセッションの要約とイベント列が自動送信されます。これ以外はPCから送信されません。","Wenn du eine Meldung sendest, werden dein Text, die letzten 2 Minuten Spielaufnahme, ein Welt-Snapshot, das Paketprotokoll, das Unity-Log und ein Screenshot übertragen. Beim Beenden werden automatisch eine Sitzungszusammenfassung und eine Ereignisliste gesendet. Sonst verlässt nichts deinen PC."
ui.playtest.consent.agree,Got it,Got it,了解,Verstanden
```
`_CompileRequester.cs` の `dummyText` を新しい値へ変更し `pnpm gen:i18n` を実行する。

bridge は Task 5 と同じ4箇所に `consentGate: "playtest.consent_gate"` と `PlaytestConsentGateDataSchema = z.object({ waiting: z.boolean() })` を足し、`actionContract.ts` に `"playtest.consent.acknowledge": Record<string, never>;` と `ACTION_TYPES` の1行を足す。

`src/features/playtestGate/PlaytestConsentGate.tsx`:
```tsx
// 初回起動だけ出る全画面の同意表示。本文を読ませてから了解で閉じる
// The first-boot-only full-screen consent notice; the body is read, then the acknowledgement closes it
import { Overlay, Portal, Stack, Text, Title } from "@mantine/core";
import { dispatchAction, Topics, useTopicSelector } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { PanelActionButton } from "@/shared/ui";

export function PlaytestConsentGate() {
  const { t } = useI18n();
  const waiting = useTopicSelector(Topics.consentGate, (data) => data?.waiting ?? false);

  if (!waiting) return null;

  return (
    <Portal>
      <Overlay
        fixed
        center
        backgroundOpacity={1}
        color="var(--event-language-gate-face)"
        zIndex="var(--z-portal-playtest-gate)"
        data-testid="playtest-consent-gate"
      >
        <Stack align="center" gap="xl">
          <Title order={1} c="white">{t(L.ui.playtest.consent.title)}</Title>
          <Text c="white" data-testid="playtest-consent-body">{t(L.ui.playtest.consent.body)}</Text>
          <PanelActionButton onClick={() => void dispatchAction("playtest.consent.acknowledge", {})} testId="playtest-consent-agree">
            {t(L.ui.playtest.consent.agree)}
          </PanelActionButton>
        </Stack>
      </Overlay>
    </Portal>
  );
}
```

`src/features/playtestGate/PlaytestConsentGate.test.ts`（`useTopicSelector` をモックして `waiting: true` を返す）:
```ts
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({ dispatchAction: vi.fn(async () => true), waiting: { value: true } }));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchAction: mocks.dispatchAction,
  useTopicSelector: () => mocks.waiting.value,
}));

import { PlaytestConsentGate } from "./PlaytestConsentGate";

const dictionary = {
  "ui.playtest.consent.title": "このプレイテストについて",
  "ui.playtest.consent.body": "報告を送ると…",
  "ui.playtest.consent.agree": "了解",
};

afterEach(() => {
  vi.clearAllMocks();
  mocks.waiting.value = true;
});

async function render(): Promise<ReactTestRenderer> {
  let renderer: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(PlaytestConsentGate));
  });
  return renderer!;
}

describe("PlaytestConsentGate", () => {
  it("待機中は本文と了解ボタンを描く", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render();
    expect(renderer.root.findByProps({ "data-testid": "playtest-consent-body" })).toBeTruthy();
    await act(async () => renderer.root.findByProps({ "data-testid": "playtest-consent-agree" }).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("playtest.consent.acknowledge", {});
    act(() => renderer.unmount());
  });

  it("待機していなければ何も描かない", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    mocks.waiting.value = false;
    const renderer = await render();
    expect(renderer.root.findAllByProps({ "data-testid": "playtest-consent-body" }).length).toBe(0);
    act(() => renderer.unmount());
  });
});
```

`index.ts` に `export { PlaytestConsentGate } from "./PlaytestConsentGate";` を足し、`App.tsx` の `<CrashReportGate />` の直前に `<PlaytestConsentGate />` を置く。

- [ ] **Step 5: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.BugReport\.PlaytestConsentGateTest$"` → `cd moorestech_web/webui && pnpm test && pnpm lint`
Expected: 全PASS

- [ ] **Step 6: コミットする**

```bash
git add Localization/localization.csv moorestech_client/Assets/Scripts moorestech_web/webui/src
git commit -m "feat(client,webui): 初回起動に送信内容の同意表示を出す"
```

---

### Task 10: 通しの EditModeInPlayingTest（種別付き送信 → 進行記録 → 残骸回収）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Playtest/PlaytestReportAndProgressTest.cs`

**Interfaces:**
- Consumes: plan A `WorldSnapshotRing`、plan B の確保セッションと `BugReportSubmitActionHandler`、Task 1・4・7・8 の型、`EditModeInPlayingTestUtil`

- [ ] **Step 1: テストを書く**

```csharp
using System.Collections;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.BugReport.LastSession;
using Client.Game.InGame.Playtest.Progress;
using Client.Game.InGame.UI.UIState;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.Paths;
using Game.SaveLoad.Snapshot;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.Playtest
{
    [Category("CiShardClientPlay3")]
    public class PlaytestReportAndProgressTest
    {
        [UnityTest]
        public IEnumerator 種別付きで送るとmanifestに載り終了で進行記録が出る()
        {
            EditModeInPlayingTestUtil.EnterPlayModeUtil();
            yield return new EnterPlayMode(expectDomainReload: true);
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();
            yield return new ExitPlayMode();

            UnityEditor.SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                await EditModeInPlayingTestUtil.LoadMainGame();
                var resolver = ClientDIContext.DIContainer.DIContainerResolver;

                // テスト起動は常時記録オフなので、リングだけ明示的に開始する
                // Test boots disable always-on capture, so start the ring explicitly
                ServerContext.GetService<WorldSnapshotRing>().Start(600, 4);
                await UniTask.Delay(3000);

                // 進行記録のヘッダはセッション開始で書かれている
                // The progress header is written when the session starts
                Assert.IsTrue(ProgressRecordFiles.HasCurrentSession(), "進行記録のヘッダが書かれていない");

                var uiState = resolver.Resolve<UIStateControl>();
                uiState.RequestTransition(UIStateEnum.PauseMenu);
                var session = resolver.Resolve<BugReportCaptureSession>();
                for (var i = 0; i < 100 && (!session.Status.Value.HasSession || session.Status.Value.CapturePending); i++) await UniTask.Delay(50);
                Assert.IsFalse(session.Status.Value.CapturePending, "サーバースナップショットが完了しない");

                var handler = new BugReportSubmitActionHandler(
                    resolver.Resolve<BugReportBundleWriter>(),
                    session,
                    resolver.Resolve<Client.Game.InGame.UI.UIState.State.PauseMenu.PauseMenuStateService>(),
                    resolver.Resolve<IPlaytestProgressSink>());
                var result = await handler.ExecuteAsync(new JObject { ["description"] = "感想テスト", ["kind"] = "feedback" });
                Assert.IsTrue(result.Ok, result.Error);

                var bundle = Directory.GetDirectories(GameSystemPaths.BugReportOutboxDirectory).OrderByDescending(d => d).First();
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
                Assert.AreEqual("feedback", (string)manifest["kind"]);
                Assert.AreEqual("", (string)manifest["steamId"]);
                Assert.IsTrue(manifest.ContainsKey("buildInfo"));
                Directory.Delete(bundle, true);

                // 終了パイプラインを回すと、正常終了マーカーと進行記録が揃う
                // Running the shutdown pipeline produces both the clean-exit marker and the progress record
                await Client.Game.Common.GameShutdownEvent.FireGameShutdownAsync();
                Assert.IsTrue(File.Exists(CleanExitMarker.FilePath), "正常終了マーカーが書かれていない");
                Assert.IsFalse(ProgressRecordFiles.HasCurrentSession(), "進行記録が閉じられていない");

                var record = Directory.GetDirectories(GameSystemPaths.ProgressRecordOutboxDirectory).OrderByDescending(d => d).First();
                var json = JObject.Parse(File.ReadAllText(Path.Combine(record, ProgressRecordPaths.RecordFileName)));
                Assert.AreEqual("quit", (string)json["endReason"]);
                Assert.IsTrue(File.Exists(Path.Combine(record, ProgressRecordPaths.ReadyMarkerFileName)));

                // UI遷移と報告送信が購読・プッシュの両方から入っている
                // Both the subscription (UI transition) and the push (report sent) landed in the events
                var types = ((JArray)json["events"]).Select(e => (string)e["type"]).ToList();
                CollectionAssert.Contains(types, ProgressEventType.UiStateChanged);
                CollectionAssert.Contains(types, ProgressEventType.ReportSent);
                Assert.Greater((double)json["playSeconds"], 0);
                Directory.Delete(record, true);

                // 後片付け: 次のテスト起動が「前回異常終了」にならないようマーカーは残す
                // Cleanup: the marker stays so the next test boot is not treated as an unclean exit
            }

            #endregion
        }
    }
}
```

- [ ] **Step 2: 実行する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.EditModeInPlayingTest\.Playtest\.PlaytestReportAndProgressTest" --timeout-seconds 900`
Expected: PASS（Domain Reload エラーは45秒待って再実行。戻らなければ `~/Library/Application Support/sakastudio/moorestech/TestResults.xml` を `start-time` で自分の結果か確認しつつ読む）

- [ ] **Step 3: 実機で前回異常終了の経路を1回通す**

1. `uloop control-play-mode --action play` で起動し、60秒プレイする。
2. Unity Editor の Play を **Stop ではなく** `uloop execute-dynamic-code` で `UnityEditor.EditorApplication.Exit(1)` を呼ぶか、Editor プロセスを `kill -9` して異常終了を作る（`GameShutdownEvent` を通さないことが条件）。
3. 再度 `uloop control-play-mode --action play` で起動し、タイトルで確認ゲートが出ることを目視する（`uloop screenshot` で1枚撮る）。説明文を入れて「送る」を押す。
4. `~/Library/Application Support/moorestech/BugReports/outbox/` の最新箱を `ls -R` し、`manifest.json` の `kind` が `crash`、`recording/` と `snapshots/` と `logs/Player-prev.log` が入っていることを確認する。
5. `~/Library/Application Support/moorestech/ProgressRecords/outbox/` の最新 `record.json` の `endReason` が `crash-recovered` であることを確認する。
6. 結果（ゲートの表示・箱の中身・`missing` の内容・クラッシュダンプが取れたか）を本plan末尾の判断記録へ転記する。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Playtest docs/superpowers/plans/2026-09-13-playtest-g-report-kind-crash-and-progress-record.md
git commit -m "test(client): 種別付き送信と進行記録と前回異常終了の通しテスト"
```

---

### Task 11: 必ず moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

- [ ] **Step 1:** `moores-code-review` を起動し、`feature/playtest-client-report` の全コミット（plan B 完了コミット以降）をレビューする。webui の変更は `webui-design` レンズ（§8.6 ボタン・§8.9 入力欄・ModeSwitch・トークン・z層）を含める。サーバー側の新プロトコルは `creating-server-protocol` の観点を含める。
- [ ] **Step 2:** 機械的指摘を反映し、`uloop compile`・`pnpm test`・`pnpm lint`・Task 1〜9 の関連テストを再実行する。反映が判定経路（`PlaytestReportKind` の値域判定、`CleanExitMarker.ConsumePreviousExitCleanFlag`、`PreviousSessionSalvage.Salvage` の分岐、`CrashReportGate.Respond`／`PlaytestConsentGate.Acknowledge` の1回きり判定、`ProgressSessionRecovery` の `endReason` 分岐、`ProgressRecordComposer.WithSynthesizedBuildModeCancel`）に触れたら、**Task 10 の EditModeInPlayingTest と Step 3 の実機確認を反映後のバイナリで再実施してから完了とする**（テスト通過・ログ無音は代替にならない）。
- [ ] **Step 3:** 設計判断が要る指摘だけを AskUserQuestion で裁定に出す。

---

## 配置と前例（spec-architecture-review）

| # | 項目（型/メンバー/ファイル） | 配置先アセンブリ・層 | 使用する機構 | 前例・根拠 |
|---|---|---|---|---|
| 1 | `PlaytestReportKind`・`BuildInfo`・`BuildInfoReader`・`IPlaytestSessionIdentity`・`PlaytestConsentFlag` | `Client.Game/InGame/BugReport/Playtest/`（5ファイル） | 定数・純関数・ファイルフラグ | plan B が `Client.Game/InGame/BugReport/` を10ファイル使い切っているためサブディレクトリへ。層は同じ「クライアントのローカル状態」（層マップ `Client.*`） |
| 2 | `manifest.kind`／`steamId`／`buildInfo` | 既存 `BugReportManifest` へフィールド追加 | Newtonsoft camelCase | バンドルの契約は plan B の manifest 1本（同一ドメイン1本の原則）。shared-contracts §2 が「plan B のファイル群＋追加フィールド」と定めている |
| 3 | 種別トグル | webui `features/pauseMenu/BugReportForm.tsx` に `ModeSwitch` | 共通 `ModeSwitch`（`@/shared/ui`） | `features/settings/LanguageSelect.tsx` が同じ択一UIを `ModeSwitch` で作っている。新規トグル部品は作らない |
| 4 | `CleanExitMarker`・`PreviousSessionSalvage`・`CrashBundleWriter` ほか | `Client.Game/InGame/BugReport/LastSession/`（7ファイル） | 同期ファイルIO | plan B の `BugReportOutbox`（配置規則を1箇所に閉じる）と同じ考え方。パス定義自体は `Game.Paths/GameSystemPaths.cs`（パス連結は `Game.Paths` 以外で行わない） |
| 5 | **正常終了マーカーの書き込み契機** | `CleanExitMarkWriter`（`Client.Game/.../LastSession`）が `GameShutdownEvent.OnGameShutdown` を**購読** | UniRx 購読 | 「汎用基盤にドメイン語彙を持ち込まない」。`GameShutdownEvent` は終了パイプラインの汎用機構であり、プレイテストの語彙（CLEAN_EXIT）を直接書き込まない。前例 `RemoteServerSaveFlushParticipant`（終了機構へは参加者/購読で足す） |
| 6 | **進行記録のイベント収集** | `ProgressRecorder` が `UIStateControl.OnStateChanged` と `SubscribeEventResponse` を**購読** | UniRx／既存イベントパケット購読 | 層マップ「`UIStateControl.OnStateChanged` 購読は**表示専用オブザーバ**に限る」。`ProgressRecorder` はステートの挙動に一切参加せず観測して書くだけの受動オブザーバなので購読側が正しい。前例 `UiStateTopic`・`BlockInventoryTopic`（同じく観測して配信するだけ）。**ステート駆動（`OnEnter` から明示呼び出し）は採らない** — 駆動にすると PauseMenu・PlaceBlock 等の各ステートへ記録の呼び出しが散り、記録の有無がステートの責務に混入する |
| 7 | **クラフトと報告送信の記録** | `CraftExecuteActionHandler`／`BugReportSubmitActionHandler` から `IPlaytestProgressSink` へ**プッシュ** | インターフェース越しの明示呼び出し | 「状態変化の検知は購読で。購読できないものは変化を起こす操作の直後にプッシュ」（moorestech-principles）。クラフトは送信のみで応答イベントが無く、購読できる変化通知が存在しないためプッシュが唯一の経路。`Update()` ポーリングは採らない |
| 8 | **タイトルのゲート2種** | `CrashReportGate`・`PlaytestConsentGate`（`Client.WebUiHost/Game/Playtest/`）＋ topic ＋ action ＋ `Client.Starter/Playtest/PlaytestStartGates` | `UniTaskCompletionSource` で開始を止め、topic の `waiting` で全画面オーバーレイ | ADR 0040 の `EventLanguageGate`／`EventLanguageGateTopic`／`EventLanguageGateBinder`／`EventModeStartGate` と**同型**（役割が同じ「開始ゲート」）。登録は待機有無に関わらず無条件（未登録によるWeb側購読の固着を避ける前例コメントも踏襲） |
| 9 | ゲートを差し込む位置 | `MainGameInitializationFinalizer.FinalizeAsync` の言語ゲート await の直後 | `await` | 同ファイル44行目の `EventModeStartGate.WaitForLanguageSelectionAsync()` と同じ場所。CEF は MainGameUI にしか無いためロード完了後でしか全画面ゲートを出せない（ADR 0040 の構造上の制約） |
| 10 | 前回記録の退避位置 | `InitializeScenePipeline.Initialize()` の `args` 解析直後 | 同期ファイル移動 | 内蔵サーバー（スナップショットリング）と `GameFrameRecorder` のどちらより前に走る唯一の点。`GameShutdownEvent.ResetForNewSession()` が置かれている「起動シーケンスの起点」と同じ場所 |
| 11 | ワールド作成日時・累計プレイ時間の取得 | `GetWorldPlaySessionInfoProtocol`（`Server.Protocol/PacketResponse/`）＋ `VanillaApiWithResponse` | Request-Response（`va:get*`） | 3点セットのうち②初期データのみ。セッション中に変化を追う必要がない読み取り1回なのでイベントパケットは作らない。前例 `GetPlayedSkitIdsProtocol`（同形の1回取得） |
| 12 | 進行記録のファイル層 | `Client.Game/InGame/Playtest/Progress/`（8ファイル） | 追記JSONL＋純関数の合成器 | 1ディレクトリ10ファイル規約。合成（集計・キャンセル合成）を純関数へ切り出すのは「複雑アルゴリズムは純関数に切り出して単体テストを持たせる」（`.decisions/2026-09-11-歯車の役割導出は純関数に切り出して単体テストを持たせる.md`） |
| 13 | `steamId` の供給元 | `IPlaytestSessionIdentity` を DI 登録し、既定は `EmptyPlaytestSessionIdentity` | DI 登録の差し替え | plan D が `PlaytestSession` で登録を差し替えるだけで切り替わる差込口。plan G 側に Steam の語彙・依存を持ち込まない |

**データフロー（Phase 1.5）**

- 種別: `webui ModeSwitch → bug_report.submit(kind) → BugReportSubmitActionHandler → BugReportBundleWriter → manifest.kind`。新規要素は既存パイプラインへの「フィールド1つの追加」だけで、分岐も逆流も足していない。
- 正常終了: `終了操作 → GameShutdownEvent.FireGameShutdownAsync → OnGameShutdown ──購読──> CleanExitMarkWriter → CLEAN_EXIT`／`同 → 参加者 ProgressRecorder.FlushOnShutdownAsync → record.json`。`GameShutdownEvent` 側のコードは1行も変えない（受動的統合）。
- 起動: `InitializeScenePipeline → PreviousSessionSalvage（マーカー読取→退避）→ …サーバー起動… → MainGame ロード → 言語ゲート → 同意ゲート → クラッシュ確認ゲート → StartGame → ProgressRecorder.Initialize（残骸回収→ヘッダ書き込み→購読開始）`。
- 進行記録: `(サーバーイベント / UIStateControl.OnStateChanged) ──購読──> ProgressRecorder → events.jsonl`、`(craft.execute / bug_report.submit) ──プッシュ──> IPlaytestProgressSink → events.jsonl`、`終了 → ProgressRecordComposer（純関数）→ ProgressRecords/outbox/<id>/record.json + READY`。

**機構選択（検査4）**

- 正常終了マーカー: 受動的統合案「`GameShutdownEvent.OnGameShutdown` を購読して書く」対 能動介入案「`GameShutdownEvent.QuitApplicationAsync` にマーカー書き込みを差し込む」。後者は汎用の終了パイプラインにプレイテストの語彙を持ち込み、かつ `FireGameShutdown()`（待てない経路）を通る終了で書かれない。**購読案を採る**。
- 録画リングの残骸: 受動的統合案「`GameFrameRecorder.Initialize` の削除を外し、掃除の所有者を `PreviousSessionSalvage` へ一本化」対 能動介入案「recorder に『前回分を残すモード』フラグを足す」。後者は録画部品にセッション跨ぎの概念を持ち込む。**所有者一本化を採る**（削除1行の除去と、起動時の1箇所での掃除）。
- 前回異常終了の確認UI: 受動的統合案「ADR 0040 の開始ゲートと同型の全画面オーバーレイをもう1枚足す」対 能動介入案「ポーズメニューへ『前回のクラッシュを送る』欄を常設する」。後者はタイトルで聞くというADR 0058 の裁定に反し、ポーズメニューの責務も膨らむ。**同型ゲートを採る**。

**死活表（Phase 2.5）**

| 現在の操作 | 計画後 | 根拠 |
|---|---|---|
| Escape → ポーズメニュー表示 | 生きる | 要素追加のみ |
| ポーズメニューの「セーブ」「セーブして終了」「言語」 | 生きる | 触らない |
| plan B のバグ報告送信（説明文＋送信） | 生きる | payload に `kind` が増えるだけ。既定 `bug` で従来と同じ箱になる |
| 録画リングの起動時リセット | **挙動が変わる（意図した変更）** | 前回分は削除でなく退避／正常終了時は削除。ディスク使用量は last-session の1世代ぶん増える |
| 出展モードの言語選択ゲート | 生きる | 同意・クラッシュ確認は言語ゲートの**後**に await するため、出展モードの「言語を選ぶまで待つ」順序は不変 |
| 出展モードの無操作180秒タイマー | 生きる（武装タイミングは不変） | 武装は言語ボタン押下時のまま。同意ゲートは配布版の初回のみ待機し、出展モードのキオスクでも1回で既読になる |
| 通常起動（同意済み・前回正常終了） | 生きる（待機ゼロ） | 両ゲートとも `startsWaiting=false` で即完了する |
| WebUI が起動しなかったときの起動 | 生きる | `PlaytestStartGates` は hub が null なら理由をログに出して素通りする（`EventModeStartGate` と同じ縮退） |

## 判断記録（ADR）

- 設計ADR: `docs/adr/0058-steam-closed-playtest-report-receiver-and-save-compat.md`（本planはそのクライアント側5裁定）、`docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md`（改訂3裁定以外は有効）、`docs/adr/0040-event-mode-language-select-gate.md`（ゲートの前例）
- 裁定: `.decisions/2026-09-13-感想もポーズメニューの報告UIで受け種別バグと感想を選ばせる.md`／`-クラッシュは次回起動時に前回の異常終了を検知し記録を送るか聞く.md`／`-プレイ状況はセッションサマリとイベント列を自動送信する.md`／`-報告の添付は外せず参加時の包括同意で一式送る.md`／`-進行記録の自動送信は参加条件とし断る選択肢を作らない.md`／`-テスター向け文言は日英独すべて必須とする.md`
- 共有契約: `scratchpad/plans/shared-contracts.md` §1・§2・§3（本plan Global Constraints へ逐語転記済み）
- **正常終了マーカーは終了パイプラインの発火時点で書く**（agent前提）: 参加者の書き出し完了を待つと、強制終了で待ちが切れたときに正常終了が異常終了として記録される。「正常終了の意図が表明されたか」を判定軸にすると、クラッシュ（パイプラインに入らない）とだけ確実に区別できる。副作用として、マーカー後に落ちると `current/` の進行記録だけが残るため、起動時の残骸回収を `endReason="quit"` で必ず行う（Task 8 のテストで固定）。
- **前回異常終了ゲートは退避物ゼロでも出す**（agent前提）: 説明文だけの箱にも価値があり、「退避物があるときだけ出す」にすると起動失敗（何も残らない）ケースで永遠に聞けなくなる。
- **「送らない」を選んだ退避物は消さず1世代だけ残す**（agent前提）: `PreviousSessionSalvage.MoveFilesInto` が退避先を毎回空にしてから移すため、`last-session/` に溜まるのは常に直近1回ぶんに限られる。積極的に消さないのは、送らない判断のあとで開発者が手で回収できる余地を残すため。増え続ける経路は無い。
- **クラッシュダンプは候補ディレクトリの探索で解決する**（agent前提）: Windows 実機が手元に無いため実パスを固定できない。`%LOCALAPPDATA%\Temp\<company>\<product>\Crashes` を第1候補、`%LOCALAPPDATA%\<company>\<product>\Crashes` を第2候補、macOS の `~/Library/Logs/DiagnosticReports` を第3候補として探索し、見つからなければ理由（探索先一覧つき）を `missing` に残す。**Task 10 Step 3 の Windows 検証（plan E の検証機）で実パスを確定し、この項に転記する**。
- **前回セッションのUnityログは `Player-prev.log`**（agent前提）: Unity は起動時に前回の `Player.log` を `Player-prev.log` へ回すため、`Application.consoleLogPath` の隣を見れば配布版・Editor の双方で同じ規則で解決できる。ハードコードされた OS 別パスは持たない。
- **進行記録は追記（`events.jsonl`）で持つ**（agent前提）: 終了時にまとめて書くとクラッシュで全部失われる。追記なら直前まで残り、`endReason="crash-recovered"` で回収できる。壊れた最終行は読み飛ばす（ADR 0058 の「イベント列を送る」を満たす最小の耐障害設計）。
- **`buildModeCancelled` はイベント列から合成する**（agent前提）: 「設置せずに建築モードを抜けた」は単一の通知として存在せず、`uiStateChanged` と `blockPlaced` の順序から導ける。記録側に判定を足すより純関数で後段合成する方がテストしやすく、`PlaceBlockState` に記録の語彙を持ち込まずに済む。
- **`worldCreatedAt`・`totalPlaySeconds` は新規プロトコルで取る**（agent前提）: 既存の初期ハンドシェイクにも既存イベントにも含まれておらず、他ドメインの応答から推測合成するのは層マップが禁じる「間接導出Applier」に当たる。1回読むだけで可変状態の同期ではないため、イベントパケットは作らず `va:get*` のみとする。
- **`steamId` は DI 差込口で受ける**（agent前提）: plan D（Steam 認証）の完成を待たずに plan G を出せるようにするため。既定実装は空文字を返し、plan D は `MainGameModelRegistration` の1行を差し替える。
- Task 10 Step 3 の実機確認結果（ゲート表示・箱の中身・`missing`・クラッシュダンプの実パス）: 2026-09-14 macOS（Mac mini / Unity Editor 6000.3.8f1・worktree `playtest-client-report`）で1回通した。手順は MainMenu 起動 → `LocalGameLauncher.StartLocalGame()`（常時記録が有効になる本番経路）→ 約100秒プレイ → `kill -9`（`GameShutdownEvent` を通さない）→ 再起動。
  - **ゲート表示**: 1回目起動で同意ゲート → 前回異常終了ゲートの順に出て、どちらも応答まで初期化が止まった（設計どおり）。2回目起動では同意ゲートは出ず（`consent-acknowledged-v1` 済み）、前回異常終了ゲートだけが `waiting:true` で出た。**ただし両ゲートとも文言が1文字も表示されない**（黒画面＋無地のボタンのみ。スクリーンショット `.superpowers/sdd/Rendering_20260914_052727_777.png`・`Rendering_20260914_052938_693.png`・`Game_20260914_053430_836.png`）。原因はゲートが `WebUiGameBinder.Bind()`（`localization.current` の配信元）より前に出るため `t()` が辞書不在の空文字を返すこと。WSで観測しても `localization.current` のsnapshotはゲート応答後に初めて届く。EventLanguageGate（ADR 0040）と同じく辞書非依存文言で描くのが前例なので、`DictionaryIndependentText` へのフォールバックに直し `playtestGateDictionaryFallback.test.ts` で固定した。
  - **箱の中身**: `BugReports/outbox/20260913_203442_b2fe1167/` に `manifest.json`・`READY`・`recording/pid_61195/live_0000/seg_00..06.mp4`（5.2MB）・`snapshots/tick_3600..6000.json`＋`packets_3601..6001.bin`（23MB）・`crashDumps/`。`manifest.kind = "crash"`、`steamId = ""`、`buildInfo = null`（Editor起動）、`platform = OSXEditor`、`description` はゲートに入れた説明文がそのまま入っていた。
  - **`missing`**: `playerLog`（「前回セッションのPlayer-prev.logが見つからない」）の1件のみ。Editor起動には `Player.log` が無いため `logs/Player-prev.log` は入らない（配布版でのみ入る項目で、欠損理由は正しく残っていた）。
  - **クラッシュダンプの実パス**: macOS の第3候補 `~/Library/Logs/DiagnosticReports` は実在し拾えたが、`kill -9` はクラッシュレポートを生成しないため**Unity自身のダンプは0件**。代わりに**無関係なnodeプロセスのクラッシュレポート98件（1.5MB）が箱へ同梱された** — 共有置き場を無差別に浚っていたため。プロセス名で絞る修正を入れ（`CrashDumpLocator.IsOwnProcessDumpName`・`CrashDumpLocatorTest`）、Windows の第1・第2候補は `<product>` 配下の専用置き場なので従来どおり無差別に拾う。**Windows実機での実パス確定は plan E の検証機で行う（本項は macOS ぶんの確定）**。
  - **進行記録**: `ProgressRecords/outbox/20260913_203443_38550465/record.json` の `endReason = "crash-recovered"`、`READY` あり、`headerMissing` なし、`worldCreatedAt`・`totalPlaySeconds`（255.8秒）も埋まっていた。`playSeconds` は18.0秒 = 最後のイベント時刻−セッション開始（クラッシュ時刻は不明なので設計どおり最終イベントへ潰れる）。
  - **副次的に見つかった破れ**: EditModeInPlayingTest は「前回異常終了」状態（PlayModeのStopは終了パイプラインを通らないので常にこうなる）で起動するとゲートが応答を待ち続け、初期化が終わらず `ClientDIContext.DIContainer` が null のまま NRE で落ちる。`EditModeInPlayingTestUtil.EnterPlayModeUtil()` で両ゲートの印を先に置いて回避した（この worktree に `moorestech_web/node` を入れて WebUiHost が実際に起動するようになるまで、hub==null で素通りしていたため露見していなかった）。あわせて Task 1 の種別必須化で赤のままだった `BugReportBundleWriteTest`（`invalid_kind`）も直した。

## Execution Handoff

planが完成し`docs/superpowers/plans/2026-09-13-playtest-g-report-kind-crash-and-progress-record.md`に保存されました。新規セッションを開き、以下を貼り付けて実装を開始してください:

```
subagent-driven-development スキルを使って、以下の実装planを実行してください。

- plan: docs/superpowers/plans/2026-09-13-playtest-g-report-kind-crash-and-progress-record.md
- 作業場所: feature/playtest-client-report（この環境では moores-wt new でタスク用worktreeを切ること）
- まずplan全文を読み、`## Requirements`・`## Global Constraints`・`## 判断記録（ADR）`を全タスク共通の制約として扱ってください
- **依存: plan A・plan B が未実装です。着手前に両planの完了コミットが作業ブランチの土台に入っていることを確認し、入っていなければ実装せずユーザーへ報告してください**
- 進捗管理はsubagent-driven-developmentスキルの規定に従ってください（SDD本体はplanのチェックボックス＋進捗台帳、単一subagent実装モードは報告ファイル＋進捗台帳が正）
- planの最終タスク（moores-code-reviewによる全ブランチレビュー）は省略不可です
```
