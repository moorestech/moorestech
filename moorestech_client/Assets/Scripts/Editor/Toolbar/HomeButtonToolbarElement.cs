using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Client.Editor.Toolbar
{
    /// <summary>
    /// ホームボタンをツールバーに追加する
    /// Add home button to the toolbar
    /// </summary>
    public static class HomeButtonToolbarElement
    {
        private const string ElementPath = "moorestech/Home";

        // ホームシーンのパス
        // Path to the home scene
        private const string HomeScenePath = "Assets/Scenes/Game/MainGame.unity";

        // アイコンのパス
        // Icon path
        private const string IconPath = "Assets/Scripts/Editor/Toolbar/HomeIcon.png";

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Left, defaultDockIndex = int.MaxValue)]
        public static MainToolbarElement CreateElement()
        {
            // ホームアイコンを読み込み
            // Load home icon
            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            var content = new MainToolbarContent(iconTexture, "ホームシーンに遷移 / Go to Home Scene (MainGame)");
            return new MainToolbarButton(content, OnButtonClicked);
        }

        private static void OnButtonClicked()
        {
            // シーンが存在するか確認
            // Check if scene exists
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(HomeScenePath);
            if (sceneAsset == null)
            {
                Debug.LogError($"ホームシーンが見つかりません: {HomeScenePath}\nHome scene not found: {HomeScenePath}");
                return;
            }

            // プレイ中はランタイムでロード
            // During play mode, load at runtime
            if (Application.isPlaying)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainGame");
                return;
            }

            // 保存確認付きでホームシーンを開く
            // Open home scene with save confirmation
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(HomeScenePath);
            }
        }
    }
}
