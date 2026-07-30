using Client.Game.Common;
using Client.Playtest.Core;
using UniRx;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Client.Playtest
{
    /// <summary>
    ///     execute-dynamic-code 1回でPlayModeを立ち上げる入口。ゲーム初期化完了でreadyマーカーを書く
    ///     One-shot entry to boot PlayMode from a single execute-dynamic-code call; writes a ready marker on init
    /// </summary>
    public static class PlaytestBoot
    {
        private const string GameInitializerScenePath = "Assets/Scenes/Game/GameInitialaizer.unity";

        public static string PrepareAndEnterPlayMode(string serverDirectory, bool noSave)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "ERROR: already playing";

            PlaytestBootLifecycle.PrepareLegacyBootSession(serverDirectory, noSave);
            EnterPlayMode();
            return PlaytestPaths.SessionDirectory;
        }

        public static string PrepareWorldAndEnterPlayMode(string serverDirectory, string worldDirectory, string mapMode, int seed)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "ERROR: already playing";
            if (string.IsNullOrWhiteSpace(serverDirectory)) return "ERROR: server directory is required";
            if (string.IsNullOrWhiteSpace(worldDirectory)) return "ERROR: world directory is required";
            if (string.IsNullOrWhiteSpace(mapMode)) return "ERROR: map mode is required";

            PlaytestBootLifecycle.PrepareWorldBootSession(serverDirectory, worldDirectory, mapMode, seed);
            EnterPlayMode();
            return PlaytestPaths.SessionDirectory;
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
            // PlayMode突入後のドメインリロードで再実行され、ここで初期化完了イベントを購読する
            // Re-runs after the play-mode domain reload; subscribe to the game-initialized event here
            if (!PlaytestBootLifecycle.RestoreAfterDomainReload(EditorApplication.isPlayingOrWillChangePlaymode)) return;
            GameInitializedEvent.OnGameInitialized.First().Subscribe(_ => PlaytestPaths.WriteReadyMarker());
        }
    }
}
