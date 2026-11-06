using System;
using Client.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery
{
    /// <summary>
    /// 部品ギャラリーの窓。族ごとに見本を並べる
    /// Component gallery window listing samples per family
    /// </summary>
    public sealed class UiComponentGalleryWindow : EditorWindow
    {
        private const string WindowTitle = "UITK Component Gallery";
        private const string ThemePath = "Assets/UI Toolkit/GameUi/GameUiTheme.tss";
        private const string GalleryStylePath = "Assets/Scripts/Client.Ui.Editor/Gallery/UiGallery.uss";

        [MenuItem("moorestech/UITK/Component Gallery")]
        public static void Open()
        {
            GetWindow<UiComponentGalleryWindow>().titleContent = new GUIContent(WindowTitle);
        }

        private void CreateGUI()
        {
            // 本番のテーマを当てる（無ければ停止）
            // Apply the production theme (stop if missing)
            var theme = AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(ThemePath);
            var galleryStyle = AssetDatabase.LoadAssetAtPath<StyleSheet>(GalleryStylePath);
            if (theme == null || galleryStyle == null) throw new InvalidOperationException($"[UiComponentGalleryWindow] missing {ThemePath} or {GalleryStylePath}");
            rootVisualElement.styleSheets.Add(theme);
            rootVisualElement.styleSheets.Add(galleryStyle);

            // 見本の文言は今の言語で解く
            // Sample texts resolve in the current language
            Localize.Initialize();
            rootVisualElement.Add(new UiGalleryPage(UiGalleryEntryCatalog.CreateAll()));
        }
    }
}
