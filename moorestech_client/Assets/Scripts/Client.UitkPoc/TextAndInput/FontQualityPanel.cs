using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Client.UitkPoc.TextAndInput
{
    // 同じ和文を、SDF（現在の既定）とビットマップの2方式・複数の大きさで並べる
    // Shows the same Japanese text in SDF (current) and bitmap at several sizes
    public class FontQualityPanel : VisualElement
    {
        public const string Sample = "電力・鉄鉱石の粉・歯車ベルトコンベア 1,234/分";
        private static readonly int[] Sizes = { 11, 12, 14, 16, 20 };

        public FontQualityPanel(FontAsset sdfFont, Font sourceFont)
        {
            AddToClassList("tx-font-panel");
            AddRow("SDF（標準）", FontDefinition.FromSDFFont(sdfFont));
            AddRow("ビットマップ", FontDefinition.FromFont(sourceFont));
        }

        private void AddRow(string caption, FontDefinition font)
        {
            var heading = new Label(caption);
            heading.AddToClassList("tx-font-panel__caption");
            Add(heading);
            foreach (var size in Sizes)
            {
                var label = new Label($"{size}px  {Sample}");
                label.AddToClassList("tx-font-panel__sample");
                label.style.fontSize = size;
                label.style.unityFontDefinition = font;
                Add(label);
            }
        }
    }
}
