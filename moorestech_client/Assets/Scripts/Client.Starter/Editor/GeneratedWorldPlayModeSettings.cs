#if UNITY_EDITOR
using Game.MapGeneration.Provisioning;
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

        // 起動ボタンと削除メニューが共有する生成ワールドの保存先
        // Generated world save path shared by the play button and the delete menu
        public static string WorldDirectoryPath => GameSystemPaths.GetSaveFilePath(WorldDirectoryName);

        public static void ApplyIfNeeded(InitializeProprieties proprieties)
        {
            if (!SessionState.GetBool(SessionStateKey, false)) return;

            // 専用ワールドと自動生成モードだけを上書きする（セーブは通常どおり有効のまま）
            // Override only the dedicated world and generated map mode (saving stays enabled)
            var settings = CliConvert.Parse<StartServerSettings>(proprieties.CreateLocalServerArgs);
            settings.WorldDirectory = WorldDirectoryPath;
            settings.MapMode = WorldProvisioner.GeneratedMapMode;
            proprieties.CreateLocalServerArgs = CliConvert.Serialize(settings);
        }
    }
}
#endif
