using System;
using System.IO;
using Client.Common;
using Client.Game.Common;
using Client.Starter;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using Server.Boot.Args;
using UniRx;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Client.Tests.EditModeInPlayingTest.Util.Boot
{
    /// <summary>
    /// テスト用にメインゲームを起動する手順。初期化の完了まで待つ入口と、初期化シーンのローダーが出た時点で戻る入口を持つ
    /// Boots the main game for tests: one entry waits for initialization to complete, the other returns once the initializer scene's loader appears
    /// </summary>
    public static class MainGameTestBoot
    {
        // Windows の負荷下では初期化が通常の 2〜3 倍かかり 60 秒を超えた実測があるため、地形生成の起動と同じ上限にそろえる
        // Under Windows load initialization was measured at 2-3x its usual time, past 60 seconds, so this matches the generated-terrain boot bound
        private const int InitializationTimeoutSeconds = 180;
        private const int LoaderTimeoutMilliseconds = 60000;

        public static async UniTask BootUntilInitialized(string serverDirectory, string worldDirectory, string mapMode)
        {
            // 完了の通知はシーンを読む前に購読する。後から購読すると速い起動の通知を取りこぼす
            // Subscribe before loading the scene; subscribing later would miss the signal of a fast boot
            var isInitialized = false;
            using var initializedSubscription = GameInitializedEvent.OnGameInitialized.Take(1).Subscribe(_ => isInitialized = true);

            await BootUntilLoaderShown(serverDirectory, worldDirectory, mapMode);

            // 時間切れは理由つきで落とす。ゲートで止まる起動は BootUntilLoaderShown を使う
            // Fail with a reason on timeout; a boot that stops at a gate uses BootUntilLoaderShown instead
            var timedOut = await UniTask.WaitUntil(() => isInitialized).TimeoutWithoutException(TimeSpan.FromSeconds(InitializationTimeoutSeconds));
            if (timedOut) Assert.Fail($"GameInitializedEvent が {InitializationTimeoutSeconds} 秒以内に来ない。初期化が失敗したか、ゲートで止まっている");
        }

        public static async UniTask BootUntilLoaderShown(string serverDirectory, string worldDirectory, string mapMode)
        {
            // テストごとに未使用の一時ワールドディレクトリを割り当てて隔離する
            // Isolate each test with an unused temporary world directory
            worldDirectory ??= Path.Combine(Path.GetTempPath(), $"moorestech_play_mode_test_{Guid.NewGuid()}");
            serverDirectory ??= EditModeInPlayingTestUtil.EditModeInPlayingTestServerDirectoryPath;

            // 初期化シーンをロード
            // Load the initialization scene
            SceneManager.sceneLoaded += SetInitializeProperty;
            SceneManager.LoadScene(SceneConstant.GameInitializerSceneName);

            // シーンのオブジェクトが初期化されるまで1フレーム待機
            // Wait 1 frame for scene objects to initialize
            await UniTask.Yield();

            await WaitStartServer();

            #region Internal

            // 初期化プロパティをセット
            // Set the initialization properties
            void SetInitializeProperty(Scene scene, LoadSceneMode mode)
            {
                SceneManager.sceneLoaded -= SetInitializeProperty;

                var localProperties = InitializeProprieties.CreateLocalServer();
                localProperties.CreateLocalServerArgs = CliConvert.Serialize(EditModeInPlayingTestUtil.CreateServerSettings(worldDirectory, serverDirectory, mapMode));

                var starter = Object.FindObjectOfType<InitializeScenePipeline>();
                starter.SetProperty(localProperties);
            }

            async UniTask WaitStartServer()
            {
                // GameInitializerSceneLoaderが表示されるか60秒のタイムアウトを待つ
                // Wait for GameInitializerSceneLoader to appear or the 60 second timeout
                var timeout = UniTask.Delay(LoaderTimeoutMilliseconds);
                var waitForLoader = UniTask.WaitUntil(() => Object.FindObjectOfType<GameInitializerSceneLoader>() != null);
                await UniTask.WhenAny(waitForLoader, timeout);

                // タイムアウトしてるかどうかを判定
                // Check if the timeout occurred
                var loader = Object.FindObjectOfType<GameInitializerSceneLoader>();
                Assert.IsNotNull(loader, "GameInitializerSceneLoader was not found within 60 seconds. This usually means the initialization failed.");
            }

            #endregion
        }
    }
}
