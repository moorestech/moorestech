# 出展自動起動テストの軽量化 Implementation Plan

> **For the controller session (実装を担うsubagentはこのブロックを無視してよい):** このplanの実行は moores-subagent-driven-development スキルが担う。Codexでは同スキルの `references/runtime-codex.md` に従う。ステップはチェックボックス記法で追跡する。

**Goal:** 毎回ワールド全量生成を行う出展自動起動テストを廃止し、本番の自動開始処理の順序・期限切れ・取消を通常のEditModeテストで守る。

**Architecture:** タイトルゲートの待機は既存の `PlaytestTitleGates.WaitUntilPassedWithinDeadlineAsync` に委譲する。待機後の処理を内部の `EventModeAutoStartSequence` へ切り出し、ワールド削除・ゲーム開始・プロセス終了という副作用だけを内部interface越しに呼ぶ。本番アダプターは現在と同じAPIを呼び、テストは呼び出しを記録する。

**Tech Stack:** Unity 6000.3.8f1 / C# / UniTask / NUnit / Unity Test Framework / uloop。

**Baseline:** `origin/master` = `bb41e34f412d5709497b6ad698c3dce81c640e4b`。PR #1415は既にマージ済み。本変更は最新masterを土台に別PRとして作成する。
**Worktree:** `/Users/sakastudio/hermes-agent/data/worktrees/moorestech/exhibition-boot-test-plan-20261013`、branch `plan/exhibition-boot-test`。
**Beads:** plan `moorestech-w7ggu.1.1`、実装・CI課題 `moorestech-w7ggu.1`。

## Requirements

- R1: `EventModeAutoStartBootTest` と専用環境クラスを削除し、CIでこのテストに伴うMainGame起動・ワールド生成が実行されなくなる。
- R2: 本番のAfterSceneLoadフックから同じ待機処理を使用する。ゲート始動前にはワールド削除もゲーム開始も行わず、ゲート通過後に1実行につき一度だけ新規開始する。
- R3: ゲートが既に通過している場合も、一度だけ新規開始する。
- R4: 期限切れではワールドを削除せずゲームも開始せず、理由付きエラーログとApplication.Quitを実行する。期限は既存 `PlaytestTitleGates.UnattendedPassTimeoutSeconds`（現在60秒）を参照する。
- R5: 終了取消時には開始・ワールド削除・期限切れ終了の副作用を起こさず、既存のForget例外ログで理由を残す。
- R6: 本番の新規開始は `GameSystemPaths.DeleteDefaultWorldDirectory()` → `LocalGameLauncher.StartLocalGame()` の順序を維持する。起動条件、言語適用、無人起動宣言も維持する。
- R7: 置換テストはEnterPlayMode・シーンロード・実ワールド削除・サーバー起動を行わない。通常のClient.TestsとしてCIに含まれ、期限切れ試験は既存試験と同じ0.2秒の短い期限で実行する。
- R8: `MethodCallInspector`を再利用し、Unity属性、本番フックからsequenceへの配線、本番アダプターの削除→開始と期限切れログ→終了を検証する。
- R9: 削除する統合テストが担っていた「本物のMainMenuからMainGameまでの全経路を実行する」検証は本変更の自動テストでは代替しない。このカバレッジ差をPRに明記する。既存の他の起動・地形テストを削除しない。
- R10: 全ブランチレビュー、C#コンパイル、対象テスト、CI成功、commit/push、新規PR作成まで実行して閉じる。

## Global Constraints

- すべてのコードファイルは200行未満。partial / Func<> / デフォルト引数は禁止。新規コードは各ディレクトリ10ファイル以内に収める。
- 主要な処理コメントは日本語→英語の2行セット。テストのためだけのpublic窓口を追加しない。新規型はinternal、既存InternalsVisibleTo("Client.Tests")を利用する。
- Unity .metaを手で作成しない。追加・削除はEditorのAssetDatabaseに反映し、Unityが生成・削除した.metaをコミットする。
- Unity作業は実装用worktreeのEditorのみ。C#変更後は必ずcompileする。run-testsと録画テストを同時実行しない。
- 既存の10月1日裁定を守る: 期限切れはワールドを消さず終了、テストは開発者の実セーブに触れない。
- 列車の経路なし解除・D5裁定更新、地形生成の高速化、CIキャッシュや40分のstep上限変更はこのplanの対象外。列車課題 `moorestech-vwdd1.1` の未回答裁定を承認済みと扱わない。
- バックグラウンドのポーリングで待たず全部前景で実行する／区切りごとにコミットする。完了は報告だけでなくgit logと報告ファイルで照合する。

## 配置と前例

| ファイル・責務 | 所属 | 同じ役割の既存部品 | 判断 |
|---|---|---|---|
| EventMode/AutoStart/IEventModeAutoStartActions.cs: 起動副作用の契約 | Client.Starter | EventMode/IEventIdleWatchArmer.cs | 新規。削除・開始・終了は監視の武装とは異なる副作用なので既存interfaceを拡張しない |
| EventMode/AutoStart/EventModeAutoStartSequence.cs: 待機後の分岐 | Client.Starter | EventModeStartGate.AwaitSelectionThenArmAsync | 同じ役割の明示駆動に従う。待機自体はPlaytestTitleGatesを呼ぶ |
| EventMode/AutoStart/EventModeAutoStartActions.cs: 本番の副作用 | Client.Starter | 現EventModeAutoStart内のDelete/Start/Quit | 既存処理を移設。新しい開始経路やコピーしたゲート判定を作らない |
| EventModeAutoStart.cs: Unity起動フック | Client.Starter | 置換対象そのもの | AfterSceneLoad属性と既存の宣言・言語適用・Forgetログを維持 |
| EventMode/AutoStart/RecordingEventModeAutoStartActions.cs | Client.Tests | EventModeStartGateTest.RecordingIdleWatchArmer | 新規。今回の副作用を記録するfixture専用実装 |
| EventMode/AutoStart/EventModeAutoStartSequenceTest.cs | Client.Tests | PlaytestTitleGatesWaitTest | 既存の本物のゲートとunattended compositionを呼ぶ。待機ロジックの写しを作らない |
| EventMode/AutoStart/EventModeAutoStartBindingTest.cs | Client.Tests | PlaytestBootUnityCallbackBindingTest / MethodCallInspector | Inspectorをそのまま呼び、IL解析器を複製しない |

| 旧Bootテストと専用環境の削除 | Client.Tests | EventModeAutoStartBootTest自身 | 上記のsequence/配線試験へ役割を限定して置換。全実シーン起動の検出範囲はPRに明記 |

パスの省略部分はTaskのFiles節で完全なリポジトリ相対パスを示す。新規本番型3個はEventMode/AutoStartへ、テスト3個も同名サブディレクトリへ配置する。

**データフロー:** Unity AfterSceneLoad → 起動条件・無人宣言・言語適用 → sequence → 既存タイトルゲートの始動/通過を待つ → 本番actions → ワールド削除→既存LocalGameLauncher。sequenceは既存の開始役。待機の書き手や別のゲートは追加しない。

**機構比較:** 本番フックにCI専用分岐や疑似シーンを足す案は採用しない。既存ゲートを無傷で使い、OS/ゲーム起動の副作用境界だけを差し替える。呼び出し方向は現在と同じ明示駆動で、購読による第2の起動経路を作らない。

| 操作 | 計画後の挙動 | 根拠 |
|---|---|---|
| 通常モード・MainMenu以外 | 自動開始しない | ShouldRunを維持し既存EventExhibitionModeTestを実行 |
| 出展モード・MainMenu | 宣言と言語適用の後、通過待ちをして新規開始 | 実sequenceのテスト＋本番フック配線のテスト |
| タイトル未始動・未通過 | 待機し、既存60秒の期限でワールドを残して終了 | 既存wait＋今回の期限切れ副作用試験 |
| 通過後の開始 | ワールド削除の後にゲーム開始 | 成功試験と実アダプターの呼び出し順検査 |
| 終了の取消 | 開始しない | cancel試験＋既存Forgetログ |

ユーザー操作を恒久に失敗させる新規状態は作らない。期限切れのプロセス終了は既存裁定どおりで、再起動の主体は出展ループスクリプト。期限を過ぎた旧実行が後から開始しないことをテストする。

---

### Task 1: 本番の待機処理を切り出し、軽量の回帰テストで配線と副作用を守る

**Files:**
- Create: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/AutoStart/IEventModeAutoStartActions.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/AutoStart/EventModeAutoStartSequence.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/AutoStart/EventModeAutoStartActions.cs`
- Modify: `moorestech_client/Assets/Scripts/Client.Starter/EventMode/EventModeAutoStart.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EventMode/AutoStart/RecordingEventModeAutoStartActions.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EventMode/AutoStart/EventModeAutoStartSequenceTest.cs`
- Create: `moorestech_client/Assets/Scripts/Client.Tests/EventMode/AutoStart/EventModeAutoStartBindingTest.cs`

**Interfaces:** Consumes: `PlaytestTitleGates.WaitUntilPassedWithinDeadlineAsync(float, CancellationToken)` / `GameSystemPaths.DeleteDefaultWorldDirectory()` / `LocalGameLauncher.StartLocalGame()` / `Application.Quit()`。Produces: `internal static UniTask EventModeAutoStartSequence.RunAsync(IEventModeAutoStartActions actions, float deadlineSeconds, CancellationToken exitToken)`。

- [ ] **Step 1: 次のテストファイルを作る。** 実行対象の操作だけを記録する。Test assembly内のfakeから本番のワールドAPIは呼ばない。

`RecordingEventModeAutoStartActions.cs`:
```csharp
using System.Collections.Generic;
using Client.Starter.EventMode;

namespace Client.Tests.EventMode
{
    internal sealed class RecordingEventModeAutoStartActions : IEventModeAutoStartActions
    {
        internal readonly List<string> Calls = new();
        internal float QuitDeadlineSeconds;

        public void StartFreshWorld() => Calls.Add("start");

        public void QuitBecauseTitleGatesTimedOut(float deadlineSeconds)
        {
            QuitDeadlineSeconds = deadlineSeconds;
            Calls.Add("quit");
        }
    }
}
```

`EventModeAutoStartSequenceTest.cs`:
```csharp
using System.Collections;
using System.Threading;
using Client.Game.InGame.BugReport.Playtest;
using Client.Localization;
using Client.PlaytestReceiver.Launch;
using Client.Starter.EventMode;
using Client.Starter.Playtest.TitleGates;
using Client.Tests.BugReport;
using Client.Tests.PlaytestReceiver;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Client.Tests.EventMode
{
    public class EventModeAutoStartSequenceTest
    {
        private const float ShortDeadlineSeconds = 0.2f;
        private const float ObservationLimitSeconds = 5f;

        [SetUp]
        public void SetUp()
        {
            Localize.Initialize();
            PlaytestTitleGates.ResetOnPlayMode();
            PlaytestStartGateBypass.ResetOnPlayMode();
            PlaytestLaunchProfile.Apply(PlaytestLaunchKind.DeveloperMode,
                new EmptyPlaytestSessionIdentity(EmptyPlaytestSessionIdentity.DeveloperModeReason));
        }

        [TearDown]
        public void TearDown()
        {
            PlaytestTitleGates.ResetOnPlayMode();
            PlaytestStartGateBypass.ResetOnPlayMode();
            PlaytestLaunchProfile.ResetOnPlayMode();
        }

        [UnityTest, Timeout(5000)]
        public IEnumerator WaitsBeforeTitleStartsAndStartsOnceAfterPass() => UniTask.ToCoroutine(async () =>
        {
            using var exit = new CancellationTokenSource();
            var actions = new RecordingEventModeAutoStartActions();
            var run = EventModeAutoStartSequence.RunAsync(actions, ObservationLimitSeconds, exit.Token);
            Assert.AreEqual(UniTaskStatus.Pending, run.Status);
            Assert.IsEmpty(actions.Calls);
            PassUnattendedTitle(exit.Token);
            await run;
            await UniTask.Yield();
            CollectionAssert.AreEqual(new[] { "start" }, actions.Calls);
            exit.Cancel();
        });

        [UnityTest, Timeout(5000)]
        public IEnumerator AlreadyPassedTitleStartsOnce() => UniTask.ToCoroutine(async () =>
        {
            using var exit = new CancellationTokenSource();
            PassUnattendedTitle(exit.Token);
            var actions = new RecordingEventModeAutoStartActions();
            await EventModeAutoStartSequence.RunAsync(actions, ObservationLimitSeconds, exit.Token);
            CollectionAssert.AreEqual(new[] { "start" }, actions.Calls);
            exit.Cancel();
        });

        [UnityTest, Timeout(5000)]
        public IEnumerator DeadlineQuitsWithoutStartingAndNeverStartsLate() => UniTask.ToCoroutine(async () =>
        {
            using var exit = new CancellationTokenSource();
            var actions = new RecordingEventModeAutoStartActions();
            await EventModeAutoStartSequence.RunAsync(actions, ShortDeadlineSeconds, exit.Token);
            CollectionAssert.AreEqual(new[] { "quit" }, actions.Calls);
            Assert.AreEqual(ShortDeadlineSeconds, actions.QuitDeadlineSeconds);
            PassUnattendedTitle(exit.Token);
            await UniTask.Yield();
            CollectionAssert.AreEqual(new[] { "quit" }, actions.Calls);
            exit.Cancel();
        });

        [UnityTest, Timeout(5000)]
        public IEnumerator ExitCancellationDoesNotStartOrQuitAgain() => UniTask.ToCoroutine(async () =>
        {
            using var exit = new CancellationTokenSource();
            var actions = new RecordingEventModeAutoStartActions();
            var run = EventModeAutoStartSequence.RunAsync(actions, ObservationLimitSeconds, exit.Token);
            exit.Cancel();
            Assert.IsTrue(await run.SuppressCancellationThrow());
            Assert.IsEmpty(actions.Calls);
        });

        [UnityTest, Timeout(5000)]
        public IEnumerator CancelledExitDoesNotStartAnAlreadyPassedTitle() => UniTask.ToCoroutine(async () =>
        {
            using var exit = new CancellationTokenSource();
            PassUnattendedTitle(exit.Token);
            exit.Cancel();
            var actions = new RecordingEventModeAutoStartActions();
            var run = EventModeAutoStartSequence.RunAsync(actions, ObservationLimitSeconds, exit.Token);
            Assert.IsTrue(await run.SuppressCancellationThrow());
            Assert.IsEmpty(actions.Calls);
        });

        private static void PassUnattendedTitle(CancellationToken exitToken)
        {
            // 本物の無人ゲートを合成し、同意ファイルには触れない
            // Compose the real unattended gates without modifying the consent file
            PlaytestTitleGates.BeginComposed(TestPreviousSessionArtifacts.Unclean(), true,
                new RecordingUploadRequester(), "eventModeAutoStart", exitToken);
        }
    }
}
```

`EventModeAutoStartBindingTest.cs`:
```csharp
using System.Reflection;
using Client.Common;
using Client.Starter.EventMode;
using Client.Tests.Playtest;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.EventMode
{
    public class EventModeAutoStartBindingTest
    {
        [Test]
        public void RuntimeHookUsesAfterSceneLoadAndProductionSequence()
        {
            var hook = typeof(EventModeAutoStart).GetMethod(nameof(EventModeAutoStart.AutoStartIfEventMode));
            var attribute = hook.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(attribute);
            Assert.AreEqual(RuntimeInitializeLoadType.AfterSceneLoad, attribute.loadType);
            var sequence = typeof(EventModeAutoStartSequence).GetMethod(nameof(EventModeAutoStartSequence.RunAsync),
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(sequence);
            Assert.IsTrue(MethodCallInspector.ContainsCall(hook, sequence));
        }

        [Test]
        public void ProductionActionsDeleteWorldBeforeStarting()
        {
            var action = typeof(EventModeAutoStartActions).GetMethod(nameof(EventModeAutoStartActions.StartFreshWorld));
            var delete = typeof(GameSystemPaths).GetMethod(nameof(GameSystemPaths.DeleteDefaultWorldDirectory));
            var start = typeof(LocalGameLauncher).GetMethod(nameof(LocalGameLauncher.StartLocalGame));
            Assert.IsTrue(MethodCallInspector.CallsInOrder(action, delete, start));
        }

        [Test]
        public void ProductionTimeoutLogsBeforeQuitting()
        {
            var action = typeof(EventModeAutoStartActions).GetMethod(nameof(EventModeAutoStartActions.QuitBecauseTitleGatesTimedOut));
            var log = typeof(Debug).GetMethod(nameof(Debug.LogError), new[] { typeof(object) });
            var quit = typeof(Application).GetMethod(nameof(Application.Quit), System.Type.EmptyTypes);
            Assert.IsTrue(MethodCallInspector.CallsInOrder(action, log, quit));
        }
    }
}
```

- [ ] **Step 2: REDを確認する。** `uloop compile --project-path ./moorestech_client` を実行。まだ存在しないIEventModeAutoStartActions/EventModeAutoStartSequence/EventModeAutoStartActionsに対するCS0246/CS0103を確認する。C#ではこれを最初のREDとし、コンパイル失敗中に古いassemblyのrun-testsを合格扱いしない。

- [ ] **Step 3: 本番3ファイルを実装し、既存フックから接続する。**

`IEventModeAutoStartActions.cs`:
```csharp
namespace Client.Starter.EventMode
{
    internal interface IEventModeAutoStartActions
    {
        void StartFreshWorld();
        void QuitBecauseTitleGatesTimedOut(float deadlineSeconds);
    }
}
```

`EventModeAutoStartSequence.cs`:
```csharp
using System.Threading;
using Client.Starter.Playtest.TitleGates;
using Cysharp.Threading.Tasks;

namespace Client.Starter.EventMode
{
    internal static class EventModeAutoStartSequence
    {
        internal static async UniTask RunAsync(IEventModeAutoStartActions actions,
            float deadlineSeconds, CancellationToken exitToken)
        {
            // 既存ゲートの期限付き待機を通し、期限後の開始は行わない
            // Use the existing bounded wait and never start after expiry
            var passed = await PlaytestTitleGates.WaitUntilPassedWithinDeadlineAsync(deadlineSeconds, exitToken);

            // 終了要求が来ていれば、開始も期限切れ終了も重ねない
            // Do not start or issue another expiry quit after exit was requested
            exitToken.ThrowIfCancellationRequested();
            if (!passed)
            {
                actions.QuitBecauseTitleGatesTimedOut(deadlineSeconds);
                return;
            }
            actions.StartFreshWorld();
        }
    }
}
```

`EventModeAutoStartActions.cs`:
```csharp
using Client.Common;
using Game.Paths;
using UnityEngine;

namespace Client.Starter.EventMode
{
    internal sealed class EventModeAutoStartActions : IEventModeAutoStartActions
    {
        public void StartFreshWorld()
        {
            // 開始が確定してからワールドを消して既存の開始入口を呼ぶ
            // Wipe only after the start is settled, then use the existing launch entry
            GameSystemPaths.DeleteDefaultWorldDirectory();
            LocalGameLauncher.StartLocalGame();
        }

        public void QuitBecauseTitleGatesTimedOut(float deadlineSeconds)
        {
            // 期限切れはワールドを残し、理由を記録して出展ループへ終了を返す
            // Preserve the world on expiry, log the reason and quit for the exhibition loop
            Debug.LogError($"EventModeAutoStart: title gates did not pass within {deadlineSeconds}s (the title composition root may not have started the sequence); quitting without wiping the world so the loop script restarts the game");
            Application.Quit();
        }
    }
}
```

`EventModeAutoStart.cs`の既存`StartWhenTitleGatesPassAsync().Forget(LogAutoStartException);`を次の呼び出しに置換し、同メソッド内のローカル関数`StartWhenTitleGatesPassAsync`全文だけを削除する。ShouldRun・ApplyLaunchLanguage・宣言/言語適用の順序・LogAutoStartExceptionは残す。不要になったusingは削除する。
```csharp
EventModeAutoStartSequence.RunAsync(new EventModeAutoStartActions(),
    PlaytestTitleGates.UnattendedPassTimeoutSeconds,
    Application.exitCancellationToken).Forget(LogAutoStartException);
```

- [ ] **Step 4: GREENと既存回帰を確認する。**
```bash
uloop compile --project-path ./moorestech_client
uloop run-tests --project-path ./moorestech_client --filter-type regex --filter-value 'EventModeAutoStartSequenceTest|EventModeAutoStartBindingTest|PlaytestTitleGatesWaitTest|EventExhibitionModeTest|EventModeLaunchLanguageTest|EventModeStartGateTest|PlaytestBootUnityCallbackBindingTest'
```
Expected: compile ErrorCount=0、対象テスト全PASS。新規sequence 5件、binding 3件が実行されたことを結果XMLで確認する。新規テストはEditModeのまま完走し、WorldDataDirectoryやMainGameを作らない。既存の無人ゲートを合成した際の正常な宣言Warningは、拒否/例外ログと区別する。

- [ ] **Step 5: .metaを含めてコミットする。**
```bash
git add moorestech_client/Assets/Scripts/Client.Starter/EventMode moorestech_client/Assets/Scripts/Client.Tests/EventMode/AutoStart
git commit -m "refactor: isolate exhibition auto-start effects and test startup order"
```

### Task 2: 重い統合テストを削除し、CI実行対象と削除範囲を確認する

**Files:**
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Boot/EventModeAutoStartBootTest.cs`
- Delete: `moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Boot/EventModeAutoStartBootEnvironment.cs`
- Delete: 上記2ファイルに対応するUnity管理の.meta。

**Interfaces:** Consumes: Task 1の本番sequenceと軽量テスト。Produces: 旧統合試験の未参照化、軽量テスト8件のCI包含。

- [ ] **Step 1: 他から参照されていないことを確認する。** `rg -n 'EventModeAutoStartBootEnvironment|EventModeAutoStartBootTest' moorestech_client/Assets/Scripts .github scripts`。旧2ファイル以外に参照が出た場合は役割を確認し、未参照の状態になるまで必要な参照を新sequenceへ更新する。現在のmasterでは旧2ファイル内の参照だけ。

- [ ] **Step 2: Unity経由で旧2ファイルと.metaを削除する。**
```bash
uloop execute-dynamic-code --project-path ./moorestech_client --code 'using UnityEditor; var testDeleted = AssetDatabase.DeleteAsset("Assets/Scripts/Client.Tests/EditModeInPlayingTest/Boot/EventModeAutoStartBootTest.cs"); var environmentDeleted = AssetDatabase.DeleteAsset("Assets/Scripts/Client.Tests/EditModeInPlayingTest/Boot/EventModeAutoStartBootEnvironment.cs"); AssetDatabase.Refresh(); return new { testDeleted, environmentDeleted };'
```
Expected: 両方true、2つの.cs/.metaのみが削除され、Boot配下の他のテストが残る。取り込み・domain reload中は規約どおり45秒待って再試行する。

- [ ] **Step 3: 削除後もコンパイル・対象テストが通ることを確認する。** Task 1 Step 4のコマンドを実行。`rg -n 'EventModeAutoStartBoot' moorestech_client/Assets/Scripts .github scripts` は一致0件。新しいテストにCiShardClientPlay2属性を付けない。既存のカテゴリによるshard分配で通常のclient-remainderへ含まれることを`.github/scripts/unity-test-shard-filter.sh`の現CLIを読んで確認し、CI結果でも8件の存在を確認する。workflowやカテゴリ分配を再実装しない。

- [ ] **Step 4: 削除をコミットする。**
```bash
git add moorestech_client/Assets/Scripts/Client.Tests/EditModeInPlayingTest/Boot
git commit -m "test: replace full exhibition world boot with lightweight startup coverage"
```

### Task 3: 全ブランチレビュー・CI確認・PR作成・記録を完了する

**Files:** 本plan、今回変更した全ファイル、レビュー所見に伴う必要な修正。
**Interfaces:** Consumes: Task 1〜2の変更・結果。Produces: commit/push済み・CI成功の新規PR、検証記録と残件のissue番号。

- [ ] **Step 1: 必ず最後にmoores-code-reviewスキルで全ブランチレビューを実行すること（自動実行・ゴール文言による省略不可）。** Codexでは同スキルの`references/codex-host.md`と`scripts/codex_workflow_runner/run.mjs`を使い、選ばれた系統を代行・縮退させない。
- [ ] **Step 2: 確定した所見を反映し、変更した条件・評価時点を含めてTask 1の全対象テストを最終バイナリで再実行する。** compile ErrorCount=0と8件実行を確認する。ローカルの録画付き通し検証は追加しない。今回の対象は本物の副作用を実行せず順序を守る内部処理であり、同じ内部処理の実行試験と本番配線試験で検証するため。実MainGame起動全体の検出範囲がなくなる点は明記する。
- [ ] **Step 3: 残件の起票・連結を行う。** 新規の未確認/未検証を放置せずbdへ起票し、報告にIDを列挙する。既存のcold生成性能課題は`moorestech-w7ggu.1`、列車の経路なし停止と旧D5裁定確認は`moorestech-vwdd1.1`へ連結し、本PRで解決したと閉じない。新規に実シーン起動のカバレッジ減少を欠陥と判断した場合はユーザーへ必要な裁定を出し、黙って全経路保護済みと報告しない。
- [ ] **Step 4: 全変更をコミットしてpushし、pr-createスキルでmaster向け新規PRを作る。** 本文は「13〜21分のgenerated-world起動試験を、実ゲート＋記録用副作用のEditMode順序試験へ置換」を中心に書き、テスト8件、既存回帰、実シーン全経路の検出範囲の変更を明記する。masterに更新があればコンフリクトを解消し、compile/対象テスト確認後にpushする。
- [ ] **Step 5: CI成功を確認する。** 新規8件が実行され、旧EventModeAutoStartBootTestが無いことを結果artifactで確認する。成功率や高速化量を単発から推測しない。実行時間は観測値として記録する。料金が発生するAPI/クレジット操作が必要になった場合は、内容と見積額を示して明示承諾を得る。
- [ ] **Step 6: 結果をshare-putで共有し、判断・実測はwiki-askで保存する。** bdへPR・テスト結果・残件IDを記録して完了した作業のみcloseする。SDD記録をarchiveし、PR作成後はmoores-wt rmで自分のworktreeとEditorを畳む。

## Self-Review・構造検査記録

- 要件対応: R1=Task 2、R2/R3/R5=Task 1 sequence試験、R4/R6/R8=Task 1 runtime actionsとbinding試験、R7=Task 1のEditMode検証とTask 2のshard確認、R9=Task 3 PR本文、R10=Task 3。
- 配置検査: 7種類のファイルを上の表へ抽出。すべて出展起動のClient.StarterまたはClient.Testsへ配置。Core・Server・スキーマ・セーブ形式へ変更を入れない。
- 前例検査: 待機は既存TitleGatesへ委譲、副作用のinterface差し替えはEventModeStartGateのIEventIdleWatchArmerと同じ役割、配線検査は既存MethodCallInspectorを呼ぶ。
- 機能パリティ: 上の5操作は存続。Unityフックと無人宣言/言語適用の呼び出し順を維持する。
- 保留/取消検査: 未始動→通過、通過済み、期限切れ→遅い通過、取消、通過後の取消の5ケース。遅い通過でも旧実行の副作用を再開しない。
- 同点規則・外部出力の規則化・複数UI保持者の鮮度: 新設しないため該当なし。IL解析規則は既存Inspectorを再利用する。
- placeholder/型照合: 定義するRunAsync・2つのeffectsメソッドと呼び出し側は一致。コードブロックはファイルごとに200行未満。計画上の検証はすべて未実施としてチェックを空のまま残す。

## 判断記録（ADR）

- 出所: ユーザー依頼 2026-10-13「そもそもそのテストをなくすってのはダメなの？」→助手の「軽い起動順序テストに置き換えて、この重いテストを削除」提案→ユーザー「$writing-plans 実行計画を立てて」。本planはこの直前の提案を対象にする。具体的な型配置と境界の選定はagent前提であり、ユーザーが個別に裁定したとは記録しない。
- 出所: agent前提。本番の期限は既存定数60秒を参照、試験の期限0.2秒と観測上限5秒は既存PlaytestTitleGatesWaitTestを参照する。CI40分のstep上限は変更しない。
- 出所: 既存ユーザー裁定 `[[2026-10-01-出展モードのタイトル確認が期限切れならプロセスを終了する]]`。期限切れのログ/終了とワールド保全を維持する。
- 出所: 既存ユーザー裁定 `[[2026-10-01-出展自動開始テストは保存先差し替えで実セーブから隔離する]]`。今回のfakeはワールドAPIを呼ばないため、実セーブへ触れないという目的をさらに直接満たす。保存先上書きの本番窓口は削除しない。
- 出所: agent前提。単純削除だけでは配線の検出範囲も失われるため、実sequenceの振る舞い試験＋既存Inspectorによる本番配線試験を同梱する。実シーン全経路検証を保持したとは主張しない。
- 出所: agent前提。取消済みでゲートが通過していても開始しないため、待機完了直後・全副作用の手前にexitToken.ThrowIfCancellationRequestedを置く。
- 出所: agent前提。今回新たな用語・ドメイン形式は導入せず、小さな内部処理の抽出なので別のspec/独立ADRは追加しない。実装はwriting-plansの既定どおり新規Codexセッションへ引き継ぐ。
