using Client.Game;
using Client.Playtest.Core;
using Client.Starter;
using Common.Debug;
using Server.Boot;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Playtest
{
    internal static class PlaytestBootLifecycle
    {
        private const string PendingBootKey = "Playtest_PendingBoot";
        private const string DebugEnvironmentTypeKey = "DebugEnvironmentTypeKey";
        private const int PureNatureEnvironmentType = 1;
        private static bool _worldBootSceneHookRegistered;

        internal static void PrepareLegacyBootSession(string serverDirectory, bool noSave)
        {
            // 従来入口はNoSave指定を保ち、前回の固定world設定だけを破棄する
            // Preserve the legacy NoSave choice while discarding only stale fixed-world settings
            SessionState.SetBool(InitializeScenePipeline.SkipSaveLoadSessionKey, noSave);
            PlaytestWorldBootSession.Clear();
            PrepareCommonBootSession(serverDirectory);
        }

        internal static void PrepareWorldBootSession(string serverDirectory, string worldDirectory, string mapMode, int seed)
        {
            // 固定worldはGuid一時パスへの上書きを止め、正式な起動設定をdomain reload越しに渡す
            // Fixed-world boot disables the GUID temp-path override and carries official settings across domain reload
            SessionState.SetBool(InitializeScenePipeline.SkipSaveLoadSessionKey, false);
            PlaytestWorldBootSession.Save(serverDirectory, worldDirectory, mapMode, seed);
            PrepareCommonBootSession(serverDirectory);
            ConfigureFixedWorldDebugSettings();
        }

        internal static void ConfigureFixedWorldDebugSettings()
        {
            // 固定QAでは自然環境を選び、初回challengeの自動Skitを抑止する
            // Fixed QA selects pure nature and suppresses the initial challenge's automatic skit
            DebugParameters.SaveInt(DebugEnvironmentTypeKey, PureNatureEnvironmentType);
            DebugParameters.SaveBool(DebugConst.SkitPlaySettingsKey, true);
        }

        internal static bool RestoreAfterDomainReload(bool isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            UnsubscribeWorldBootScene();

            // pending中のPlayModeだけを復元し、固定worldの場合に限ってsceneLoadedを購読する
            // Restore only a pending PlayMode boot and subscribe to sceneLoaded solely for fixed worlds
            if (!SessionState.GetBool(PendingBootKey, false)) return false;
            if (!isPlayingOrWillChangePlaymode) return false;
            if (!PlaytestWorldBootSession.IsPending()) return true;

            SceneManager.sceneLoaded += HandleWorldBootSceneLoaded;
            _worldBootSceneHookRegistered = true;
            return true;
        }

        internal static bool IsWorldBootSceneHookRegistered()
        {
            return _worldBootSceneHookRegistered;
        }

        internal static bool InjectWorldBootSettings(InitializeScenePipeline pipeline)
        {
            UnsubscribeWorldBootScene();
            if (!PlaytestWorldBootSession.TryCreateInitializeProprieties(out var proprieties)) return false;

            // sceneLoadedはStartより前に発火するため、初期化が読む前に固定引数を注入する
            // sceneLoaded fires before Start, so inject fixed arguments before initialization reads them
            pipeline.SetProperty(proprieties);
            return true;
        }

        internal static void HandlePlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;

            // 通常再生へ設定を漏らさないよう起動状態と購読を一括解除する
            // Clear boot state and subscriptions together so nothing leaks into normal play
            SessionState.SetBool(InitializeScenePipeline.SkipSaveLoadSessionKey, false);
            SessionState.SetBool(PendingBootKey, false);
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            PlaytestWorldBootSession.Clear();
            UnsubscribeWorldBootScene();
            EditorSceneManager.playModeStartScene = null;

            var sessionDebugCache = PlaytestPaths.DebugCacheDirectory;
            if (!string.IsNullOrEmpty(sessionDebugCache) && DebugParametersCacheDirectory.GetOverride() == sessionDebugCache)
                DebugParametersCacheDirectory.SetOverride(null);
        }

        private static void PrepareCommonBootSession(string serverDirectory)
        {
            SessionState.SetBool(PendingBootKey, true);
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", true);
            PlaytestPaths.ResetSession();

            // 開発者設定を複製したセッション専用cacheへ切り替えてからmasterパスを書く
            // Switch to a session-local copy of developer settings before writing the master path
            DebugParametersCacheDirectory.CopyDefaultTo(PlaytestPaths.DebugCacheDirectory);
            DebugParametersCacheDirectory.SetOverride(PlaytestPaths.DebugCacheDirectory);
            if (!string.IsNullOrEmpty(serverDirectory))
                DebugParameters.SaveString(ServerDirectory.DebugServerDirectorySettingKey, serverDirectory);
        }

        private static void HandleWorldBootSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var pipeline = Object.FindFirstObjectByType<InitializeScenePipeline>();
            InjectWorldBootSettings(pipeline);
        }

        private static void UnsubscribeWorldBootScene()
        {
            SceneManager.sceneLoaded -= HandleWorldBootSceneLoaded;
            _worldBootSceneHookRegistered = false;
        }
    }
}
