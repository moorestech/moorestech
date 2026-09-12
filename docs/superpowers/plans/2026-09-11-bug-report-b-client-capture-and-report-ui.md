# バグ報告 B: クライアント常時記録・確保セッション・バンドル・ポーズメニュー報告UI Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** Escapeでポーズメニューを開いた瞬間の記録（サーバー即時スナップショット・録画境界・ログ・クライアント状態）を確保し、説明文を書いて送信するとバンドルがoutboxへ書かれ、ゲームは止まらずトーストで完了を知る（ADR 0057 取得側クライアント部分）。

**Architecture:** (1) `GameFrameRecorder` が10fps・720pで描画結果を非同期読み出しし、子プロセスの `ffmpeg` へ流して直近120秒を分割ファイルでリング保持する（各フレームのサーバーtickを `frames.tsv` に併記）。(2) `UnityLogRing` が `Application.logMessageReceivedThreaded` を直近2000件保持する。(3) `BugReportCaptureSession` が `PauseMenuStateService.OnEnter` から駆動され、サーバーへ `va:bugReportCapture` を要求し、録画区間を確定し、ログとクライアント状態を写し取り、完了イベント（`BugReportCaptureEventHandler`、3点セット③）で「確保済み」になる。状態は `ReactiveProperty` で `PauseMenuTopic` に載せてWebUIへ配信する。(4) 送信は `bug_report.submit` アクション→`BugReportBundleWriter` が outbox `<GameSystemDirectory>/BugReports/outbox/<id>/` に manifest・スナップショット・パケットログ・動画・連番フレーム・ログ・スクリーンショット・未コミット差分を書き、`READY` マーカーで完了を示す。欠損は manifest の `missing` に理由付きで残す。(5) WebUIはポーズメニューに素の `<textarea>`（§8.9様式）と `PanelActionButton` を直置きし、`pause_menu.current` の `bugReport` 状態で「確保中／欠けている項目」を出す。

**Tech Stack:** Unity C#（`Client.Game`・`Client.WebUiHost`・`Client.Tests`）、VContainer、UniRx、UniTask、`AsyncGPUReadback`、`ffmpeg`（外部プロセス）、`git`（外部プロセス・Editor時のみ）、React 18＋Mantine（webui）、vitest、Localization CSV。

## Requirements

- R1. 録画リング: `GameFrameRecorder` が起動後に `ffmpeg` を子プロセスで起動し、`ScreenCapture.CaptureScreenshotIntoRenderTexture`→縮小Blit→`AsyncGPUReadback` で1280x720・10fpsのRGBAフレームをstdinへ流し、`<GameSystemDirectory>/BugReports/recording/seg_%02d.mp4`（10秒×12本、`-segment_wrap`）を保持する。`CutSegment()` で現在の区間を確定して新しい区間を始める。`ffmpeg` が見つからなければ起動せず、理由を `Debug.LogWarning` と `Status.Missing` に残す。受入: EditModeInPlayingTest で30秒後に `seg_*.mp4` が2本以上・0byteでない。
- R2. フレームとtickの対応: 各取り込みフレームの `unixMs` と `GameUpdater.CurrentTick`（同一プロセスのローカルサーバー）を `FrameTickLog` が直近1200件保持し、バンドルに `frames.tsv` として書く。受入: `frames.tsv` の行数が取り込みフレーム数と一致し、tickが単調非減少。
- R3. Unityログリング: `UnityLogRing` が `Application.logMessageReceivedThreaded` を直近2000件（時刻・tick・種別・本文・Error系はスタックトレース）保持し、`Dump()` で複製を返す。受入: 単体テストで2001件入れると先頭が消える。
- R4. 確保セッション: `BugReportCaptureSession.BeginOnPauseMenu()` が (a) `VanillaApi.Response.RequestBugReportCapture` を送り要求IDを保持、(b) `GameFrameRecorder.CutSegment()` を呼び現時点の区間ファイル一覧を保持、(c) `UnityLogRing.Dump()`、(d) `ClientStateSnapshot`（カメラ位置・向き、プレイヤー位置、`UIStateControl.CurrentState`、`GameUpdater.CurrentTick`）、(e) `ScreenCapture.CaptureScreenshot` を取り、`Status`（`CapturePending`・`Missing`）を `ReactiveProperty` で公開する。`BugReportCaptureEventHandler` が `va:event:bugReportCaptureCompleted` を購読し、要求IDが一致したら `SnapshotDirectory`・ファイル名一覧を保持して `CapturePending=false` にする。15秒経っても届かなければ `Missing` に「サーバースナップショット（タイムアウト）」を足す。次の `BeginOnPauseMenu()` で前回分は破棄する。受入: EditModeInPlayingTest で `Begin` 後2秒以内に `CapturePending` が false になる。
- R5. バンドル書き出し: `BugReportBundleWriter.WriteAsync(description)` が outbox ディレクトリ `<GameSystemDirectory>/BugReports/outbox/<yyyyMMdd_HHmmss>_<8桁hex>/` に、`snapshots/`（イベントで届いた `tick_*.json`・`packets_*.bin` をコピー）、`world/`（`world.json`・`map.json`）、`video.mp4`（区間結合）、`frames/frame_%04d.jpg`（2fps）、`frames.tsv`、`logs/unity.log`、`screenshot.png`、`repo/head.diff`・`repo/untracked/`・`repo/master.diff`・`repo/master-untracked/`、最後に `manifest.json` と空ファイル `READY` を書く。欠けた項目は `manifest.missing[]` に `{item, reason}` で残し、決して例外で止まらない。受入: EditModeInPlayingTest で `READY` と `manifest.json` が存在し、`manifest.snapshots` に少なくとも1件、`missing` にサーバースナップショットが含まれない。
- R6. manifest: `schemaVersion`(1)、`createdAt`(ISO)、`description`、`platform`、`isEditor`、`reportTick`、`snapshotTicks[]`、`snapshotFiles[]`、`packetLogFiles[]`、`repository{commit, dirty, branch}`、`masterData{commit, dirty}`、`clientState{cameraPosition, cameraRotation, playerPosition, uiState}`、`missing[]{item, reason}`、`videoSeconds`。受入: 単体テストで JSON にこれらのキーが出る。
- R7. リポジトリ状態: Editor実行時は `git`（外部プロセス）で `rev-parse HEAD`・`rev-parse --abbrev-ref HEAD`・`status --porcelain`・`diff HEAD`・`ls-files --others --exclude-standard` を本体repoと `../moorestech_master` で取り、差分と未追跡ファイル（合計20MB上限、超過はパス一覧のみ）をバンドルへ入れる。ビルド実行時は `StreamingAssets/build-info.json`（`BuildInfoWriter : IPreprocessBuildWithReport` が生成）を読む。受入: EditMode単体テストで本repoのHEADが取れ、`build-info.json` の生成関数が有効なJSONを書く。
- R8. WebUI: ポーズメニューに説明文 `<textarea data-testid="bug-report-description">`（§8.9様式）と `PanelActionButton`「送信」（`data-testid="bug-report-send"`）を直置き。`pause_menu.current` に `bugReport: { capturePending: boolean, missing: string[] }` を追加し、確保中は「記録を確保しています…」、欠損があれば「欠けている項目: …」を `--text-muted` で出す。送信は `dispatchAction("bug_report.submit", { description })`、成功時に `emitToast(t(L.ui.bugReport.sent), "info")`、失敗時は bridge の既存 notify（`dispatchAction` 内）に任せる。空文字は送信ボタンを押しても送らない（`data-disabled`）。受入: vitest で (a) textarea と送信ボタンが描かれる、(b) 入力後の送信で action が正しいpayloadで呼ばれる、(c) `missing` が1件以上なら一覧が描かれる、(d) `capturePending` で確保中文言が出る。
- R9. 送信後の挙動: `BugReportSubmitActionHandler` が書き出し完了後に `PauseMenuStateService.RequestClose()` を呼び、次のUI更新でポーズメニューが閉じる。受入: EditModeInPlayingTest で送信後 `UIStateControl.CurrentState == GameScreen`。
- R10. ローカライズ: `Localization/localization.csv` に `ui.bugReport.placeholder`・`ui.bugReport.send`・`ui.bugReport.sent`・`ui.bugReport.capturePending`・`ui.bugReport.missing` を追加し `pnpm gen:i18n` と C# 生成を通す。受入: `L.ui.bugReport.send` が TS/C# 両方で参照できる。
- やらないこと: 運搬（plan C）／Mac mini側／クラッシュ時の報告／プレイヤー配布版向けの受け口・匿名化／録画の音声／動画の見た目調整／ビルド実機での動作確認（初版はEditorまで）／`PauseMenuPanel` の Mantine `Button` 置換（既存負債、別タスク）。

## Global Constraints

- 作業ブランチ: `feature/bug-report-auto-fix`。plan A の完了コミットを土台にする（`BugReportCaptureProtocol`・`BugReportCaptureCompletedEventPacket`・`VanillaApiWithResponse.RequestBugReportCapture`・`WorldSnapshotRing`・`StartServerSettings.CaptureRing` が存在すること）。
- `.cs` を変更したら `uloop compile --project-path ./moorestech_client`。EditModeInPlayingTest は `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.EditModeInPlayingTest\.<クラス名>"`（Domain Reload 後45秒待ってリトライ）。webui は `cd moorestech_web/webui && pnpm test`、`pnpm lint`。
- webui は `.agents/skills/webui-design/SKILL.md` のホワイトリストに従う: Mantine `TextInput` 不使用、素の `<textarea>` に `--gauge-track` 面・`--text-muted` プレースホルダ・`:focus-visible` は ModeSwitch 踏襲、寸法は固定長トークン（%禁止）、色は `tokens.css` の変数のみ、`PanelActionButton` を副次アクションに使う、`z-index` 直書き禁止。vitest は `.test.ts` のみ（`.tsx` は include されない）。JSX は `createElement`＋`react-test-renderer`（前例 `features/settings/LanguageSelect.test.ts`）。
- WebUI⇄C#: 新アクションは `bridge/transport/actionContract.ts` の `ActionPayloads` と `ACTION_TYPES` の両方に追加（片方だけだとコンパイルエラー）。トピックのスキーマは `bridge/contract/schemas/ui.ts` の zod、型は `payloadTypes.ts` から導出。C# 側 DTO は `WebUiJson.Serialize`（camelCase）。
- 外部プロセス（`ffmpeg`・`git`）の起動は外部境界として `try-catch` 可。`EditorProcessRunner` の書き方（`SanitizedProcessEnvironment.Sanitize`、両ストリーム排水）を踏襲する。それ以外の try-catch 禁止。
- fail-closed 経路（ffmpeg不在・git不在・イベントタイムアウト・コピー失敗）は必ず `Debug.LogWarning/LogError` と manifest の `missing` に理由を残す。無音の縮退禁止。
- コメントは「// 日本語 → // English」2行セット。1ファイル200行以下、1ディレクトリ10ファイル以下。partial・`Func<>`・デフォルト引数・単純getter/setter禁止。イベントは UniRx。`Update()` ポーリング禁止（フレーム取り込みは物理進行相当の毎フレーム処理として `ITickable`/UniTask ループで行う）。
- 経過時間: クライアント側の録画レートやタイムアウトは `Time.unscaledTime`／`UniTask.Delay` を使う（サーバーロジックではない）。サーバーtickは `Core.Update.GameUpdater.CurrentTick` を読むだけ（書かない）。
- 型名・ファイル名は本plan記載のとおり（`GameFrameRecorder`・`FfmpegProcess`・`FfmpegLocator`・`FrameTickLog`・`VideoAssembler`・`UnityLogRing`・`BugReportCaptureSession`・`BugReportCaptureStatus`・`BugReportCaptureEventHandler`・`ClientStateSnapshot`・`BugReportBundleWriter`・`BugReportManifest`・`BugReportOutbox`・`RepositoryStateProbe`・`BuildInfoWriter`・`BugReportSubmitActionHandler`）。
- 各タスク末尾でコミット。コミットメッセージ末尾に以下を付ける:
  ```
  Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>
  Claude-Session: https://claude.ai/code/session_01SE4dG7rpvBJhbf1rsbiQXN
  ```

---

### Task 1: `UnityLogRing`・`BugReportOutbox`・`BugReportManifest`（純粋ロジック、EditMode単体テスト）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/UnityLogRing.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportOutbox.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportManifest.cs`
- Modify: `moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs`（`BugReportDirectory`・`BugReportOutboxDirectory`・`BugReportRecordingDirectory` を追加）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/UnityLogRingTest.cs`・`BugReportOutboxTest.cs`・`BugReportManifestTest.cs`

**Interfaces:**
- Consumes: `Core.Update.GameUpdater.CurrentTick`
- Produces:
  - `GameSystemPaths.BugReportDirectory`（`<GameSystemDirectory>/BugReports`）、`BugReportOutboxDirectory`（`.../outbox`）、`BugReportRecordingDirectory`（`.../recording`）
  - `public sealed class UnityLogRing : IInitializable { public const int Capacity = 2000; public void Initialize(); public IReadOnlyList<UnityLogEntry> Dump(); }`、`public readonly struct UnityLogEntry { public DateTime Time; public ulong Tick; public LogType Type; public string Message; public string StackTrace; }`（フィールドは readonly、ctor 1本）
  - `public static class BugReportOutbox { public static string CreateBundleDirectory(DateTime now, string shortId); public static string ReadyMarkerFileName = "READY"; public static void MarkReady(string bundleDirectory); }`
  - `public sealed class BugReportManifest { public int SchemaVersion = 1; public string CreatedAt; public string Description; public string Platform; public bool IsEditor; public ulong ReportTick; public List<ulong> SnapshotTicks; public List<string> SnapshotFiles; public List<string> PacketLogFiles; public RepositoryState Repository; public RepositoryState MasterData; public ClientStateSnapshot ClientState; public List<MissingItem> Missing; public double VideoSeconds; public string ToJson(); }`、`public sealed class RepositoryState { public string Commit; public string Branch; public bool Dirty; }`、`public sealed class MissingItem { public string Item; public string Reason; }`

- [ ] **Step 1: 失敗するテストを書く**

`Client.Tests/BugReport/UnityLogRingTest.cs`:
```csharp
using Client.Game.InGame.BugReport;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BugReport
{
    public class UnityLogRingTest
    {
        [Test]
        public void 上限を超えると先頭が消える()
        {
            var ring = new UnityLogRing();
            for (var i = 0; i < UnityLogRing.Capacity + 1; i++) ring.Add(LogType.Log, $"m{i}", "");
            var dump = ring.Dump();
            Assert.AreEqual(UnityLogRing.Capacity, dump.Count);
            Assert.AreEqual("m1", dump[0].Message);
            Assert.AreEqual($"m{UnityLogRing.Capacity}", dump[dump.Count - 1].Message);
        }

        [Test]
        public void エラー系だけスタックトレースを保持する()
        {
            var ring = new UnityLogRing();
            ring.Add(LogType.Log, "info", "trace-a");
            ring.Add(LogType.Error, "err", "trace-b");
            var dump = ring.Dump();
            Assert.AreEqual("", dump[0].StackTrace);
            Assert.AreEqual("trace-b", dump[1].StackTrace);
        }
    }
}
```

`Client.Tests/BugReport/BugReportOutboxTest.cs`:
```csharp
using System;
using System.IO;
using Client.Game.InGame.BugReport;
using Game.Paths;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class BugReportOutboxTest
    {
        [Test]
        public void バンドルディレクトリ名は時刻と短いIDで一意になりREADYを置ける()
        {
            var dir = BugReportOutbox.CreateBundleDirectory(new DateTime(2026, 9, 11, 21, 5, 7, DateTimeKind.Utc), "0123abcd");
            StringAssert.StartsWith(GameSystemPaths.BugReportOutboxDirectory, dir);
            StringAssert.EndsWith("20260911_210507_0123abcd", dir);
            Assert.IsTrue(Directory.Exists(dir));
            BugReportOutbox.MarkReady(dir);
            Assert.IsTrue(File.Exists(Path.Combine(dir, BugReportOutbox.ReadyMarkerFileName)));
            Directory.Delete(dir, true);
        }
    }
}
```

`Client.Tests/BugReport/BugReportManifestTest.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class BugReportManifestTest
    {
        [Test]
        public void 主要キーがcamelCaseで出力される()
        {
            var manifest = new BugReportManifest
            {
                CreatedAt = "2026-09-11T12:00:00Z",
                Description = "ベルトが止まる",
                Platform = "OSXEditor",
                IsEditor = true,
                ReportTick = 1234,
                SnapshotTicks = new List<ulong> { 600, 1200 },
                SnapshotFiles = new List<string> { "tick_600.json" },
                PacketLogFiles = new List<string> { "packets_601.bin" },
                Repository = new RepositoryState { Commit = "abc", Branch = "feature/x", Dirty = true },
                MasterData = new RepositoryState { Commit = "def", Branch = "HEAD", Dirty = false },
                ClientState = new ClientStateSnapshot(new UnityEngine.Vector3(1, 2, 3), new UnityEngine.Vector3(0, 90, 0), new UnityEngine.Vector3(4, 5, 6), "PauseMenu", 1234),
                Missing = new List<MissingItem> { new MissingItem { Item = "video", Reason = "ffmpeg not found" } },
                VideoSeconds = 0,
            };
            var json = JObject.Parse(manifest.ToJson());
            foreach (var key in new[] { "schemaVersion", "createdAt", "description", "platform", "isEditor", "reportTick", "snapshotTicks", "snapshotFiles", "packetLogFiles", "repository", "masterData", "clientState", "missing", "videoSeconds" })
            {
                Assert.IsTrue(json.ContainsKey(key), $"キー {key} が無い");
            }
            Assert.AreEqual("ffmpeg not found", (string)json["missing"][0]["reason"]);
        }
    }
}
```
（`ClientStateSnapshot` は Task 3 で定義する。本タスクではテストが参照する最小の型として同時に作る）

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー

- [ ] **Step 3: 実装する**

`GameSystemPaths.cs` の `WorldCacheDirectory` の直後に:
```csharp
        // バグ報告の常時記録とoutbox。ワールドとは独立に持つ
        // Always-on capture and outbox for bug reports; independent of any world
        public static string BugReportDirectory => Path.Combine(GameSystemDirectory, "BugReports");
        public static string BugReportOutboxDirectory => Path.Combine(BugReportDirectory, "outbox");
        public static string BugReportRecordingDirectory => Path.Combine(BugReportDirectory, "recording");
```

`UnityLogRing.cs`:
```csharp
using System;
using System.Collections.Generic;
using Core.Update;
using UnityEngine;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport
{
    public readonly struct UnityLogEntry
    {
        public readonly DateTime Time;
        public readonly ulong Tick;
        public readonly LogType Type;
        public readonly string Message;
        public readonly string StackTrace;

        public UnityLogEntry(DateTime time, ulong tick, LogType type, string message, string stackTrace)
        {
            Time = time;
            Tick = tick;
            Type = type;
            Message = message;
            StackTrace = stackTrace;
        }
    }

    // 直近のUnityログを保持する。どのスレッドのログも受けるためロックで守る
    // Keeps the most recent Unity logs; locked because logs arrive from any thread
    public sealed class UnityLogRing : IInitializable
    {
        public const int Capacity = 2000;

        private readonly object _lock = new();
        private readonly Queue<UnityLogEntry> _entries = new(Capacity + 1);

        public void Initialize()
        {
            Application.logMessageReceivedThreaded += OnLogReceived;
        }

        public void Add(LogType type, string message, string stackTrace)
        {
            var isError = type == LogType.Error || type == LogType.Exception || type == LogType.Assert;
            var entry = new UnityLogEntry(DateTime.UtcNow, GameUpdater.CurrentTick, type, message, isError ? stackTrace : "");
            lock (_lock)
            {
                _entries.Enqueue(entry);
                if (_entries.Count > Capacity) _entries.Dequeue();
            }
        }

        public IReadOnlyList<UnityLogEntry> Dump()
        {
            lock (_lock)
            {
                return new List<UnityLogEntry>(_entries);
            }
        }

        private void OnLogReceived(string condition, string stackTrace, LogType type)
        {
            Add(type, condition, stackTrace);
        }
    }
}
```

`BugReportOutbox.cs`:
```csharp
using System;
using System.IO;
using Game.Paths;

namespace Client.Game.InGame.BugReport
{
    // outbox の配置規則はここだけが持つ。READY は「manifest まで書き終えた」合図で、運搬側はこれが無い箱を触らない
    // Owns the outbox layout; READY signals the manifest is written, and the shipper ignores boxes without it
    public static class BugReportOutbox
    {
        public const string ReadyMarkerFileName = "READY";

        public static string CreateBundleDirectory(DateTime now, string shortId)
        {
            var directory = Path.Combine(GameSystemPaths.BugReportOutboxDirectory, $"{now:yyyyMMdd_HHmmss}_{shortId}");
            Directory.CreateDirectory(directory);
            return directory;
        }

        public static void MarkReady(string bundleDirectory)
        {
            File.WriteAllText(Path.Combine(bundleDirectory, ReadyMarkerFileName), "");
        }
    }
}
```

`BugReportManifest.cs`:
```csharp
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace Client.Game.InGame.BugReport
{
    // バンドルの唯一の契約。取得側と再現側はこの形だけで会話する（ADR 0057）
    // The bundle's single contract; capture and reproduction sides talk only through this shape (ADR 0057)
    public sealed class BugReportManifest
    {
        private static readonly JsonSerializerSettings Settings = new()
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
            Formatting = Formatting.Indented,
        };

        public int SchemaVersion = 1;
        public string CreatedAt;
        public string Description;
        public string Platform;
        public bool IsEditor;
        public ulong ReportTick;
        public List<ulong> SnapshotTicks = new();
        public List<string> SnapshotFiles = new();
        public List<string> PacketLogFiles = new();
        public RepositoryState Repository;
        public RepositoryState MasterData;
        public ClientStateSnapshot ClientState;
        public List<MissingItem> Missing = new();
        public double VideoSeconds;

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Settings);
        }
    }

    public sealed class RepositoryState
    {
        public string Commit;
        public string Branch;
        public bool Dirty;
    }

    public sealed class MissingItem
    {
        public string Item;
        public string Reason;
    }
}
```

`ClientStateSnapshot.cs`（Task 3 で使う。ここで作る）:
```csharp
using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    // Escape時点のクライアント側の状態。サーバー側には無い情報だけを持つ
    // Client-side state at the Escape moment; holds only what the server does not know
    public sealed class ClientStateSnapshot
    {
        public Vector3 CameraPosition { get; }
        public Vector3 CameraEulerAngles { get; }
        public Vector3 PlayerPosition { get; }
        public string UiState { get; }
        public ulong Tick { get; }

        public ClientStateSnapshot(Vector3 cameraPosition, Vector3 cameraEulerAngles, Vector3 playerPosition, string uiState, ulong tick)
        {
            CameraPosition = cameraPosition;
            CameraEulerAngles = cameraEulerAngles;
            PlayerPosition = playerPosition;
            UiState = uiState;
            Tick = tick;
        }
    }
}
```

- [ ] **Step 4: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.BugReport\.(UnityLogRingTest|BugReportOutboxTest|BugReportManifestTest)$"`
Expected: 全PASS

- [ ] **Step 5: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport moorestech_client/Assets/Scripts/Client.Tests/BugReport moorestech_server/Assets/Scripts/Game.Paths/GameSystemPaths.cs
git commit -m "feat(client): バグ報告のログリング・outbox・manifest"
```

---

### Task 2: 録画リング（`FfmpegLocator`・`FfmpegProcess`・`FrameTickLog`・`GameFrameRecorder`・`VideoAssembler`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Recording/FfmpegLocator.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Recording/FfmpegProcess.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Recording/FrameTickLog.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Recording/GameFrameRecorder.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Recording/VideoAssembler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（`builder.RegisterEntryPoint<UnityLogRing>().AsSelf(); builder.RegisterEntryPoint<GameFrameRecorder>().AsSelf();` を `GameSaveRequester` 登録の直後に追加）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/FfmpegLocatorTest.cs`・`FrameTickLogTest.cs`・`VideoAssemblerTest.cs`

**Interfaces:**
- Consumes: Task 1 `GameSystemPaths.BugReportRecordingDirectory`、`Client.Common.CameraManager.MainCamera`
- Produces:
  - `public static class FfmpegLocator { public static string Find(); }`（見つからなければ `null`。探索順: 環境変数 `MOORESTECH_FFMPEG` → PATH の各ディレクトリ → `/opt/homebrew/bin/ffmpeg` → `/usr/local/bin/ffmpeg`）
  - `public sealed class FfmpegProcess { public static FfmpegProcess StartSegmentRecorder(string ffmpegPath, string outputDirectory, int width, int height, int fps, bool flipVertically); public void WriteFrame(byte[] rgba); public void Stop(); public bool IsRunning { get; } public static int RunAndWait(string ffmpegPath, string arguments, string workingDirectory); }`
  - `public sealed class FrameTickLog { public const int Capacity = 1200; public void Add(long unixMs, ulong tick); public IReadOnlyList<(long unixMs, ulong tick)> Dump(); public static string ToTsv(IReadOnlyList<(long unixMs, ulong tick)> rows); }`
  - `public sealed class GameFrameRecorder : IInitializable, ITickable { public const int Width = 1280; public const int Height = 720; public const int Fps = 10; public const int SegmentSeconds = 10; public const int SegmentCount = 12; public bool IsRecording { get; } public string UnavailableReason { get; } public void CutSegment(); public IReadOnlyList<string> CompletedSegmentFilesInOrder(); public FrameTickLog TickLog { get; } public void Stop(); }`
  - `public static class VideoAssembler { public static bool Concat(string ffmpegPath, IReadOnlyList<string> segmentFiles, string outputMp4); public static bool ExtractFrames(string ffmpegPath, string inputMp4, string outputDirectory, int fps); public static double DurationSeconds(IReadOnlyList<string> segmentFiles); }`

- [ ] **Step 1: 失敗するテストを書く**

`FfmpegLocatorTest.cs`:
```csharp
using System.IO;
using Client.Game.InGame.BugReport.Recording;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class FfmpegLocatorTest
    {
        [Test]
        public void この開発機ではffmpegが見つかる()
        {
            var path = FfmpegLocator.Find();
            Assert.IsNotNull(path, "ffmpeg が見つからない（brew install ffmpeg）");
            Assert.IsTrue(File.Exists(path));
        }
    }
}
```

`FrameTickLogTest.cs`:
```csharp
using Client.Game.InGame.BugReport.Recording;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class FrameTickLogTest
    {
        [Test]
        public void 上限を超えると先頭が消えTSVは2列()
        {
            var log = new FrameTickLog();
            for (var i = 0; i < FrameTickLog.Capacity + 5; i++) log.Add(1000 + i, (ulong)(i / 2));
            var rows = log.Dump();
            Assert.AreEqual(FrameTickLog.Capacity, rows.Count);
            Assert.AreEqual(1005, rows[0].unixMs);
            var tsv = FrameTickLog.ToTsv(rows);
            StringAssert.StartsWith("unixMs\ttick\n1005\t2\n", tsv);
        }
    }
}
```

`VideoAssemblerTest.cs`:
```csharp
using System;
using System.IO;
using Client.Game.InGame.BugReport.Recording;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class VideoAssemblerTest
    {
        [Test]
        public void 生フレームから作った区間を結合しフレームを抜ける()
        {
            var ffmpeg = FfmpegLocator.Find();
            Assert.IsNotNull(ffmpeg, "ffmpeg が見つからない");
            var dir = Path.Combine(Path.GetTempPath(), $"moorestech-video-{Guid.NewGuid():N}");
            Directory.CreateDirectory(dir);

            // 2秒ぶんの単色フレームを流して区間ファイルを作る
            // Feed two seconds of solid frames to produce segment files
            var recorder = FfmpegProcess.StartSegmentRecorder(ffmpeg, dir, 64, 36, 10, false);
            var frame = new byte[64 * 36 * 4];
            for (var i = 0; i < frame.Length; i += 4) { frame[i] = 200; frame[i + 3] = 255; }
            for (var i = 0; i < 20; i++) recorder.WriteFrame(frame);
            recorder.Stop();

            var segments = Directory.GetFiles(dir, "seg_*.mp4");
            Assert.GreaterOrEqual(segments.Length, 1);
            var output = Path.Combine(dir, "video.mp4");
            Assert.IsTrue(VideoAssembler.Concat(ffmpeg, segments, output));
            Assert.Greater(new FileInfo(output).Length, 0);

            var frames = Path.Combine(dir, "frames");
            Assert.IsTrue(VideoAssembler.ExtractFrames(ffmpeg, output, frames, 2));
            Assert.GreaterOrEqual(Directory.GetFiles(frames, "frame_*.jpg").Length, 3);
            Directory.Delete(dir, true);
        }
    }
}
```

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー

- [ ] **Step 3: `FfmpegLocator`・`FrameTickLog` を実装する**

`FfmpegLocator.cs`:
```csharp
using System;
using System.IO;

namespace Client.Game.InGame.BugReport.Recording
{
    public static class FfmpegLocator
    {
        private static readonly string[] KnownPaths = { "/opt/homebrew/bin/ffmpeg", "/usr/local/bin/ffmpeg" };

        // 環境変数→PATH→既知の場所の順に探す。無ければ null（呼び出し側が縮退を記録する）
        // Search env var, then PATH, then known locations; null if absent (the caller records the degradation)
        public static string Find()
        {
            var overridePath = Environment.GetEnvironmentVariable("MOORESTECH_FFMPEG");
            if (!string.IsNullOrEmpty(overridePath) && File.Exists(overridePath)) return overridePath;

            var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? "";
            foreach (var directory in pathVariable.Split(Path.PathSeparator))
            {
                var candidate = Path.Combine(directory, "ffmpeg");
                if (File.Exists(candidate)) return candidate;
            }
            foreach (var candidate in KnownPaths)
            {
                if (File.Exists(candidate)) return candidate;
            }
            return null;
        }
    }
}
```

`FrameTickLog.cs`:
```csharp
using System.Collections.Generic;
using System.Text;

namespace Client.Game.InGame.BugReport.Recording
{
    // 取り込んだフレームの時刻とサーバーtickの対応。動画のフレームをパケットログと同じ軸で読むために使う
    // Maps captured frames' clock time to the server tick so video frames can be read on the packet log's axis
    public sealed class FrameTickLog
    {
        public const int Capacity = 1200;

        private readonly object _lock = new();
        private readonly Queue<(long unixMs, ulong tick)> _rows = new(Capacity + 1);

        public void Add(long unixMs, ulong tick)
        {
            lock (_lock)
            {
                _rows.Enqueue((unixMs, tick));
                if (_rows.Count > Capacity) _rows.Dequeue();
            }
        }

        public IReadOnlyList<(long unixMs, ulong tick)> Dump()
        {
            lock (_lock)
            {
                return new List<(long unixMs, ulong tick)>(_rows);
            }
        }

        public static string ToTsv(IReadOnlyList<(long unixMs, ulong tick)> rows)
        {
            var builder = new StringBuilder("unixMs\ttick\n");
            foreach (var (unixMs, tick) in rows) builder.Append(unixMs).Append('\t').Append(tick).Append('\n');
            return builder.ToString();
        }
    }
}
```

- [ ] **Step 4: `FfmpegProcess` を実装する（外部境界）**

```csharp
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Debug = UnityEngine.Debug;

namespace Client.Game.InGame.BugReport.Recording
{
    // ffmpeg 子プロセス。フレームは書き込みスレッドが stdin へ流す（メインスレッドをパイプで塞がない）
    // The ffmpeg child process; a writer thread streams frames into stdin so the main thread never blocks on the pipe
    public sealed class FfmpegProcess
    {
        private readonly Process _process;
        private readonly BlockingCollection<byte[]> _frames = new(boundedCapacity: 30);
        private readonly Thread _writer;

        public bool IsRunning => !_process.HasExited;

        private FfmpegProcess(Process process)
        {
            _process = process;
            _writer = new Thread(WriteLoop) { Name = "[moorestech] ffmpeg書き込みスレッド", IsBackground = true };
            _writer.Start();
        }

        public static FfmpegProcess StartSegmentRecorder(string ffmpegPath, string outputDirectory, int width, int height, int fps, bool flipVertically)
        {
            Directory.CreateDirectory(outputDirectory);
            var pattern = Path.Combine(outputDirectory, "seg_%02d.mp4");
            var flip = flipVertically ? "-vf vflip " : "";
            var arguments =
                $"-hide_banner -loglevel error -y -f rawvideo -pix_fmt rgba -s {width}x{height} -r {fps} -i - {flip}" +
                $"-c:v libx264 -preset ultrafast -pix_fmt yuv420p -g {fps} -f segment -segment_time {GameFrameRecorder.SegmentSeconds} " +
                $"-segment_wrap {GameFrameRecorder.SegmentCount} -reset_timestamps 1 \"{pattern}\"";
            var process = Start(ffmpegPath, arguments, outputDirectory, redirectStdin: true);
            return process == null ? null : new FfmpegProcess(process);
        }

        public static int RunAndWait(string ffmpegPath, string arguments, string workingDirectory)
        {
            var process = Start(ffmpegPath, arguments, workingDirectory, redirectStdin: false);
            if (process == null) return -1;
            process.WaitForExit();
            return process.ExitCode;
        }

        public void WriteFrame(byte[] rgba)
        {
            // 書き込みが追いつかないときは古いフレームを捨てる（録画は落ちてもゲームは止めない）
            // Drop frames when the writer lags; recording may skip but the game never stalls
            if (!_frames.TryAdd(rgba)) Debug.LogWarning("録画フレームを破棄しました（ffmpeg書き込みが追いついていない）");
        }

        public void Stop()
        {
            _frames.CompleteAdding();
            _writer.Join(5000);
            if (!_process.HasExited) _process.WaitForExit(5000);
        }

        private void WriteLoop()
        {
            var stdin = _process.StandardInput.BaseStream;
            foreach (var frame in _frames.GetConsumingEnumerable())
            {
                if (_process.HasExited) break;
                stdin.Write(frame, 0, frame.Length);
            }
            stdin.Flush();
            stdin.Close();
        }

        private static Process Start(string ffmpegPath, string arguments, string workingDirectory, bool redirectStdin)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardInput = redirectStdin,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // 外部プロセス起動は例外を返す境界のため、ここに限りcatchして null へ変換する
            // Process spawning is an external boundary; only here we catch and convert failures into null
            Process process;
            try
            {
                process = Process.Start(startInfo);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[FfmpegProcess] failed to start '{ffmpegPath} {arguments}': {exception.GetBaseException().Message}");
                return null;
            }
            if (process == null)
            {
                Debug.LogError($"[FfmpegProcess] no process was created for '{ffmpegPath}'");
                return null;
            }

            // 両ストリームを排水する（読まないとパイプ64KB超で子がwriteブロックしハングする）
            // Drain both streams; otherwise the child blocks on write once the pipe exceeds 64KB
            process.OutputDataReceived += (_, _) => { };
            process.ErrorDataReceived += (_, e) => { if (!string.IsNullOrEmpty(e.Data)) Debug.LogWarning($"[ffmpeg] {e.Data}"); };
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            return process;
        }
    }
}
```
（`SanitizedProcessEnvironment` は `Client.WebUiHost` にあり `Client.Game` から参照できない。同ファイルの `Sanitize` は不正UTF-8のenvを落とす処理なので、`ffmpeg` 起動では省略し、env汚染で起動失敗した場合は `LogError` に出る）

- [ ] **Step 5: `GameFrameRecorder` を実装する**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Client.Common;
using Core.Update;
using Game.Paths;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport.Recording
{
    // 描画結果を10fpsで読み出し ffmpeg へ流す。区間は10秒×12本のリングで直近2分を保持する
    // Reads the rendered frame at 10fps and streams it to ffmpeg; a ring of 12×10s segments keeps the last two minutes
    public sealed class GameFrameRecorder : IInitializable, ITickable
    {
        public const int Width = 1280;
        public const int Height = 720;
        public const int Fps = 10;
        public const int SegmentSeconds = 10;
        public const int SegmentCount = 12;

        private readonly string _directory = GameSystemPaths.BugReportRecordingDirectory;
        private string _ffmpegPath;
        private FfmpegProcess _ffmpeg;
        private RenderTexture _screenTexture;
        private RenderTexture _scaledTexture;
        private float _nextCaptureTime;
        private bool _readbackInFlight;

        public FrameTickLog TickLog { get; } = new();
        public bool IsRecording => _ffmpeg != null && _ffmpeg.IsRunning;
        public string UnavailableReason { get; private set; } = "";

        public void Initialize()
        {
            _ffmpegPath = FfmpegLocator.Find();
            if (_ffmpegPath == null)
            {
                UnavailableReason = "ffmpeg が見つかりません（MOORESTECH_FFMPEG か PATH で指定）";
                Debug.LogWarning($"録画リングを開始しません: {UnavailableReason}");
                return;
            }
            if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
            _scaledTexture = new RenderTexture(Width, Height, 0, RenderTextureFormat.ARGB32);
            StartProcess();
        }

        public void Tick()
        {
            if (!IsRecording || _readbackInFlight || Time.unscaledTime < _nextCaptureTime) return;
            _nextCaptureTime = Time.unscaledTime + 1f / Fps;

            EnsureScreenTexture();
            ScreenCapture.CaptureScreenshotIntoRenderTexture(_screenTexture);
            Graphics.Blit(_screenTexture, _scaledTexture);
            _readbackInFlight = true;
            var tick = GameUpdater.CurrentTick;
            var unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            AsyncGPUReadback.Request(_scaledTexture, 0, TextureFormat.RGBA32, request => OnReadback(request, unixMs, tick));
        }

        // 現在の区間を確定して新しい区間から録り直す。確保時点より後のフレームを混ぜないため
        // Finalize the current segment and restart on a fresh one so frames after the capture moment stay out
        public void CutSegment()
        {
            if (!IsRecording) return;
            _ffmpeg.Stop();
            StartProcess();
        }

        // 更新時刻順（古い→新しい）。書き込み中の最新区間は含めない
        // Ordered oldest to newest by write time; excludes the segment currently being written
        public IReadOnlyList<string> CompletedSegmentFilesInOrder()
        {
            if (!Directory.Exists(_directory)) return Array.Empty<string>();
            return Directory.GetFiles(_directory, "seg_*.mp4").Select(p => new FileInfo(p)).Where(f => f.Length > 0)
                .OrderBy(f => f.LastWriteTimeUtc).Select(f => f.FullName).ToList();
        }

        public void Stop()
        {
            _ffmpeg?.Stop();
            _ffmpeg = null;
        }

        private void StartProcess()
        {
            // Metal/D3D は読み出し行が上から、OpenGL系は下からなので後者だけ反転する
            // Metal/D3D read back rows top-down while OpenGL-style APIs read bottom-up, so flip only the latter
            var flip = !SystemInfo.graphicsUVStartsAtTop;
            _ffmpeg = FfmpegProcess.StartSegmentRecorder(_ffmpegPath, _directory, Width, Height, Fps, flip);
            if (_ffmpeg == null) UnavailableReason = "ffmpeg の起動に失敗しました（ログ参照）";
        }

        private void EnsureScreenTexture()
        {
            if (_screenTexture != null && _screenTexture.width == Screen.width && _screenTexture.height == Screen.height) return;
            if (_screenTexture != null) _screenTexture.Release();
            _screenTexture = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        }

        private void OnReadback(AsyncGPUReadbackRequest request, long unixMs, ulong tick)
        {
            _readbackInFlight = false;
            if (request.hasError)
            {
                Debug.LogWarning("録画フレームのGPU読み出しに失敗しました");
                return;
            }
            if (!IsRecording) return;
            var data = request.GetData<byte>();
            var frame = new byte[data.Length];
            data.CopyTo(frame);
            _ffmpeg.WriteFrame(frame);
            TickLog.Add(unixMs, tick);
        }
    }
}
```

- [ ] **Step 6: `VideoAssembler` を実装する**

```csharp
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Client.Game.InGame.BugReport.Recording
{
    // 区間ファイルの結合と静止画抜き出し。どちらも ffmpeg を同期実行する（送信時のみ）
    // Concatenates segments and extracts stills, each by running ffmpeg synchronously (only when sending)
    public static class VideoAssembler
    {
        public static bool Concat(string ffmpegPath, IReadOnlyList<string> segmentFiles, string outputMp4)
        {
            if (segmentFiles.Count == 0) return false;
            var listPath = outputMp4 + ".list.txt";
            var list = new StringBuilder();
            foreach (var file in segmentFiles) list.Append("file '").Append(file.Replace("'", "'\\''")).Append("'\n");
            File.WriteAllText(listPath, list.ToString());
            var exit = FfmpegProcess.RunAndWait(ffmpegPath, $"-hide_banner -loglevel error -y -f concat -safe 0 -i \"{listPath}\" -c copy \"{outputMp4}\"", Path.GetDirectoryName(outputMp4));
            File.Delete(listPath);
            return exit == 0 && File.Exists(outputMp4);
        }

        public static bool ExtractFrames(string ffmpegPath, string inputMp4, string outputDirectory, int fps)
        {
            Directory.CreateDirectory(outputDirectory);
            var pattern = Path.Combine(outputDirectory, "frame_%04d.jpg");
            var exit = FfmpegProcess.RunAndWait(ffmpegPath, $"-hide_banner -loglevel error -y -i \"{inputMp4}\" -vf fps={fps} -q:v 4 \"{pattern}\"", outputDirectory);
            return exit == 0;
        }

        public static double DurationSeconds(IReadOnlyList<string> segmentFiles)
        {
            return segmentFiles.Count * (double)GameFrameRecorder.SegmentSeconds;
        }
    }
}
```

- [ ] **Step 7: DI登録**

`MainGameModelRegistration.cs` の `builder.Register<GameSaveRequester>(Lifetime.Singleton);` の直後:
```csharp
            // バグ報告の常時記録（ログリング・録画リング）
            // Always-on capture for bug reports (log ring, frame recording ring)
            builder.RegisterEntryPoint<UnityLogRing>().AsSelf();
            builder.RegisterEntryPoint<GameFrameRecorder>().AsSelf();
```
（`using Client.Game.InGame.BugReport; using Client.Game.InGame.BugReport.Recording;`）

- [ ] **Step 8: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.BugReport\.(FfmpegLocatorTest|FrameTickLogTest|VideoAssemblerTest)$"`
Expected: 全PASS

- [ ] **Step 9: 向きの実機確認**

`uloop control-play-mode --action play` で通常起動し60秒待ってから `~/Library/Application Support/moorestech/BugReports/recording/seg_00.mp4` を `ffmpeg -i seg_00.mp4 -frames:v 1 check.png` で1枚抜き、`Read` ツールで画像を見て上下が正しいことを確認する。逆なら `StartProcess` の `flip` 条件を反転してこのステップを再実行し、結果を判断記録に書く。

- [ ] **Step 10: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport moorestech_client/Assets/Scripts/Client.Tests/BugReport moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs
git commit -m "feat(client): 描画結果をffmpegへ流す録画リングとフレームtick対応"
```

---

### Task 3: 確保セッション（`BugReportCaptureSession`・`BugReportCaptureStatus`・`BugReportCaptureEventHandler`）と `PauseMenuStateService` からの駆動

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportCaptureStatus.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportCaptureSession.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportCaptureEventHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PauseMenu/PauseMenuStateService.cs`（ctor で `BugReportCaptureSession` を受け、`OnEnter` で `BeginOnPauseMenu()`。`RequestClose()` を追加し `IsClosePause()` が消費）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（`builder.Register<BugReportCaptureSession>(Lifetime.Singleton); builder.RegisterEntryPoint<BugReportCaptureEventHandler>();`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/BugReportCaptureSessionTest.cs`

**Interfaces:**
- Consumes: plan A `VanillaApiWithResponse.RequestBugReportCapture`／`BugReportCaptureCompletedEventPacket`、Task 1 `UnityLogRing`・`ClientStateSnapshot`、Task 2 `GameFrameRecorder`、`CameraManager.MainCamera`、`PlayerObjectController.Position`、`UIStateControl.CurrentState`
- Produces:
  - `public sealed class BugReportCaptureStatus { public bool HasSession { get; } public bool CapturePending { get; } public IReadOnlyList<string> Missing { get; } }`
  - `public sealed class BugReportCaptureSession { public IReadOnlyReactiveProperty<BugReportCaptureStatus> Status { get; } public const float ServerCaptureTimeoutSeconds = 15f; public void BeginOnPauseMenu(); public void OnServerCaptureCompleted(long captureId, ulong tick, string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames); public BugReportCapturedData TakeCapturedData(); }`
  - `public sealed class BugReportCapturedData { public long CaptureId; public ulong ReportTick; public string SnapshotDirectory; public List<string> SnapshotFileNames; public List<string> PacketLogFileNames; public List<string> VideoSegmentFiles; public IReadOnlyList<(long unixMs, ulong tick)> FrameTicks; public IReadOnlyList<UnityLogEntry> Logs; public ClientStateSnapshot ClientState; public string ScreenshotPath; public List<MissingItem> Missing; }`（`BugReportCaptureStatus.cs` に同居）
  - `PauseMenuStateService.RequestClose()`

- [ ] **Step 1: 失敗するテストを書く（サーバー依存を切った状態遷移）**

セッションはサーバー要求・録画・カメラ等の外部依存を `IBugReportCaptureSources` 経由で受け、テストはフェイクで差し替える:

```csharp
// BugReportCaptureSession が依存する取得元。実装は BugReportCaptureSources（ゲーム内）とテストのフェイク
// Sources the session captures from; implemented by BugReportCaptureSources in-game and by a fake in tests
public interface IBugReportCaptureSources
{
    UniTask<long> RequestServerCapture();               // 要求ID。失敗時は 0
    void CutRecordingSegment();
    IReadOnlyList<string> CompletedVideoSegments();
    string RecordingUnavailableReason();               // 録画中なら ""
    IReadOnlyList<(long unixMs, ulong tick)> FrameTicks();
    IReadOnlyList<UnityLogEntry> Logs();
    ClientStateSnapshot ClientState();
    UniTask<string> CaptureScreenshot();                // パス。失敗時は null
}
```

`Client.Tests/BugReport/BugReportCaptureSessionTest.cs`:
```csharp
using System.Collections.Generic;
using Client.Game.InGame.BugReport;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.BugReport
{
    public class BugReportCaptureSessionTest
    {
        private sealed class FakeSources : IBugReportCaptureSources
        {
            public long CaptureIdToReturn = 7;
            public int CutCount;
            public string Unavailable = "";
            public UniTask<long> RequestServerCapture() => UniTask.FromResult(CaptureIdToReturn);
            public void CutRecordingSegment() => CutCount++;
            public IReadOnlyList<string> CompletedVideoSegments() => new List<string> { "/tmp/seg_00.mp4" };
            public string RecordingUnavailableReason() => Unavailable;
            public IReadOnlyList<(long unixMs, ulong tick)> FrameTicks() => new List<(long, ulong)> { (1, 2) };
            public IReadOnlyList<UnityLogEntry> Logs() => new List<UnityLogEntry>();
            public ClientStateSnapshot ClientState() => new(Vector3.zero, Vector3.zero, Vector3.zero, "PauseMenu", 2);
            public UniTask<string> CaptureScreenshot() => UniTask.FromResult("/tmp/shot.png");
        }

        [Test]
        public void 開始で確保中になり完了イベントで解除される()
        {
            var sources = new FakeSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            Assert.IsTrue(session.Status.Value.HasSession);
            Assert.IsTrue(session.Status.Value.CapturePending);
            Assert.AreEqual(1, sources.CutCount);

            session.OnServerCaptureCompleted(7, 2, "/w/snapshots", new List<string> { "tick_2.json" }, new List<string> { "packets_3.bin" });
            Assert.IsFalse(session.Status.Value.CapturePending);
            var data = session.TakeCapturedData();
            Assert.AreEqual(2UL, data.ReportTick);
            CollectionAssert.AreEqual(new[] { "tick_2.json" }, data.SnapshotFileNames);
            Assert.AreEqual(0, data.Missing.Count);
        }

        [Test]
        public void 別の要求IDの完了は無視する()
        {
            var sources = new FakeSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(99, 5, "/w/snapshots", new List<string>(), new List<string>());
            Assert.IsTrue(session.Status.Value.CapturePending);
        }

        [Test]
        public void サーバー要求が失敗すると欠損に載る()
        {
            var sources = new FakeSources { CaptureIdToReturn = 0 };
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            Assert.IsFalse(session.Status.Value.CapturePending);
            CollectionAssert.Contains(session.Status.Value.Missing, "serverSnapshot");
        }

        [Test]
        public void 録画が無効なら欠損に載る()
        {
            var sources = new FakeSources { Unavailable = "ffmpeg なし" };
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            CollectionAssert.Contains(session.Status.Value.Missing, "video");
        }

        [Test]
        public void 再開始で前回分は破棄される()
        {
            var sources = new FakeSources();
            var session = new BugReportCaptureSession(sources);
            session.BeginOnPauseMenu();
            session.OnServerCaptureCompleted(7, 2, "/w/snapshots", new List<string>(), new List<string>());
            sources.CaptureIdToReturn = 8;
            session.BeginOnPauseMenu();
            Assert.IsTrue(session.Status.Value.CapturePending);
            Assert.AreEqual(2, sources.CutCount);
        }
    }
}
```
（`RequestServerCapture` は同期完了する `UniTask` なので `BeginOnPauseMenu` 直後に状態が確定する）

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー

- [ ] **Step 3: 型を実装する**

`BugReportCaptureStatus.cs`:
```csharp
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Client.Game.InGame.BugReport
{
    public sealed class BugReportCaptureStatus
    {
        public bool HasSession { get; }
        public bool CapturePending { get; }
        public IReadOnlyList<string> Missing { get; }

        public BugReportCaptureStatus(bool hasSession, bool capturePending, IReadOnlyList<string> missing)
        {
            HasSession = hasSession;
            CapturePending = capturePending;
            Missing = missing;
        }
    }

    // 確保した記録一式。送信時に BugReportBundleWriter へ渡す
    // Everything captured for one report; handed to BugReportBundleWriter on send
    public sealed class BugReportCapturedData
    {
        public long CaptureId;
        public ulong ReportTick;
        public string SnapshotDirectory;
        public List<string> SnapshotFileNames = new();
        public List<string> PacketLogFileNames = new();
        public List<string> VideoSegmentFiles = new();
        public IReadOnlyList<(long unixMs, ulong tick)> FrameTicks;
        public IReadOnlyList<UnityLogEntry> Logs;
        public ClientStateSnapshot ClientState;
        public string ScreenshotPath;
        public List<MissingItem> Missing = new();
    }

    public interface IBugReportCaptureSources
    {
        UniTask<long> RequestServerCapture();
        void CutRecordingSegment();
        IReadOnlyList<string> CompletedVideoSegments();
        string RecordingUnavailableReason();
        IReadOnlyList<(long unixMs, ulong tick)> FrameTicks();
        IReadOnlyList<UnityLogEntry> Logs();
        ClientStateSnapshot ClientState();
        UniTask<string> CaptureScreenshot();
    }
}
```

`BugReportCaptureSession.cs`:
```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using UniRx;
using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    // Escapeの瞬間の記録を確保し、サーバー側スナップショットの完了を待つ。次のEscapeで前回分は捨てる
    // Secures the Escape-moment records and waits for the server snapshot; the next Escape discards the previous set
    public sealed class BugReportCaptureSession
    {
        public const float ServerCaptureTimeoutSeconds = 15f;

        private readonly IBugReportCaptureSources _sources;
        private readonly ReactiveProperty<BugReportCaptureStatus> _status = new(new BugReportCaptureStatus(false, false, Array.Empty<string>()));
        private BugReportCapturedData _data;
        private bool _pending;
        private int _beginCount;

        public IReadOnlyReactiveProperty<BugReportCaptureStatus> Status => _status;

        public BugReportCaptureSession(IBugReportCaptureSources sources)
        {
            _sources = sources;
        }

        public void BeginOnPauseMenu()
        {
            _beginCount++;
            var data = new BugReportCapturedData();
            _data = data;
            _pending = false;

            // 録画境界・ログ・クライアント状態・スクショはサーバー要求と独立に即時確保する
            // Secure the recording cut, logs, client state and screenshot immediately, independent of the server request
            var unavailable = _sources.RecordingUnavailableReason();
            if (unavailable.Length == 0)
            {
                _sources.CutRecordingSegment();
                data.VideoSegmentFiles = _sources.CompletedVideoSegments().ToList();
                data.FrameTicks = _sources.FrameTicks();
            }
            else
            {
                AddMissing(data, "video", unavailable);
            }
            data.Logs = _sources.Logs();
            data.ClientState = _sources.ClientState();
            data.ReportTick = data.ClientState.Tick;
            RequestServerCapture(data).Forget();
            CaptureScreenshot(data).Forget();
            PublishStatus();
        }

        public void OnServerCaptureCompleted(long captureId, ulong tick, string snapshotDirectory, IReadOnlyList<string> snapshotFileNames, IReadOnlyList<string> packetLogFileNames)
        {
            if (_data == null || _data.CaptureId != captureId)
            {
                Debug.Log($"要求IDが一致しない完了イベントを無視します captureId:{captureId}");
                return;
            }
            _data.ReportTick = tick;
            _data.SnapshotDirectory = snapshotDirectory;
            _data.SnapshotFileNames = snapshotFileNames.ToList();
            _data.PacketLogFileNames = packetLogFileNames.ToList();
            _pending = false;
            PublishStatus();
        }

        public BugReportCapturedData TakeCapturedData()
        {
            return _data;
        }

        private async UniTaskVoid RequestServerCapture(BugReportCapturedData data)
        {
            var beginCount = _beginCount;
            _pending = true;
            var captureId = await _sources.RequestServerCapture();
            if (beginCount != _beginCount) return;
            if (captureId == 0)
            {
                _pending = false;
                AddMissing(data, "serverSnapshot", "サーバーが即時スナップショット要求を受け付けなかった（常時記録が無効か通信失敗）");
                PublishStatus();
                return;
            }
            data.CaptureId = captureId;
            PublishStatus();

            await UniTask.Delay(TimeSpan.FromSeconds(ServerCaptureTimeoutSeconds));
            if (beginCount != _beginCount || !_pending) return;
            _pending = false;
            AddMissing(data, "serverSnapshot", $"完了イベントが {ServerCaptureTimeoutSeconds}s 以内に届かなかった");
            PublishStatus();
        }

        private async UniTaskVoid CaptureScreenshot(BugReportCapturedData data)
        {
            var path = await _sources.CaptureScreenshot();
            if (path == null) AddMissing(data, "screenshot", "スクリーンショットの書き出しに失敗した");
            data.ScreenshotPath = path;
            PublishStatus();
        }

        private static void AddMissing(BugReportCapturedData data, string item, string reason)
        {
            Debug.LogWarning($"バグ報告の記録が欠けます item:{item} reason:{reason}");
            data.Missing.Add(new MissingItem { Item = item, Reason = reason });
        }

        private void PublishStatus()
        {
            var missing = _data == null ? new List<string>() : _data.Missing.Select(m => m.Item).ToList();
            _status.Value = new BugReportCaptureStatus(_data != null, _pending, missing);
        }
    }
}
```
（`RequestServerCapture` 内で同期完了する `UniTask` は `await` 後もそのまま続くため、テストの `BeginOnPauseMenu` 直後に `CapturePending`＝true／`captureId==0` なら false が確定する）

`BugReportCaptureEventHandler.cs`（3点セット③）:
```csharp
using Client.Game.InGame.Context;
using MessagePack;
using Server.Event.EventReceive;
using VContainer.Unity;

namespace Client.Game.InGame.BugReport
{
    // サーバーの即時スナップショット完了イベントを購読し、確保セッションへ渡す
    // Subscribes to the server's immediate-snapshot completion event and forwards it to the capture session
    public sealed class BugReportCaptureEventHandler : IInitializable
    {
        private readonly BugReportCaptureSession _session;

        public BugReportCaptureEventHandler(BugReportCaptureSession session)
        {
            _session = session;
        }

        public void Initialize()
        {
            ClientContext.VanillaApi.Event.SubscribeEventResponse(BugReportCaptureCompletedEventPacket.EventTag, OnCompleted);
        }

        private void OnCompleted(byte[] payload)
        {
            var data = MessagePackSerializer.Deserialize<BugReportCaptureCompletedEventPacket.BugReportCaptureCompletedMessagePack>(payload);
            _session.OnServerCaptureCompleted(data.CaptureId, data.Tick, data.SnapshotDirectory, data.SnapshotFileNames, data.PacketLogFileNames);
        }
    }
}
```

ゲーム内の取得元実装 `BugReportCaptureSources.cs`（同ディレクトリ）:
```csharp
using System.Collections.Generic;
using System.IO;
using Client.Common;
using Client.Game.InGame.BugReport.Recording;
using Client.Game.InGame.Context;
using Client.Game.InGame.Player;
using Client.Game.InGame.UI.UIState;
using Core.Update;
using Cysharp.Threading.Tasks;
using Game.Paths;
using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    public sealed class BugReportCaptureSources : IBugReportCaptureSources
    {
        private readonly GameFrameRecorder _recorder;
        private readonly UnityLogRing _logRing;
        private readonly PlayerObjectController _player;
        private readonly UIStateControl _uiStateControl;

        public BugReportCaptureSources(GameFrameRecorder recorder, UnityLogRing logRing, PlayerObjectController player, UIStateControl uiStateControl)
        {
            _recorder = recorder;
            _logRing = logRing;
            _player = player;
            _uiStateControl = uiStateControl;
        }

        public async UniTask<long> RequestServerCapture()
        {
            var response = await ClientContext.VanillaApi.Response.RequestBugReportCapture(default);
            return response?.RequestedCaptureId ?? 0;
        }

        public void CutRecordingSegment() => _recorder.CutSegment();
        public IReadOnlyList<string> CompletedVideoSegments() => _recorder.CompletedSegmentFilesInOrder();
        public string RecordingUnavailableReason() => _recorder.IsRecording ? "" : _recorder.UnavailableReason;
        public IReadOnlyList<(long unixMs, ulong tick)> FrameTicks() => _recorder.TickLog.Dump();
        public IReadOnlyList<UnityLogEntry> Logs() => _logRing.Dump();

        public ClientStateSnapshot ClientState()
        {
            var camera = CameraManager.MainCamera?.Camera;
            var cameraPosition = camera == null ? Vector3.zero : camera.transform.position;
            var cameraEuler = camera == null ? Vector3.zero : camera.transform.eulerAngles;
            return new ClientStateSnapshot(cameraPosition, cameraEuler, _player.Position, _uiStateControl.CurrentState.ToString(), GameUpdater.CurrentTick);
        }

        public async UniTask<string> CaptureScreenshot()
        {
            Directory.CreateDirectory(GameSystemPaths.BugReportDirectory);
            var path = Path.Combine(GameSystemPaths.BugReportDirectory, "screenshot.png");
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            var start = Time.realtimeSinceStartup;
            while (!File.Exists(path))
            {
                if (10f < Time.realtimeSinceStartup - start) return null;
                await UniTask.Yield();
            }
            return path;
        }
    }
}
```
（`PlayerObjectController`・`UIStateControl` はヒエラルキーの component。`MainGameStarter.StartGame` の `builder.RegisterComponent(...)` で登録されているか `grep -n "RegisterComponent" moorestech_client/Assets/Scripts/Client.Starter/MainGameStarter.cs` で確認し、無ければ `UIStateControl` は `resolver.Resolve<UIStateControl>()` が `WebUiGameBinder` で使われている登録元に合わせる）

- [ ] **Step 4: `PauseMenuStateService` から駆動し DI 登録する**

`PauseMenuStateService.cs`:
```csharp
using Client.Game.InGame.BugReport;
using Client.Input;

namespace Client.Game.InGame.UI.UIState.State.PauseMenu
{
    public class PauseMenuStateService
    {
        private readonly BugReportCaptureSession _bugReportCaptureSession;
        private bool _closeRequested;

        public PauseMenuStateService(BugReportCaptureSession bugReportCaptureSession)
        {
            _bugReportCaptureSession = bugReportCaptureSession;
        }

        public bool IsClosePause()
        {
            if (_closeRequested)
            {
                _closeRequested = false;
                return true;
            }
            return InputManager.UI.CloseUI.GetKeyDown;
        }

        // バグ報告の送信完了など、ステート外からの閉じ要求。次の更新で消費される
        // Close request from outside the state (e.g. after a bug report is sent); consumed on the next update
        public void RequestClose()
        {
            _closeRequested = true;
        }

        public void OnEnter()
        {
            InputManager.MouseCursorVisible(true);
            // Escapeを押した瞬間の記録を確保する（ADR 0057）。記入中もワールドは止めない
            // Secure the Escape-moment records (ADR 0057); the world keeps running while typing
            _bugReportCaptureSession.BeginOnPauseMenu();
        }

        public void OnExit()
        {
        }
    }
}
```
`MainGameModelRegistration.cs` の `GameFrameRecorder` 登録の直後:
```csharp
            builder.Register<IBugReportCaptureSources, BugReportCaptureSources>(Lifetime.Singleton);
            builder.Register<BugReportCaptureSession>(Lifetime.Singleton);
            builder.RegisterEntryPoint<BugReportCaptureEventHandler>();
```

- [ ] **Step 5: コンパイル・テスト**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.BugReport\.BugReportCaptureSessionTest$"`
Expected: 5件PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport moorestech_client/Assets/Scripts/Client.Game/InGame/UI/UIState/State/PauseMenu/PauseMenuStateService.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs moorestech_client/Assets/Scripts/Client.Tests/BugReport
git commit -m "feat(client): Escape時点の記録を確保するBugReportCaptureSessionと完了イベント購読"
```

---

### Task 4: リポジトリ状態（`RepositoryStateProbe`・`BuildInfoWriter`）とバンドル書き出し（`BugReportBundleWriter`）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/RepositoryStateProbe.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/BugReportBundleWriter.cs`
- Create: `moorestech_client/Assets/Scripts/Editor/Build/BuildInfoWriter.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（`builder.Register<BugReportBundleWriter>(Lifetime.Singleton);`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/RepositoryStateProbeTest.cs`・`BuildInfoWriterTest.cs`

**Interfaces:**
- Consumes: Task 1〜3 の型、Task 2 `VideoAssembler`／`FfmpegLocator`
- Produces:
  - `public sealed class RepositoryProbeResult { public RepositoryState State; public string DiffText; public List<string> UntrackedFiles; public string Error; }`
  - `public static class RepositoryStateProbe { public static RepositoryProbeResult ProbeGit(string repositoryRoot); public static RepositoryState ReadBuildInfo(); public static string RepositoryRoot { get; } public static string MasterDataRoot { get; } }`（`RepositoryRoot = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."))`、`MasterDataRoot = Path.Combine(RepositoryRoot, "..", "moorestech_master")`）
  - `BuildInfoWriter : IPreprocessBuildWithReport`（`Assets/StreamingAssets/build-info.json` に `{ "commit", "branch", "dirty", "masterCommit", "masterDirty", "builtAt" }` を書く。`public static string Compose(RepositoryProbeResult repo, RepositoryProbeResult master, DateTime builtAt)` が JSON 文字列を返す）
  - `public sealed class BugReportBundleWriter { public const long UntrackedBytesLimit = 20L * 1024 * 1024; public UniTask<BugReportBundleResult> WriteAsync(BugReportCapturedData data, string description); }`、`public sealed class BugReportBundleResult { public string BundleDirectory; public IReadOnlyList<MissingItem> Missing; }`

- [ ] **Step 1: 失敗するテストを書く**

`RepositoryStateProbeTest.cs`:
```csharp
using Client.Game.InGame.BugReport;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class RepositoryStateProbeTest
    {
        [Test]
        public void 本repoのHEADとブランチが取れる()
        {
            var result = RepositoryStateProbe.ProbeGit(RepositoryStateProbe.RepositoryRoot);
            Assert.IsNull(result.Error, result.Error);
            Assert.AreEqual(40, result.State.Commit.Length);
            Assert.IsNotEmpty(result.State.Branch);
        }

        [Test]
        public void 存在しないパスはErrorに理由が入る()
        {
            var result = RepositoryStateProbe.ProbeGit("/nonexistent/path/for/test");
            Assert.IsNotNull(result.Error);
        }
    }
}
```

`BuildInfoWriterTest.cs`:
```csharp
using System;
using Client.Editor.Build;
using Client.Game.InGame.BugReport;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.BugReport
{
    public class BuildInfoWriterTest
    {
        [Test]
        public void 生成JSONは必要なキーを持つ()
        {
            var repo = new RepositoryProbeResult { State = new RepositoryState { Commit = "a", Branch = "b", Dirty = true } };
            var master = new RepositoryProbeResult { State = new RepositoryState { Commit = "c", Branch = "HEAD", Dirty = false } };
            var json = JObject.Parse(BuildInfoWriter.Compose(repo, master, new DateTime(2026, 9, 11, 0, 0, 0, DateTimeKind.Utc)));
            Assert.AreEqual("a", (string)json["commit"]);
            Assert.AreEqual(true, (bool)json["dirty"]);
            Assert.AreEqual("c", (string)json["masterCommit"]);
            Assert.AreEqual("2026-09-11T00:00:00Z", (string)json["builtAt"]);
        }
    }
}
```
（`Client.Editor.Build` は Editor アセンブリ。`Tests.asmdef` が Editor アセンブリを参照していなければ `Compose` を `RepositoryStateProbe` 側の `public static string ComposeBuildInfoJson(...)` として `Client.Game` に置き、`BuildInfoWriter` はそれを呼ぶだけにする）

- [ ] **Step 2: 実行して失敗を確認する**

Run: `uloop compile --project-path ./moorestech_client`
Expected: コンパイルエラー

- [ ] **Step 3: `RepositoryStateProbe` を実装する（git は外部境界）**

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Client.Game.InGame.BugReport
{
    public sealed class RepositoryProbeResult
    {
        public RepositoryState State;
        public string DiffText = "";
        public List<string> UntrackedFiles = new();
        public string Error;
    }

    // Editor実行時は git で作業ツリーの状態を取り、ビルド実行時は焼き込まれた build-info.json を読む
    // Probes the working tree via git when running in the Editor; reads the baked build-info.json in a build
    public static class RepositoryStateProbe
    {
        public const string BuildInfoFileName = "build-info.json";
        public static string RepositoryRoot => Path.GetFullPath(Path.Combine(Application.dataPath, "..", ".."));
        public static string MasterDataRoot => Path.GetFullPath(Path.Combine(RepositoryRoot, "..", "moorestech_master"));

        public static RepositoryProbeResult ProbeGit(string repositoryRoot)
        {
            var result = new RepositoryProbeResult();
            if (!Directory.Exists(repositoryRoot))
            {
                result.Error = $"ディレクトリが無い: {repositoryRoot}";
                return result;
            }
            if (!TryGit(repositoryRoot, "rev-parse HEAD", out var commit, out var error))
            {
                result.Error = error;
                return result;
            }
            TryGit(repositoryRoot, "rev-parse --abbrev-ref HEAD", out var branch, out _);
            TryGit(repositoryRoot, "status --porcelain", out var status, out _);
            TryGit(repositoryRoot, "diff HEAD", out var diff, out _);
            TryGit(repositoryRoot, "ls-files --others --exclude-standard", out var untracked, out _);
            result.State = new RepositoryState { Commit = commit.Trim(), Branch = branch.Trim(), Dirty = status.Trim().Length > 0 };
            result.DiffText = diff;
            result.UntrackedFiles = new List<string>(untracked.Split('\n', StringSplitOptions.RemoveEmptyEntries));
            return result;
        }

        public static RepositoryState ReadBuildInfo()
        {
            var path = Path.Combine(Application.streamingAssetsPath, BuildInfoFileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"build-info.json が無いためリポジトリ状態は不明です path:{path}");
                return new RepositoryState { Commit = "", Branch = "", Dirty = false };
            }
            var json = JObject.Parse(File.ReadAllText(path));
            return new RepositoryState { Commit = (string)json["commit"], Branch = (string)json["branch"], Dirty = (bool)json["dirty"] };
        }

        public static string ComposeBuildInfoJson(RepositoryProbeResult repo, RepositoryProbeResult master, DateTime builtAt)
        {
            var info = new JObject
            {
                ["commit"] = repo.State?.Commit ?? "",
                ["branch"] = repo.State?.Branch ?? "",
                ["dirty"] = repo.State?.Dirty ?? false,
                ["masterCommit"] = master.State?.Commit ?? "",
                ["masterDirty"] = master.State?.Dirty ?? false,
                ["builtAt"] = builtAt.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            };
            return info.ToString(Formatting.Indented);
        }

        private static bool TryGit(string workingDirectory, string arguments, out string stdout, out string error)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git",
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            // 外部プロセス起動は例外を返す境界のため、ここに限りcatchして失敗理由へ変換する
            // Process spawning is an external boundary; only here we catch and convert failures into a reason
            try
            {
                using var process = Process.Start(startInfo);
                stdout = process.StandardOutput.ReadToEnd();
                var stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();
                error = process.ExitCode == 0 ? null : $"git {arguments} failed ({process.ExitCode}): {stderr.Trim()}";
                return process.ExitCode == 0;
            }
            catch (Exception exception)
            {
                stdout = "";
                error = $"git を起動できない: {exception.GetBaseException().Message}";
                return false;
            }
        }
    }
}
```

`Editor/Build/BuildInfoWriter.cs`:
```csharp
using System;
using System.IO;
using Client.Game.InGame.BugReport;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Client.Editor.Build
{
    // ビルドにリポジトリ状態を焼き込む。バグ報告の manifest がビルド版でも出所を書けるようにする
    // Bakes the repository state into the build so a bug report's manifest can name its origin in a player build
    public class BuildInfoWriter : IPreprocessBuildWithReport
    {
        public int callbackOrder => 1;

        public void OnPreprocessBuild(BuildReport report)
        {
            var repo = RepositoryStateProbe.ProbeGit(RepositoryStateProbe.RepositoryRoot);
            var master = RepositoryStateProbe.ProbeGit(RepositoryStateProbe.MasterDataRoot);
            var path = Path.Combine(Application.streamingAssetsPath, RepositoryStateProbe.BuildInfoFileName);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, RepositoryStateProbe.ComposeBuildInfoJson(repo, master, DateTime.UtcNow));
            Debug.Log($"build-info.json を書きました commit:{repo.State?.Commit} dirty:{repo.State?.Dirty}");
        }
    }
}
```
（テストは `BuildInfoWriter.Compose` でなく `RepositoryStateProbe.ComposeBuildInfoJson` を呼ぶ形に直す）

- [ ] **Step 4: `BugReportBundleWriter` を実装する**

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Client.Game.InGame.BugReport.Recording;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.Game.InGame.BugReport
{
    public sealed class BugReportBundleResult
    {
        public string BundleDirectory;
        public IReadOnlyList<MissingItem> Missing;
    }

    // 確保済みの記録を outbox の1箱へ書く。欠けた項目は manifest.missing に理由付きで残し、例外で止めない
    // Writes the captured records into one outbox box; missing items go to manifest.missing with reasons, never throwing
    public sealed class BugReportBundleWriter
    {
        public const long UntrackedBytesLimit = 20L * 1024 * 1024;

        public async UniTask<BugReportBundleResult> WriteAsync(BugReportCapturedData data, string description)
        {
            var directory = BugReportOutbox.CreateBundleDirectory(DateTime.UtcNow, Guid.NewGuid().ToString("N").Substring(0, 8));
            var manifest = new BugReportManifest
            {
                CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
                Description = description,
                Platform = Application.platform.ToString(),
                IsEditor = Application.isEditor,
                ReportTick = data.ReportTick,
                ClientState = data.ClientState,
                Missing = new List<MissingItem>(data.Missing),
            };

            // ファイルコピーと ffmpeg はメインスレッドを塞がないようスレッドプールで行う
            // File copies and ffmpeg run on the thread pool so the main thread never blocks
            await UniTask.RunOnThreadPool(() =>
            {
                CopyWorld(data, directory, manifest);
                AssembleVideo(data, directory, manifest);
                WriteLogs(data, directory);
                CopyScreenshot(data, directory, manifest);
                WriteRepository(directory, manifest);
            });

            File.WriteAllText(Path.Combine(directory, "manifest.json"), manifest.ToJson());
            BugReportOutbox.MarkReady(directory);
            Debug.Log($"バグ報告バンドルを書きました {directory} missing:{manifest.Missing.Count}");
            return new BugReportBundleResult { BundleDirectory = directory, Missing = manifest.Missing };
        }

        private static void CopyWorld(BugReportCapturedData data, string directory, BugReportManifest manifest)
        {
            if (string.IsNullOrEmpty(data.SnapshotDirectory)) return;
            var snapshots = Path.Combine(directory, "snapshots");
            Directory.CreateDirectory(snapshots);
            foreach (var name in data.SnapshotFileNames.Concat(data.PacketLogFileNames))
            {
                var source = Path.Combine(data.SnapshotDirectory, name);
                if (!File.Exists(source))
                {
                    manifest.Missing.Add(new MissingItem { Item = name, Reason = "スナップショットディレクトリに無かった" });
                    continue;
                }
                File.Copy(source, Path.Combine(snapshots, name));
            }
            manifest.SnapshotFiles = data.SnapshotFileNames.ToList();
            manifest.PacketLogFiles = data.PacketLogFileNames.ToList();
            manifest.SnapshotTicks = data.SnapshotFileNames.Select(ParseTick).Where(t => t > 0).OrderBy(t => t).ToList();

            var worldRoot = Path.GetDirectoryName(data.SnapshotDirectory);
            var world = Path.Combine(directory, "world");
            Directory.CreateDirectory(world);
            foreach (var name in new[] { "world.json", "map.json" })
            {
                var source = Path.Combine(worldRoot, name);
                if (File.Exists(source)) File.Copy(source, Path.Combine(world, name));
                else manifest.Missing.Add(new MissingItem { Item = name, Reason = "ワールドディレクトリに無かった" });
            }
        }

        private static void AssembleVideo(BugReportCapturedData data, string directory, BugReportManifest manifest)
        {
            if (data.VideoSegmentFiles.Count == 0) return;
            var ffmpeg = FfmpegLocator.Find();
            var output = Path.Combine(directory, "video.mp4");
            if (ffmpeg == null || !VideoAssembler.Concat(ffmpeg, data.VideoSegmentFiles, output))
            {
                manifest.Missing.Add(new MissingItem { Item = "video", Reason = "区間の結合に失敗した" });
                return;
            }
            manifest.VideoSeconds = VideoAssembler.DurationSeconds(data.VideoSegmentFiles);
            if (!VideoAssembler.ExtractFrames(ffmpeg, output, Path.Combine(directory, "frames"), 2))
            {
                manifest.Missing.Add(new MissingItem { Item = "frames", Reason = "静止画の抜き出しに失敗した" });
            }
            File.WriteAllText(Path.Combine(directory, "frames.tsv"), FrameTickLog.ToTsv(data.FrameTicks));
        }

        private static void WriteLogs(BugReportCapturedData data, string directory)
        {
            var logs = Path.Combine(directory, "logs");
            Directory.CreateDirectory(logs);
            var builder = new StringBuilder();
            foreach (var entry in data.Logs)
            {
                builder.Append(entry.Time.ToString("HH:mm:ss.fff")).Append('\t').Append(entry.Tick).Append('\t').Append(entry.Type).Append('\t').Append(entry.Message).Append('\n');
                if (entry.StackTrace.Length > 0) builder.Append(entry.StackTrace).Append('\n');
            }
            File.WriteAllText(Path.Combine(logs, "unity.log"), builder.ToString());
        }

        private static void CopyScreenshot(BugReportCapturedData data, string directory, BugReportManifest manifest)
        {
            if (data.ScreenshotPath == null || !File.Exists(data.ScreenshotPath)) return;
            File.Copy(data.ScreenshotPath, Path.Combine(directory, "screenshot.png"));
        }

        private static void WriteRepository(string directory, BugReportManifest manifest)
        {
            var repo = Path.Combine(directory, "repo");
            Directory.CreateDirectory(repo);
            if (!Application.isEditor)
            {
                manifest.Repository = RepositoryStateProbe.ReadBuildInfo();
                manifest.MasterData = new RepositoryState { Commit = "", Branch = "", Dirty = false };
                return;
            }
            manifest.Repository = WriteOne(RepositoryStateProbe.RepositoryRoot, repo, "head.diff", "untracked", manifest);
            manifest.MasterData = WriteOne(RepositoryStateProbe.MasterDataRoot, repo, "master.diff", "master-untracked", manifest);
        }

        private static RepositoryState WriteOne(string root, string repoDirectory, string diffName, string untrackedDirectoryName, BugReportManifest manifest)
        {
            var probe = RepositoryStateProbe.ProbeGit(root);
            if (probe.Error != null)
            {
                manifest.Missing.Add(new MissingItem { Item = diffName, Reason = probe.Error });
                return new RepositoryState { Commit = "", Branch = "", Dirty = false };
            }
            File.WriteAllText(Path.Combine(repoDirectory, diffName), probe.DiffText);
            File.WriteAllText(Path.Combine(repoDirectory, untrackedDirectoryName + ".txt"), string.Join("\n", probe.UntrackedFiles));
            long copied = 0;
            foreach (var relative in probe.UntrackedFiles)
            {
                var source = Path.Combine(root, relative);
                if (!File.Exists(source)) continue;
                copied += new FileInfo(source).Length;
                if (copied > UntrackedBytesLimit)
                {
                    manifest.Missing.Add(new MissingItem { Item = untrackedDirectoryName, Reason = "未追跡ファイルが20MBを超えたため一覧のみ" });
                    break;
                }
                var target = Path.Combine(repoDirectory, untrackedDirectoryName, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(target));
                File.Copy(source, target);
            }
            return probe.State;
        }

        private static ulong ParseTick(string fileName)
        {
            var core = fileName.Replace("tick_", "").Replace(".json", "");
            return ulong.TryParse(core, out var tick) ? tick : 0;
        }
    }
}
```
（200行を超える場合は `WriteRepository`／`WriteOne` を `BugReportRepositoryFiles.cs` へ分離する）

- [ ] **Step 5: DI登録・コンパイル・テスト**

`MainGameModelRegistration.cs` の `BugReportCaptureEventHandler` 登録の直後に `builder.Register<BugReportBundleWriter>(Lifetime.Singleton);`。

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "^Client\.Tests\.BugReport\.(RepositoryStateProbeTest|BuildInfoWriterTest)$"`
Expected: PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport moorestech_client/Assets/Scripts/Editor/Build/BuildInfoWriter.cs moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs moorestech_client/Assets/Scripts/Client.Tests/BugReport
git commit -m "feat(client): バンドル書き出しとリポジトリ状態の取得・ビルド情報の焼き込み"
```

---

### Task 5: 報告UI（`pause_menu.current` の拡張・`bug_report.submit` アクション・ポーズメニューの記述欄と送信・ローカライズ）

**Files:**
- Modify: `Localization/localization.csv`（末尾に5行追加）・`moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs`（`dummyText` を更新）
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts:41`（`PauseMenuDataSchema` 拡張）
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts`（`ActionPayloads` と `ACTION_TYPES` に `bug_report.submit`）
- Modify: `moorestech_web/webui/src/app/tokens.css`（`--bug-report-textarea-width`・`--bug-report-textarea-height` を `--panel-action-button-*` の直後に追加）
- Modify: `moorestech_web/webui/src/features/pauseMenu/PauseMenuPanel.tsx`・`style.module.css`
- Create: `moorestech_web/webui/src/features/pauseMenu/BugReportForm.tsx`・`moorestech_web/webui/src/features/pauseMenu/BugReportForm.test.ts`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/PauseMenuTopic.cs`（`BugReportCaptureSession.Status` を購読し `bugReport` を載せる）
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/BugReportSubmitActionHandler.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:97-98,198`（トピック生成に session を渡し、アクションを登録）

**Interfaces:**
- Consumes: Task 3 `BugReportCaptureSession.Status`／`TakeCapturedData()`、Task 4 `BugReportBundleWriter.WriteAsync`、`PauseMenuStateService.RequestClose()`
- Produces:
  - トピック `pause_menu.current` の payload: `{ disconnected: boolean, bugReport: { hasSession: boolean, capturePending: boolean, missing: string[] } }`
  - アクション `bug_report.submit`: payload `{ description: string }`。成功で `ActionResult.Success()`、説明文が空白のみなら `ActionResult.Fail("empty_description")`、確保セッションが無ければ `Fail("no_capture_session")`
  - ローカライズキー `ui.bugReport.placeholder`／`ui.bugReport.send`／`ui.bugReport.sent`／`ui.bugReport.capturePending`／`ui.bugReport.missing`
  - `BugReportForm`（props: `status: PauseMenuData["bugReport"]`、`onSent: () => void`）

- [ ] **Step 1: ローカライズ行を追加して生成する**

`Localization/localization.csv` 末尾に追加（列: key,Source,english,japanese,german）:
```
ui.bugReport.placeholder,What happened? (what you did and what went wrong),What happened? (what you did and what went wrong),何が起きた？（何をして・何がおかしかったか）,Was ist passiert? (was du getan hast und was schiefging)
ui.bugReport.send,Send bug report,Send bug report,バグ報告を送信,Fehlerbericht senden
ui.bugReport.sent,Bug report saved to outbox,Bug report saved to outbox,バグ報告をoutboxに書き出しました,Fehlerbericht im Postausgang gespeichert
ui.bugReport.capturePending,Securing the recording…,Securing the recording…,記録を確保しています…,Aufzeichnung wird gesichert…
ui.bugReport.missing,Missing: {items},Missing: {items},欠けている項目: {items},Fehlt: {items}
```
`_CompileRequester.cs` の `dummyText` を `uuidgen | tr a-z A-Z` の値へ変更。
Run: `cd moorestech_web/webui && pnpm gen:i18n && cd ../.. && uloop compile --project-path ./moorestech_client`
Expected: `src/shared/i18n/generated/localizationKeys.ts` に `bugReport` が出る。C# 側 `LocalizationKeys.Ui.BugReport.Send` が生成される（`grep -rn "BugReport" moorestech_client/Library/Bee/artifacts` は不要。コンパイルが通れば可）

- [ ] **Step 2: webui の失敗するテストを書く**

`src/features/pauseMenu/BugReportForm.test.ts`:
```ts
import { createElement } from "react";
import { act, create, type ReactTestRenderer } from "react-test-renderer";
import { afterEach, describe, expect, it, vi } from "vitest";
import { setDictionaries } from "@/shared/i18n/i18nStore";

const mocks = vi.hoisted(() => ({ dispatchAction: vi.fn(async () => true), emitToast: vi.fn() }));

vi.mock("@/bridge", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/bridge")>()),
  dispatchAction: mocks.dispatchAction,
}));
vi.mock("@/features/toast", () => ({ emitToast: mocks.emitToast }));
vi.mock("@/shared/ui", () => ({
  PanelActionButton: ({ children, onClick, testId }: { children: unknown; onClick: () => void; testId?: string }) =>
    createElement("mock-button", { onClick, "data-testid": testId }, children as never),
}));

import { BugReportForm } from "./BugReportForm";

const dictionary = {
  "ui.bugReport.placeholder": "何が起きた？",
  "ui.bugReport.send": "バグ報告を送信",
  "ui.bugReport.sent": "書き出しました",
  "ui.bugReport.capturePending": "記録を確保しています…",
  "ui.bugReport.missing": "欠けている項目: {items}",
};

afterEach(() => {
  vi.clearAllMocks();
});

describe("BugReportForm", () => {
  it("記述欄と送信ボタンを描く", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ hasSession: true, capturePending: false, missing: [] });
    expect(renderer.root.findByProps({ "data-testid": "bug-report-description" })).toBeTruthy();
    expect(renderer.root.findByProps({ "data-testid": "bug-report-send" })).toBeTruthy();
    act(() => renderer.unmount());
  });

  it("入力後の送信で bug_report.submit を説明文付きで送りトーストを出す", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const onSent = vi.fn();
    const renderer = await render({ hasSession: true, capturePending: false, missing: [] }, onSent);
    const textarea = renderer.root.findByProps({ "data-testid": "bug-report-description" });
    act(() => textarea.props.onChange({ currentTarget: { value: "ベルトが止まる" } }));
    await act(async () => renderer.root.findByProps({ "data-testid": "bug-report-send" }).props.onClick());
    expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.submit", { description: "ベルトが止まる" });
    expect(mocks.emitToast).toHaveBeenCalledWith("書き出しました", "info");
    expect(onSent).toHaveBeenCalled();
    act(() => renderer.unmount());
  });

  it("空文字では送らない", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ hasSession: true, capturePending: false, missing: [] });
    await act(async () => renderer.root.findByProps({ "data-testid": "bug-report-send" }).props.onClick());
    expect(mocks.dispatchAction).not.toHaveBeenCalled();
    act(() => renderer.unmount());
  });

  it("確保中と欠損の文言を出す", async () => {
    setDictionaries("japanese", dictionary, {}, {});
    const renderer = await render({ hasSession: true, capturePending: true, missing: ["video", "screenshot"] });
    const texts = renderer.root.findAllByProps({ "data-testid": "bug-report-status" }).map((n) => n.children.join(""));
    expect(texts.some((t) => t.includes("記録を確保しています…"))).toBe(true);
    expect(texts.some((t) => t.includes("欠けている項目: video, screenshot"))).toBe(true);
    act(() => renderer.unmount());
  });
});

async function render(status: { hasSession: boolean; capturePending: boolean; missing: string[] }, onSent = vi.fn()): Promise<ReactTestRenderer> {
  let renderer: ReactTestRenderer;
  await act(async () => {
    renderer = create(createElement(BugReportForm, { status, onSent }));
  });
  return renderer!;
}
```
（`setDictionaries` の引数順は `features/settings/LanguageSelect.test.ts` と同じ。`t()` の補間 `{items}` は `useI18n().t(key, values)` の既存仕様に従う。シグネチャは `shared/i18n/i18nStore.ts` の `createTranslator` で確認する）

`src/bridge/contract/schemas/ui.test.ts` に追加:
```ts
import { PauseMenuDataSchema } from "./ui";

describe("PauseMenuDataSchema", () => {
  it("bugReport を持つ", () => {
    const parsed = PauseMenuDataSchema.parse({ disconnected: false, bugReport: { hasSession: true, capturePending: false, missing: ["video"] } });
    expect(parsed.bugReport.missing).toEqual(["video"]);
  });
});
```

Run: `cd moorestech_web/webui && pnpm test -- BugReportForm ui.test`
Expected: FAIL（モジュールが無い／スキーマに `bugReport` が無い）

- [ ] **Step 3: bridge 契約を更新する**

`schemas/ui.ts:41`:
```ts
export const BugReportStatusSchema = z.object({
  hasSession: z.boolean(),
  capturePending: z.boolean(),
  missing: z.array(z.string()),
});
export const PauseMenuDataSchema = z.object({ disconnected: z.boolean(), bugReport: BugReportStatusSchema });
```
`actionContract.ts`: `ActionPayloads` に `"bug_report.submit": { description: string };`（`"pause_menu.save_and_quit"` の直後）、`ACTION_TYPES` に `"bug_report.submit",`（同位置）。

- [ ] **Step 4: `BugReportForm` と CSS を実装する**

`tokens.css`（`--panel-action-button-hover-mix` の直後）:
```css
  /* ポーズメニューのバグ報告記述欄。パネル内幅（420−32×2）と3行ぶんを固定長で持つ */
  /* Bug-report textarea in the pause menu; fixed lengths for the inner panel width (420−32×2) and three lines */
  --bug-report-textarea-width: 356px;
  --bug-report-textarea-height: 72px;
```

`style.module.css` に追加（§8.9 検索入力と同族）:
```css
.description {
  width: var(--bug-report-textarea-width);
  height: var(--bug-report-textarea-height);
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

.status {
  color: var(--text-muted);
}
```

`BugReportForm.tsx`:
```tsx
// ポーズメニュー直置きのバグ報告欄。Escape時点の記録はC#側が確保済みで、ここは説明文と送信だけを担う
// Bug-report form placed directly in the pause menu; C# has secured the Escape-moment records, this only adds text and sends
import { useState } from "react";
import { dispatchAction, type PauseMenuData } from "@/bridge";
import { emitToast } from "@/features/toast";
import { L, useI18n } from "@/shared/i18n";
import { PanelActionButton } from "@/shared/ui";
import styles from "./style.module.css";

type Props = {
  status: PauseMenuData["bugReport"];
  onSent: () => void;
};

export function BugReportForm({ status, onSent }: Props) {
  const { t } = useI18n();
  const [description, setDescription] = useState("");

  const send = async () => {
    const trimmed = description.trim();
    if (trimmed.length === 0) return;
    const ok = await dispatchAction("bug_report.submit", { description: trimmed });
    if (!ok) return;
    emitToast(t(L.ui.bugReport.sent), "info");
    setDescription("");
    onSent();
  };

  return (
    <>
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
（`t(key, values)` の第2引数の形は `i18nStore.ts` の `createTranslator` に合わせる。`PanelActionButton` が `@/shared/ui` の `index.ts` から、`PauseMenuData` 型が `@/bridge` の `index.ts` から export されていなければ追加する。feature 層は `@/bridge` 以外の bridge 内部パスを import しない）

`PauseMenuPanel.tsx`: `<LanguageSelect />` の直前に `{data && <BugReportForm status={data.bugReport} onSent={() => {}} />}` を追加。`onSent` はC#側が `RequestClose()` で閉じるため webui は何もしない（コメントに明記）。

Run: `cd moorestech_web/webui && pnpm test -- BugReportForm ui.test && pnpm lint`
Expected: PASS

- [ ] **Step 5: C# 側トピックとアクション**

`PauseMenuTopic.cs` を次に置き換える:
```csharp
using System;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.Presenter.PauseMenu;
using Client.WebUiHost.Boot;
using Client.WebUiHost.Common;
using Cysharp.Threading.Tasks;
using UniRx;

namespace Client.WebUiHost.Game.Topics
{
    public class PauseMenuTopic : ITopicHandler, IDisposable
    {
        public const string TopicName = "pause_menu.current";

        private readonly WebSocketHub _hub;
        private readonly NetworkDisconnectState _state;
        private readonly BugReportCaptureSession _bugReportCaptureSession;
        private readonly CompositeDisposable _subscriptions = new();

        public PauseMenuTopic(WebSocketHub hub, NetworkDisconnectState state, BugReportCaptureSession bugReportCaptureSession)
        {
            _hub = hub;
            _state = state;
            _bugReportCaptureSession = bugReportCaptureSession;

            // 切断状態と確保状態の変化だけを配信し、再接続時はsnapshotから復元する
            // Publish only disconnect and capture-status changes; restore from the snapshot after reconnect
            state.OnDisconnectedChanged.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
            bugReportCaptureSession.Status.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);
        }

        public UniTask<string> GetSnapshotJsonAsync()
        {
            return UniTask.FromResult(BuildJson());
        }

        public void Dispose()
        {
            _subscriptions.Dispose();
        }

        private void Publish()
        {
            _hub.Publish(TopicName, BuildJson());
        }

        private string BuildJson()
        {
            var status = _bugReportCaptureSession.Status.Value;
            return WebUiJson.Serialize(new PauseMenuDto
            {
                Disconnected = _state.IsDisconnected,
                BugReport = new BugReportStatusDto { HasSession = status.HasSession, CapturePending = status.CapturePending, Missing = new System.Collections.Generic.List<string>(status.Missing) },
            });
        }
    }

    public class PauseMenuDto
    {
        public bool Disconnected;
        public BugReportStatusDto BugReport;
    }

    public class BugReportStatusDto
    {
        public bool HasSession;
        public bool CapturePending;
        public System.Collections.Generic.List<string> Missing;
    }
}
```

`Actions/BugReportSubmitActionHandler.cs`:
```csharp
using Client.Game.InGame.BugReport;
using Client.Game.InGame.UI.UIState.State.PauseMenu;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.WebUiHost.Game.Actions
{
    // 説明文を受け取り、確保済みの記録と一緒に outbox へ書き、ポーズメニューを閉じる
    // Takes the description, writes it with the secured records into the outbox, then closes the pause menu
    public class BugReportSubmitActionHandler : IActionHandler
    {
        private readonly BugReportBundleWriter _writer;
        private readonly BugReportCaptureSession _session;
        private readonly PauseMenuStateService _pauseMenuStateService;
        public string ActionType => "bug_report.submit";

        public BugReportSubmitActionHandler(BugReportBundleWriter writer, BugReportCaptureSession session, PauseMenuStateService pauseMenuStateService)
        {
            _writer = writer;
            _session = session;
            _pauseMenuStateService = pauseMenuStateService;
        }

        public async UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            var description = payload?["description"]?.ToString()?.Trim() ?? "";
            if (description.Length == 0)
            {
                Debug.LogWarning("バグ報告の説明文が空のため送信しません");
                return ActionResult.Fail("empty_description");
            }
            var data = _session.TakeCapturedData();
            if (data == null)
            {
                Debug.LogWarning("確保セッションが無いためバグ報告を送信しません（ポーズメニューを開き直してください）");
                return ActionResult.Fail("no_capture_session");
            }

            var result = await _writer.WriteAsync(data, description);
            Debug.Log($"バグ報告を書き出しました {result.BundleDirectory}");
            _pauseMenuStateService.RequestClose();
            return ActionResult.Success();
        }
    }
}
```
`WebUiGameBinder.cs`: `new PauseMenuTopic(hub, networkDisconnectState)` → `new PauseMenuTopic(hub, networkDisconnectState, resolver.Resolve<BugReportCaptureSession>())`。`PauseMenuSaveAndQuitActionHandler` 登録の直後に:
```csharp
            hub.RegisterAction(new BugReportSubmitActionHandler(resolver.Resolve<BugReportBundleWriter>(), resolver.Resolve<BugReportCaptureSession>(), resolver.Resolve<PauseMenuStateService>()));
```

- [ ] **Step 6: コンパイル・確認**

Run: `uloop compile --project-path ./moorestech_client` → `cd moorestech_web/webui && pnpm test && pnpm lint`
Expected: PASS

- [ ] **Step 7: コミットする**

```bash
git add Localization/localization.csv moorestech_client/Assets/Scripts/Client.Localization/_CompileRequester.cs moorestech_web/webui/src moorestech_client/Assets/Scripts/Client.WebUiHost
git commit -m "feat(webui,client): ポーズメニューにバグ報告の記述欄と送信を直置きし確保状態を配信"
```

---

### Task 6: 通しの EditModeInPlayingTest（確保→送信→outbox）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/BugReport/BugReportBundleWriteTest.cs`

**Interfaces:**
- Consumes: plan A `WorldSnapshotRing`（`ServerContext.GetService<WorldSnapshotRing>()`）、Task 3〜5

- [ ] **Step 1: テストを書く**

```csharp
using System.Collections;
using System.IO;
using System.Linq;
using Client.Game.InGame.BugReport;
using Client.Game.InGame.UI.UIState;
using Client.Tests.EditModeInPlayingTest.Util;
using Client.WebUiHost.Game.Actions;
using Cysharp.Threading.Tasks;
using Game.Context;
using Game.SaveLoad.Snapshot;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEngine.TestTools;
using VContainer;

namespace Client.Tests.EditModeInPlayingTest.BugReport
{
    [Category("CiShardClientPlay3")]
    public class BugReportBundleWriteTest
    {
        [UnityTest]
        public IEnumerator ポーズメニューを開いて送信するとoutboxにバンドルが揃う()
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

                var uiState = resolver.Resolve<UIStateControl>();
                uiState.RequestTransition(UIStateEnum.PauseMenu);
                var session = resolver.Resolve<BugReportCaptureSession>();
                for (var i = 0; i < 100 && (!session.Status.Value.HasSession || session.Status.Value.CapturePending); i++)
                {
                    await UniTask.Delay(50);
                }
                Assert.IsTrue(session.Status.Value.HasSession, "確保セッションが始まっていない");
                Assert.IsFalse(session.Status.Value.CapturePending, "サーバースナップショットが2秒以内に完了しない");

                var handler = new BugReportSubmitActionHandler(resolver.Resolve<BugReportBundleWriter>(), session, resolver.Resolve<Client.Game.InGame.UI.UIState.State.PauseMenu.PauseMenuStateService>());
                var result = await handler.ExecuteAsync(new JObject { ["description"] = "テスト報告" });
                Assert.IsTrue(result.Ok, result.Error);

                var outbox = Game.Paths.GameSystemPaths.BugReportOutboxDirectory;
                var bundle = Directory.GetDirectories(outbox).OrderByDescending(d => d).First();
                Assert.IsTrue(File.Exists(Path.Combine(bundle, "READY")));
                var manifest = JObject.Parse(File.ReadAllText(Path.Combine(bundle, "manifest.json")));
                Assert.AreEqual("テスト報告", (string)manifest["description"]);
                Assert.GreaterOrEqual(((JArray)manifest["snapshotFiles"]).Count, 1, "スナップショットが同梱されていない");
                Assert.IsFalse(((JArray)manifest["missing"]).Any(m => (string)m["item"] == "serverSnapshot"), "サーバースナップショットが欠損扱い");
                Assert.IsTrue(Directory.GetFiles(Path.Combine(bundle, "snapshots"), "tick_*.json").Length >= 1);
                Assert.IsTrue(File.Exists(Path.Combine(bundle, "logs", "unity.log")));
                Assert.IsTrue(File.Exists(Path.Combine(bundle, "repo", "head.diff")));
                Assert.IsTrue(File.Exists(Path.Combine(bundle, "video.mp4")) || ((JArray)manifest["missing"]).Any(m => (string)m["item"] == "video"), "動画が無いのに欠損にも載っていない");

                // 送信後はポーズメニューが閉じてゲームに戻る
                // After sending, the pause menu closes and play resumes
                for (var i = 0; i < 20 && uiState.CurrentState != UIStateEnum.GameScreen; i++) await UniTask.Delay(50);
                Assert.AreEqual(UIStateEnum.GameScreen, uiState.CurrentState);
                Directory.Delete(bundle, true);
            }

            #endregion
        }
    }
}
```
（`UIStateControl.RequestTransition` の実在は `UIStateControl.cs:38` で確認済み。`ClientDIContext` の名前空間は `EquipmentSelectionSynchronizationTest` と同じ）

- [ ] **Step 2: 実行する**

Run: `uloop run-tests --project-path ./moorestech_client --test-mode EditMode --filter-type regex --filter-value "Client\.Tests\.EditModeInPlayingTest\.BugReport\.BugReportBundleWriteTest" --timeout-seconds 900`
Expected: PASS（Domain Reload エラーは45秒待って再実行。戻らなければ `~/Library/Application Support/sakastudio/moorestech/TestResults.xml` を読む）

- [ ] **Step 3: 実機で1回通す**

`uloop control-play-mode --action play` で通常起動し、60秒プレイ→Escape→説明文を入力→送信。`~/Library/Application Support/moorestech/BugReports/outbox/` の最新箱を `ls -R` し、`video.mp4` を `ffmpeg -i video.mp4 -frames:v 1 first.png` で1枚抜いて `Read` で目視（上下・内容）。`manifest.json` の `missing` が空であることを確認し、結果を判断記録へ書く。

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/BugReport docs/superpowers/plans/2026-09-11-bug-report-b-client-capture-and-report-ui.md
git commit -m "test(client): バグ報告の確保→送信→outboxの通しテスト"
```

---

### Task 7: 必ずmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）

- [ ] **Step 1:** `moores-code-review` を起動し、plan B のコミット範囲（plan A 完了コミット以降）をレビューする。webui の変更は `webui-design` レンズ（§8.9 入力欄・§8.6 ボタン・トークン・z層）を含める。
- [ ] **Step 2:** 機械的指摘を反映し、`uloop compile`・`pnpm test`・`pnpm lint`・関連テストを再実行する。反映が判定経路（`BugReportCaptureSession` の要求ID照合・タイムアウト、`BugReportBundleWriter` の欠損記録、`PauseMenuStateService.IsClosePause`）に触れたら Task 6 の通しテストと実機確認を反映後のバイナリで再実施する。
- [ ] **Step 3:** 設計判断が要る指摘だけを AskUserQuestion で裁定に出す。

### Task 8: セッション終了可能状態にすること

- [ ] **Step 1:** `git status` で未コミットが無いことを確認（`.moorestech-external-revisions.json` の自動書き換えは `git checkout --`）。
- [ ] **Step 2:** `bd note moorestech-yoag "plan B 完了: <最終コミット> / 実機確認: 向き=…, missing=…"`。
- [ ] **Step 3:** plan C の開始プロンプトを出力する。

---

## 配置と前例（spec-architecture-review）

| # | 項目 | 配置 | 前例・根拠 |
|---|---|---|---|
| 1 | `UnityLogRing`・`BugReportCaptureSession`・`BugReportBundleWriter`・`RepositoryStateProbe` ほか | `Client.Game/InGame/BugReport/`（9ファイル） | クライアントのローカル状態と表示のための論理モデル（層マップ `Client.*`）。`Client.Game` は `Core.Update`・`Game.Paths`・`Server.Event` を既に参照する。新アセンブリは `PauseMenuStateService`（`Client.Game`）からの駆動で循環するため作らない |
| 2 | 録画 `GameFrameRecorder`・`FfmpegProcess`・`FfmpegLocator`・`FrameTickLog`・`VideoAssembler` | `Client.Game/InGame/BugReport/Recording/`（5ファイル） | 外部プロセス起動の書き方は `Client.WebUiHost/Editor/EditorProcessRunner.cs`（境界try-catch・両ストリーム排水）に従う。毎フレーム取り込みは `ITickable`（前例 `PlayerPositionSender`） |
| 3 | 確保セッションの駆動元 | `PauseMenuStateService.OnEnter` | 層マップ「制御に参加するコンポーネントはステートから明示駆動」。`UIStateControl.OnStateChanged` 購読は表示専用オブザーバに限る |
| 4 | 完了イベントの購読 `BugReportCaptureEventHandler` | `Client.Game/InGame/BugReport/`、`RegisterEntryPoint` | 3点セット③。前例 `ItemStackLevelEventHandler`（`IInitializable`＋`SubscribeEventResponse`） |
| 5 | 状態の配信 | 既存 `PauseMenuTopic` に `bugReport` を追加 | ポーズメニューの表示データはこのトピックが所有。新トピックを作らない（同一ドメイン1本の原則） |
| 6 | 送信アクション `BugReportSubmitActionHandler` | `Client.WebUiHost/Game/Actions/` | `PauseMenuSaveActionHandler` と同型（`IActionHandler`、`WebUiGameBinder` で登録） |
| 7 | webui `BugReportForm` | `features/pauseMenu/` | 入力欄は §8.9（`BuildMenuSearchInput` の CSS 同族）、ボタンは §8.6 `PanelActionButton`。`PauseMenuPanel` の Mantine `Button` は前例として引用しない（負債は据え置き） |
| 8 | outbox のパス | `Game.Paths/GameSystemPaths.cs` | 既存 `SaveFileDirectory`・`WorldCacheDirectory` と同じ場所に定義（パス連結は `Game.Paths` 以外で行わない） |
| 9 | `BuildInfoWriter` | `Editor/Build/` | `WebUiProductionArtifactBuilder`（`IPreprocessBuildWithReport`、StreamingAssets へ成果物）と同型。ロジックは `RepositoryStateProbe.ComposeBuildInfoJson`（ランタイム側）に置きテスト可能にする |
| 10 | ローカライズ | `Localization/localization.csv` 1本 | TS/C# ともCSVから生成。`_CompileRequester.cs` の印を更新してコミット |

データフロー（Phase 1.5）: `Escape → GameScreenState → UIStateControl → PauseMenuState.OnEnter → PauseMenuStateService.OnEnter ──駆動──> BugReportCaptureSession.Begin → (サーバー要求 / 録画境界 / ログ / クライアント状態 / スクショ) → Status(ReactiveProperty) → PauseMenuTopic → webui`、`webui 送信 → bug_report.submit → BugReportSubmitActionHandler → BugReportBundleWriter → outbox(READY) → PauseMenuStateService.RequestClose → 次更新で GameScreen`。新規要素は「書き手」（記録・outbox）と「読み手」（トピック）で、既存UIステート機械への分岐は `RequestClose` の1フラグのみ（`IsClosePause` が消費）。

機構選択（検査4）: ポーズメニューを閉じる経路は「既存の `IsClosePause` 判定に閉じ要求フラグを合流させる」受動的統合。対案「`UIStateControl.RequestTransition(GameScreen)` を直接呼ぶ」は `RequestTransition` がWeb要求用の入口として存在するため同等だが、ステートの遷移判定を1箇所（`GetNextUpdate`）に保つため前者を採る。

死活表（Phase 2.5）: Escape→ポーズメニュー表示 → 生きる／セーブ・セーブして終了・言語 → 生きる（要素追加のみ）／Escape で閉じる → 生きる／スキット中・列車HUD中の入れ子ポーズ（`PauseMenuNestedSubState`）→ **`PauseMenuStateService.OnEnter` を経由するか未確認**。`NestedPauseSubStateController` が `pauseMenuStateService.OnEnter()` を呼ぶかを Task 3 で `grep -n "OnEnter" State/NestedPause/PauseMenuNestedSubState.cs` で確認し、呼ばないなら入れ子ポーズからは確保セッションが始まらない（報告は不可、`hasSession=false` で送信ボタンが `no_capture_session` を返す）ことを判断記録に書く。

## 判断記録（ADR）

- 設計ADR: `docs/adr/0057-bug-report-bundle-and-isolated-auto-fix.md`、裁定 `.decisions/2026-09-11-バグ報告*.md`
- **フレームのサーバーtickは同一プロセスの `GameUpdater.CurrentTick` を読む**（agent前提）: 初版はローカルサーバー（同一プロセス）限定。リモート接続時はクライアント側の値になり不正確だが、送り手は開発者本人のみ（裁定）。
- **録画は `ScreenCapture.CaptureScreenshotIntoRenderTexture`→縮小Blit→`AsyncGPUReadback`**（agent前提）: CEFのUIも写るゲーム画面の最終出力を取る。`Unity Recorder` はEditor専用のため不採用。
- **区間確定は ffmpeg を止めて再起動する**（agent前提）: `-segment` は書き込み中区間を外から閉じられない。停止〜再起動の数百msは録画が抜けるが、Escape直後の空白なので許容。
- **`SanitizedProcessEnvironment` は使わない**（agent前提）: `Client.WebUiHost` にあり `Client.Game` から参照できない。共通化は別タスク。
- **未追跡ファイルは20MB上限で同梱**（agent前提）: 超過はパス一覧のみ、manifest に理由を残す。
- **入れ子ポーズ（スキット・列車HUD）からの報告**: Task 3 の確認結果を追記。
- Task 2 Step 9 の向き確認・Task 6 Step 3 の実機確認結果: （実装時に転記）
