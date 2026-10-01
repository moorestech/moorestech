using System.Collections;
using System.IO;
using Client.Common;
using Cysharp.Threading.Tasks;
using Game.Paths;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// 出展モードでMainMenuから起動すると、クリック無しでMainGameまで進みメインメニューへ戻されないことを本物の起動フックで確かめる。
    /// 自動開始はAfterSceneLoadで動き、タイトルの確認列はタイトル合成ルートのStartで始まる。待たずに開始すると初期化が「確認が未開始」で断りメインメニューへ戻す。
    /// Verifies through the real boot hook that an exhibition boot from MainMenu reaches MainGame without a click and is never bounced back to the main menu.
    /// The auto start runs at AfterSceneLoad while the title sequence starts in the title composition root's Start; starting without waiting is refused as "not started" and bounced to the menu.
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay2")]
    public class EventModeAutoStartBootTest
    {
        // 内蔵サーバー起動込みの到達上限秒
        // Seconds limit to reach MainGame incl. server boot
        private const float ReachMainGameTimeoutSeconds = 300f;

        // ドメインリロードを跨いでPlay中の観測結果をPlay終了後の判定へ渡すキー
        // Key carrying the in-Play observation across the domain reload to the verdict after Play
        private const string OutcomeKey = "EventModeAutoStartBootTest_Outcome";

        private const string ReachedMainGame = "reachedMainGame";

        // 正常・失敗・打ち切りのどの経路でも環境を戻し、一時ワールドを消す
        // Restore the environment and remove the temporary world on every path: pass, failure or abort
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
            EventModeAutoStartBootEnvironment.Restore();
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
        }

        // 到達待ちの上限より長く取り、Play終了後の復元と判定まで打ち切られないようにする
        // Longer than the reach deadline so the restore and verdict after Play are never cut off
        [UnityTest, Timeout(600000)]
        public IEnumerator 出展モードはメインメニューからクリック無しでMainGameへ進む()
        {
            EnterPlayModeUtil();
            SessionState.SetString(OutcomeKey, "Body did not run");
            EventModeAutoStartBootEnvironment.Prepare();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            // 言語は選ばずに抜ける。待ちはPlay終了のキャンセルで打ち切られる
            // Leave without choosing a language; the play-exit cancellation ends the wait
            yield return new ExitPlayMode();

            // 復元はTearDownが担う。ここで断言が落ちても復元は走る
            // The TearDown restores, so a failing assert here still restores
            var outcome = SessionState.GetString(OutcomeKey, "");
            SessionState.EraseString(OutcomeKey);
            Assert.AreEqual(ReachedMainGame, outcome, "出展モードの自動開始がMainGameへ届かなかった");

            #region Internal

            // 断言せず観測だけを残す。Play中に例外で抜けるとPlay終了後の復元が走らない
            // Record the observation without asserting; throwing inside Play would skip the restore after Play
            async UniTask Body()
            {
                var deadline = Time.realtimeSinceStartup + ReachMainGameTimeoutSeconds;
                while (Time.realtimeSinceStartup < deadline)
                {
                    var activeSceneName = SceneManager.GetActiveScene().name;
                    if (activeSceneName == SceneConstant.MainGameSceneName)
                    {
                        // 開発機のworld_1ではなく一時ディレクトリにワールドが作られたことも観測する
                        // Also observe that the world was created in the temporary directory, not the developer's world_1
                        var worldDirectory = GameSystemPaths.DefaultWorldDirectory;
                        var usedTemporaryWorld = worldDirectory == EventModeAutoStartBootEnvironment.TemporaryWorldDirectory && Directory.Exists(worldDirectory);
                        SessionState.SetString(OutcomeKey, usedTemporaryWorld ? ReachedMainGame : $"reached MainGame but the world was not created in the temporary directory (default world: {worldDirectory})");
                        return;
                    }

                    // 起動時のMainMenuは時刻0で読まれる。0より後に読まれたMainMenuは、開始が断られたか初期化が失敗して戻されたもの
                    // The boot MainMenu loads at time 0; a MainMenu loaded later was bounced back by a refused start or a failed initialization
                    // 往復はBodyが動き出す前に済むことがあるため、シーン名の遷移ではなくシーンの読み込み時刻で判定する
                    // The round trip can finish before Body starts, so this judges by the scene's load time rather than by watching scene names change
                    var activeSceneLoadedAt = Time.time - Time.timeSinceLevelLoad;
                    if (activeSceneName == SceneConstant.MainMenuSceneName && 0f < activeSceneLoadedAt)
                    {
                        SessionState.SetString(OutcomeKey, $"returned to MainMenu (reloaded at {activeSceneLoadedAt}s; see the [PlaytestTitleGates] refusal log)");
                        return;
                    }
                    await UniTask.Yield();
                }
                SessionState.SetString(OutcomeKey, $"neither MainGame nor a return to MainMenu within {ReachMainGameTimeoutSeconds}s (active scene: {SceneManager.GetActiveScene().name})");
            }

            #endregion
        }
    }
}
