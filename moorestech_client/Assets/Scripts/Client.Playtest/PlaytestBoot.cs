using Client.Game.Common;
using Client.Playtest.Core;
using Client.Starter;
using Common.Debug;
using Server.Boot;
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
        private const string PendingBootKey = "Playtest_PendingBoot";

        public static string PrepareAndEnterPlayMode(string serverDirectory, bool noSave)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return "ERROR: already playing";

            // worktree必須のmasterパス設定（未指定なら既存設定を維持）
            // Set the master data path required in worktrees (keep the existing value when unspecified)
            if (!string.IsNullOrEmpty(serverDirectory))
            {
                DebugParameters.SaveString(ServerDirectory.DebugServerDirectorySettingKey, serverDirectory);
            }

            // NoSaveフラグと起動待ちフラグはSessionStateでドメインリロードを越えて保持される
            // The NoSave flag and pending-boot flag persist across domain reload via SessionState
            SessionState.SetBool(InitializeScenePipeline.SkipSaveLoadSessionKey, noSave);
            SessionState.SetBool(PendingBootKey, true);

            // テストと同様にデバッグオブジェクト生成を無効化する（IngameDebugConsole等のノイズ防止）
            // Disable debug object bootstrap as tests do (prevents IngameDebugConsole etc. noise)
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", true);
            PlaytestPaths.ResetSession();

            // ゲーム初期化シーンから再生を開始する
            // Start play mode from the game initializer scene
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameInitializerScenePath);
            EditorApplication.EnterPlaymode();
            return PlaytestPaths.SessionDirectory;
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
            GameInitializedEvent.OnGameInitialized.First().Subscribe(_ => PlaytestPaths.WriteReadyMarker());
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;

            // 再生終了時にフラグと開始シーン設定を復元する（通常の再生ボタンへ影響させない）
            // Restore flags and the start-scene setting when play ends (keeps the normal play button unaffected)
            SessionState.SetBool(InitializeScenePipeline.SkipSaveLoadSessionKey, false);
            SessionState.SetBool(PendingBootKey, false);
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            EditorSceneManager.playModeStartScene = null;
        }
    }
}
