# テンプレート

## 基本テンプレート

最もシンプルなEditModeInPlayingTest。ゲームを起動して何らかの検証を行う。

```csharp
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// テスト自体はEditModeで実行されるが、実行中にプレイモードに変更する
    /// This test is executed in EditMode, but it switches to PlayMode during execution.
    /// </summary>
    public class {Feature}Test
    {
        [UnityTest]
        public IEnumerator {テスト名}Test()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return TestBody().ToCoroutine();

            yield return new ExitPlayMode();

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask TestBody()
            {
                await LoadMainGame();

                // テスト実装
                // Test implementation
                Assert.Pass();
            }

            #endregion
        }
    }
}
```

## UIテストテンプレート

UI要素の表示・状態を検証するパターン。

```csharp
// 追加のusing（Object.FindFirstObjectByType使用時）
// Additional using (when using Object.FindFirstObjectByType)
using Object = UnityEngine.Object;

[UnityTest]
public IEnumerator {UI名}DisplayTest()
{
    EnterPlayModeUtil();
    yield return new EnterPlayMode(expectDomainReload: true);
    LogAssert.ignoreFailingMessages = true;

    yield return TestBody().ToCoroutine();

    yield return new ExitPlayMode();
    SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

    #region Internal

    async UniTask TestBody()
    {
        await LoadMainGame();

        // UI要素を検索（非アクティブも含む）
        // Find UI element (including inactive)
        var targetUI = Object.FindFirstObjectByType<TargetUIComponent>(FindObjectsInactive.Include);
        Assert.IsNotNull(targetUI, "TargetUIComponent was not found in scene.");

        // UI状態を検証
        // Verify UI state
        Assert.AreEqual(expected, actual);
    }

    #endregion
}
```

## エンティティ/物理テストテンプレート

ブロック配置後のエンティティやオブジェクトの挙動を一定時間検証するパターン。

```csharp
[UnityTest]
public IEnumerator {Feature}BehaviorTest()
{
    EnterPlayModeUtil();
    yield return new EnterPlayMode(expectDomainReload: true);
    LogAssert.ignoreFailingMessages = true;

    yield return TestBody().ToCoroutine();

    yield return new ExitPlayMode();
    SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

    #region Internal

    async UniTask TestBody()
    {
        await LoadMainGame();

        // ブロック配置とクライアント側スポーン待機
        // Place block and wait for client-side spawn
        var block = PlaceBlock("ブロック名", Vector3Int.zero, BlockDirection.North);
        await WaitBlockGameObjectSpawn(Vector3Int.zero);

        // 物理演算の同期を待つ
        // Wait for physics sync
        Physics.SyncTransforms();
        await UniTask.WaitForFixedUpdate();

        // 時間経過しながら検証（例: 10秒間）
        // Verify over time (e.g. 10 seconds)
        var startTime = Time.time;
        while (Time.time - startTime < 10f)
        {
            // アサーション
            // Assertions

            Physics.SyncTransforms();
            await UniTask.WaitForFixedUpdate();
        }
    }

    #endregion
}
```

## タイムアウト付きテンプレート

ゲーム起動にタイムアウトを設定するパターン（StartGameTestで使用）。

```csharp
[UnityTest]
public IEnumerator StartupWithTimeoutTest()
{
    EnterPlayModeUtil();
    yield return new EnterPlayMode(expectDomainReload: true);
    LogAssert.ignoreFailingMessages = true;

    yield return TestBody().ToCoroutine();

    yield return new ExitPlayMode();
    SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

    #region Internal

    async UniTask TestBody()
    {
        var loadTask = LoadMainGame();
        var timeOuter = UniTask.Delay(System.TimeSpan.FromSeconds(30));

        var result = await UniTask.WhenAny(loadTask, timeOuter);
        if (result == 1)
        {
            Assert.Fail("LoadMainGame timed out.");
        }
    }

    #endregion
}
```

## OS入力注入テストテンプレート

キーボード/マウス入力を注入するテスト。CI環境で不安定なため`[Category("IgnoreCI")]`を付与する。
実例: `EditModeInPlayingTest/PlayerMovementTest.cs`

```csharp
using Client.Tests.EditModeInPlayingTest.OsInput;

[Category("IgnoreCI")]
public class {Feature}Test
{
    // CI/バッチ環境で仮想デバイスを確保する
    // Ensure virtual input devices exist in CI/batch environments.
    [SetUp]
    public void SetUp() => OsInputSpoof.EnsureDevices();

    // テスト終了時にキー状態リークを防止し仮想デバイスを破棄する
    // Prevent key state leaks and clean up virtual devices on test end.
    [TearDown]
    public void TearDown()
    {
        OsInputSpoof.ReleaseAllKeys();
        OsInputSpoof.CleanupDevices();
    }

    // TestBody内の先頭で使用不可環境をスキップ:
    // Skip unavailable environments at the top of TestBody:
    //   OsInputSpoof.AssertAvailableOrSkip();
    //   OsInputSpoof.KeyDown(OsInputSpoof.DebugKey.W); / KeyUp(...)
}
```
