# エディタ直Playの常時記録有効化 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は subagent-driven-development スキルが担う。実行モード（規模ゲート未満の単一subagent実装モード／閾値超のタスクごと派遣）は同スキルの規模ゲートに従って決める。ステップはチェックボックス（`- [ ]`）記法で書く。

**Goal:** エディタで GameInitializer シーンを直接Playしたとき、無人起動でなければ常時記録を有効にし、バグ報告の video／serverSnapshot／reportSent が揃うようにする。

**Architecture:** エディタ専用の起動上書きの束 `PlayModeLaunchOverrides` に「直Playの常時記録」を1本足す。判定は既存の無人起動の印（`PlaytestStartGateBypass`）を**消費せずに覗いて**行い、無人でなければ `AlwaysOnCaptureSetting.Apply(Enabled())` を呼ぶ。無人なら何もしない（理由をログ）。決定の持ち主は引き続き `AlwaysOnCaptureSetting` 1つ。

**Tech Stack:** Unity (C#)・NUnit・uloop CLI。bd タスク: `moorestech-sdme`。設計: `docs/adr/0066-editor-direct-play-enables-always-on-capture.md`・`.decisions/2026-09-21-エディタの手動直Playは常時記録を常に有効にする.md`。

## Requirements

- R1: エディタの直Play（素のPlayボタン・「生成ワールドでPlay」・「セーブ無しでPlay」）で、無人起動でなければ常時記録が有効になる。受入: 起動ログに `[AlwaysOnCaptureSetting] 常時記録 enabled:True` が出て、`スナップショットリングを開始しません`／`進行記録を開始しません` が出ない。
- R2: 無人起動（`PlaytestStartGateBypass` の印・batchMode・無人プロセス宣言）では自動有効化しない。受入: 理由入りのログ `[DirectPlayAlwaysOnCaptureSettings] 無人起動のため直Playの常時記録を自動では有効にしません reason:<理由>` が出て、`AlwaysOnCaptureSetting.Current` は呼び出し前の値のまま。
- R3: 無人起動のときに Disabled へ上書きしない。受入: 事前に `Apply(Enabled())` 済みの状態で無人判定を通しても `Current.IsEnabled == true` のまま（`PlaytestReportAndProgressTest` が通り続ける）。
- R4: 常時記録の判定は無人起動の印を消費しない。受入: 印を立てて覗いた後でも、開始ゲート側の `PlaytestStartGateBypass.UnattendedReason()` が非 null を返す。理由: 起動上書きは `InitializeScenePipeline` 序盤、開始ゲートは `MainGameInitializationFinalizer.cs:58` の終盤で走る。先に消費するとゲートが応答待ちで恒久停止する。
- R5: 「本番のプレイ開始だけが有効にする」旨のコメント（プロダクション4箇所＝うち `ProgressRecorder` は文言違いの同趣旨・テスト4箇所）を実態に合わせて書き換える。
- R6: `SkipSaveLoadPlayModeSettingsTest` から「セーブ無しPlayでは常時記録が無効」のアサートを外す（裁定で有効になったため）。`EditModeInPlayingTestUtilTest`・`PlaytestWorldBootSessionTest`・`StandaloneTerrainQaSettingsTest` の「無効のまま」アサートは維持する。
- やらないこと: バグ報告のアップロード判定（エディタは DeveloperMode で outbox 止まり）の変更／`ProgressRecords/` の保存先分離／uloop起動Playの除外／トグル・専用ボタンの追加／`PlaytestStartGateBypass` のクラス名変更。

## Global Constraints

- 作業ブランチ: `feature/editor-direct-play-always-on-capture`。最初に `pwd` で場所を確認する。
- `.cs` を変更したら必ず `uloop compile --project-path ./moorestech_client`。「Unity is reloading」エラーは45秒待って再試行。
- `.meta` は手で作らない（Unity が生成したものはコミット可）。`partial`・`Func<>`・デフォルト引数・try-catch は禁止。
- コメントは「// 日本語」→「// English」の2行セット。各1行。
- エディタ専用コードは `#if UNITY_EDITOR` で囲む。
- fail-closed で何もしない経路は理由をログへ出す（無音の縮退は禁止）。
- `moorestech_server/Assets/Scripts/Core.Master/_CompileRequester.cs` の dirty は戻さない・コミットもしない。
- テストは `--filter-type regex` で絞る。CI と `uloop run-tests` は batchMode のことがあり、`PlaytestStartGateBypass` は batchMode を常に無人と判定する。よって新規テストは無人理由を**引数で渡す**入口を叩く（前例: `PlaytestStartGates.WaitForGatesAsync`）。

## File Structure

| # | 項目 | 配置先 | 機構・前例 |
|---|---|---|---|
| 1 | `DirectPlayAlwaysOnCaptureSettings`（新規） | `moorestech_client/Assets/Scripts/Client.Starter/Editor/`（asmdef: Client.Starter・`#if UNITY_EDITOR`） | 同ディレクトリの `SkipSaveLoadPlayModeSettings`／`GeneratedWorldPlayModeSettings` と同役割（専用の起動上書き1本）。ディレクトリ内 .cs は5→6本 |
| 2 | `PlayModeLaunchOverrides.ApplyIfNeeded` へ1行追加 | 同上 | 上書きの束ね口。既存2本と同じ並び |
| 3 | `PlaytestStartGateBypass.PeekUnattendedReason()`（既存クラスへ追加） | `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Playtest/` | 無人起動の判定の持ち主はこのクラス。判定式を複製せず、消費の有無だけを分ける |
| 4 | テスト新規 `DirectPlayAlwaysOnCaptureSettingsTest` | `moorestech_client/Assets/Scripts/Client.Tests/Starter/` | `SkipSaveLoadPlayModeSettingsTest` と同じ置き場 |

層責務: `Client.Starter` は既に `Server.Boot`（`LocalGameLauncher` が `AlwaysOnCaptureSetting` を呼ぶ）と `Client.Game`（`PlaytestStartGates` が `PlaytestStartGateBypass` を呼ぶ）を参照済み。asmdef の変更は無い。`internal` メンバーは `Client.Starter/AssemblyInfo.cs` の `InternalsVisibleTo("Client.Tests")` でテストから見える。

データフロー: （起動経路）→［`AlwaysOnCaptureSetting.Current`］→（`ServerInstanceManager`・`GameFrameRecorder`・`ProgressRecorder`）。新規コンポーネントは**書き手**が1人増えるだけ。読み手・既存の書き手（`LocalGameLauncher`・`ConnectServer`）は無変更。

機能パリティ（死活表）:

| 操作 | 計画後 | 根拠 |
|---|---|---|
| メインメニュー→ローカル開始 | 生きる | `LocalGameLauncher` は無変更。上書き口で二重に Enabled が Apply されるが結果同じ |
| メインメニュー→接続 | 生きる | リモートは `PlayModeLaunchOverrides` が早期 return。`ConnectServer` が有効化済み |
| EditModeInPlayingTest／プレイ録画テストDSL | 生きる（無効のまま） | どちらも `PlaytestStartGateBypass.Apply()` 済み。印は消費しないのでゲート迂回も従来どおり |
| `PlaytestReportAndProgressTest`（無人＋明示Enabled） | 生きる | 無人時は上書きしない（R3） |
| 配布ビルド | 無影響 | 追加コードは全て `#if UNITY_EDITOR` |

---

### Task 1: 無人起動の理由を消費せずに覗く入口

**Files:**
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/BugReport/Playtest/PlaytestStartGateBypass.cs`
- Test: `moorestech_client/Assets/Scripts/Client.Tests/BugReport/Playtest/PlaytestStartGatesTest.cs`

**Interfaces:**
- Produces: `public static string PlaytestStartGateBypass.PeekUnattendedReason()` — 無人なら理由文字列、有人なら null。印を消費しない。既存の `UnattendedReason()` は「覗く＋消費」のまま戻り値不変。

- [x] **Step 1: 失敗するテストを書く** — `PlaytestStartGatesTest` の最後のテストの後ろへ追加する（SetUp/TearDown が印を読み捨てているのでそのまま使える）。

```csharp
        // 常時記録の判定は開始ゲートより先に走る。覗いただけで印が消えると、後から読むゲートが応答待ちで恒久停止する
        // The capture decision runs before the start gates; if peeking erased the mark, the gates reading later would wait forever
        [Test]
        public void 無人起動の理由は覗いても消費されない()
        {
            PlaytestStartGateBypass.Apply();

            var peeked = PlaytestStartGateBypass.PeekUnattendedReason();
            var consumed = PlaytestStartGateBypass.UnattendedReason();

            Assert.IsNotNull(peeked);
            Assert.AreEqual(peeked, consumed, "覗いた後にゲートが読む理由が変わっている");
        }
```

- [x] **Step 2: コンパイルして失敗を確認** — Run: `uloop compile --project-path ./moorestech_client` / Expected: `PeekUnattendedReason` 未定義の CS0117。

- [x] **Step 3: 実装** — `UnattendedReason()` を次の2メソッドへ置き換える（上のコメント3組は `UnattendedReason` の上に残す）。

```csharp
        public static string UnattendedReason()
        {
            var reason = PeekUnattendedReason();
            EraseUnattendedBootMark();
            return reason;
        }

        // 印を消費せずに同じ判定を返す。開始ゲートより先に走る判定（直Playの常時記録）が使う
        // Returns the same decision without consuming the mark, for decisions that run before the start gates (direct-play capture)
        public static string PeekUnattendedReason()
        {
            if (Application.isBatchMode) return "batchMode";
            if (_unattendedProcessReason != null) return _unattendedProcessReason;
            return IsUnattendedBootMarked() ? "unattendedBootMark" : null;
        }
```

`#if UNITY_EDITOR` 側の `ConsumeUnattendedBootMark` を次の2つへ置き換える:

```csharp
        private static bool IsUnattendedBootMarked()
        {
            return UnityEditor.SessionState.GetBool(SessionStateKey, false);
        }

        private static void EraseUnattendedBootMark()
        {
            UnityEditor.SessionState.EraseBool(SessionStateKey);
        }
```

`#else` 側の `ConsumeUnattendedBootMark` を次の2つへ置き換える（既存の2行コメントは `IsUnattendedBootMarked` の上へ残す）:

```csharp
        private static bool IsUnattendedBootMarked()
        {
            return false;
        }

        private static void EraseUnattendedBootMark()
        {
        }
```

- [x] **Step 4: テスト** — Run: `uloop compile --project-path ./moorestech_client` → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaytestStartGatesTest"` / Expected: 全件 PASS（既存の消費系テストも含む）。
- [x] **Step 5: コミット** — `git add` 上記2ファイル → `git commit -m "feat: 無人起動の理由を消費せずに覗く入口を足す"`

### Task 2: 直Playの常時記録有効化

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/Editor/DirectPlayAlwaysOnCaptureSettings.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/Editor/PlayModeLaunchOverrides.cs`
- Create (Test): `moorestech_client/Assets/Scripts/Client.Tests/Starter/DirectPlayAlwaysOnCaptureSettingsTest.cs`

**Interfaces:**
- Consumes: `PlaytestStartGateBypass.PeekUnattendedReason()`（Task 1）、`AlwaysOnCaptureSetting.Apply/Enabled/Disabled/Current`（`Server.Boot`）。
- Produces: `DirectPlayAlwaysOnCaptureSettings.ApplyIfNeeded()`（public）、`DirectPlayAlwaysOnCaptureSettings.ApplyForUnattendedReason(string unattendedReason)`（internal）。

- [x] **Step 1: 失敗するテストを書く**

```csharp
using System.Text.RegularExpressions;
using Client.Starter.Editor;
using NUnit.Framework;
using Server.Boot;
using UnityEngine;
using UnityEngine.TestTools;

namespace Client.Tests.Starter
{
    // エディタの直Playが、無人起動でないときだけ常時記録を有効にすることの回帰ガード（ADR 0066）
    // Regression guard that an Editor direct play enables always-on capture only when the boot is attended (ADR 0066)
    public class DirectPlayAlwaysOnCaptureSettingsTest
    {
        [SetUp]
        public void SetUp()
        {
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Disabled());
        }

        [TearDown]
        public void TearDown()
        {
            // 静的な決定の残置は後続テストの起動を録り始めさせるため必ず戻す
            // A leftover static decision would make later test boots start recording, so always reset it
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Disabled());
        }

        [Test]
        public void 有人の直Playは常時記録を有効にする()
        {
            DirectPlayAlwaysOnCaptureSettings.ApplyForUnattendedReason(null);

            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.True);
        }

        [Test]
        public void 無人起動は常時記録を有効にせず理由をログへ出す()
        {
            LogAssert.Expect(LogType.Log, new Regex("無人起動のため直Playの常時記録を自動では有効にしません reason:unattendedBootMark"));

            DirectPlayAlwaysOnCaptureSettings.ApplyForUnattendedReason("unattendedBootMark");

            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.False);
        }

        // 無人起動でも記録したいテストは起動前に明示的に有効化する。それを無効へ上書きしてはいけない
        // A test that wants capture on an unattended boot enables it up front; this must never overwrite that back to disabled
        [Test]
        public void 無人起動でも事前の明示的な有効化は潰さない()
        {
            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Enabled());

            DirectPlayAlwaysOnCaptureSettings.ApplyForUnattendedReason("batchMode");

            Assert.That(AlwaysOnCaptureSetting.Current.IsEnabled, Is.True);
        }
    }
}
```

- [x] **Step 2: コンパイルして失敗を確認** — Expected: `DirectPlayAlwaysOnCaptureSettings` 未定義の CS0103/CS0246。

- [x] **Step 3: 実装** — 新規ファイル:

```csharp
#if UNITY_EDITOR
using Client.Game.InGame.BugReport.Playtest;
using Server.Boot;
using UnityEngine;

namespace Client.Starter.Editor
{
    /// <summary>
    /// エディタの直Playで常時記録を有効にする。メインメニューを通らない起動でもバグ報告の中身が揃うようにする（ADR 0066）。
    /// Enables always-on capture for an Editor direct play, so a boot that skips the main menu still yields a complete bug report (ADR 0066).
    /// </summary>
    public static class DirectPlayAlwaysOnCaptureSettings
    {
        public static void ApplyIfNeeded()
        {
            // 印は開始ゲートが後で消費するので、ここでは覗くだけにする
            // The start gates consume the mark later, so this only peeks at it
            ApplyForUnattendedReason(PlaytestStartGateBypass.PeekUnattendedReason());
        }

        // 無人の理由を引数で受ける本体。CIは常にbatchModeで無人になるため、有人の経路も検証できるよう分ける
        // The body takes the unattended reason, so the attended path stays verifiable under CI's always-unattended batch mode
        internal static void ApplyForUnattendedReason(string unattendedReason)
        {
            // 無人起動は有効にしないだけで、無効へは上書きしない。記録したい無人テストは起動前に明示的に有効化している
            // An unattended boot is merely left alone, never forced to disabled: unattended tests that want capture enable it up front
            if (unattendedReason != null)
            {
                Debug.Log($"[DirectPlayAlwaysOnCaptureSettings] 無人起動のため直Playの常時記録を自動では有効にしません reason:{unattendedReason}");
                return;
            }

            AlwaysOnCaptureSetting.Apply(AlwaysOnCaptureSetting.Enabled());
        }
    }
}
#endif
```

`PlayModeLaunchOverrides.ApplyIfNeeded` の `GeneratedWorldPlayModeSettings.ApplyIfNeeded(proprieties);` の直後へ追加（リモート接続の早期 return より後ろに置く。リモートは `ConnectServer` が既に有効化している）:

```csharp

            // 人が押した直Playは常時記録を有効にする
            // A direct play started by a person enables always-on capture
            DirectPlayAlwaysOnCaptureSettings.ApplyIfNeeded();
```

- [x] **Step 4: テスト** — Run: compile → `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "DirectPlayAlwaysOnCaptureSettingsTest|AlwaysOnCaptureSettingTest|SkipSaveLoadPlayModeSettingsTest|PlaytestWorldBootSessionTest|StandaloneTerrainQaSettingsTest|EditModeInPlayingTestUtilTest"` / Expected: 全件 PASS。
- [x] **Step 5: コミット** — Unity が生成した新規 `.meta` 2本も含めて `git add` → `git commit -m "feat: エディタの直Playで常時記録を有効にする（無人起動は除外）"`

### Task 3: コメントと既存テストを裁定に合わせる

**Files:**
- Modify: `moorestech_server/Assets/Scripts/Server.Boot/Args/AlwaysOnCaptureSetting.cs:20-21`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/LocalGameLauncher.cs:14-15`
- Modify: `moorestech_client/Assets/Scripts/Client.MainMenu/ConnectServer.cs:59-60`
- Modify: `moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/ProgressRecorder.cs:47-48`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Starter/SkipSaveLoadPlayModeSettingsTest.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/Playtest/PlaytestWorldBootSessionTest.cs:36-37`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Util/EditModeInPlayingTestUtilTest.cs:20-21`
- Modify: `moorestech_client/Assets/Scripts/Client.Tests/StandaloneQa/StandaloneTerrainQaSettingsTest.cs:35-36`

- [x] **Step 1: プロダクションのコメント4箇所を置換**

`AlwaysOnCaptureSetting.cs`:
```csharp
        // 既定は無効。有効化を明示した起動経路（人が始めたプレイ）だけが録り始め、無人起動は無効のまま走る
        // Disabled by default; only a boot path that explicitly enables it — a play a person started — begins recording, while unattended boots stay disabled
```
`LocalGameLauncher.cs`:
```csharp
            // 人が始めたプレイは常時記録を有効にする。エディタの直Playは DirectPlayAlwaysOnCaptureSettings が同じ決定を入れる
            // A play a person started enables always-on capture; an Editor direct play gets the same decision from DirectPlayAlwaysOnCaptureSettings
```
`ConnectServer.cs`:
```csharp
            // 人が始めたプレイは常時記録を有効にする。接続先が別プロセスでも録画リングはこちらで回る
            // A play a person started enables always-on capture; the recording ring runs here even when the server is another process
```

`ProgressRecorder.cs`（直Play＝開発・調査のプレイは裁定で `ProgressRecords/` へ書くことになったので「調査用」の語を外す）:
```csharp
        // 記録するかの決定は AlwaysOnCaptureSetting が1つだけ持つ。持たないと無人起動（テスト・プレイ録画テスト）まで本番のProgressRecords/へ書き始める
        // AlwaysOnCaptureSetting holds the only decision on whether to record; without it even unattended boots (tests, recorded playtests) write into the real ProgressRecords/
```

- [x] **Step 2: `SkipSaveLoadPlayModeSettingsTest` を書き換える** — クラス上のコメント、1本目のテスト名、末尾のアサートを次へ。`using Server.Boot;` は `CliConvert`／`StartServerSettings` が `Server.Boot`／`Server.Boot.Args` 由来なので残す。

```csharp
    // SkipSaveLoadPlayModeがAutoSaveを無効にして起動させ続けることの回帰ガード。常時記録はここでは決めない（ADR 0066）
    // Regression guard that SkipSaveLoadPlayMode keeps booting with auto-save off; always-on capture is not decided here (ADR 0066)
```
```csharp
        [Test]
        public void フラグ有効時はAutoSaveを無効化する()
        {
            SessionState.SetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, true);
            var proprieties = InitializeProprieties.CreateLocalServer(null);

            SkipSaveLoadPlayModeSettings.ApplyIfNeeded(proprieties);

            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            Assert.That(settings.AutoSave, Is.False);
        }
```

- [x] **Step 3: 残すアサートのコメント3箇所を置換** — アサート自体は触らない。

`PlaytestWorldBootSessionTest.cs`:
```csharp
            // 常時記録は人が始めたプレイだけが有効にするので、この経路を通っても無効のまま
            // Only a play a person started enables always-on capture, so this path leaves it disabled
```
`EditModeInPlayingTestUtilTest.cs`:
```csharp
            // 常時記録は人が始めたプレイだけが有効にするので、無人のテスト起動では無効のまま
            // Only a play a person started enables always-on capture, so an unattended test boot leaves it disabled
```
`StandaloneTerrainQaSettingsTest.cs`:
```csharp
            // 常時記録は人が始めたプレイだけが有効にするので、QA起動では無効のまま
            // Only a play a person started enables always-on capture, so a QA boot leaves it disabled
```

- [x] **Step 4: 取りこぼし確認** — Run: `grep -rn "本番のプレイ開始だけ" --include="*.cs" moorestech_client/Assets/Scripts moorestech_server/Assets/Scripts` と `grep -n "調査用" moorestech_client/Assets/Scripts/Client.Game/InGame/Playtest/Progress/ProgressRecorder.cs` / Expected: どちらも0件（`ServerShutdownReleasesCaptureTest.cs` の「本番のプレイ開始と同じく明示的に有効化」は事実のままなので対象外）。
- [x] **Step 5: テスト** — compile → Task 2 Step 4 と同じ regex / Expected: 全件 PASS。
- [x] **Step 6: コミット** — `git commit -m "docs: 常時記録の有効化条件のコメントとテストをADR 0066へ合わせる"`

### Task 4: 実機確認（エディタ直Play／無人起動）

**Files:** なし（確認のみ。結果は `bd note moorestech-sdme` へ）

前提: 画面ロック中は uloop が無言ハングする。別 worktree の Unity が PlayMode 中でないこと（ポート11564固定）。

- [ ] **Step 0: 残留印の掃除** — 直前の無人テストが開始ゲートへ届かずに落ちていると印が残り、Step 1 が無人扱いになって空振りする。`uloop execute-dynamic-code` で `Client.Game.InGame.BugReport.Playtest.PlaytestStartGateBypass.UnattendedReason();` を1回呼んで読み捨てる（戻り値が非 null だったら残留していた旨を bd note に書く）。
- [ ] **Step 1: 有人の直Play** — GameInitializer シーンを開いた状態で `uloop clear-console` → `uloop control-play-mode --project-path ./moorestech_client --action Play`（印を立てない素のPlay＝人のPlayと同じ扱い・ADR 0066 裁定2）。MainGame 到達後に `uloop get-logs --project-path ./moorestech_client --log-type Log`。
  合格: `常時記録 enabled:True` が1行以上あり、警告・拒否側の語 `開始しません`／`無効のため`／`有効にしません` が**0件**。続けて Error ログも引き、今回の変更由来の例外が無いこと。
- [ ] **Step 2: バグ報告の中身** — 同じPlayの中でバグ報告を1件送る（Web UI のバグ報告。操作は unity-playmode-recorded-playtest スキルの references を参照）。`~/Library/Application Support/moorestech/BugReports/outbox/` の最新箱の `manifest.json` を読む。
  合格: `missing` に `video`・`serverSnapshot` が**無い**（`steamId` は Steam 未起動なら残ってよい）。進行記録へ reportSent が追記されている。アップロードされず outbox に残るのは仕様（DeveloperMode）。Play を Stop する。
- [ ] **Step 3: 無人起動** — `uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value "PlaytestReportAndProgressTest|EditModeInPlayingTestUtilTest"`（ドメインリロードのエラーは45秒待って再試行）。
  合格: 全件 PASS（無人でもゲートで停止しない＝印が消費されていない、明示 Enabled が潰れていない）。可能なら DSL シナリオも1本回し、ログに `無人起動のため直Playの常時記録を自動では有効にしません reason:unattendedBootMark` が出て `enabled:True` が出ないこと。
- [ ] **Step 4: 記録** — 各ステップの実測（ログ行・manifest の missing）を `bd note moorestech-sdme "..."` へ。未確認が残ったら Task 5 (a) で起票する。

### Task 5: 全ブランチレビュー（省略不可）

- [ ] **Step 1:** 必ず最後に moores-code-review スキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。
- [ ] **Step 2:** レビュー反映が判定経路（`PeekUnattendedReason`・`ApplyForUnattendedReason`・`PlayModeLaunchOverrides` の呼び出し位置）に触れたら、反映後のコードで Task 4 を再実施してから完了とする。
- [ ] **Step 3 (a):** plan・bd note に書いた「未確認」「残差」は1件ずつ `bd create --parent moorestech-sdme` で起票し、結論には issue id を列挙する。
- [ ] **Step 4 (b):** Task 4 の合否は肯定行の存在ではなく、警告・拒否語（`開始しません`・`有効にしません`・`無効のため`・`missing`）が該当区間で期待どおり（有人=0件／無人=理由付き1件）であることで判定する。
- [ ] **Step 5:** `bd close moorestech-sdme --reason="..."`、全作業をコミット、pr-create スキルで PR を作る（マージは通常のマージコミット）。

## Self-Review 結果

- Requirements coverage: R1→Task 2・4／R2→Task 2／R3→Task 2・4／R4→Task 1・4／R5・R6→Task 3。
- 保留・縮退経路: 「無人なら有効にしない」は恒久の設計どおりの縮退で、解消待ちの保留ではない。最小構成（印なし・印あり・事前Enabled＋無人）を全てテスト指定済み。印の消費主体（開始ゲート）が起動失敗で到達しない場合、印が次の手動Playへ残りうるのは既存挙動で、本planは悪化させない（その場合は理由付きログが出る）。
- 既知の弱点: `無人起動の理由は覗いても消費されない` は batchMode 実行では両方 `"batchMode"` になり印の検証として空振りする。GUI の Editor で走らせたときだけ印を検証する。Task 4 Step 3（無人テストがゲートで止まらない）が実機側の担保。

## 判断記録（ADR）

- 設計: `docs/adr/0066-editor-direct-play-enables-always-on-capture.md`、`.decisions/2026-09-21-エディタの手動直Playは常時記録を常に有効にする.md`
- 印は消費せず覗く（`PeekUnattendedReason` 追加）。出所: agent前提（上書き口がゲートより先に走る実測。キャッシュ方式は `PlaytestStartGatesTest` の読み捨て前提を壊すため不採用）
- `PlaytestStartGateBypass` のクラス名は据え置き。無人起動の判定が開始ゲート以外にも使われるようになるが、改名は参照が広く本件の範囲外。出所: agent前提
- 有効化は `PlayModeLaunchOverrides` のリモート早期 return の後ろ。出所: agent前提（リモートは `ConnectServer` が有効化済み・直Playは常に内蔵サーバー）
