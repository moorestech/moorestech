using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Editor.Toolbar
{
    /// <summary>
    /// Scene一覧ドロップダウンをツールバーに追加する
    /// Add Scene list dropdown to the toolbar
    /// </summary>
    public static class SceneListToolbarElement
    {
        private const string ElementPath = "moorestech/Scene List";

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Right)]
        public static MainToolbarElement CreateElement()
        {
            // アイコンのみのScene一覧ドロップダウンを作成
            // Create icon-only Scene list dropdown
            var icon = ToolbarUtility.GetBuiltInIcon("d_SceneAsset Icon");
            var content = new MainToolbarContent(icon, "シーン一覧 / Scene List");
            return new MainToolbarDropdown(content, ShowDropdownMenu);
        }

        private static void ShowDropdownMenu(Rect dropDownRect)
        {
            var menu = new GenericMenu();

            var sceneGuids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
            var allScenePaths = sceneGuids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .ToArray();

            if (allScenePaths.Length == 0)
            {
                menu.AddDisabledItem(new GUIContent("シーンなし / No scenes found"));
                menu.ShowAsContext();
                return;
            }

            // Build Settings登録シーン（有効なもののみ）
            // Build Settings scenes (enabled only)
            var buildScenePaths = EditorBuildSettings.scenes
                .Where(scene => scene.enabled)
                .Select(scene => scene.path)
                .Where(path => allScenePaths.Contains(path))
                .ToArray();

            // Build Settings外のシーン
            // Other scenes not in Build Settings
            var otherScenePaths = allScenePaths
                .Where(path => !buildScenePaths.Contains(path))
                .OrderBy(Path.GetFileNameWithoutExtension)
                .ToArray();

            // ビルド設定のシーンを上部に追加
            // Add build scenes at the top
            if (buildScenePaths.Length > 0)
            {
                menu.AddSeparator("\u25bcScenes in build");
                foreach (var scenePath in buildScenePaths)
                {
                    var sceneName = Path.GetFileNameWithoutExtension(scenePath);
                    var path = scenePath;
                    menu.AddItem(new GUIContent(sceneName), false, () => LoadScene(path));
                }
            }

            // ビルド設定外のシーンを下部に追加
            // Add other scenes at the bottom
            if (otherScenePaths.Length > 0)
            {
                menu.AddSeparator("\u25bcOther Scenes");
                foreach (var scenePath in otherScenePaths)
                {
                    var sceneName = Path.GetFileNameWithoutExtension(scenePath);
                    var path = scenePath;
                    menu.AddItem(new GUIContent(sceneName), false, () => LoadScene(path));
                }
            }

            menu.ShowAsContext();
        }

        private static void LoadScene(string scenePath)
        {
            // プレイ中はランタイムでロード、エディタ中は保存確認付きで開く
            // During play: runtime load, in editor: open with save confirmation
            if (Application.isPlaying)
            {
                var sceneName = Path.GetFileNameWithoutExtension(scenePath);
                SceneManager.LoadScene(sceneName);
            }
            else
            {
                if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    EditorSceneManager.OpenScene(scenePath);
                }
            }
        }
    }
}
