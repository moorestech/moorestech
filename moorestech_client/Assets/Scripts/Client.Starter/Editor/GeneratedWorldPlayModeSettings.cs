#if UNITY_EDITOR
using System.IO;
using Client.DebugSystem.Environment;
using Common.Debug;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Server.Boot;
using Server.Boot.Args;
using UnityEditor;

namespace Client.Starter.Editor
{
    public static class GeneratedWorldPlayModeSettings
    {
        public const string SessionStateKey = "moorestech_GeneratedWorldPlayMode";
        private const string WorldDirectoryName = "world_generated";

        // DebugEnvironmentControllerと重複定義（各所で共有されているprivate constの既存流儀）
        // Duplicated from DebugEnvironmentController (existing convention of a shared private const per file)
        private const string DebugEnvironmentTypeKey = "DebugEnvironmentTypeKey";

        // 起動ボタンと削除メニューが共有する生成ワールドの保存先
        // Generated world save path shared by the play button and the delete menu
        public static string WorldDirectoryPath => GameSystemPaths.GetSaveFilePath(WorldDirectoryName);

        // 開発者の永続デバッグ設定を汚さないための一時cache置き場
        // Temporary cache location that keeps the developer's persistent debug settings clean
        public static string DebugCacheDirectory => Path.Combine(Path.GetTempPath(), "moorestech-generated-play-debug-cache");

        public static void ApplyIfNeeded(InitializeProprieties proprieties)
        {
            if (!SessionState.GetBool(SessionStateKey, false)) return;

            // 専用ワールドと自動生成モードだけを上書きする（セーブは通常どおり有効のまま）
            // Override only the dedicated world and generated map mode (saving stays enabled)
            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            settings.WorldDirectory = WorldDirectoryPath;
            settings.MapMode = WorldMapMode.Generated;
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(settings);
        }

        public static void BeginIsolatedDebugEnvironment()
        {
            // 開発者設定を複製した一時cacheへ隔離するので、Runtime指定は通常再生に残らない
            // Isolate into a temp cache cloned from developer settings, so the Runtime choice never leaks into normal play
            DebugParametersCacheDirectory.CopyDefaultTo(DebugCacheDirectory);
            DebugParametersCacheDirectory.SetOverride(DebugCacheDirectory);
            DebugParameters.SaveInt(DebugEnvironmentTypeKey, (int)DebugEnvironmentType.Runtime);
        }

        public static void EndIsolatedDebugEnvironment()
        {
            // 自分が張った隔離だけを外す。Begin時の他者overrideは退避しないので戻らない
            // Clear only this feature's isolation; an override present at Begin is not saved and never comes back
            if (DebugParametersCacheDirectory.GetOverride() != DebugCacheDirectory) return;

            DebugParametersCacheDirectory.SetOverride(null);
        }
    }
}
#endif
