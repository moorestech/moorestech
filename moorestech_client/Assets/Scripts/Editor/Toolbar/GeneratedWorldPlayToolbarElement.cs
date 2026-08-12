using Client.Starter.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;

namespace Client.Editor.Toolbar
{
    /// <summary>
    /// 自動生成ワールドでゲームを起動する専用の再生ボタンをツールバーに追加する
    /// Add a dedicated play button that launches the game with a generated world
    /// </summary>
    public static class GeneratedWorldPlayToolbarElement
    {
        private const string ElementPath = "moorestech/Generated Play";
        private const string GameInitializerScenePath = "Assets/Scenes/Game/GameInitialaizer.unity";

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // 再生終了時の後始末を登録する
            // Register cleanup for when play mode ends
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        [MainToolbarElement(ElementPath, defaultDockPosition = MainToolbarDockPosition.Right, defaultDockIndex = 1)]
        public static MainToolbarElement CreateElement()
        {
            // 地形アイコン付きボタンを作成する
            // Create a button with a terrain icon
            var icon = ToolbarUtility.GetBuiltInIcon("d_Terrain Icon");
            var content = new MainToolbarContent(icon, "自動生成ワールドでゲームを起動する（初回は生成、以後は続きから）\nLaunch the game with a generated world (created once, then resumed)");
            return new MainToolbarButton(content, OnClicked);
        }

        private static void OnClicked()
        {
            // 既に再生中なら何もしない
            // Do nothing if already playing
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;

            // 生成ワールド起動フラグを立てる（ドメインリロードを越えて保持される）
            // Set the generated-world launch flag (persists across domain reload)
            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, true);

            // オーサリング地形との重畳を避けるためデバッグ環境をRuntimeへ切替える
            // Switch the debug environment to Runtime to avoid overlap with authored terrain
            GeneratedWorldPlayModeSettings.ApplyDebugEnvironmentOverride();

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

            SessionState.SetBool(GeneratedWorldPlayModeSettings.SessionStateKey, false);
            EditorSceneManager.playModeStartScene = null;

            // 自分が切り替えていた場合だけデバッグ環境を復元する（通常再生では何もしない）
            // Restore the debug environment only if this feature switched it (no-op for normal play)
            GeneratedWorldPlayModeSettings.RestoreDebugEnvironmentIfNeeded();
        }
    }
}
