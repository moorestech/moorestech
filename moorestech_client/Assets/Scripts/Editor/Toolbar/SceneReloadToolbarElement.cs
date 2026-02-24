using UnityEditor;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Client.Editor.Toolbar
{
    /// <summary>
    /// シーンリロードボタンをツールバーに追加する
    /// Add scene reload button to the toolbar
    /// </summary>
    public static class SceneReloadToolbarElement
    {
        private const string ElementPath = "moorestech/Scene Reload";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // プレイモード変更時にツールバーを更新する
            // Refresh toolbar when play mode changes
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Left, defaultDockIndex = int.MaxValue)]
        public static MainToolbarElement CreateElement()
        {
            // リロードボタンを作成（プレイ中のみ有効）
            // Create reload button (enabled only during play mode)
            var icon = ToolbarUtility.GetBuiltInIcon("d_preAudioAutoPlayOff");
            var content = new MainToolbarContent(icon, "シーンリロード / Reload Scene");
            var button = new MainToolbarButton(content, OnReloadClicked);
            button.enabled = EditorApplication.isPlaying;
            return button;
        }

        private static void OnReloadClicked()
        {
            // 現在のシーンを名前でリロード
            // Reload the current scene by name
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            MainToolbar.Refresh(ElementPath);
        }
    }
}
