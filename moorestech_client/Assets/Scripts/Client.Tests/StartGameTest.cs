using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using static Client.Tests.PlayModeTest.Util.PlayModeTestUtil;

namespace Client.Tests
{
    /// <summary>
    /// ゲームが正常に起動できるかを検証する統合テスト。
    ///
    /// 【重要: unity-test.sh (CliTestRunner) からは実行できません】
    /// このテストは EnterPlayMode を使用しており、ドメインリロードが発生します。
    /// CliTestRunner は runSynchronously = true で動作するため、ドメインリロード時に
    /// ResultCallbacks インスタンスが破棄され、テスト結果が 0件（passed: 0, failed: 0）として報告されます。
    ///
    /// 実行方法: Unity エディタの Test Runner ウィンドウ (Window > General > Test Runner) から手動実行してください。
    /// </summary>
    public class StartGameTest
    {
        [UnityTest]
        public IEnumerator StartGameCheckTest()
        {
            // テスト中はデバッグオブジェクトの生成を無効化（ドメインリロード後も保持される）
            // Disable debug object creation during test (persists across domain reload).
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", true);

            // 前回のテストで解放されなかったAssetBundleをプレイモード前にクリーンアップ
            // Clean up stale AssetBundles before entering play mode to avoid conflicts.
            AssetBundle.UnloadAllAssetBundles(true);

            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return SetUp().ToCoroutine();

            yield return new ExitPlayMode();

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask SetUp()
            {
                var loadTask = LoadMainGame();
                var timeOuter = UniTask.Delay(TimeSpan.FromSeconds(30));

                var result = await UniTask.WhenAny(loadTask, timeOuter);
                if (result == 1)
                {
                   Assert.Fail("LoadMainGame timed out.");
                }
            }

            #endregion
        }
    }
}
