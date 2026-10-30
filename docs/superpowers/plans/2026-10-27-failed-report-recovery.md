# 送信できない報告の再送・書き出し Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** テスターがポーズ中のバグ報告画面から、送信できない報告を再送し、未送信の報告を zip にまとめて提出できるようにし、開発側は届いた zip を受け口経由と同じ形で取り込めるようにする。

**Architecture:** outbox の箱の状態判定を `PlaytestOutboxBoxState` 1か所へ集め、件数表示・再送・書き出しが同じ判定を使う。C# 側は `Client.PlaytestReceiver/Recovery/` に再送（失敗印の解除→既存の走行役へ送信要求）と書き出し（zip 作成→デスクトップ→ファイルマネージャで選択表示）を置き、書き出しの進行は static な ReactiveProperty 板（`PlaytestDeliveryStatusBoard` と同型）から既存の `pause_menu.current` topic に載せる。webui は2つの action を発行し、topic の書き出し状態を表示する。開発側は `scripts/playtest/import-export-zip.sh` が zip を `moorestech_logs/harness/playtest/{reports,progress}/<steamId>/<id>` へ展開する。

**Tech Stack:** Unity C#（UniRx・UniTask・VContainer・System.IO.Compression）、React + zod + vitest（moorestech_web/webui）、bash + python3（scripts/playtest）

## Requirements

1. バグ報告画面の配送状況の行の下に「再送」ボタンを出す。表示条件は `delivery.inventory.kind == "known"` かつ `failedCount > 0`。押すと、対象範囲の失敗箱の `UPLOAD_FAILED` と `UPLOAD_ATTEMPTS` だけを消し、既存の `IPlaytestUploadRequester.RequestUpload()` で送信を要求する。`UPLOAD_DECLARED`・`UPLOAD_SKIPPED`・`UPLOAD_LOCK` は消さない。受け入れ: 失敗箱2件の outbox で再送すると2件とも印が消え、送信要求が1回出る。
2. 同じ場所に「書き出し」ボタンを出す。表示条件は `known` かつ `pendingCount + failedCount > 0`、または inventory が `unavailable`。押すと、未送信の箱（READY あり・UPLOADED/SHIPPED なし。送信待ちと送信できない分の両方）を1つの zip にまとめる。受け入れ: 送信待ち1件・送信できない1件・送信済み1件の outbox から、前2件だけが入った zip ができる。
3. 対象範囲は画面の件数と揃える。現在の配送経路の `Scope` に従い、`ReportsAndProgress`（Steam）ならバグ報告と進行記録の両方、`ReportsOnly`（開発者トークン）ならバグ報告だけ。受け入れ: 範囲 `ReportsOnly` のとき、進行記録の失敗箱は印が消えず、zip にも入らない。
4. zip はデスクトップに `moorestech-reports-yyyyMMdd-HHmmss.zip` として作る。中身は `reports/<id>/...`・`progress/<id>/...`（箱の中身と印ファイル。`UPLOAD_LOCK` は除く）と、ルートの `export.json`（書き出し日時・箱一覧・欠落一覧）。作った後、Windows は `explorer.exe /select,"<zip>"`、macOS は `open -R <zip>` でその zip を選んだ状態で開く。受け入れ: zip を開くと上の配置で、`export.json` に全箱が載っている。
5. 読めない箱やファイルがあっても、読める分だけで zip を作る。入れられなかった項目と理由を `export.json` の `missing` と画面に出す。受け入れ: 読めないファイルを1つ含む箱を書き出すと、その1ファイルだけが抜けて `missing` に載り、画面に「1件は入れられませんでした」と出る。
6. 書き出し中・完了・失敗を画面に出す。完了時は zip のパスと「Google ドライブ等に上げて、共有リンクを開発者へ送ってください」を4言語（英・日・独・韓）で案内する。送り先の具体名は書かない。書き出し中の二度押しは無視してログに出す。受け入れ: vitest で4言語とも文言が出て、未翻訳キーが残らない。
7. 書き出した後も、箱は outbox に残す（消さない・印を変えない）。
8. デスクトップを取れない、zip を書けない（容量・権限）ときは書き出しを失敗にし、理由を画面と `Debug.LogWarning` に出す。途中まで書いた zip は消す。
9. 開発側の取り込みスクリプト `scripts/playtest/import-export-zip.sh <zip>` を作る。各箱を `harness/playtest/<reports|progress>/<steamId>/<id>` に置き、受け口経由と同じ `READY`（files[] 入り）と `ingest.json`（表示名入り）を付け、コミット資料を復元する。steamId はバグ報告なら `manifest.json`、進行記録なら `record.json` の `steamId` から取る。既に同じ場所に箱がある報告は上書きせず飛ばす。受け入れ: テスト用 zip を取り込むと `find-reports.sh` の取り込み済み一覧に出る。
10. 置き場はポーズ中のバグ報告画面だけ。タイトル画面には置かない。

**やらないこと:** 失敗理由を受け口へ自動で送ること／タイトル画面への配置／zip の自動アップロード（Drive 等）／書き出し後の箱の削除／再送できなかった箱だけを別に表示する画面。

## Global Constraints

- C# は1ファイル200行未満。1ディレクトリのコードは10ファイルまで。`partial`・`Func<>`・デフォルト引数は使わない。イベントは UniRx（`Action` イベント禁止）。
- try-catch は外部境界（ディスクIO・外部プロセス起動）だけで使う。境界である根拠をコメントに書き、catch した失敗はログと欠損表明（画面の状態・`export.json` の `missing`）の両方に残す。
- fail-closed で何もしない経路（二度押しの無視・解除できない印）は、理由を `Debug.LogWarning` に出す。
- コメントは主要セクションごとに日本語1行→英語1行。
- 時刻: zip 名と `export.json` の `exportedAt` は実世界の日時の記録なので `DateTime` を使ってよい（AGENTS.md の DateTime 例外）。
- `System.Environment` は `Client.*` の名前空間と衝突するため完全修飾する（前例: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/LastSession/CrashDumpLocator.cs:30-33`）。
- `.meta` は手で作らない。Unity にコンパイルさせて生成させる。
- localization.csv の列は `key,Source,english,japanese,german,korean`。`Source` には英語と同じ文を入れる。置換は `{name}` 形式。CSV を変えたら `moorestech_web/webui` で `pnpm gen:i18n` を実行する。
- 実機の outbox（`~/Library/Application Support/moorestech/BugReports/outbox`）の既存の箱を変えない。テストは一時ディレクトリで組んだ `PlaytestOutboxDirectories` を使う。

## 設計検査記録

- 配置検査（spec-architecture-review Phase 1〜2.5）: 実施済み / 違反2件・修正2件 / Upload/・Actions/ の10ファイル上限超過をサブディレクトリ Upload/Outbox/・Actions/BugReport/ で解消。死活表で死ぬ操作なし
- Phase 2.6（型閉包・重複・ADR矛盾）: 実施済み / 強4・弱4・第3バケツ3 / 強3件は前例同形でシミュレーター確信高の前提宣言、文言検査は全7文言へ拡大、弱のうち失敗印解除とscope判定を一本化、Func<>相当のラムダを除去

---

## 配置と前例

| 項目 | 配置 | 前例 |
|---|---|---|
| 箱の状態判定 `PlaytestOutboxBoxState` | `Client.PlaytestReceiver/Upload/Outbox/` | `PlaytestOutboxScanner.ReadDeliveryInventory` の判定をそのまま移す |
| 再送・書き出し本体 | `Client.PlaytestReceiver/Recovery/`（新設） | 送信要求の押し場 `PlaytestUploadRunner`、outbox の排他 `PlaytestOutboxAccess.Gate` |
| 書き出し状態の板 | `Recovery/PlaytestExportStatus.cs` の static `ReactiveProperty` | `Delivery/PlaytestDeliveryStatusBoard.cs` |
| 外部プロセスでの選択表示 | `Recovery/OsFileReveal.cs`（try-catch 境界） | `Client.Game/InGame/BugReport/Recording/FfmpegProcess.cs` の `Process.Start` |
| WebUI action | `Client.WebUiHost/Game/Actions/BugReport/BugReportRecoveryActions.cs` | `PauseMenuActions.cs` の payload 無し handler |
| topic への載せ方 | `PauseMenuTopic` に `Export` フィールドと購読を追加 | 同 topic の `Delivery`（`PlaytestDeliveryDto`） |
| DI | `MainGameModelRegistration.cs` の `PlaytestUploadRunner` 登録の直後 | 同所 |
| webui の型 | `bridge/contract/schemas/ui.ts` の zod 手書きスキーマ | `DeliveryStatusSchema` |
| 取り込みスクリプト | `scripts/playtest/import-export-zip.sh` + `lib/import_export_zip.py` | `scripts/playtest/ingest.sh` の `ingest_one` |

再送の機構は2案を比べた。(a) 本案: 門の下で失敗印を外し、既存の `RequestUpload` に任せる。(b) 走行役 `PlaytestUploadRunner` に「失敗箱も対象にする」モードを足す。(b) は走行役と `ScanPending` に分岐が増え、印は残ったままなので次回以降の自動走行では再び飛ばされる。(a) は既存の走行をそのまま使い、手動手順（README「隔離された報告の再送」）と同じ結果になるため (a) を採る。

サーバー可変状態の同期ではない（クライアント内の状態を自前の Web UI に出すだけ）ため、イベントパケットは使わない。topic の初期データ（`GetSnapshotJsonAsync`）と購読（`Skip(1).Subscribe`）は既存の `PauseMenuTopic` に相乗りする。

---

### Task 1: 箱の状態判定を1か所に集める

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/Outbox/PlaytestOutboxBoxState.cs`
- Move（`git mv`。名前空間 `Client.PlaytestReceiver.Upload` は変えない）: `Upload/PlaytestOutboxScanner.cs`・`Upload/PlaytestOutboxBox.cs`・`Upload/PlaytestOutboxDirectories.cs` → `Upload/Outbox/`。`Upload/` 直下は既に10ファイルあり、新ファイルを足すと上限を超えるため（前例: `Upload/Attempt/`・`Upload/Synchronization/` のサブディレクトリ分割）。`.meta` は `git mv` で一緒に動かす（Unity がGUIDを保つ）
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/Outbox/PlaytestOutboxScanner.cs`（`ReadDeliveryInventory` の `Count` を新しい判定へ置き換える。`ReadMarkers`・`IsDelivered` は `Scan` が使うので残す）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Upload/PlaytestOutboxBoxStateTest.cs`

**Interfaces:**
- Produces:
  - `public enum PlaytestOutboxBoxKind { NotReady, Delivered, Pending, Failed }`
  - `public static class PlaytestOutboxBoxState { public static PlaytestOutboxBoxKind Classify(string boxDirectory); }`。ディスクの例外（`IOException`・`UnauthorizedAccessException`）はそのまま呼び手へ投げる。
  - `public static IReadOnlyList<string> PlaytestOutboxScanner.EnumerateBoxDirectories(string outbox)`（既存の private `EnumerateOutboxDirectories` を public に改名。未作成の outbox は空）

- [ ] **Step 1: テストを書く**

```csharp
using System.IO;
using Client.PlaytestReceiver.Upload;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Upload
{
    public class PlaytestOutboxBoxStateTest
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "box-state-" + Path.GetRandomFileName());
            Directory.CreateDirectory(_root);
        }

        [TearDown]
        public void TearDown() { Directory.Delete(_root, true); }

        [TestCase(new string[0], PlaytestOutboxBoxKind.NotReady)]
        [TestCase(new[] { "READY" }, PlaytestOutboxBoxKind.Pending)]
        [TestCase(new[] { "READY", "UPLOAD_FAILED" }, PlaytestOutboxBoxKind.Failed)]
        [TestCase(new[] { "READY", "UPLOADED" }, PlaytestOutboxBoxKind.Delivered)]
        [TestCase(new[] { "READY", "SHIPPED" }, PlaytestOutboxBoxKind.Delivered)]
        [TestCase(new[] { "READY", "UPLOADED", "UPLOAD_FAILED" }, PlaytestOutboxBoxKind.Delivered)]
        public void ClassifiesByRootMarkers(string[] markers, PlaytestOutboxBoxKind expected)
        {
            var box = Directory.CreateDirectory(Path.Combine(_root, "box")).FullName;
            foreach (var marker in markers) File.WriteAllText(Path.Combine(box, marker), "");
            Assert.AreEqual(expected, PlaytestOutboxBoxState.Classify(box));
        }

        [Test]
        public void FailureMarkerDirectoryCountsAsFailed()
        {
            // 印を書けない同名ディレクトリも失敗として数える（既存の件数表示と同じ判定）
            // A directory occupying the failure marker name still counts as failed, as the inventory did
            var box = Directory.CreateDirectory(Path.Combine(_root, "box")).FullName;
            File.WriteAllText(Path.Combine(box, "READY"), "");
            Directory.CreateDirectory(Path.Combine(box, "UPLOAD_FAILED"));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("failure marker is a directory"));
            Assert.AreEqual(PlaytestOutboxBoxKind.Failed, PlaytestOutboxBoxState.Classify(box));
        }
    }
}
```

- [ ] **Step 2: 実装を書く**

`PlaytestOutboxBoxState.cs`:

```csharp
using System.IO;
using System.Linq;

namespace Client.PlaytestReceiver.Upload
{
    public enum PlaytestOutboxBoxKind { NotReady, Delivered, Pending, Failed }

    // 箱1つの配送状態の正本。件数表示・再送・書き出しはここだけで箱を分類する
    // The single source of a box's delivery state; inventory, resend and export classify boxes only here
    public static class PlaytestOutboxBoxState
    {
        public static PlaytestOutboxBoxKind Classify(string boxDirectory)
        {
            // File.Existsは権限拒否もfalseへ畳むため、箱の読取成功を先に確定する
            // File.Exists folds access denial into false, so verify box enumeration before classifying markers
            var markers = Directory.GetFiles(boxDirectory).Select(Path.GetFileName).ToArray();
            if (!markers.Contains(PlaytestOutboxScanner.ReadyMarker)) return PlaytestOutboxBoxKind.NotReady;
            if (markers.Contains(PlaytestOutboxScanner.UploadedMarker) || markers.Contains(PlaytestOutboxScanner.ShippedMarker)) return PlaytestOutboxBoxKind.Delivered;

            // 印を書けない同名ディレクトリも失敗として画面へ残す
            // A directory blocking the failure marker also remains visible as failed
            var blockedMarker = Directory.Exists(Path.Combine(boxDirectory, PlaytestOutboxScanner.FailedMarker));
            if (blockedMarker) UnityEngine.Debug.LogWarning($"[PlaytestReceiver] failure marker is a directory: {boxDirectory}");
            return blockedMarker || markers.Contains(PlaytestOutboxScanner.FailedMarker) ? PlaytestOutboxBoxKind.Failed : PlaytestOutboxBoxKind.Pending;
        }
    }
}
```

`Upload/Outbox/PlaytestOutboxScanner.cs` の変更（移動後）:
- 定数 `public const string ShippedMarker = "SHIPPED";` を追加し、`Markers` 配列の `"SHIPPED"` をこの定数に置き換える。
- `EnumerateOutboxDirectories` を `public static IReadOnlyList<string> EnumerateBoxDirectories(string outbox)` に改名・公開する（中身はそのまま）。呼び出し箇所もすべて改名する。
- `ReadDeliveryInventory` 内の `Count` を次に置き換える（`ReadMarkers` を使う他の箇所はそのまま残す）。

```csharp
            void Count(string outbox)
            {
                // 存在しないoutboxはまだ報告を作っていない状態
                // An absent outbox means no report has been created yet
                foreach (var directory in EnumerateBoxDirectories(outbox))
                {
                    switch (PlaytestOutboxBoxState.Classify(directory))
                    {
                        case PlaytestOutboxBoxKind.Failed: failed++; break;
                        case PlaytestOutboxBoxKind.Pending: pending++; break;
                    }
                }
            }
```

- `IsDelivered(string[] markers)` が `Scan` からも使われている場合は残し、本体を `markers.Contains(UploadedMarker) || markers.Contains(ShippedMarker)` にする。

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaytestOutboxBoxStateTest|PlaytestOutboxScannerTest|PlaytestUploadRunnerTest|PauseMenuTopicDeliveryTest"`
Expected: ErrorCount 0 / 全 PASS（既存の件数表示テストが変わらず通る）

- [ ] **Step 4: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Upload
git commit -m "refactor(playtest): 箱の配送状態の判定を PlaytestOutboxBoxState に集める"
```

---

### Task 2: 失敗印の解除と再送要求

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Recovery/PlaytestFailedBoxRelease.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Recovery/PlaytestOutboxRecovery.cs`（再送の入口。Task 3 で書き出しの入口を足す）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（`PlaytestUploadRunner` 登録の直後に `builder.Register<PlaytestOutboxRecovery>(Lifetime.Singleton);`。Task 3 の依存登録もここ）
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Upload/Outbox/PlaytestFailureMarkers.cs`（失敗印と試行回数を消す処理の正本）
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/BoxFilePolicies/CommitTransfer/CommitEvidenceTransfer.cs:34-35` と `moorestech_client/Assets/Scripts/Client.Starter/Playtest/PlaytestExistingReportEvidence.cs:35-36`（2行の `File.Delete` を `PlaytestFailureMarkers.Clear(<箱>)` 1行へ置き換える）
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Delivery/PlaytestDeliveryRoute.cs`（`PlaytestOutboxScopes.IncludesProgress` を追加）と `Upload/Outbox/PlaytestOutboxScanner.cs` の `scope == PlaytestOutboxScope.ReportsAndProgress` 2か所をそれへ置き換える
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Recovery/PlaytestFailedBoxReleaseTest.cs`

**Interfaces:**
- Consumes: `PlaytestOutboxBoxState.Classify`、`PlaytestOutboxScanner.EnumerateBoxDirectories`（Task 1）、`PlaytestOutboxAccess.Gate`、`IPlaytestUploadRequester.RequestUpload()`、`PlaytestDeliveryRouteResolver.ResolveCurrent().Scope`
- Produces:
  - `public static class PlaytestFailedBoxRelease { public static int ReleaseAll(PlaytestOutboxDirectories directories, PlaytestOutboxScope scope); }`。戻り値は解除できた箱の数。呼び手が `PlaytestOutboxAccess.Gate` を持っている前提。
  - `public sealed class PlaytestOutboxRecovery { public void RequestResendFailed(); internal UniTask ResendFailedAsync(PlaytestOutboxScope scope); }`
  - `public static class PlaytestFailureMarkers { public static void Clear(string boxDirectory); }`。`UPLOAD_FAILED` と `UPLOAD_ATTEMPTS` のファイルを消す（無ければ何もしない）。IO の例外は呼び手へ投げる。
  - `public static class PlaytestOutboxScopes { public static bool IncludesProgress(this PlaytestOutboxScope scope); }`（`ReportsAndProgress` なら true）

- [ ] **Step 1: テストを書く**

```csharp
using System.IO;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.PlaytestReceiver.Upload;
using Client.PlaytestReceiver.BoxFilePolicies;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Recovery
{
    public class PlaytestFailedBoxReleaseTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "release-" + Path.GetRandomFileName());
            _directories = new PlaytestOutboxDirectories(Path.Combine(_root, "reports"), new BugReportBoxFilePolicy(), Path.Combine(_root, "progress"), new ProgressRecordBoxFilePolicy());
        }

        [TearDown]
        public void TearDown() { Directory.Delete(_root, true); }

        [Test]
        public void RemovesOnlyFailureAndAttemptMarkers()
        {
            var box = MakeBox("reports", "a", "READY", "UPLOAD_FAILED", "UPLOAD_ATTEMPTS", "UPLOAD_DECLARED", "UPLOAD_SKIPPED");
            Assert.AreEqual(1, PlaytestFailedBoxRelease.ReleaseAll(_directories, PlaytestOutboxScope.ReportsAndProgress));
            CollectionAssert.AreEquivalent(new[] { "READY", "UPLOAD_DECLARED", "UPLOAD_SKIPPED", "payload.txt" }, Names(box));
        }

        [Test]
        public void LeavesPendingAndDeliveredBoxesUntouched()
        {
            var pending = MakeBox("reports", "p", "READY");
            var delivered = MakeBox("reports", "d", "READY", "UPLOADED", "UPLOAD_FAILED");
            Assert.AreEqual(0, PlaytestFailedBoxRelease.ReleaseAll(_directories, PlaytestOutboxScope.ReportsAndProgress));
            CollectionAssert.AreEquivalent(new[] { "READY", "payload.txt" }, Names(pending));
            CollectionAssert.AreEquivalent(new[] { "READY", "UPLOADED", "UPLOAD_FAILED", "payload.txt" }, Names(delivered));
        }

        [Test]
        public void ReportsOnlyScopeKeepsProgressFailures()
        {
            MakeBox("reports", "r", "READY", "UPLOAD_FAILED");
            var progress = MakeBox("progress", "g", "READY", "UPLOAD_FAILED");
            Assert.AreEqual(1, PlaytestFailedBoxRelease.ReleaseAll(_directories, PlaytestOutboxScope.ReportsOnly));
            Assert.IsTrue(File.Exists(Path.Combine(progress, "UPLOAD_FAILED")));
        }

        [Test]
        public void MarkerDirectoryIsReportedAndKept()
        {
            var box = MakeBox("reports", "x", "READY");
            Directory.CreateDirectory(Path.Combine(box, "UPLOAD_FAILED"));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("failure marker is a directory"));
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("cannot release"));
            Assert.AreEqual(0, PlaytestFailedBoxRelease.ReleaseAll(_directories, PlaytestOutboxScope.ReportsAndProgress));
            Assert.IsTrue(Directory.Exists(Path.Combine(box, "UPLOAD_FAILED")));
        }

        [Test]
        public void MissingOutboxReleasesNothing()
        {
            Assert.AreEqual(0, PlaytestFailedBoxRelease.ReleaseAll(_directories, PlaytestOutboxScope.ReportsAndProgress));
        }

        private string MakeBox(string outbox, string id, params string[] markers)
        {
            var box = Directory.CreateDirectory(Path.Combine(_root, outbox, id)).FullName;
            File.WriteAllText(Path.Combine(box, "payload.txt"), "x");
            foreach (var marker in markers) File.WriteAllText(Path.Combine(box, marker), "reason");
            return box;
        }

        private static string[] Names(string box)
        {
            return System.Array.ConvertAll(Directory.GetFiles(box), Path.GetFileName);
        }
    }
}
```

- [ ] **Step 2: 実装を書く**

`PlaytestFailedBoxRelease.cs`:

```csharp
using System;
using System.IO;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    // 送れなかった箱の失敗印と試行回数だけを外し、次の走行の対象へ戻す。宣言済み・見送りの記録は残す
    // Removes only the failure and attempt markers so the next run picks the box up again; declaration and skip records stay
    public static class PlaytestFailedBoxRelease
    {
        public static int ReleaseAll(PlaytestOutboxDirectories directories, PlaytestOutboxScope scope)
        {
            var released = Release(directories.ReportOutbox);
            if (scope.IncludesProgress()) released += Release(directories.ProgressOutbox);
            Debug.Log($"[PlaytestReceiver] released {released} failed box(es) for resend");
            return released;

            #region Internal

            int Release(string outbox)
            {
                var count = 0;
                foreach (var box in PlaytestOutboxScanner.EnumerateBoxDirectories(outbox))
                {
                    if (IsFailed(box) && TryRelease(box)) count++;
                }
                return count;
            }

            bool IsFailed(string box)
            {
                // 外部ディスク境界。読めない箱1つで残りの箱の再送を止めない
                // External disk boundary: one unreadable box never stops the rest from being resent
                try { return PlaytestOutboxBoxState.Classify(box) == PlaytestOutboxBoxKind.Failed; }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"[PlaytestReceiver] cannot release {box}: {error.Message}");
                    return false;
                }
            }

            bool TryRelease(string box)
            {
                var failed = Path.Combine(box, PlaytestOutboxScanner.FailedMarker);
                // 印の位置を塞ぐディレクトリは消さない。中身が箱の資料かもしれないため
                // A directory occupying the marker is never deleted because it may hold box payload
                if (Directory.Exists(failed))
                {
                    Debug.LogWarning($"[PlaytestReceiver] cannot release {box}: the failure marker is a directory");
                    return false;
                }
                // 外部ディスクIO境界。ロック・権限で消せない箱は失敗のまま残り、件数表示に出続ける
                // External disk IO boundary: a box that cannot be unmarked stays failed and keeps showing in the count
                try
                {
                    PlaytestFailureMarkers.Clear(box);
                    return true;
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Debug.LogWarning($"[PlaytestReceiver] cannot release {box}: {error.Message}");
                    return false;
                }
            }

            #endregion
        }
    }
}
```

`Upload/Outbox/PlaytestFailureMarkers.cs`:

```csharp
using System.IO;

namespace Client.PlaytestReceiver.Upload
{
    // 失敗印と試行回数を外して箱を次の走行へ戻す処理の正本。再送・容量保留の解除・起動時補完が共有する
    // The single routine that removes the failure and attempt markers; resend, capacity-hold release and startup completion share it
    public static class PlaytestFailureMarkers
    {
        public static void Clear(string boxDirectory)
        {
            File.Delete(Path.Combine(boxDirectory, PlaytestOutboxScanner.FailedMarker));
            File.Delete(Path.Combine(boxDirectory, PlaytestOutboxScanner.AttemptsMarker));
        }
    }
}
```

`Delivery/PlaytestDeliveryRoute.cs` に追加:

```csharp
    // 範囲に進行記録が入るかの判定はここだけで行う
    // The only place deciding whether a scope includes progress records
    public static class PlaytestOutboxScopes
    {
        public static bool IncludesProgress(this PlaytestOutboxScope scope) { return scope == PlaytestOutboxScope.ReportsAndProgress; }
    }
```

既存の `CommitEvidenceTransfer.cs:34-35` と `PlaytestExistingReportEvidence.cs:35-36` の2行の `File.Delete` を `PlaytestFailureMarkers.Clear(directory)`（後者は `PlaytestFailureMarkers.Clear(box.Directory)`）に置き換える。`PlaytestOutboxScanner.cs` の `if (scope == PlaytestOutboxScope.ReportsAndProgress)` 2か所を `if (scope.IncludesProgress())` に置き換える。

`PlaytestOutboxRecovery.cs`（Task 2 時点）:

```csharp
using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    // バグ報告画面からの再送・書き出しの入口。outboxへの書き込みは配送と同じ門で直列化する
    // Entry point for resend and export from the bug-report page; outbox writes share the delivery gate
    public sealed class PlaytestOutboxRecovery
    {
        private readonly IPlaytestUploadRequester _uploadRequester;
        private readonly PlaytestOutboxDirectories _directories;

        public PlaytestOutboxRecovery(IPlaytestUploadRequester uploadRequester, PlaytestOutboxDirectories directories)
        {
            _uploadRequester = uploadRequester;
            _directories = directories;
        }

        public void RequestResendFailed()
        {
            ResendFailedAsync(PlaytestDeliveryRouteResolver.ResolveCurrent().Scope).Forget();
        }

        internal async UniTask ResendFailedAsync(PlaytestOutboxScope scope)
        {
            // 走行中の配送が終わるまで待ってから印を外す。走行が箱を数えている最中に印を変えない
            // Wait for any in-flight run before unmarking so markers never change under a counting run
            await PlaytestOutboxAccess.Gate.WaitAsync(Application.exitCancellationToken);
            try { PlaytestFailedBoxRelease.ReleaseAll(_directories, scope); }
            finally { PlaytestOutboxAccess.Gate.Release(); }
            // 解除できた数に関わらず要求する。走行が件数表示を最新にする
            // Request regardless of the released count; the run refreshes the displayed counts
            _uploadRequester.RequestUpload();
        }
    }
}
```

`try { } finally { }` は catch を持たない門の解放保証で、try-catch 規約の対象外（前例: `PlaytestUploadRunner.cs` の `finally`）。

`ResendFailedAsync` のテストを `PlaytestFailedBoxReleaseTest` に1本追加する。偽の `IPlaytestUploadRequester`（呼ばれた回数を数えるだけ）を使い、`ResendFailedAsync(PlaytestOutboxScope.ReportsAndProgress).GetAwaiter().GetResult()` の後に、印が消えていることと `RequestUpload` が1回だけ呼ばれたことを確かめる。

```csharp
        private sealed class CountingRequester : Client.Game.InGame.BugReport.Submit.IPlaytestUploadRequester
        {
            public int Count { get; private set; }
            public void RequestUpload() { Count++; }
        }

        [Test]
        public void ResendReleasesThenRequestsUploadOnce()
        {
            var box = MakeBox("reports", "a", "READY", "UPLOAD_FAILED");
            var requester = new CountingRequester();
            new PlaytestOutboxRecovery(requester, _directories).ResendFailedAsync(PlaytestOutboxScope.ReportsAndProgress).GetAwaiter().GetResult();
            Assert.IsFalse(File.Exists(Path.Combine(box, "UPLOAD_FAILED")));
            Assert.AreEqual(1, requester.Count);
        }
```

（Task 3 でコンストラクタ引数が増えたら、このテストの `new PlaytestOutboxRecovery(...)` も Task 3 の形に合わせる。）

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type class --filter-value "Client.Tests.PlaytestReceiver"`（既存の容量保留・起動時補完のテストも通ること）
Expected: ErrorCount 0 / PASS

- [ ] **Step 4: コミットする**

```bash
git add -A moorestech_client/Assets/Scripts/Client.PlaytestReceiver moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver moorestech_client/Assets/Scripts/Client.Starter
git commit -m "feat(playtest): 送信できない報告の失敗印を外して再送を要求する"
```

---

### Task 3: 未送信の箱の zip 書き出し

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Recovery/PlaytestUnsentZipWriter.cs`
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Recovery/PlaytestExportStatus.cs`（状態クラス群と板）
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Recovery/PlaytestExportDestination.cs`（`IPlaytestExportDestination` と `DesktopExportDestination`）
- Create: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Recovery/OsFileReveal.cs`（`IPlaytestFileReveal` と `OsFileReveal`）
- Modify: `moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Recovery/PlaytestOutboxRecovery.cs`（書き出しの入口を追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs`（`builder.Register<IPlaytestExportDestination, DesktopExportDestination>(Lifetime.Singleton); builder.Register<IPlaytestFileReveal, OsFileReveal>(Lifetime.Singleton);`）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Recovery/PlaytestUnsentZipWriterTest.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Recovery/PlaytestOutboxRecoveryExportTest.cs`

**Interfaces:**
- Consumes: Task 1 の `PlaytestOutboxBoxState.Classify`・`PlaytestOutboxScanner.EnumerateBoxDirectories`・`PlaytestOutboxScanner.LockMarker`・`FailedMarker`
- Produces:
  - `public sealed class PlaytestExportMissing { public readonly string Item; public readonly string Reason; public PlaytestExportMissing(string item, string reason); }`
  - `public abstract class PlaytestExportStatus`、派生 `PlaytestExportIdle`（`Instance`）、`PlaytestExportRunning`（`Instance`）、`PlaytestExportDone(string zipPath, int boxCount, IReadOnlyList<PlaytestExportMissing> missing, bool revealed)`、`PlaytestExportFailed(string reason)`。各値は `public readonly` フィールド。
  - `public static class PlaytestExportStatusBoard { public static IReadOnlyReactiveProperty<PlaytestExportStatus> Current; internal static void Set(PlaytestExportStatus status); }`（PlayMode 開始時に Idle へ戻す）
  - `public sealed class PlaytestUnsentZipResult { public readonly int BoxCount; public readonly IReadOnlyList<PlaytestExportMissing> Missing; }`
  - `public static class PlaytestUnsentZipWriter { public static PlaytestUnsentZipResult Write(PlaytestOutboxDirectories directories, PlaytestOutboxScope scope, string zipPath, DateTime exportedAtUtc); }`。zip 自体を作れない・書けない失敗（`IOException`・`UnauthorizedAccessException`）は呼び手へ投げる。箱・ファイル単位の読取失敗は `Missing` に入れて続ける。
  - `public interface IPlaytestExportDestination { string ResolveDirectory(); }`。取れなければ `null` を返す。
  - `public interface IPlaytestFileReveal { bool Reveal(string filePath); }`。開けなければ `false`。
  - `PlaytestOutboxRecovery` のコンストラクタを `(IPlaytestUploadRequester uploadRequester, PlaytestOutboxDirectories directories, IPlaytestExportDestination destination, IPlaytestFileReveal reveal)` に変える。
  - `public void PlaytestOutboxRecovery.RequestExportUnsent()`、`internal string BeginExport()`（二度押し・書き出し先なしは null）、`internal PlaytestExportStatus PackUnsent(PlaytestOutboxScope scope, string zipPath, DateTime exportedAtUtc)`（`Done`（revealed=false）か `Failed`）、`internal void FinishExport(PlaytestExportStatus packed, string zipPath)`、`internal static void FailIfStillRunning()`

**zip の中身:**
- `reports/<箱名>/<相対パス>`・`progress/<箱名>/<相対パス>`。対象は `Classify` が `Pending` または `Failed` の箱。箱直下の `UPLOAD_LOCK` は入れない（他プロセスが排他で開いている可能性があり、中身に意味がない）。他の印（`READY`・`UPLOAD_FAILED`・`UPLOAD_ATTEMPTS`・`UPLOAD_DECLARED`・`UPLOAD_SKIPPED`）は失敗理由の手がかりなので入れる。
- ルートの `export.json`:

```json
{
  "schemaVersion": 1,
  "exportedAt": "2026-10-27T11:00:00Z",
  "boxes": [ { "kind": "reports", "id": "20261019_150326_9c89b48b", "state": "failed" } ],
  "missing": [ { "item": "reports/20261019_150326_9c89b48b/video.mp4", "reason": "IOException: ..." } ]
}
```

- `state` は `pending` か `failed`。`missing` の `item` は zip 内の想定パス。outbox ごと読めないときは `item` を `reports` か `progress` にする。

- [ ] **Step 1: テストを書く**

`PlaytestUnsentZipWriterTest.cs`:

```csharp
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Client.PlaytestReceiver.BoxFilePolicies;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.PlaytestReceiver.Upload;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Recovery
{
    public class PlaytestUnsentZipWriterTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;
        private string _zip;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "export-" + Path.GetRandomFileName());
            _directories = new PlaytestOutboxDirectories(Path.Combine(_root, "reports"), new BugReportBoxFilePolicy(), Path.Combine(_root, "progress"), new ProgressRecordBoxFilePolicy());
            _zip = Path.Combine(_root, "out.zip");
        }

        [TearDown]
        public void TearDown() { Directory.Delete(_root, true); }

        [Test]
        public void IncludesPendingAndFailedButNotDelivered()
        {
            MakeBox("reports", "pending1", "READY");
            MakeBox("reports", "failed1", "READY", "UPLOAD_FAILED");
            MakeBox("reports", "sent1", "READY", "UPLOADED");
            MakeBox("progress", "progress1", "READY");

            var result = PlaytestUnsentZipWriter.Write(_directories, PlaytestOutboxScope.ReportsAndProgress, _zip, new DateTime(2026, 10, 27, 11, 0, 0, DateTimeKind.Utc));

            Assert.AreEqual(3, result.BoxCount);
            CollectionAssert.IsEmpty(result.Missing);
            using var archive = ZipFile.OpenRead(_zip);
            var names = archive.Entries.Select(e => e.FullName).ToList();
            CollectionAssert.Contains(names, "reports/pending1/payload.txt");
            CollectionAssert.Contains(names, "reports/failed1/UPLOAD_FAILED");
            CollectionAssert.Contains(names, "progress/progress1/payload.txt");
            Assert.IsFalse(names.Any(n => n.StartsWith("reports/sent1/")));
            Assert.IsFalse(names.Any(n => n.EndsWith("/UPLOAD_LOCK")));
            var meta = JObject.Parse(new StreamReader(archive.GetEntry("export.json").Open()).ReadToEnd());
            Assert.AreEqual("2026-10-27T11:00:00Z", (string)meta["exportedAt"]);
            Assert.AreEqual(3, ((JArray)meta["boxes"]).Count);
        }

        [Test]
        public void ReportsOnlyScopeSkipsProgress()
        {
            MakeBox("reports", "r1", "READY");
            MakeBox("progress", "g1", "READY");
            var result = PlaytestUnsentZipWriter.Write(_directories, PlaytestOutboxScope.ReportsOnly, _zip, DateTime.UtcNow);
            Assert.AreEqual(1, result.BoxCount);
        }

        [Test]
        public void UnreadableFileIsListedAsMissingAndOthersStay()
        {
            var box = MakeBox("reports", "r1", "READY");
            var locked = Path.Combine(box, "locked.bin");
            File.WriteAllText(locked, "x");
            // 排他で開いたままにして読めないファイルを作る（Windowsの他プロセスのロックと同じ失敗）
            // Hold the file open exclusively so reading fails, as another process's lock would on Windows
            using (new FileStream(locked, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("export skipped"));
                var result = PlaytestUnsentZipWriter.Write(_directories, PlaytestOutboxScope.ReportsOnly, _zip, DateTime.UtcNow);
                Assert.AreEqual(1, result.BoxCount);
                Assert.AreEqual("reports/r1/locked.bin", result.Missing.Single().Item);
            }
            using var archive = ZipFile.OpenRead(_zip);
            Assert.IsNotNull(archive.GetEntry("reports/r1/payload.txt"));
        }

        private string MakeBox(string outbox, string id, params string[] markers)
        {
            var box = Directory.CreateDirectory(Path.Combine(_root, outbox, id)).FullName;
            File.WriteAllText(Path.Combine(box, "payload.txt"), "x");
            File.WriteAllText(Path.Combine(box, "UPLOAD_LOCK"), "");
            foreach (var marker in markers) File.WriteAllText(Path.Combine(box, marker), "reason");
            return box;
        }
    }
}
```

macOS では `FileShare.None` で開いていても他のストリームから読めることがある。その場合、このテストは Windows でしか失敗を再現できない。macOS で `Missing` が空になったら、テストを `[Platform(Include = "Win")]` にせず、読取不能の再現を「ファイルの代わりに同名ディレクトリを置き、`File.OpenRead` が `UnauthorizedAccessException` を投げる」形に変える。macOS・Windows の両方で失敗するのはこちらなので、最初からこちらで書いてよい。

`PlaytestOutboxRecoveryExportTest.cs`（偽の書き出し先・偽の選択表示で、状態の遷移と失敗時の後始末を確かめる）。CI の batchmode では `UniTask.SwitchToThreadPool` からの復帰が戻らないため（`docs/development/async-and-ci-pitfalls.md`）、テストは門とスレッド切り替えを持たない同期の `internal` メソッド `PackUnsent` と `FinishExport` を直接呼ぶ:

```csharp
using System;
using System.IO;
using Client.PlaytestReceiver.BoxFilePolicies;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Recovery;
using Client.PlaytestReceiver.Upload;
using NUnit.Framework;

namespace Client.Tests.PlaytestReceiver.Recovery
{
    public class PlaytestOutboxRecoveryExportTest
    {
        private string _root;
        private PlaytestOutboxDirectories _directories;

        private sealed class FixedDestination : IPlaytestExportDestination
        {
            private readonly string _directory;
            public FixedDestination(string directory) { _directory = directory; }
            public string ResolveDirectory() { return _directory; }
        }

        private sealed class RecordingReveal : IPlaytestFileReveal
        {
            public string Revealed { get; private set; }
            public bool Reveal(string filePath) { Revealed = filePath; return true; }
        }

        private sealed class NoopRequester : Client.Game.InGame.BugReport.Submit.IPlaytestUploadRequester
        {
            public void RequestUpload() { }
        }

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "recovery-" + Path.GetRandomFileName());
            _directories = new PlaytestOutboxDirectories(Path.Combine(_root, "reports"), new BugReportBoxFilePolicy(), Path.Combine(_root, "progress"), new ProgressRecordBoxFilePolicy());
            PlaytestExportStatusBoard.ResetOnPlayMode();
        }

        [TearDown]
        public void TearDown() { Directory.Delete(_root, true); }

        [Test]
        public void SuccessfulExportPublishesDoneAndRevealsZip()
        {
            var box = Directory.CreateDirectory(Path.Combine(_root, "reports", "a")).FullName;
            File.WriteAllText(Path.Combine(box, "READY"), "");
            var desktop = Directory.CreateDirectory(Path.Combine(_root, "desktop")).FullName;
            var reveal = new RecordingReveal();
            var recovery = new PlaytestOutboxRecovery(new NoopRequester(), _directories, new FixedDestination(desktop), reveal);

            var zipPath = Path.Combine(desktop, "moorestech-reports-20261027-200000.zip");
            recovery.FinishExport(recovery.PackUnsent(PlaytestOutboxScope.ReportsAndProgress, zipPath, DateTime.UtcNow), zipPath);

            var done = (PlaytestExportDone)PlaytestExportStatusBoard.Current.Value;
            Assert.AreEqual(1, done.BoxCount);
            Assert.IsTrue(done.Revealed);
            Assert.AreEqual(zipPath, reveal.Revealed);
            Assert.IsTrue(File.Exists(zipPath));
            Assert.IsTrue(File.Exists(Path.Combine(box, "READY")), "書き出し後も箱は残る");
        }

        [Test]
        public void UnwritableDestinationFailsAndLeavesNoPartialZip()
        {
            var box = Directory.CreateDirectory(Path.Combine(_root, "reports", "a")).FullName;
            File.WriteAllText(Path.Combine(box, "READY"), "");
            // 書き出し先が存在しないディレクトリなら zip を作れない
            // A destination directory that does not exist cannot receive the zip
            var zipPath = Path.Combine(_root, "no-such-dir", "out.zip");
            var recovery = new PlaytestOutboxRecovery(new NoopRequester(), _directories, new FixedDestination(_root), new RecordingReveal());
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("export failed"));

            recovery.FinishExport(recovery.PackUnsent(PlaytestOutboxScope.ReportsAndProgress, zipPath, DateTime.UtcNow), zipPath);

            Assert.IsInstanceOf<PlaytestExportFailed>(PlaytestExportStatusBoard.Current.Value);
            Assert.IsFalse(File.Exists(zipPath));
        }

        [Test]
        public void MissingDesktopFailsBeforeRunning()
        {
            var recovery = new PlaytestOutboxRecovery(new NoopRequester(), _directories, new FixedDestination(null), new RecordingReveal());
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("export failed"));
            Assert.IsNull(recovery.BeginExport());
            Assert.IsInstanceOf<PlaytestExportFailed>(PlaytestExportStatusBoard.Current.Value);
        }

        [Test]
        public void SecondBeginWhileRunningIsIgnored()
        {
            var desktop = Directory.CreateDirectory(Path.Combine(_root, "desktop")).FullName;
            var recovery = new PlaytestOutboxRecovery(new NoopRequester(), _directories, new FixedDestination(desktop), new RecordingReveal());
            Assert.IsNotNull(recovery.BeginExport());
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("already running"));
            Assert.IsNull(recovery.BeginExport());
            Assert.IsInstanceOf<PlaytestExportRunning>(PlaytestExportStatusBoard.Current.Value);
        }

        [Test]
        public void AbandonedRunningFallsBackToFailed()
        {
            // 想定外の例外で走行が抜けても、ボタンが押せないまま固まらない
            // An unexpected exception leaving the run must not freeze the buttons in the running state
            var desktop = Directory.CreateDirectory(Path.Combine(_root, "desktop")).FullName;
            var recovery = new PlaytestOutboxRecovery(new NoopRequester(), _directories, new FixedDestination(desktop), new RecordingReveal());
            recovery.BeginExport();
            UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Warning, new System.Text.RegularExpressions.Regex("export failed"));
            PlaytestOutboxRecovery.FailIfStillRunning();
            Assert.IsInstanceOf<PlaytestExportFailed>(PlaytestExportStatusBoard.Current.Value);
        }
    }
}
```
- [ ] **Step 2: 状態と板を書く**

`PlaytestExportStatus.cs`:

```csharp
using System.Collections.Generic;
using UniRx;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    public sealed class PlaytestExportMissing
    {
        public readonly string Item;
        public readonly string Reason;
        public PlaytestExportMissing(string item, string reason) { Item = item; Reason = reason; }
    }

    public abstract class PlaytestExportStatus { }
    public sealed class PlaytestExportIdle : PlaytestExportStatus { public static readonly PlaytestExportIdle Instance = new(); }
    public sealed class PlaytestExportRunning : PlaytestExportStatus { public static readonly PlaytestExportRunning Instance = new(); }
    public sealed class PlaytestExportDone : PlaytestExportStatus
    {
        public readonly string ZipPath;
        public readonly int BoxCount;
        public readonly IReadOnlyList<PlaytestExportMissing> Missing;
        public readonly bool Revealed;
        public PlaytestExportDone(string zipPath, int boxCount, IReadOnlyList<PlaytestExportMissing> missing, bool revealed)
        { ZipPath = zipPath; BoxCount = boxCount; Missing = missing; Revealed = revealed; }
    }
    public sealed class PlaytestExportFailed : PlaytestExportStatus
    {
        public readonly string Reason;
        public PlaytestExportFailed(string reason) { Reason = reason; }
    }

    // 書き出しの進行をWeb UIへ渡す板。配送状況の板と同じく再生の跨ぎで初期値へ戻す
    // Board carrying export progress to the Web UI; like the delivery board it resets between play sessions
    public static class PlaytestExportStatusBoard
    {
        private static readonly ReactiveProperty<PlaytestExportStatus> _current = new(PlaytestExportIdle.Instance);
        public static IReadOnlyReactiveProperty<PlaytestExportStatus> Current => _current;
        internal static void Set(PlaytestExportStatus status) { _current.Value = status; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetOnPlayMode() { Set(PlaytestExportIdle.Instance); }
    }
}
```

テストアセンブリから `internal` が見えるか確認する。`Client.PlaytestReceiver/AssemblyInfo.cs` に `InternalsVisibleTo` が無ければ、既存テストが `ResetOnPlayMode` 等を呼ぶ前例（`PlaytestUploadRunnerTest`）と同じ形に合わせる。

- [ ] **Step 3: zip の書き手を書く**

`PlaytestUnsentZipWriter.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    public sealed class PlaytestUnsentZipResult
    {
        public readonly int BoxCount;
        public readonly IReadOnlyList<PlaytestExportMissing> Missing;
        public PlaytestUnsentZipResult(int boxCount, IReadOnlyList<PlaytestExportMissing> missing) { BoxCount = boxCount; Missing = missing; }
    }

    // 未送信の箱を提出用のzip 1本にまとめる。読めない箱・ファイルは飛ばして一覧に残す
    // Packs unsent boxes into one zip for hand-in; unreadable boxes and files are skipped and listed
    public static class PlaytestUnsentZipWriter
    {
        public static PlaytestUnsentZipResult Write(PlaytestOutboxDirectories directories, PlaytestOutboxScope scope, string zipPath, DateTime exportedAtUtc)
        {
            var boxes = new JArray();
            var missing = new List<PlaytestExportMissing>();
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                AddOutbox(archive, "reports", directories.ReportOutbox);
                if (scope.IncludesProgress()) AddOutbox(archive, "progress", directories.ProgressOutbox);
                WriteExportJson(archive);
            }
            return new PlaytestUnsentZipResult(boxes.Count, missing);

            #region Internal

            void AddOutbox(ZipArchive archive, string kind, string outbox)
            {
                IReadOnlyList<string> boxDirectories;
                // 外部ディスク境界。outboxごと読めなければその種別を欠落にして続ける
                // External disk boundary: an unreadable outbox is listed as missing and the export continues
                try { boxDirectories = PlaytestOutboxScanner.EnumerateBoxDirectories(outbox); }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Skip(kind, error);
                    return;
                }
                foreach (var box in boxDirectories) AddBox(archive, kind, box);
            }

            void AddBox(ZipArchive archive, string kind, string box)
            {
                var id = Path.GetFileName(box);
                PlaytestOutboxBoxKind state;
                string[] files;
                // 外部ディスク境界。箱を分類・列挙できなければ箱ごと欠落にする
                // External disk boundary: a box that cannot be classified or listed is missing as a whole
                try
                {
                    state = PlaytestOutboxBoxState.Classify(box);
                    if (state != PlaytestOutboxBoxKind.Pending && state != PlaytestOutboxBoxKind.Failed) return; // silent-ok: 送信済み・未完成の箱は対象外 / 件数表示にも出ない
                    files = Directory.GetFiles(box, "*", SearchOption.AllDirectories);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Skip($"{kind}/{id}", error);
                    return;
                }
                boxes.Add(new JObject { ["kind"] = kind, ["id"] = id, ["state"] = state == PlaytestOutboxBoxKind.Failed ? "failed" : "pending" });
                foreach (var file in files)
                {
                    var relative = PlaytestOutboxScanner.ToRelativePath(box, file);
                    if (relative == PlaytestOutboxScanner.LockMarker) continue; // silent-ok: 排他用の空ファイル / 提出物に意味がない
                    AddFile(archive, $"{kind}/{id}/{relative}", file);
                }
            }

            void AddFile(ZipArchive archive, string entryName, string file)
            {
                // 外部ディスク境界。他プロセスのロックや消失で読めないファイルは欠落にする
                // External disk boundary: files locked by another process or gone are listed as missing
                try
                {
                    using var source = File.OpenRead(file);
                    using var target = archive.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Fastest).Open();
                    source.CopyTo(target);
                }
                catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
                {
                    Skip(entryName, error);
                }
            }

            void Skip(string item, Exception error)
            {
                Debug.LogWarning($"[PlaytestReceiver] export skipped {item}: {error.Message}");
                missing.Add(new PlaytestExportMissing(item, $"{error.GetType().Name}: {error.Message}"));
            }

            void WriteExportJson(ZipArchive archive)
            {
                var missingJson = new JArray();
                foreach (var entry in missing) missingJson.Add(new JObject { ["item"] = entry.Item, ["reason"] = entry.Reason });
                var meta = new JObject
                {
                    ["schemaVersion"] = 1,
                    ["exportedAt"] = exportedAtUtc.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                    ["boxes"] = boxes,
                    ["missing"] = missingJson,
                };
                using var writer = new StreamWriter(archive.CreateEntry("export.json").Open());
                writer.Write(meta.ToString());
            }

            #endregion
        }
    }
}
```

注意: 1ファイルの読み出しが途中で失敗した場合、`CreateEntry` 済みの途中までのエントリが zip に残る。`AddFile` は `File.OpenRead` を `CreateEntry` より先に呼んでいるので、開けない失敗ではエントリは作られない。読み出し途中の失敗で途中までのエントリが残るのは許容し、`missing` に載ることで気づけるようにする（判断記録 D5）。

- [ ] **Step 4: 書き出し先・選択表示・入口を書く**

`PlaytestExportDestination.cs`:

```csharp
using System.IO;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    public interface IPlaytestExportDestination
    {
        // 書き出し先のディレクトリ。取れなければnull
        // Directory receiving the export, or null when unavailable
        string ResolveDirectory();
    }

    // テスターがそのままドラッグで提出できるようデスクトップへ置く
    // Put the zip on the desktop so testers can drag it straight into their upload
    public sealed class DesktopExportDestination : IPlaytestExportDestination
    {
        public string ResolveDirectory()
        {
            // Client.* の同名名前空間と衝突するため完全修飾する
            // Fully qualified because Client.* has a same-named namespace
            var desktop = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
            if (!string.IsNullOrEmpty(desktop) && Directory.Exists(desktop)) return desktop;
            Debug.LogWarning($"[PlaytestReceiver] desktop folder unavailable: '{desktop}'");
            return null;
        }
    }
}
```

`OsFileReveal.cs`:

```csharp
using System;
using System.ComponentModel;
using System.Diagnostics;

namespace Client.PlaytestReceiver.Recovery
{
    public interface IPlaytestFileReveal
    {
        // ファイルマネージャでファイルを選んだ状態で開く。開けなければfalse
        // Opens the file manager with the file selected; false when it could not
        bool Reveal(string filePath);
    }

    public sealed class OsFileReveal : IPlaytestFileReveal
    {
        public bool Reveal(string filePath)
        {
            // 外部プロセス起動境界。開けなくてもzipは出来ているので、パスを画面に出して続ける
            // External process boundary: the zip already exists, so a failure only leaves the path on screen
            try
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                Process.Start("explorer.exe", $"/select,\"{filePath}\"");
#elif UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
                Process.Start("open", $"-R \"{filePath}\"");
#else
                UnityEngine.Debug.LogWarning($"[PlaytestReceiver] reveal is not supported on this platform: {filePath}");
                return false;
#endif
                return true;
            }
            catch (Exception error) when (error is Win32Exception || error is InvalidOperationException)
            {
                UnityEngine.Debug.LogWarning($"[PlaytestReceiver] cannot reveal {filePath}: {error.Message}");
                return false;
            }
        }
    }
}
```

`PlaytestOutboxRecovery.cs` を次の形にする（Task 2 の再送はそのまま）。async の入口は門・スレッド切り替え・後始末だけを持ち、判断と書き出しは同期の `internal` メソッド（`BeginExport`・`PackUnsent`・`FinishExport`・`FailIfStillRunning`）に置く。テストはこの同期メソッドを呼ぶ（前例: `CrashBundleWriter.Write`。`docs/development/async-and-ci-pitfalls.md`）。

```csharp
using System;
using System.IO;
using Client.Game.InGame.BugReport.Submit;
using Client.PlaytestReceiver.Delivery;
using Client.PlaytestReceiver.Upload;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Client.PlaytestReceiver.Recovery
{
    // バグ報告画面からの再送・書き出しの入口。outboxへの読み書きは配送と同じ門で直列化する
    // Entry point for resend and export from the bug-report page; outbox access shares the delivery gate
    public sealed class PlaytestOutboxRecovery
    {
        private readonly IPlaytestUploadRequester _uploadRequester;
        private readonly PlaytestOutboxDirectories _directories;
        private readonly IPlaytestExportDestination _destination;
        private readonly IPlaytestFileReveal _reveal;

        public PlaytestOutboxRecovery(IPlaytestUploadRequester uploadRequester, PlaytestOutboxDirectories directories, IPlaytestExportDestination destination, IPlaytestFileReveal reveal)
        {
            _uploadRequester = uploadRequester;
            _directories = directories;
            _destination = destination;
            _reveal = reveal;
        }

        public void RequestResendFailed()
        {
            ResendFailedAsync(CurrentScope()).Forget();
        }

        public void RequestExportUnsent()
        {
            ExportUnsentAsync(CurrentScope()).Forget();
        }

        // 画面の件数と同じ範囲を対象にするため、現在の配送経路から範囲を引く
        // Use the current delivery route's scope so the targets match the displayed counts
        private static PlaytestOutboxScope CurrentScope()
        {
            return PlaytestDeliveryRouteResolver.ResolveCurrent().Scope;
        }

        internal async UniTask ResendFailedAsync(PlaytestOutboxScope scope)
        {
            // 走行中の配送が終わるまで待ってから印を外す。走行が箱を数えている最中に印を変えない
            // Wait for any in-flight run before unmarking so markers never change under a counting run
            await PlaytestOutboxAccess.Gate.WaitAsync(Application.exitCancellationToken);
            try { PlaytestFailedBoxRelease.ReleaseAll(_directories, scope); }
            finally { PlaytestOutboxAccess.Gate.Release(); }
            // 解除できた数に関わらず要求する。走行が件数表示を最新にする
            // Request regardless of the released count; the run refreshes the displayed counts
            _uploadRequester.RequestUpload();
        }

        private async UniTask ExportUnsentAsync(PlaytestOutboxScope scope)
        {
            var zipPath = BeginExport();
            if (zipPath == null) return;
            var exportedAt = DateTime.UtcNow;
            // 想定外の例外で抜けても書き出し中のまま固めない。理由はForgetのログとPlayer.logに出る
            // Never stay frozen in running after an unexpected exception; its reason reaches the Forget log and Player.log
            try
            {
                // 配送の走行と同時に箱を読まない。大きなzipはワーカーで書く
                // Never read boxes alongside an upload run; large zips are written on a worker
                await PlaytestOutboxAccess.Gate.WaitAsync(Application.exitCancellationToken);
                PlaytestExportStatus packed;
                try
                {
                    await UniTask.SwitchToThreadPool();
                    packed = PackUnsent(scope, zipPath, exportedAt);
                    await UniTask.SwitchToMainThread();
                }
                finally { PlaytestOutboxAccess.Gate.Release(); }
                FinishExport(packed, zipPath);
            }
            finally
            {
                FailIfStillRunning();
            }
        }

        // 二度押しと書き出し先の欠如をここで止め、書き出し中へ進める。進めないときはnull
        // Stops a second press and a missing destination here, then enters running; null when it cannot start
        internal string BeginExport()
        {
            if (PlaytestExportStatusBoard.Current.Value is PlaytestExportRunning)
            {
                Debug.LogWarning("[PlaytestReceiver] export already running; ignoring the second request");
                return null;
            }
            var directory = _destination.ResolveDirectory();
            if (directory == null)
            {
                Fail("desktop folder unavailable");
                return null;
            }
            PlaytestExportStatusBoard.Set(PlaytestExportRunning.Instance);
            return Path.Combine(directory, $"moorestech-reports-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
        }

        // zipを書く。zip自体を書けなければ途中のzipを消して失敗を返す。どのスレッドからでも呼べる
        // Writes the zip; when the zip itself cannot be written, removes the partial file and returns failure; callable from any thread
        internal PlaytestExportStatus PackUnsent(PlaytestOutboxScope scope, string zipPath, DateTime exportedAtUtc)
        {
            // 外部ディスクIO境界。容量不足・権限でzip自体を書けなければ失敗にする
            // External disk IO boundary: failing to write the zip itself (disk full, permissions) is an export failure
            try
            {
                var result = PlaytestUnsentZipWriter.Write(_directories, scope, zipPath, exportedAtUtc);
                return new PlaytestExportDone(zipPath, result.BoxCount, result.Missing, false);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                DeletePartial(zipPath);
                return new PlaytestExportFailed($"{error.GetType().Name}: {error.Message}");
            }
        }

        // 結果を画面へ出す。成功ならファイルマネージャでzipを選んで開く。メインスレッドで呼ぶ
        // Publishes the outcome; on success opens the file manager with the zip selected; call on the main thread
        internal void FinishExport(PlaytestExportStatus packed, string zipPath)
        {
            if (packed is PlaytestExportFailed failed)
            {
                Fail(failed.Reason);
                return;
            }
            var done = (PlaytestExportDone)packed;
            var revealed = _reveal.Reveal(zipPath);
            Debug.Log($"[PlaytestReceiver] exported {done.BoxCount} box(es) to {zipPath}; {done.Missing.Count} item(s) missing");
            PlaytestExportStatusBoard.Set(new PlaytestExportDone(zipPath, done.BoxCount, done.Missing, revealed));
        }

        internal static void FailIfStillRunning()
        {
            if (PlaytestExportStatusBoard.Current.Value is PlaytestExportRunning) Fail("export ended unexpectedly; see Player.log");
        }

        private static void DeletePartial(string zipPath)
        {
            // 外部ディスクIO境界。消せない途中のzipはログに残し、画面は失敗のまま出す
            // External disk IO boundary: a partial zip that cannot be removed is logged; the screen still shows the failure
            try { if (File.Exists(zipPath)) File.Delete(zipPath); }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                Debug.LogWarning($"[PlaytestReceiver] cannot remove partial export {zipPath}: {error.Message}");
            }
        }

        private static void Fail(string reason)
        {
            Debug.LogWarning($"[PlaytestReceiver] export failed: {reason}");
            PlaytestExportStatusBoard.Set(new PlaytestExportFailed(reason));
        }
    }
}
```

`try { } finally { }` は catch を持たない後始末の保証で、try-catch 規約の対象外（前例: `PlaytestUploadRunner.cs` の `finally`）。
スレッドプール上で `PlaytestOutboxBoxState.Classify` が `Debug.LogWarning` を呼ぶ。Unity の `Debug.Log*` はワーカースレッドから呼んでよい。

- [ ] **Step 5: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "Client\.Tests\.PlaytestReceiver\.Recovery"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 6: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.PlaytestReceiver/Recovery moorestech_client/Assets/Scripts/Client.Tests/PlaytestReceiver/Recovery moorestech_client/Assets/Scripts/Client.Starter/Registration/MainGameModelRegistration.cs
git commit -m "feat(playtest): 未送信の報告をデスクトップへzipで書き出す"
```

---

### Task 4: WebUI への配線（action と topic）

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Actions/BugReport/BugReportRecoveryActions.cs`
- Move（`git mv`。名前空間は変えない）: `Client.WebUiHost/Game/Actions/BugReportSubmitActionHandler.cs` → `Actions/BugReport/`。`Actions/` 直下は既に14ファイルで上限超過のため、足す側をサブディレクトリにまとめる（前例: `Actions/Hotbar/`・`Actions/Settings/`）
- Create: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/PlaytestExportDto.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/Topics/C2/PauseMenuTopic.cs`（`PauseMenuDto.Export` と購読）
- Modify: `moorestech_client/Assets/Scripts/Client.WebUiHost/Game/WebUiGameBinder.cs:194` の直後に2行
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/action_names.json`（2件追加）
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/WireFixtures/pause_menu.json`（`export` を追加）
- Test: `moorestech_client/Assets/Scripts/Client.Tests/WebUi/PauseMenu/PauseMenuTopicExportTest.cs`

**Interfaces:**
- Consumes: `PlaytestOutboxRecovery.RequestResendFailed()`・`RequestExportUnsent()`、`PlaytestExportStatusBoard.Current`（Task 2・3）
- Produces（wire 契約。Task 5 がこの形に合わせる）:
  - action `bug_report.resend_failed`（payload `{}`）、`bug_report.export_unsent`（payload `{}`）。どちらも受理したら即 `ActionResult.Success()` を返す。
  - `pause_menu.current` に `export` を追加。形は次のどれか（null のフィールドは `WebUiJson` が省く）:
    - `{"kind":"idle"}` / `{"kind":"running"}`
    - `{"kind":"done","zipPath":"...","boxCount":3,"missing":[{"item":"...","reason":"..."}],"revealed":true}`
    - `{"kind":"failed","reason":"..."}`

- [ ] **Step 1: テストを書く**

`PauseMenuTopicExportTest.cs` は、`PauseMenuTopicDeliveryTest.cs` の組み立て（topic の生成と `GetSnapshotJsonAsync` の読み方）を写し、`PlaytestExportStatusBoard.Set(...)` に4状態を入れて JSON の `export` を確かめる。`Set` は internal なので、既存の `PauseMenuTopicDeliveryTest` が `PlaytestDeliveryStatusBoard.Set` を呼んでいる方法と同じ方法を使う。

```csharp
        [Test]
        public void DoneExportCarriesPathCountMissingAndReveal()
        {
            PlaytestExportStatusBoard.Set(new PlaytestExportDone("/tmp/a.zip", 2, new[] { new PlaytestExportMissing("reports/x/video.mp4", "IOException: locked") }, true));
            var export = JObject.Parse(SnapshotJson())["export"];
            Assert.AreEqual("done", (string)export["kind"]);
            Assert.AreEqual("/tmp/a.zip", (string)export["zipPath"]);
            Assert.AreEqual(2, (int)export["boxCount"]);
            Assert.AreEqual("reports/x/video.mp4", (string)export["missing"][0]["item"]);
            Assert.IsTrue((bool)export["revealed"]);
        }

        [TestCase("idle")]
        [TestCase("running")]
        public void SimpleStatesCarryOnlyKind(string kind)
        {
            PlaytestExportStatusBoard.Set(kind == "idle" ? PlaytestExportIdle.Instance : PlaytestExportRunning.Instance);
            var export = (JObject)JObject.Parse(SnapshotJson())["export"];
            Assert.AreEqual(kind, (string)export["kind"]);
            Assert.AreEqual(1, export.Count);
        }

        [Test]
        public void FailedExportCarriesReason()
        {
            PlaytestExportStatusBoard.Set(new PlaytestExportFailed("desktop folder unavailable"));
            var export = JObject.Parse(SnapshotJson())["export"];
            Assert.AreEqual("failed", (string)export["kind"]);
            Assert.AreEqual("desktop folder unavailable", (string)export["reason"]);
        }
```

`SnapshotJson()` は `PauseMenuTopicDeliveryTest` の組み立てを同じクラス内に持つ（ヘルパーをテストクラス間で共有する前例があればそれに従う）。

- [ ] **Step 2: 実装を書く**

`BugReportRecoveryActions.cs`:

```csharp
using Client.PlaytestReceiver.Recovery;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Client.WebUiHost.Game.Actions
{
    // 送信できない報告の再送。結果は配送状況のtopicで届く
    // Resend of undeliverable reports; the outcome arrives through the delivery topic
    public class BugReportResendFailedActionHandler : IActionHandler
    {
        private readonly PlaytestOutboxRecovery _recovery;
        public string ActionType => "bug_report.resend_failed";
        public BugReportResendFailedActionHandler(PlaytestOutboxRecovery recovery) { _recovery = recovery; }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _recovery.RequestResendFailed();
            return UniTask.FromResult(ActionResult.Success());
        }
    }

    // 未送信の報告のzip書き出し。数分かかり得るため受理だけ返し、進行はtopicで届く
    // Zip export of unsent reports; it can take minutes, so only acceptance returns and progress arrives on the topic
    public class BugReportExportUnsentActionHandler : IActionHandler
    {
        private readonly PlaytestOutboxRecovery _recovery;
        public string ActionType => "bug_report.export_unsent";
        public BugReportExportUnsentActionHandler(PlaytestOutboxRecovery recovery) { _recovery = recovery; }

        public UniTask<ActionResult> ExecuteAsync(JObject payload)
        {
            _recovery.RequestExportUnsent();
            return UniTask.FromResult(ActionResult.Success());
        }
    }
}
```

`WebUiGameBinder.cs` の `BugReportSubmitActionHandler` 登録の直後:

```csharp
            hub.RegisterAction(new BugReportResendFailedActionHandler(resolver.Resolve<PlaytestOutboxRecovery>()));
            hub.RegisterAction(new BugReportExportUnsentActionHandler(resolver.Resolve<PlaytestOutboxRecovery>()));
```

`PlaytestExportDto.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using Client.PlaytestReceiver.Recovery;

namespace Client.WebUiHost.Game.Topics
{
    // 書き出し状態ごとの値だけを送り、null値はWebUiJsonで省略する
    // Send only the fields of each export state; WebUiJson omits null fields
    public sealed class PlaytestExportDto
    {
        public readonly string Kind;
        public readonly string ZipPath;
        public readonly int? BoxCount;
        public readonly List<PlaytestExportMissingDto> Missing;
        public readonly bool? Revealed;
        public readonly string Reason;

        private PlaytestExportDto(string kind, string zipPath, int? boxCount, List<PlaytestExportMissingDto> missing, bool? revealed, string reason)
        { Kind = kind; ZipPath = zipPath; BoxCount = boxCount; Missing = missing; Revealed = revealed; Reason = reason; }

        internal static PlaytestExportDto From(PlaytestExportStatus status)
        {
            return status switch
            {
                PlaytestExportIdle => new PlaytestExportDto("idle", null, null, null, null, null),
                PlaytestExportRunning => new PlaytestExportDto("running", null, null, null, null, null),
                PlaytestExportDone done => new PlaytestExportDto("done", done.ZipPath, done.BoxCount, done.Missing.Select(m => new PlaytestExportMissingDto(m.Item, m.Reason)).ToList(), done.Revealed, null),
                PlaytestExportFailed failed => new PlaytestExportDto("failed", null, null, null, null, failed.Reason),
                _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown export status"),
            };
        }
    }

    public sealed class PlaytestExportMissingDto
    {
        public readonly string Item;
        public readonly string Reason;
        public PlaytestExportMissingDto(string item, string reason) { Item = item; Reason = reason; }
    }
}
```

`PauseMenuTopic.cs`:
- コンストラクタの購読に `PlaytestExportStatusBoard.Current.Skip(1).Subscribe(_ => Publish()).AddTo(_subscriptions);` を足す。
- `BuildJson` の `PauseMenuDto` に `Export = PlaytestExportDto.From(PlaytestExportStatusBoard.Current.Value),` を足す。
- `PauseMenuDto` に `public PlaytestExportDto Export;` を足す。
- `using Client.PlaytestReceiver.Recovery;` を足す。

`PlaytestExportStatusBoard.Set` はワーカースレッドから呼ばない（Task 3 の `ExportUnsentAsync` は `SwitchToMainThread` 後に `Set` する）。`Publish` はメインスレッドで走る。

`action_names.json` に `"bug_report.resend_failed"` と `"bug_report.export_unsent"` を、既存の並び（`bug_report.submit` の近く）に合わせて追加する。`pause_menu.json` の例に `"export":{"kind":"idle"}` を追加する。

- [ ] **Step 3: コンパイルしテストを実行する**

Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PauseMenuTopicExportTest|PauseMenuTopicDeliveryTest|PauseMenuWireContractTest|WireContractActionNamesTest"`
Expected: ErrorCount 0 / 全 PASS

- [ ] **Step 4: コミットする**

```bash
git add moorestech_client/Assets/Scripts/Client.WebUiHost moorestech_client/Assets/Scripts/Client.Tests/WebUi
git commit -m "feat(webui-host): 再送・書き出しの action と書き出し状態の topic を配線する"
```

---

### Task 5: バグ報告画面のボタンと表示（webui）

**Files:**
- Create: `moorestech_web/webui/src/features/pauseMenu/BugReportRecoveryControls.tsx`
- Modify: `moorestech_web/webui/src/features/pauseMenu/BugReportForm.tsx`（配送状況の行の直後に `<BugReportRecoveryControls delivery={delivery} exportStatus={exportStatus} />`、Props に `exportStatus` を追加）
- Modify: `BugReportForm` を描画している親（`PauseMenuSubPage.tsx` か `PauseMenuPanel.tsx`。`<BugReportForm` を grep して特定する）で `exportStatus={data.export}` を渡す
- Modify: `moorestech_web/webui/src/bridge/contract/schemas/ui.ts`（`ExportStatusSchema` と `PauseMenuDataSchema.export`）
- Modify: `moorestech_web/webui/src/bridge/transport/actionContract.ts`（`ActionPayloads` と `ACTION_TYPES` に2件）
- Modify: `moorestech_web/webui/e2e/mock-host/pauseMenuActions.ts`（2件を受理するだけの分岐）
- Modify: `moorestech_web/webui/e2e/mock-host/topics/topicFixtures.ts:49` と `moorestech_web/webui/e2e/mock-host/topics/pauseMenuControls.ts:9-10`（`pause_menu.current` を作る箇所に `export: { kind: "idle" }` を足す。`delivery` を grep して他の生成箇所が無いことも確かめる）
- Modify: `moorestech_web/webui/src/features/pauseMenu/BugReportFormTests/deliveryStatus.test.ts`（`再送復旧|recover|…` を期待している行を、その語が出ないことの確認へ変える）
- Modify: `Localization/localization.csv`（下の7行を `ui.bugReport.deliveryFailed` の行の直後に追加）
- Modify: `moorestech_web/webui/src/shared/i18n/generated/localizationKeys.ts`（`pnpm gen:i18n` で再生成）
- Modify: `moorestech_web/webui/src/features/pauseMenu/BugReportFormTests/testSupport.ts`（`renderDelivery` に書き出し状態の引数を追加。呼び出し側をすべて直す）
- Test: `moorestech_web/webui/src/features/pauseMenu/BugReportFormTests/recoveryControls.test.ts`

**Interfaces:**
- Consumes: Task 4 の wire 契約（action 名2件、`pause_menu.current.export`）
- Produces: `PauseMenuData["export"]` 型（zod から推論）

**文言（localization.csv。列は key,Source,english,japanese,german,korean）:**

```
ui.bugReport.resendFailed,Resend,Resend,再送する,Erneut senden,다시 보내기
ui.bugReport.exportUnsent,Export unsent reports,Export unsent reports,未送信の報告を書き出す,Nicht gesendete Berichte exportieren,보내지 못한 보고서 내보내기
ui.bugReport.exportRunning,Exporting reports...,Exporting reports...,報告を書き出しています…,Berichte werden exportiert...,보고서를 내보내는 중...
ui.bugReport.exportDone,"Exported {count} report(s) to {path}. Upload it to Google Drive or similar and send the share link to the developer","Exported {count} report(s) to {path}. Upload it to Google Drive or similar and send the share link to the developer","{count}件を {path} に書き出しました。Googleドライブ等に上げて、共有リンクを開発者へ送ってください","{count} Bericht(e) nach {path} exportiert. Laden Sie die Datei z. B. auf Google Drive hoch und senden Sie den Freigabelink an den Entwickler","{count}건을 {path}에 내보냈습니다. Google 드라이브 등에 올리고 공유 링크를 개발자에게 보내 주세요"
ui.bugReport.exportMissing,"{count} item(s) could not be included: {items}","{count} item(s) could not be included: {items}","{count}件は入れられませんでした: {items}","{count} Element(e) konnten nicht aufgenommen werden: {items}","{count}개 항목을 넣지 못했습니다: {items}"
ui.bugReport.exportNotRevealed,Open the folder above yourself,Open the folder above yourself,上のフォルダを手動で開いてください,Öffnen Sie den obigen Ordner selbst,위 폴더를 직접 열어 주세요
ui.bugReport.exportFailed,Could not export: {reason},Could not export: {reason},書き出せませんでした: {reason},Export fehlgeschlagen: {reason},내보내지 못했습니다: {reason}
```

既存の `ui.bugReport.deliveryFailed`（`Localization/localization.csv:403`）は「開発者に再送復旧を依頼してください」と書いてあり、すぐ下の「再送する」ボタンと矛盾する。4言語とも件数だけの文に変える:

```
ui.bugReport.deliveryFailed,{count} report(s) could not be sent,{count} report(s) could not be sent,送信できない報告 {count}件,{count} Bericht(e) konnten nicht gesendet werden,전송할 수 없는 보고서 {count}건
```

カンマや引用符を含む行は既存 CSV の引用規則（`"` で囲む）に合わせる。既存行の書式を見てから足す。

- [ ] **Step 1: テストを書く**

`recoveryControls.test.ts`（`submissionResults.test.ts` と `deliveryStatus.test.ts` の書き方に合わせる。`renderDelivery` の拡張後の引数順は `(status, delivery, exportStatus)`）:

```ts
import { act } from "react-test-renderer";
import { expect, it } from "vitest";
import type { PauseMenuData } from "@/bridge";
import { loadProductionDictionaries, mocks, renderDelivery } from "./testSupport";

const failed: PauseMenuData["delivery"] = { kind: "pending", inventory: { kind: "known", pendingCount: 1, failedCount: 16 } };
const pendingOnly: PauseMenuData["delivery"] = { kind: "pending", inventory: { kind: "known", pendingCount: 2, failedCount: 0 } };
const idle: PauseMenuData["export"] = { kind: "idle" };

const byTestId = (r: ReturnType<typeof renderDelivery> extends Promise<infer T> ? T : never, id: string) =>
  r.root.findAll((n) => n.props["data-testid"] === id);

it("送信できない報告があるときだけ再送ボタンを出し、押すと action を出す", async () => {
  const r = await renderDelivery({ kind: "ready", missing: [] }, failed, idle);
  const resend = byTestId(r, "bug-report-resend-failed")[0];
  await act(async () => resend.props.onClick());
  expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.resend_failed", {});
  act(() => r.unmount());
});

it("送信待ちだけなら再送は出さず書き出しは出す", async () => {
  const r = await renderDelivery({ kind: "ready", missing: [] }, pendingOnly, idle);
  expect(byTestId(r, "bug-report-resend-failed")).toHaveLength(0);
  const exportButton = byTestId(r, "bug-report-export-unsent")[0];
  await act(async () => exportButton.props.onClick());
  expect(mocks.dispatchAction).toHaveBeenCalledWith("bug_report.export_unsent", {});
  act(() => r.unmount());
});

it("すべて送信済みなら両方とも出さない", async () => {
  const r = await renderDelivery({ kind: "ready", missing: [] }, { kind: "allSent" }, idle);
  expect(byTestId(r, "bug-report-resend-failed")).toHaveLength(0);
  expect(byTestId(r, "bug-report-export-unsent")).toHaveLength(0);
  act(() => r.unmount());
});

it("書き出し中はボタンを押せない", async () => {
  const r = await renderDelivery({ kind: "ready", missing: [] }, failed, { kind: "running" });
  expect(byTestId(r, "bug-report-export-unsent")[0].props.disabled).toBe(true);
  act(() => r.unmount());
});

it.each(["japanese", "english", "german", "korean"])("%s辞書でボタン2つと書き出し中の文言が未翻訳キーにならない", async (locale) => {
  const r = await renderDelivery({ kind: "ready", missing: [] }, failed, { kind: "running" });
  await act(async () => loadProductionDictionaries(locale));
  for (const id of ["bug-report-resend-failed", "bug-report-export-unsent", "bug-report-export-status"]) {
    const text = byTestId(r, id)[0].children.join("");
    expect(text.length).toBeGreaterThan(0);
    expect(text).not.toContain("ui.bugReport.");
  }
  act(() => r.unmount());
});

it.each(["japanese", "english", "german", "korean"])("%s辞書で完了・欠落・開けなかった案内・失敗の文言が未翻訳キーにならない", async (locale) => {
  const done: PauseMenuData["export"] = { kind: "done", zipPath: "C:/Users/t/Desktop/a.zip", boxCount: 17, missing: [{ item: "reports/x/video.mp4", reason: "IOException" }], revealed: false };
  const r = await renderDelivery({ kind: "ready", missing: [] }, failed, done);
  await act(async () => loadProductionDictionaries(locale));
  const text = byTestId(r, "bug-report-export-status")[0].children.join("");
  expect(text).toContain("C:/Users/t/Desktop/a.zip");
  expect(text).toContain("17");
  expect(text).toContain("reports/x/video.mp4");
  expect(text).toContain("IOException");
  expect(text).not.toContain("ui.bugReport.");
  act(() => r.unmount());

  const failedRender = await renderDelivery({ kind: "ready", missing: [] }, failed, { kind: "failed", reason: "desktop folder unavailable" });
  await act(async () => loadProductionDictionaries(locale));
  const failedText = byTestId(failedRender, "bug-report-export-status")[0].children.join("");
  expect(failedText).toContain("desktop folder unavailable");
  expect(failedText).not.toContain("ui.bugReport.");
  act(() => failedRender.unmount());
});
```

`testSupport.ts` の `mocks` が export されていなければ export する。`byTestId` の型が書きにくければ、`testSupport.ts` に `findAllByTestId(renderer, id)` を足して使う。

- [ ] **Step 2: スキーマと action 契約を書く**

`schemas/ui.ts` の `DeliveryStatusSchema` の後:

```ts
// 書き出しの状態ごとの必須値を契約で固定する
// Pin the required fields of each export state at the boundary
const ExportStatusSchema = z.discriminatedUnion("kind", [
  z.object({ kind: z.literal("idle") }).strict(),
  z.object({ kind: z.literal("running") }).strict(),
  z.object({
    kind: z.literal("done"),
    zipPath: z.string().min(1),
    boxCount: z.number().int().nonnegative(),
    missing: z.array(z.object({ item: z.string(), reason: z.string() }).strict()),
    revealed: z.boolean(),
  }).strict(),
  z.object({ kind: z.literal("failed"), reason: z.string().min(1) }).strict(),
]);
```

`PauseMenuDataSchema` に `export: ExportStatusSchema,` を足す。

`actionContract.ts` の `ActionPayloads` に `"bug_report.resend_failed": Record<string, never>;` と `"bug_report.export_unsent": Record<string, never>;`（既存の payload 無し action、例えば `pause_menu.save` の書き方に合わせる）、`ACTION_TYPES` に同じ2つを足す。

- [ ] **Step 3: 画面を書く**

`BugReportRecoveryControls.tsx`:

```tsx
// 送れずに残った報告を手元で救う操作。件数と書き出しの状態はC#が配る
// Controls that rescue reports left unsent; counts and export state are published by C#
import { Button } from "@mantine/core";
import { dispatchAction, type PauseMenuData } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

type Props = {
  delivery: PauseMenuData["delivery"];
  exportStatus: PauseMenuData["export"];
};

export function BugReportRecoveryControls({ delivery, exportStatus }: Props) {
  const { t } = useI18n();
  const exporting = exportStatus.kind === "running";

  // 件数が読めないときも書き出しだけは出す。読めない箱の救出が書き出しの役目のため
  // Export stays available even when counts are unreadable, because rescuing such boxes is its job
  const inventory = delivery.kind === "allSent" ? null : delivery.inventory;
  const known = inventory?.kind === "known" ? inventory : null;
  const unknown = inventory?.kind === "unavailable";
  const showResend = known !== null && known.failedCount > 0;
  const showExport = unknown || (known !== null && known.pendingCount + known.failedCount > 0);
  const statusLine = describeExport();

  return (
    <>
      {showResend && (
        <Button onClick={() => dispatchAction("bug_report.resend_failed", {})} disabled={exporting} data-testid="bug-report-resend-failed">
          {t(L.ui.bugReport.resendFailed)}
        </Button>
      )}
      {showExport && (
        <Button onClick={() => dispatchAction("bug_report.export_unsent", {})} disabled={exporting} data-testid="bug-report-export-unsent">
          {t(L.ui.bugReport.exportUnsent)}
        </Button>
      )}
      {statusLine && <span className={styles.status} data-testid="bug-report-export-status">{statusLine}</span>}
    </>
  );

  function describeExport(): string | null {
    switch (exportStatus.kind) {
      case "idle": return null;
      case "running": return t(L.ui.bugReport.exportRunning);
      case "failed": return t(L.ui.bugReport.exportFailed, { reason: exportStatus.reason });
      case "done": {
        const parts = [t(L.ui.bugReport.exportDone, { count: exportStatus.boxCount, path: exportStatus.zipPath })];
        if (exportStatus.missing.length > 0) parts.push(t(L.ui.bugReport.exportMissing, { count: exportStatus.missing.length, items: exportStatus.missing.map((m) => `${m.item} (${m.reason})`).join(", ") }));
        if (!exportStatus.revealed) parts.push(t(L.ui.bugReport.exportNotRevealed));
        return parts.join(" / ");
      }
    }
  }
}
```

`dispatchAction` は失敗時に自分でトーストを出す（`actions.ts:69`）ので、ここでは結果を見ない。ボタンが2つ以上縦に並ぶときの見た目は、既存の `bug-report-send` ボタンと同じ `Button` のまま並べる（`ref-webui-design` スキルのホワイトリストに従う。新しい CSS クラスは足さない）。

`BugReportForm.tsx` の Props に `exportStatus: PauseMenuData["export"];` を足し、`bug-report-delivery` の `<span>` の直後に `<BugReportRecoveryControls delivery={delivery} exportStatus={exportStatus} />` を置く。親で `data.export` を渡す。

- [ ] **Step 4: 文言を足して生成し、テストを実行する**

Run（`moorestech_web/webui` で）: `pnpm gen:i18n && pnpm vitest run src/features/pauseMenu src/bridge src/shared/i18n && pnpm typecheck && pnpm exec tsc -p e2e/tsconfig.json --noEmit`
Expected: すべて PASS（`localizationKeysFreshness.test.ts`・`wireContract.test.ts`・`actionNames.test.ts` を含む）。`typecheck` スクリプトが無ければ `package.json` の型検査スクリプト名を使う。

- [ ] **Step 5: コミットする**

```bash
git add Localization/localization.csv moorestech_web/webui
git commit -m "feat(webui): バグ報告画面に再送・書き出しのボタンと書き出し状態を出す"
```

---

### Task 6: 開発側の zip 取り込みスクリプト

**Files:**
- Create: `scripts/playtest/import-export-zip.sh`
- Create: `scripts/playtest/lib/import_export_zip.py`
- Create: `scripts/playtest/lib/logs-commit.sh`（`ingest.sh` の `commit_logs` をここへ移す）
- Modify: `scripts/playtest/ingest.sh`（`commit_logs` の定義を消し、`. "$HERE/lib/logs-commit.sh"` で読む。`commit_logs "<メッセージ>"` の形で呼ぶ）
- Modify: `scripts/bugreport/README.md`（「隔離された報告の再送」に、ゲーム内の再送・書き出しと取り込み手順を追記）
- Test: `scripts/playtest/tests/test-import-export-zip.sh`

**Interfaces:**
- Consumes: Task 3 の zip 形式（`reports/<id>/...`・`progress/<id>/...`・`export.json`）、既存の `scripts/bugreport/lib/restore-commit-transfer.py <dir>`、`lib/steam-persona.sh` の `steam_persona_resolve <steamId> <out>`、`lib/ingest_metadata.py <kind> <steamId> <id> <readyAt> <ingestedAt> <persona> <out>`、`lib/safe_segment.py segment <s>`
- Produces: `bash scripts/playtest/import-export-zip.sh <zip>`。終了コードは、全箱取り込み（既存で飛ばしたものを含む）なら 0、1件でも失敗があれば 1。

**取り込みの手順（1箱ごと）:**
1. zip を一時ディレクトリへ展開する（`python3 -I` で `zipfile` を使い、各エントリ名が `..` や絶対パスを含まないか検証してから展開する）。
2. `export.json` の `boxes` を順に処理する。`kind` は `reports` → `report`、`progress` → `progress` に読み替える（`ingest.sh` の `dest_subdir` の逆）。
3. `steamId` を、バグ報告は `manifest.json`、進行記録は `record.json` の `steamId` から読む。無い・`safe_segment.py segment` を通らなければ、その箱を失敗にして理由を出す（開発者トークンの箱は `steamId` を持たないことがある。その場合は失敗の理由に「steamId が無い（開発者の箱）」と書く）。
4. 置き先 `$LOGS/harness/playtest/<reports|progress>/<steamId>/<id>` が既にあれば、「取り込み済み」と出して飛ばす。
5. `<置き先>.partial` に、箱の中身から印ファイル（`READY`・`UPLOADED`・`SHIPPED`・`UPLOAD_FAILED`・`UPLOAD_ATTEMPTS`・`UPLOAD_SKIPPED`・`UPLOAD_DECLARED`・`UPLOAD_LOCK`）を除いてコピーする。印ファイルは `<置き先>.partial/export-markers/` にそのまま置く（失敗理由を後から読めるようにする）。
6. 受け口と同じ形の `READY` を書く: `{"kind": "report"|"progress", "id": <id>, "fileCount": N, "files": [<印以外の相対パス。export-markers/ を含めない>], "skipped": [], "manifest": <manifest.json の本文文字列。無ければ null>, "importedFrom": "export-zip"}`。
7. `python3 scripts/bugreport/lib/restore-commit-transfer.py <partial>`、`steam_persona_resolve`、`ingest_metadata.py`（`readyAt` は `export.json` の `exportedAt`、`ingestedAt` は今の UTC）を `ingest.sh` と同じ順で実行する。どれかが失敗したら `partial` を消して、その箱を失敗にする。
8. `mv <partial> <置き先>`。
9. 全箱の後に `commit_logs "playtest: import export zip <zip のファイル名>"` で logs repo にコミット・push する。

`$LOGS` は `ingest.sh` と同じく `${MOORESTECH_LOGS:-$REPO/../moorestech_logs}`。`$LOGS/.git` が無ければ何もせず終了コード 1（`ingest.sh` と同じ fail-closed）。

- [ ] **Step 1: テストを書く**

`test-import-export-zip.sh` は `test-ingest.sh` と同じく一時ディレクトリに偽の logs repo（`git init`）を作り、`GIT_PUSH=0` と、`steam-persona.sh` が外部へ問い合わせない設定（`test-ingest.sh`／`ingest-fixture.sh` が使っている差し替え方法をそのまま使う）で実行する。python で次の zip を作る:
- `reports/20261019_150326_bug1/manifest.json`（`{"kind":"bug","steamId":"7656001"}`）、`reports/20261019_150326_bug1/video.mp4`、`reports/20261019_150326_bug1/READY`、`reports/20261019_150326_bug1/UPLOAD_FAILED`（本文 `reason-x`）
- `progress/20261019_160000_pg1/record.json`（`{"steamId":"7656001"}`）、`progress/20261019_160000_pg1/READY`
- `reports/20261019_170000_dev1/manifest.json`（`steamId` 無し）、`READY`
- `export.json`（上の3箱）

確かめること:
- `reports/7656001/20261019_150326_bug1/` に `manifest.json`・`video.mp4`・`READY`・`ingest.json`・`export-markers/UPLOAD_FAILED` があり、`READY` の `files` が `["manifest.json","video.mp4"]`（順不同）。
- `progress/7656001/20261019_160000_pg1/record.json` と `ingest.json` がある。
- `steamId` の無い箱は置かれず、終了コードが 1、ログに「steamId」が出る。
- もう一度同じ zip で実行すると、既存の2箱は「取り込み済み」で飛ばされ、内容が変わらない。
- `find-reports.sh`（`MOORESTECH_LOGS` を偽の logs に向ける）の取り込み済み一覧に `20261019_150326_bug1` が出る。
- `../evil` という名前のエントリを含む zip では何も展開せず終了コード 1。

- [ ] **Step 2: 実装を書く**

`lib/logs-commit.sh` は `ingest.sh` の `commit_logs` をそのまま移し、コミットメッセージだけを第1引数にする（`ingest.sh` 側は `commit_logs "playtest: ingest $(now_utc)"`）。`log` と `now_utc` は呼び出し側のものを使う前提を冒頭コメントに書く。

`import-export-zip.sh` は `ingest.sh` の冒頭（`HERE`・`REPO`・`ENV_FILE` の読み込み・`lib/steam-persona.sh` の source・`LOGS`・`PLAYTEST_DIR`）と同じ形で始め、ロックは `ingest.sh` と同じ `lib/ingest-lock.sh` の `acquire_lock` を使う（取り込みと同時に logs repo を触らないため）。展開・箱ごとの処理は `lib/import_export_zip.py` に `unpack <zip> <dir>` と `stage <unpacked-box> <kind> <partial>`（印の除外コピーと `READY` の書き出し、steamId の出力）の2サブコマンドで持たせ、bash 側は `ingest_one` と同じ順でコミット資料の復元・表示名・`ingest.json`・`mv` を行う。

- [ ] **Step 3: テストを実行する**

Run: `bash scripts/playtest/tests/test-import-export-zip.sh && bash scripts/playtest/tests/test-ingest.sh && bash scripts/playtest/tests/test-ingest-guards.sh && bash scripts/playtest/tests/test-ingest-lock.sh`
Expected: すべて OK（`ingest.sh` の `commit_logs` 移設で既存テストが壊れていない）

- [ ] **Step 4: README を更新してコミットする**

`scripts/bugreport/README.md` の「隔離された報告の再送」の先頭に次を足す:

```
テスター機では、ポーズ中のバグ報告画面の「再送する」で失敗印を外して送り直せる。原因がアップデートで直っていない箱は、また5回失敗して止まる。
それでも届かない箱は「未送信の報告を書き出す」でデスクトップにzipを作ってもらい、受け取ったzipを `bash scripts/playtest/import-export-zip.sh <zip>` で取り込む。箱は受け口経由と同じ場所に置かれ、印ファイルは各箱の `export-markers/` に残る。
```

```bash
git add scripts/playtest scripts/bugreport/README.md
git commit -m "feat(playtest): テスターが書き出したzipを受け口経由と同じ形で取り込む"
```

---

### Task 7: unityプレイ録画テスト

**Files:**
- Create: `unity-playmode-recorded-playtest` スキルの手順に従ったシナリオファイル（置き場・形式はスキルの references に従う）

ランタイムの UI に触れる変更なので、録画テストで画面上の動作を確かめる。

- [ ] **Step 1: 準備**

`unity-playmode-recorded-playtest` スキルを読み、シナリオ DSL で次を組む。

- 実機の outbox を汚さないため、PlayMode 前に `GameSystemPaths.BugReportOutboxDirectory` に `99991231_000000_recoveryplaytest` という名前の箱を1つ作る（`READY` と `UPLOAD_FAILED`（本文 `playtest`）と `payload.txt`）。他の箱には触れない。
- 偽の箱の `UPLOAD_FAILED` の本文は `playtest` にする。`PlaytestOutboxScanner.IsCompletionHold` の保留理由（`evidence-completion-required: ` で始まる文など）に当たらないので、起動時の送信と自動の走行は `ScanPending` でこの箱を飛ばす。PlayMode に入る前に、この前提を `IsCompletionHold`・`BugReportBoxFilePolicy.CanResumeHold` の実装で確かめ、当たるなら本文を変える。
- この Mac mini の Editor は開発者トークンの経路で本当に送信する。そのため、録画では「再送する」を押さない（押すと偽の箱が本番の受け口へ送られる。bd `moorestech-albof.6.3` と同種の事故）。再送は Task 2 の単体テストで確かめる。

- [ ] **Step 2: シナリオ**

1. ゲームに入り、Escape でポーズ → バグ報告画面を開く。
2. 配送状況の行に「送信できない報告 1件」と出て、その下に「再送する」と「未送信の報告を書き出す」の両方が出ていることを、DOM（`data-testid`）とスクショで確かめる。
3. 「未送信の報告を書き出す」をクリックする。
4. `bug-report-export-status` に完了の文言とデスクトップの zip パスが出るまで待つ（最大60秒）。
5. そのパスの zip を開き、`reports/99991231_000000_recoveryplaytest/payload.txt` と `export.json` が入っていることを確かめる。
6. 後片付け: 書き出した zip と偽の箱を消す。Finder のウィンドウが開いていれば閉じる。

- [ ] **Step 3: 実行して result.json と録画を確認する**

Expected: result.json が成功で、録画にボタン2つと完了の文言が映っている。

- [ ] **Step 4: コミットする**

シナリオファイルを追加・コミットする（録画はスキルの規定に従う。リポジトリに動画を入れない）。

---

### Task 8: moores-code-review による全ブランチレビュー

- [ ] 必ず moores-code-review スキルで全ブランチレビューを実行する（自動実行・ゴール文言による省略不可）。レビュー指摘の反映が判定経路・条件式・評価時点に触れたら、Task 7 の録画テストを反映後のバイナリで再実施してから完了とする。

### Task 9: PR を作成してセッションを閉じられる状態にする

- [ ] pr-create スキルで PR を作成する。master とコンフリクトがあれば master をマージして解消し、コンパイルを確認して push する（解消の実作業は pr-create 経由で opus subagent に委譲される）。全作業がコミット・push 済みで、このセッションをそのまま閉じても PR がマージ可能な状態になっていることを確認して終える。PR 本文には、テスター maitan 氏の16件はこの PR の配布後に「再送する」→ 届かなければ「書き出す」で回収する手順を書く。

---

## 判断記録（ADR）

ADR（`docs/adr/`）は作らない。どの決定も後から変えやすく、3条件（取り返しにくい・前提無しでは意外・本物のトレードオフ）を満たさないため。

| # | 決定 | 出所 |
|---|---|---|
| D1 | 復旧手段はゲーム内にアップデートで出し、テスター向けのスクリプトは配らない | ユーザー裁定 2026-10-27 原文「アプデでできるようにしたい 今後もそういうかのうせいあるし」（`.decisions/2026-10-27-送信できないバグ報告はゲーム内アプデで復旧手段を出す.md`） |
| D2 | 操作は「再送」と「書き出し」の両方。書き出し対象は未送信すべて（送信待ち＋送信できない）。失敗理由の受け口への自動送信は入れない | ユーザー裁定 2026-10-27（質問への回答。`.decisions/2026-10-27-送信できないバグ報告は再送と未送信全件のzip書き出しで復旧する.md`） |
| D3 | zip はデスクトップに置いて選択状態で開く。案内は「Google ドライブ等に上げて共有リンクを開発者へ」を4言語で、送り先の具体名は書かない。開発側の取り込みスクリプトを作る | ユーザー裁定 2026-10-27（質問への回答。`.decisions/2026-10-27-バグ報告zipはデスクトップへ書き出し開発側に取り込み道具を作る.md`） |
| D4 | 対象は画面の件数と揃えて進行記録も含める。置き場はポーズ中のバグ報告画面だけ。読めない箱・ファイルは飛ばして欠落を画面と zip 内に出す | ユーザー裁定 2026-10-27（質問への回答。`.decisions/2026-10-27-バグ報告の再送と書き出しは進行記録も含めポーズ画面だけに置き壊れた箱は欠落明示で部分救出する.md`） |
| D5 | ファイルの読み出しが途中で失敗したとき、zip に途中までのエントリが残るのを許容し、`missing` に載せて気づけるようにする。zip のエントリは書いた後に取り消せないため | agent前提（`System.IO.Compression.ZipArchive` の仕様） |
| D6 | 書き出し後も箱は outbox に残し、印も変えない | agent前提（grill で提示し、ユーザーが「OK 進めて」で計画に進んだ。単独の質問としては聞いていない） |
| D7 | 再送は失敗印（`UPLOAD_FAILED`・`UPLOAD_ATTEMPTS`）だけを外し、`UPLOAD_DECLARED`・`UPLOAD_SKIPPED` は残す。印の位置を塞ぐディレクトリは消さない | agent前提（`scripts/bugreport/README.md`「隔離された報告の再送」の手動手順と同じ） |
| D8 | 箱の状態判定を `PlaytestOutboxBoxState` に集め、件数表示・再送・書き出しが同じ判定を使う | agent前提（同じ判定を3か所に書かないため。`PlaytestOutboxScanner.cs` は191行で、足すと200行を超える） |
| D9 | 書き出しの進行は新しい topic を作らず、既存の `pause_menu.current` に `export` を足して届ける。action は受理だけ返す | agent前提（action の結果型 `ActionOutcome` は値を運べず、書き出しは数分かかり得るため。配送状況 `delivery` と同じ載せ方） |
| D10 | 録画テストでは「再送する」を押さない。再送は単体テストで確かめる | agent前提（この Mac mini の Editor は開発者トークンで本番の受け口へ送るため、偽の箱が本番に届く） |
| D11 | 開発者トークンの箱など `steamId` を持たない箱は、取り込みスクリプトで失敗として理由を出し、置かない | agent前提（受け口の置き場 `<steamId>/<id>` を偽の ID で埋めないため） |
| D12 | 書き出し状態の DTO は kind 文字列＋状態ごとの欄を null で省く形（前例 `PlaytestDeliveryDto` と同形）、板は読み書き同居の static クラス（前例 `PlaytestDeliveryStatusBoard` と同形）、WebUiHost が PlaytestReceiver の板を直接読む（前例 `PauseMenuTopic` と同形） | シミュレーター予測・確信高（前提宣言・拒否権つき） 2026-10-27 |
| D13 | 新しい文言7つすべてを4言語で、未翻訳キーが残らないか vitest で検査する | ユーザー裁定 2026-10-27（質問への回答「7つ全部を4言語で検査」） |
| D14 | 失敗印と試行回数を消す処理を `PlaytestFailureMarkers.Clear` に一本化し、既存の容量保留の解除・起動時補完もそれを使う。範囲に進行記録が入るかの判定を `PlaytestOutboxScopes.IncludesProgress` に一本化する。解除の失敗理由を型で分けること、門を持つ前提を型で縛ることはしない（ログで理由が出るため） | ユーザー裁定 2026-10-27（質問への回答「失敗印の解除とscope判定を一本化」） |
| D15 | 既存文言 `ui.bugReport.deliveryFailed` の「開発者に再送復旧を依頼してください」を消し、件数だけにする | agent前提（user-simulator review の適用推奨。同じ画面の「再送する」ボタンと矛盾するため） |
| D16 | 書き出しの本体を同期 `internal` メソッドに切り出し、テストはそれを呼ぶ | agent前提（`docs/development/async-and-ci-pitfalls.md`: batchmode でスレッドプールからの復帰が戻らない。前例 `CrashBundleWriter.Write`） |

## 本PR外のリファクタ提案（第3バケツ・着手可否は人間が決める）

- READY 要約の生成が2本になる: 既存 `tools/playtest-receiver/src/routes/uploadsComplete.ts`（受け口）／新設 `scripts/playtest/lib/import_export_zip.py` の `stage`。同じ形の READY を作る役割。取り込み側の予約名検査 `scripts/playtest/lib/safe_segment.py ready-files` を新設側も通すかを含めて揃える余地がある
- OS ごとの `Process.Start` 表示処理が2本になる: 既存 `moorestech_server/Assets/Scripts/Tests/Watchdog/OsDefaultOpener.cs`（テスト用・既定アプリで開く）／新設 `Recovery/OsFileReveal.cs`（選択表示）。役割の重なりは弱い

