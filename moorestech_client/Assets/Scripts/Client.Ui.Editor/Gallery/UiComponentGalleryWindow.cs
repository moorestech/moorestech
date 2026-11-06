using System;
using Client.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery
{
    /// <summary>
    /// 部品ギャラリーの窓。本番の tss を当て、IUiGalleryEntry の見本を族ごとに並べる（設計書 §3.3・A-9。uloop screenshot の window モードで撮る）
    /// The component gallery window: applies the production tss and lists IUiGalleryEntry samples per family (design §3.3, A-9; captured with uloop screenshot window mode)
    /// </summary>
    public sealed class UiComponentGalleryWindow : EditorWindow
    {
        public const string WindowTitle = "UITK Component Gallery";
        private const string ThemePath = "Assets/UI Toolkit/GameUi/GameUiTheme.tss";
        private const string GalleryStylePath = "Assets/Scripts/Client.Ui.Editor/Gallery/UiGallery.uss";

        [MenuItem("moorestech/UITK/Component Gallery")]
        public static void Open()
        {
            GetWindow<UiComponentGalleryWindow>().titleContent = new GUIContent(WindowTitle);
        }

        private void CreateGUI()
        {
            // 本番と同じテーマとトークンを当てる。無ければ窓を開いた段階で止める
            // Apply the same theme and tokens as production; stop as the window opens if they are missing
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
