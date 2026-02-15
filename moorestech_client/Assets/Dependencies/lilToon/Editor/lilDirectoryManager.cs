#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace lilToon
{
    internal class lilDirectoryManager
    {
        public const string editorSettingTempPath           = "Temp/lilToonEditorSetting";
        public const string languageSettingTempPath         = "Temp/lilToonLanguageSetting";
        public const string versionInfoTempPath             = "Temp/lilToonVersion";
        public const string packageListTempPath             = "Temp/lilToonPackageList";
        public const string forceOptimizeBuildTempPath      = "Temp/lilToonForceOptimizeBuild";
        public const string postBuildTempPath               = "Temp/lilToonPostBuild";
        public const string startupTempPath                 = "Temp/lilToonStartup";

        #if NET_4_6
            public const string rspPath = "Assets/csc.rsp";
        #else
            public const string rspPath = "Assets/mcs.rsp";
        #endif

        public static string GetPackageJsonPath()           => GUIDToPath("3d0fceb0fac2e486c901303f200d9a08"); // "package.json"
        public static string GetBaseShaderFolderPath()      => GUIDToPath("10192b226f79f42818d5205261638066"); // "BaseShaderResources"
        public static string GetEditorFolderPath()          => GUIDToPath("1c21482cf9c2945bfb1c84889fa5e7eb"); // "Editor"
        public static string GetPresetsFolderPath()         => GUIDToPath("a41c530ac9717430584e8bab20b1be6c"); // "Presets"
        public static string GetEditorPath()                => GUIDToPath("43457116803c54ee79163cc9891fb7c1"); // "Editor/lilInspector/lilInspector.cs"
        public static string GetShaderFolderPath()          => GUIDToPath("82bf9e58018a84d71b24df202741a464"); // "Shader"
        public static string GetShaderPipelinePath()        => GetShaderFolderPath() + "/Includes/lil_pipeline.hlsl"; // .metaが存在しないためパス構築
        public static string GetShaderCommonPath()          => GUIDToPath("f06a6e421bf7b4bc7a65da9660c56a08"); // "Shader/Includes/lil_common.hlsl";
        public static string GetGUIBoxInDarkPath()          => GUIDToPath("a24018224a0504345a88590b3664a7fb"); // "Editor/Resources/gui_box_inner_dark.png"
        public static string GetGUIBoxInLightPath()         => GUIDToPath("928eb99d4cfef400bb4074ba81771cee"); // "Editor/Resources/gui_box_inner_light.png"
        public static string GetGUIBoxInHalfDarkPath()      => GUIDToPath("63bcd584db6494211a5e3f75a0bd5a00"); // "Editor/Resources/gui_box_inner_half_dark.png"
        public static string GetGUIBoxInHalfLightPath()     => GUIDToPath("57c21f83cda8042b796943106efc0d76"); // "Editor/Resources/gui_box_inner_half_light.png"
        public static string GetGUIBoxOutDarkPath()         => GUIDToPath("06db0e1754f0b4954b4a406afcbb1f36"); // "Editor/Resources/gui_box_outer_dark.png"
        public static string GetGUIBoxOutLightPath()        => GUIDToPath("ae606761de1be415ab8c2e23b2c94a41"); // "Editor/Resources/gui_box_outer_light.png"
        public static string GetGUICustomBoxDarkPath()      => GUIDToPath("e0439ae55172942c7af7cae830bde350"); // "Editor/Resources/gui_custom_box_dark.png"
        public static string GetGUICustomBoxLightPath()     => GUIDToPath("54bb7b553df8b4425b25f099a9b1df78"); // "Editor/Resources/gui_custom_box_light.png"
        public static string GetCurrentRPPath()             => GetEditorFolderPath() + "/CurrentRP.txt"; // .metaが存在しないためパス構築
        public static string GetClusterCreatorKitPath()     => GUIDToPath("6f11c0d5c326e4a6c851aa1c02ff11ee"); // "ClusterCreatorKit/package.json"
        public static string GetMainFolderPath() // "Assets/lilToon"
        {
            string editorPath = GetEditorFolderPath();
            // バッチモードでGUID解決が失敗する場合のフォールバック
            if (string.IsNullOrEmpty(editorPath) || editorPath.Length < 7)
            {
                return "Assets/Dependencies/lilToon";
            }
            return editorPath.Substring(0, editorPath.Length - 7);
        }
        public static string GetShaderSettingPath()         => "ProjectSettings/lilToonSetting.json";     // "ProjectSettings/lilToonSetting.json"
        public static string GetSettingLockPath()           => GetMainFolderPath() + "/SettingLock.json"; // "SettingLock.json"
        public static string[] GetShaderFolderPaths()       => new[] {GetShaderFolderPath()};
        public static string GetSettingFolderPath()         => GetMainFolderPath();
        public static string GUIDToPath(string GUID)        => AssetDatabase.GUIDToAssetPath(GUID);

        public static bool ExistsClusterCreatorKit() => !string.IsNullOrEmpty(GetClusterCreatorKitPath());

        public static IEnumerable<string> FindAssetsPath(string filter, string[] folders)
        {
            return AssetDatabase.FindAssets(filter, folders).Select(id => GUIDToPath(id));
        }

        public static IEnumerable<string> FindAssetsPath(string filter)
        {
            return AssetDatabase.FindAssets(filter).Select(id => GUIDToPath(id));
        }

        public static IEnumerable<T> FindAssets<T>(string filter, string[] folders) where T : Object
        {
            return FindAssetsPath(filter, folders).Select(p => AssetDatabase.LoadAssetAtPath<T>(p));
        }

        public static IEnumerable<T> FindAssets<T>(string filter) where T : Object
        {
            return FindAssetsPath(filter).Select(p => AssetDatabase.LoadAssetAtPath<T>(p));
        }
    }
}
#endif