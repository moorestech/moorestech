using Client.Game;
using Client.DebugSystem.Environment;
using Client.Playtest.Core;
using Client.Starter;
using Client.Starter.Editor;
using Common.Debug;
using Game.MapGeneration.Transfer;
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
        private static bool _worldBootSceneHookRegistered;
        private static bool _environmentSceneHookRegistered;

        internal static void PrepareLegacyBootSession(string serverDirectory, bool noSave)
        {
            // 従来入口はNoSave指定を保ち、前回の固定world設定だけを破棄する
            // Preserve the legacy NoSave choice while discarding only stale fixed-world settings
            SessionState.SetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, noSave);

            // Generated Playの残置フラグはworldDirectoryとmapModeを乗っ取るので明示解除する
            // A leftover Generated Play flag hijacks worldDirectory and mapMode, so clear it explicitly
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);
            PlaytestWorldBootSession.Clear();
            PrepareCommonBootSession(serverDirectory);
        }

        internal static void PrepareWorldBootSession(string serverDirectory, string worldDirectory, string mapMode, int seed)
        {
            // 不正modeは起動状態とcacheを変更する前に拒否する
            // Reject an invalid mode before changing boot state or cache
            ValidateMapMode(mapMode);

            // 固定worldはGuid一時パスへの上書きを止め、正式な起動設定をdomain reload越しに渡す
            // Fixed-world boot disables the GUID temp-path override and carries official settings across domain reload
            SessionState.SetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, false);

            // Generated Playの残置フラグは指定worldDirectoryとmapModeを乗っ取るので明示解除する
            // A leftover Generated Play flag hijacks the requested worldDirectory and mapMode, so clear it explicitly
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);
            PlaytestWorldBootSession.Save(serverDirectory, worldDirectory, mapMode, seed);
            PrepareCommonBootSession(serverDirectory);

            // 固定QAはデバッグ環境設定を適用するため、共通準備で止めたbootstrapを戻す
            // Fixed QA restores the bootstrap stopped by common setup so its debug environment is applied
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            ConfigureFixedWorldDebugSettings(mapMode);
        }

        internal static void ConfigureFixedWorldDebugSettings(string mapMode)
        {
            // generatedはオーサリング済み地形を全て除外し、templateは外周mapobjectを含む既存地形を維持する
            // Generated excludes every authored terrain, while template preserves existing terrain with outer map objects
            var environmentType = mapMode switch
            {
                WorldMapMode.Generated => DebugEnvironmentType.Runtime,
                WorldMapMode.Template => DebugEnvironmentType.Other,
                _ => throw new System.ArgumentException($"Unknown map mode: '{mapMode}'", nameof(mapMode)),
            };

            DebugParameters.SaveInt(DebugEnvironmentTypeKey, (int)environmentType);
            DebugParameters.SaveBool(DebugConst.SkitPlaySettingsKey, true);
        }

        internal static bool RestoreAfterDomainReload(bool isPlayingOrWillChangePlaymode)
        {
            EditorApplication.playModeStateChanged -= HandlePlayModeStateChanged;
            EditorApplication.playModeStateChanged += HandlePlayModeStateChanged;
            UnsubscribeWorldBootScene();
            UnsubscribeEnvironmentScene();

            // pending中のPlayModeだけを復元し、固定worldの場合に限ってsceneLoadedを購読する
            // Restore only a pending PlayMode boot and subscribe to sceneLoaded solely for fixed worlds
            if (!SessionState.GetBool(PendingBootKey, false)) return false;
            if (!isPlayingOrWillChangePlaymode) return false;
            if (!PlaytestWorldBootSession.IsPending()) return true;

            SceneManager.sceneLoaded += HandleWorldBootSceneLoaded;
            _worldBootSceneHookRegistered = true;
            SceneManager.sceneLoaded += HandleEnvironmentSceneLoaded;
            _environmentSceneHookRegistered = true;
            return true;
        }

        internal static bool IsWorldBootSceneHookRegistered()
        {
            return _worldBootSceneHookRegistered;
        }

        internal static bool IsEnvironmentSceneHookRegistered()
        {
            return _environmentSceneHookRegistered;
        }

        internal static bool ApplyFixedWorldEnvironment()
        {
            // MainGameのsceneLoadedで保存済み環境を適用し、地形構築より先にauthored地形を除外する
            // Apply the saved environment on MainGame sceneLoaded, excluding authored terrain before terrain building
            var savedValue = DebugParameters.GetValueOrDefaultInt(
                DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Debug);
            return DebugEnvironmentController.TrySetEnvironment((DebugEnvironmentType)savedValue);
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
            SessionState.SetBool(SkipSaveLoadPlayModeSettings.SessionStateKey, false);
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);
            SessionState.SetBool(PendingBootKey, false);
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", false);
            PlaytestWorldBootSession.Clear();
            UnsubscribeWorldBootScene();
            UnsubscribeEnvironmentScene();
            EditorSceneManager.playModeStartScene = null;

            // このPlaytestが設定した隔離だけを外し、テストfixture側の隔離を守る
            // Clear only this playtest's isolation, preserving any test-fixture isolation
            var sessionDebugCache = PlaytestPaths.DebugCacheDirectory;
            if (!string.IsNullOrEmpty(sessionDebugCache) && DebugParametersCacheDirectory.GetOverride() == sessionDebugCache)
                DebugParametersCacheDirectory.SetOverride(null);
        }

        private static void PrepareCommonBootSession(string serverDirectory)
        {
            SessionState.SetBool(PendingBootKey, true);

            // IngameDebugConsole等のPlaytestノイズを防ぐためdebug object生成を止める
            // Stop debug object creation to prevent playtest noise such as IngameDebugConsole
            SessionState.SetBool("DebugObjectsBootstrap_Disabled", true);
            PlaytestPaths.ResetSession();

            // 開発者設定を複製したセッション専用cacheへ切り替えてからmasterパスを書く
            // Switch to a session-local copy of developer settings before writing the master path
            DebugParametersCacheDirectory.CopyDefaultTo(PlaytestPaths.DebugCacheDirectory);
            DebugParametersCacheDirectory.SetOverride(PlaytestPaths.DebugCacheDirectory);
            if (!string.IsNullOrEmpty(serverDirectory))
                DebugParameters.SaveString(ServerDirectory.DebugServerDirectorySettingKey, serverDirectory);
        }

        private static void ValidateMapMode(string mapMode)
        {
            if (mapMode == WorldMapMode.Generated || mapMode == WorldMapMode.Template) return;
            throw new System.ArgumentException($"Unknown map mode: '{mapMode}'", nameof(mapMode));
        }

        private static void HandleWorldBootSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            var pipeline = Object.FindFirstObjectByType<InitializeScenePipeline>();
            InjectWorldBootSettings(pipeline);
        }

        private static void HandleEnvironmentSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // 環境ルートを含まない起動シーンでは購読を維持し、MainGameロード時にだけ解除する
            // Keep listening through the boot scene and unsubscribe only when MainGame roots become available
            if (!ApplyFixedWorldEnvironment()) return;
            UnsubscribeEnvironmentScene();
        }

        private static void UnsubscribeWorldBootScene()
        {
            SceneManager.sceneLoaded -= HandleWorldBootSceneLoaded;
            _worldBootSceneHookRegistered = false;
        }

        private static void UnsubscribeEnvironmentScene()
        {
            SceneManager.sceneLoaded -= HandleEnvironmentSceneLoaded;
            _environmentSceneHookRegistered = false;
        }
    }
}
