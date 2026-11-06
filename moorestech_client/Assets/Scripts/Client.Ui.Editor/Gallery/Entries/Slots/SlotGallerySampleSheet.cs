using Client.Ui.Foundation.Icons;
using Client.Ui.State.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Slots
{
    /// <summary>
    /// スロット族の見本の入れ物と絵（見本寸法と上書き USS を当てる）
    /// Containers and pictures for slot-family samples (sample-size and override USS)
    /// </summary>
    public static class SlotGallerySampleSheet
    {
        private const string SheetPath = "Assets/Scripts/Client.Ui.Editor/Gallery/Entries/Slots/SlotGallerySamples.uss";
        private const string SampleIconName = "d_Prefab Icon";

        public static VisualElement CreateSampleRoot()
        {
            // 見本の寸法が欠けると参照と比べられないので、ギャラリーを開いた開発者へ知らせて素の並びで続ける
            // Without the sample sizes nothing can be compared, so tell the developer who opened the gallery and continue unstyled
            var root = new VisualElement();
            root.AddToClassList("slot-gallery");
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SheetPath);
            if (sheet == null)
            {
                Debug.LogError($"[UiGallery] slot sample sheet not found: {SheetPath}");
                return root;
            }
            root.styleSheets.Add(sheet);
            return root;
        }

        public static VisualElement AddRow(VisualElement parent, string knobClass)
        {
            // 行に画面のつまみの上書きを当て、画面の中に置いたのと同じ形にする
            // Apply the screen's knob overrides on the row so samples sit as they would inside that screen
            var row = new VisualElement();
            row.AddToClassList("slot-gallery__row");
            row.AddToClassList(knobClass);
            parent.Add(row);
            return row;
        }

        public static UiIcon Icon(string idText, string name)
        {
            // 見本の絵は Editor 内蔵のアイコン。参照の DEMO 画像の中身は比べず、枠・個数・余白を比べる
            // The sample picture is a built-in Editor icon; the reference DEMO art is not compared, only frame, count and padding
            var texture = EditorGUIUtility.FindTexture(SampleIconName);
            if (texture == null) Debug.LogError($"[UiGallery] built-in icon not found: {SampleIconName}; samples fall back to #id");
            return new UiIcon(texture, idText, UiText.FromVerbatim(name));
        }

        public static UiIcon MissingIcon(string idText, string name)
        {
            return new UiIcon(null, idText, UiText.FromVerbatim(name));
        }
    }
}
