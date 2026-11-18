using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Panels
{
    /// <summary>
    /// 面・装飾の見本の入れ物を作る。見本の寸法とつまみの上書きの USS を当て、見本を枠に包む
    /// Builds containers for the surface and decor samples, applying the sample-size and knob-override USS and wrapping each sample in a cell
    /// </summary>
    public static class PanelsAndDecorSampleSheet
    {
        private const string SheetPath = "Assets/Scripts/Client.Ui.Editor/Gallery/Entries/Panels/PanelsAndDecorSamples.uss";

        public static VisualElement CreateSampleRow()
        {
            // 見本の寸法が欠けると参照と比べられないので、ギャラリーを開いた開発者へ知らせて素の並びで続ける
            // Without the sample sizes nothing can be compared, so tell the developer who opened the gallery and continue unstyled
            var row = new VisualElement();
            row.AddToClassList("gallery-samples");
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SheetPath);
            if (sheet == null)
            {
                Debug.LogError($"[UiGallery] sample sheet not found: {SheetPath}");
                return row;
            }
            row.styleSheets.Add(sheet);
            return row;
        }

        public static VisualElement Cell(VisualElement sample, string sizeClassName)
        {
            sample.AddToClassList(sizeClassName);
            var cell = new VisualElement();
            cell.AddToClassList("gallery-samples__cell");
            cell.Add(sample);
            return cell;
        }
    }
}
