using System;
using System.Collections;
using Client.Common;
using Client.Game.Common;
using Client.Starter.EventMode;
using Client.Tests.WebUi.Gate;
using Client.WebUiHost.Game.EventMode;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UniRx;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using static Client.Tests.EditModeInPlayingTest.Util.EditModeInPlayingTestUtil;

namespace Client.Tests.EditModeInPlayingTest
{
    /// <summary>
    /// 出展モードの言語選択ゲート待機中に、MainGameのGameStateController.Start()がロックした後でも実カーソルが見えていることを実測する。
    /// ゲートはsceneLoaded内でStartより先に待ちへ入るため、待ち側が後勝ちしないと来場者が言語ボタンを押せない。
    /// Empirically verifies that the real cursor stays visible during the exhibition language gate, even after MainGame's GameStateController.Start() locks it.
    /// The gate starts waiting inside sceneLoaded before Start, so unless the waiting side wins last, visitors cannot press a language button.
    /// </summary>
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardClientPlay2")]
    public class EventLanguageGateCursorTest
    {
        // MainGame待ちの上限フレーム
        // Frame limit for waiting on MainGame
        private const int MainGameWaitFrameLimit = 10000;

        // MainGameのStart()と後続の初回フレームを確実に越えるための待ちフレーム数
        // Frames to advance so MainGame's Start() and the following first frames have surely run
        private const int SettleFrameCount = 10;

        private static readonly string[] EventModeEnvKeys = { EventExhibitionSettings.EnableEnvKey, EventExhibitionSettings.EditorOptInEnvKey };

        // 打ち切りでPlayに残ると次のテストのEnterPlayModeと競合するため、必ずPlayを抜ける
        // Always leave Play, since staying in it after an abort would clash with the next test's EnterPlayMode
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (EditorApplication.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator 言語選択ゲートの待機中はマウスカーソルが見えている()
        {
            EnterPlayModeUtil();

            // yield return new EnterPlayMode　は必ず[UnityTest]関数の直下で呼び出すこと。そうでないとなぜかわからないがプレイモードに入らない
            // Always call yield return new EnterPlayMode directly under the [UnityTest] function. Otherwise, for unknown reasons, it will not enter PlayMode.
            yield return new EnterPlayMode(expectDomainReload: true);

            // EnterPlayMode時のテストフレームワーク内部エラーでテストが失敗するのを防ぐ
            // Prevent test failure from test framework internal errors during EnterPlayMode.
            LogAssert.ignoreFailingMessages = true;

            yield return Body().ToCoroutine();

            // 言語は選ばずに抜ける。待ちはPlay終了のキャンセルで打ち切られ、初期化失敗扱いにならない
            // Leave without choosing a language; the play-exit cancellation ends the wait without counting as an init failure
            yield return new ExitPlayMode();

            // テスト終了後にデバッグオブジェクト無効化フラグをクリア
            // Clear debug objects disabled flag after test.
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);

            #region Internal

            async UniTask Body()
            {
                // 出展モードはPlay突入後に有効化する。突入前だとMainMenu起点のEventModeAutoStartが開発機のワールドを消し得る
                // Enable exhibition mode after entering Play; before it, EventModeAutoStart from MainMenu could wipe the developer's world
                var savedEnvValues = new string[EventModeEnvKeys.Length];
                for (var i = 0; i < EventModeEnvKeys.Length; i++) savedEnvValues[i] = Environment.GetEnvironmentVariable(EventModeEnvKeys[i]);
                for (var i = 0; i < EventModeEnvKeys.Length; i++) Environment.SetEnvironmentVariable(EventModeEnvKeys[i], "1");

                // 初期化完了の発火を記録し、ゲートを越えて進んだ状態で緑にならないようにする
                // Record the initialization signal so the test never goes green after passing the gate
                var gameInitialized = false;
                var initializedSubscription = GameInitializedEvent.OnGameInitialized.Subscribe(_ => gameInitialized = true);

                // 失敗しても環境変数と購読は必ず戻す。残すと後続テストが出展モードで起動する
                // Always restore the env vars and subscription; leaving them would boot later tests in exhibition mode
                try
                {
                    await LoadMainGame();
                    await WaitMainGameActive();
                    for (var frame = 0; frame < SettleFrameCount; frame++) await UniTask.Yield();

                    // ゲートで待機中であること（Startは走り終え、UIStateはまだ無い）を前提として固定する
                    // Pin the precondition that the gate is waiting: Start has run and no UIState exists yet
                    var hub = Client.WebUiHost.Boot.WebUiHost.Hub;
                    Assert.IsNotNull(hub, "WebUiHost hub is not running; the language gate cannot be shown");
                    Assert.IsFalse(gameInitialized, "言語を選んでいないのに初期化が完了した");
                    StartGateTopicAssert.AssertWaiting(hub, EventLanguageGateTopic.TopicName, true);

                    // 待機中は実カーソルが見える
                    // The real cursor is visible while waiting
                    Assert.IsTrue(Cursor.visible, "言語選択ゲートの待機中にカーソルが非表示");
                    Assert.AreEqual(CursorLockMode.None, Cursor.lockState, "言語選択ゲートの待機中にカーソルがロックされている");
                }
                finally
                {
                    initializedSubscription.Dispose();
                    for (var i = 0; i < EventModeEnvKeys.Length; i++) Environment.SetEnvironmentVariable(EventModeEnvKeys[i], savedEnvValues[i]);
                }
            }

            async UniTask WaitMainGameActive()
            {
                for (var frame = 0; frame < MainGameWaitFrameLimit; frame++)
                {
                    if (SceneManager.GetActiveScene().name == SceneConstant.MainGameSceneName) return;
                    await UniTask.Yield();
                }
                Assert.Fail("MainGameシーンがアクティブにならなかった");
            }

            #endregion
        }
    }
}
