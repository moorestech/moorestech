using System.Collections;
using System.IO;
using Common.Debug;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// デバッグ設定の隔離がPlayMode遷移のドメインリロードを跨いで維持されることを実測する。
    /// プロセス環境変数で切り替えている根拠がこれで、失われるとPlayMode中のサーバーが開発者の実cacheを読む。
    /// Empirically verifies that debug parameter isolation survives the PlayMode transition's domain reload.
    /// This is why a process environment variable is used; losing it would make the in-play server read the developer's real cache.
    /// </summary>
    public class DebugParametersIsolationAcrossDomainReloadTest
    {
        [UnityTest]
        public IEnumerator DebugParametersIsolation_SurvivesPlayModeDomainReload()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            // リロード後も隔離先が生きており、既定の ../cache へ戻っていないこと
            // The isolation target is still live after the reload and has not fallen back to the default ../cache
            var cacheDirectory = DebugParametersCacheDirectory.Resolve();
            Assert.IsFalse(string.IsNullOrEmpty(DebugParametersCacheDirectory.GetOverride()));
            Assert.AreNotEqual(Path.GetFullPath("../cache"), cacheDirectory);
            Assert.IsTrue(Directory.Exists(cacheDirectory));

            // 隔離先は空なので、実cacheに何が書かれていても既定値が読まれる
            // The isolated cache is empty, so defaults are read no matter what the real cache contains
            Assert.IsFalse(DebugParameters.GetValueOrDefaultBool(DebugParameterKeys.FreeBlockPlacement));

            yield return new ExitPlayMode();

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }
    }
}
