using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;
using YujiAp.UnityToolbarExtension.Editor;

namespace Client.Editor.Toolbar
{
    /// <summary>
    /// ホームボタンをツールバーに追加するエディタ拡張
    /// Editor extension to add a home button to the toolbar
    /// </summary>
    [InitializeOnLoad]
    public class HomeButtonToolbarElement : IToolbarElement
    {
        // ホームシーンのパス
        // Path to the home scene
        private const string HomeScenePath = "Assets/Scenes/Game/MainGame.unity";

        // アイコンのパス
        // Icon path
        private const string IconPath = "Assets/Scripts/Editor/Toolbar/HomeIcon.png";

        public ToolbarElementLayoutType DefaultLayoutType => ToolbarElementLayoutType.LeftSideRightAlign;

        public VisualElement CreateElement()
        {
            // ホームアイコンを読み込み
            // Load home icon
            var iconTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);

            // ボタンを作成
            // Create button
            var button = new EditorToolbarButton(OnButtonClicked);

            // アイコン画像を設定
            // Set icon image
            var image = new Image { image = iconTexture };
            image.style.width = 16;
            image.style.height = 16;
            button.Add(image);

            // ボタンの背景色を設定
            // Set button background color
            var normalColor = new Color(80f / 255f, 80f / 255f, 80f / 255f);
            var hoverColor = new Color(120f / 255f, 120f / 255f, 120f / 255f);

            button.style.backgroundColor = normalColor;

            // ホバー時の色変更を設定
            // Set hover color change
            button.RegisterCallback<MouseEnterEvent>(evt =>
            {
                button.style.backgroundColor = hoverColor;
            });

            button.RegisterCallback<MouseLeaveEvent>(evt =>
            {
                button.style.backgroundColor = normalColor;
            });

            button.tooltip = "Transition to Home Scene";

            return button;
        }

        /// <summary>
        /// ボタンがクリックされた時の処理
        /// Process when button is clicked
        /// </summary>
        private void OnButtonClicked()
        {
            // シーンが存在するか確認
            // Check if scene exists
            var sceneAsset = AssetDatabase.LoadAssetAtPath<SceneAsset>(HomeScenePath);
            if (sceneAsset == null)
            {
                Debug.LogError($"Home scene not found: {HomeScenePath}");
                return;
            }

            // 現在のシーンに変更がある場合は保存を促す
            // Prompt to save if there are changes in the current scene
            if (EditorSceneManager.GetActiveScene().isDirty)
            {
                var result = EditorUtility.DisplayDialogComplex(
                    "Save Scene Changes",
                    "There are unsaved changes in the current scene. Do you want to save?",
                    "Save",
                    "Don't Save",
                    "Cancel"
                );

                switch (result)
                {
                    case 0: // 保存 / Save
                        EditorSceneManager.SaveOpenScenes();
                        break;
                    case 1: // 保存しない / Don't Save
                        break;
                    case 2: // キャンセル / Cancel
                        return;
                }
            }

            // ホームシーンを開く
            // Open home scene
            EditorSceneManager.OpenScene(HomeScenePath);
        }
    }
}
