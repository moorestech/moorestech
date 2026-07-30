using Client.Game.Common;
using Client.Playtest.Core;
using Client.Starter;
using Common.Debug;
using Server.Boot;
using UniRx;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Playtest
{
    /// <summary>
    ///     execute-dynamic-code 1回でPlayModeを立ち上げる入口。ゲーム初期化完了でreadyマーカーを書く
    ///     One-shot entry to boot PlayMode from a single execute-dynamic-code call; writes a ready marker on init
    /// </summary>
    public static class PlaytestBoot
    {
        private const string GameInitializerScenePath = "Assets/Scenes/Game/GameInitialaizer.unity";
        private const string PendingBootKey = "Playtest_PendingBoot";
        private const string DebugEnvironmentTypeKey = "DebugEnvironmentTypeKey";
        private const int PureNatureEnvironmentType = 1;

        public static string PrepareAndEnterPlayMode(string serverDirectory, bool noSave)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "ERROR: already playing";

            // NoSaveフラグと起動待ちフラグはSessionStateでドメインリロードを越えて保持される
            // The NoSave flag and pending-boot flag persist across domain reload via SessionState
            SessionState.SetBool(InitializeScenePipeline.SkipSaveLoadSessionKey, noSave);
            PlaytestWorldBootSession.Clear();
            PrepareBootSession(serverDirectory);
            EnterPlayMode();
            return PlaytestPaths.SessionDirectory;
        }

        public static string PrepareWorldAndEnterPlayMode(string serverDirectory, string worldDirectory, string mapMode, int seed)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "ERROR: already playing";
            if (string.IsNullOrWhiteSpace(serverDirectory)) return "ERROR: server directory is required";
            if (string.IsNullOrWhiteSpace(worldDirectory)) return "ERROR: world directory is required";
            if (string.IsNullOrWhiteSpace(mapMode)) return "ERROR: map mode is required";

            // 固定world起動はNoSaveのGuid上書きを止め、正式な起動引数を別セッションに保存する
            // Fixed-world boot disables the NoSave GUID override and stores official boot arguments separately
            SessionState.SetBool(InitializeScenePipeline.SkipSaveLoadSessionKey, false);
            PlaytestWorldBootSession.Save(serverDirectory, worldDirectory, mapMode, seed);
            PrepareBootSession(serverDirectory);
            DebugParameters.SaveInt(DebugEnvironmentTypeKey, PureNatureEnvironmentType);
            EnterPlayMode();
            return PlaytestPaths.SessionDirectory;
        }

        private static void PrepareBootSession(string serverDirectory)
        {
            SessionState.SetBool(PendingBootKey, true);
            // テストと同様にデバッグオブジェクト生成を無効化する（IngameDebugConsole等のノイズ防止）
            // Disable debug object bootstrap as tests do (prevents IngameDebugConsole etc. noise)
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", true);
            PlaytestPaths.ResetSession();

            // デバッグ設定をセッション専用キャッシュへ隔離する。実キャッシュを複製するので開発者設定は引き継がれる
            // Isolate debug parameters into a session-local cache; copying the real cache carries developer settings over
            DebugParametersCacheDirectory.CopyDefaultTo(PlaytestPaths.DebugCacheDirectory);
            DebugParametersCacheDirectory.SetOverride(PlaytestPaths.DebugCacheDirectory);

            // worktree必須のmasterパス設定（未指定なら既存設定を維持）。隔離後に書くため実キャッシュは汚れない
            // Set the master data path required in worktrees (keep the existing value when unspecified); written after isolation so the real cache stays clean
            if (!string.IsNullOrEmpty(serverDirectory))
                DebugParameters.SaveString(ServerDirectory.DebugServerDirectorySettingKey, serverDirectory);
        }

        private static void EnterPlayMode()
        {
            // ゲーム初期化シーンから再生を開始する
            // Start play mode from the game initializer scene
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameInitializerScenePath);
            EditorApplication.EnterPlaymode();
        }

        [InitializeOnLoadMethod]
        private static void HookAfterDomainReload()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

            // PlayMode突入後のドメインリロードで再実行され、ここで初期化完了イベントを購読する
            // Re-runs after the play-mode domain reload; subscribe to the game-initialized event here
            if (!SessionState.GetBool(PendingBootKey, false)) return;
            if (!EditorApplication.isPlayingOrWillChangePlaymode) return;
            SceneManager.sceneLoaded -= InjectWorldBootSettings;
            SceneManager.sceneLoaded += InjectWorldBootSettings;
            GameInitializedEvent.OnGameInitialized.First().Subscribe(_ => PlaytestPaths.WriteReadyMarker());
        }

        private static void InjectWorldBootSettings(Scene scene, LoadSceneMode mode)
        {
            if (!PlaytestWorldBootSession.TryCreateInitializeProprieties(out var proprieties)) return;

            // sceneLoadedはStartより前に発火するため、初期化処理が読む前に固定引数を注入できる
            // sceneLoaded fires before Start, allowing fixed arguments to be injected before initialization reads them
            SceneManager.sceneLoaded -= InjectWorldBootSettings;
            var pipeline = Object.FindFirstObjectByType<InitializeScenePipeline>();
            pipeline.SetProperty(proprieties);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;

            // 再生終了時にフラグと開始シーン設定を復元する（通常の再生ボタンへ影響させない）
            // Restore flags and the start-scene setting when play ends (keeps the normal play button unaffected)
            SessionState.SetBool(InitializeScenePipeline.SkipSaveLoadSessionKey, false);
            SessionState.SetBool(PendingBootKey, false);
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            PlaytestWorldBootSession.Clear();
            SceneManager.sceneLoaded -= InjectWorldBootSettings;
            EditorSceneManager.playModeStartScene = null;

            // このプレイテストが張った隔離だけを解除する。テスト側SetUpFixtureの隔離を巻き込まないため
            // Clear only the isolation this playtest installed, so a test fixture's isolation is never torn down with it
            var sessionDebugCache = PlaytestPaths.DebugCacheDirectory;
            if (!string.IsNullOrEmpty(sessionDebugCache) && DebugParametersCacheDirectory.GetOverride() == sessionDebugCache)
                DebugParametersCacheDirectory.SetOverride(null);
        }
    }
}
