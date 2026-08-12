#if UNITY_EDITOR
using System.IO;
using UnityEditor;

namespace Client.Starter.Editor
{
    /// <summary>
    /// 生成ワールドを削除して次回起動時に新しいseedで再生成させるメニュー項目
    /// Menu item that deletes the generated world so the next launch regenerates it with a new seed
    /// </summary>
    public static class GeneratedWorldDeleteMenu
    {
        private const string DialogTitle = "Delete Generated World";

        [MenuItem("moorestech/Delete Generated World")]
        private static void DeleteGeneratedWorld()
        {
            // 再生中はサーバーがワールドを使用中のため削除を拒否する
            // Refuse deletion during play mode because the server is using the world
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(DialogTitle, "再生中は削除できません。再生を停止してください。", "OK");
                return;
            }

            var worldDirectory = GeneratedWorldPlayModeSettings.WorldDirectoryPath;
            if (!Directory.Exists(worldDirectory))
            {
                EditorUtility.DisplayDialog(DialogTitle, $"生成ワールドはありません。\n{worldDirectory}", "OK");
                return;
            }

            var confirmed = EditorUtility.DisplayDialog(DialogTitle,
                $"生成ワールドを削除します。次回起動時に新しいseedで再生成されます。\n{worldDirectory}", "削除", "キャンセル");
            if (!confirmed) return;

            Directory.Delete(worldDirectory, true);
        }
    }
}
#endif
