using Client.Starter;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Client.Editor.Toolbar
{
    /// <summary>
    /// セーブデータをロードも保存もせずにゲームを起動する専用の再生ボタンをツールバーに追加する
    /// Add a dedicated play button that launches the game without loading or saving save data
    /// </summary>
    public static class NoSaveLoadPlayToolbarElement
    {
        private const string ElementPath = "moorestech/NoSave Play";
        private const string GameInitializerScenePath = "Assets/Scenes/Game/GameInitialaizer.unity";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // 再生終了時の後始末を登録する
            // Register cleanup for when play mode ends
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 0)]
        public static MainToolbarElement CreateElement()
        {
            // 再生アイコン付きボタンを作成する
            // Create a button with a play icon
            var icon = ToolbarUtility.GetBuiltInIcon("d_PlayButton");
            var content = new MainToolbarContent(icon, "セーブデータをロード・保存せずにゲームを起動する\nLaunch the game without loading or saving save data");
            return new MainToolbarButton(content, OnClicked);
        }

        private static void OnClicked()
        {
            // 既に再生中なら何もしない
            // Do nothing if already playing
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // セーブをロード・保存しないフラグを立てる（ドメインリロードを越えて保持される）
            // Set the skip-save-load flag (persists across domain reload)
            SessionState.SetBool(InitializeScenePipeline.SkipSaveLoadSessionKey, true);

            // ゲーム初期化シーンから再生を開始する
            // Start play mode from the game initializer scene
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(GameInitializerScenePath);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            // 再生終了時にフラグと開始シーン設定を元へ戻す（通常の再生ボタンに影響させない）
            // Reset the flag and start-scene setting when play mode ends (so the normal play button is unaffected)
            if (state != PlayModeStateChange.EnteredEditMode) return;

            SessionState.SetBool(InitializeScenePipeline.SkipSaveLoadSessionKey, false);
            EditorSceneManager.playModeStartScene = null;
        }
    }
}
