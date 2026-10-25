using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace Client.UitkStage0Probe
{
    // 段階0実測用のPanelSettingsと和文フォントをResourcesから組み立てる
    // Builds the stage-0 probe's PanelSettings and Japanese font from Resources
    public static class ProbeAssets
    {
        private static FontAsset _japaneseFont;

        public static PanelSettings CreatePanelSettings(int sortingOrder)
        {
            // PoCの設定（既定ランタイムテーマ・1920x1080基準）を複製し、前後だけ変える
            // Clone the PoC settings (default runtime theme, 1920x1080 reference) and change only the order
            var source = Resources.Load<PanelSettings>("UitkPoc/UitkPocPanelSettings");
            var panelSettings = Object.Instantiate(source);
            panelSettings.name = $"UitkStage0Probe_so{sortingOrder}";
            panelSettings.sortingOrder = sortingOrder;
            return panelSettings;
        }

        public static FontDefinition JapaneseFontDefinition()
        {
            // FromFontはビットマップ描画になるため、SDFの動的フォントアセットを1度だけ作る
            // FromFont renders bitmap glyphs, so build a dynamic SDF font asset once
            if (_japaneseFont == null)
            {
                var font = Resources.Load<Font>("UitkStage0Probe/NotoSansJP-Medium");
                _japaneseFont = FontAsset.CreateFontAsset(font, 90, 9, GlyphRenderMode.SDFAA, 2048, 2048, AtlasPopulationMode.Dynamic, true);
            }
            return FontDefinition.FromSDFFont(_japaneseFont);
        }
    }
}
