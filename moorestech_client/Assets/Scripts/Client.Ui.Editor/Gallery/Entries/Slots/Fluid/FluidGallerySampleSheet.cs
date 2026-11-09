using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Slots.Fluid
{
    /// <summary>
    /// 液体の族の見本の入れ物（つまみ上書き付き）
    /// Container for fluid-family samples: the slot-family container plus fluid screen knob overrides
    /// </summary>
    public static class FluidGallerySampleSheet
    {
        private const string SheetPath = "Assets/Scripts/Client.Ui.Editor/Gallery/Entries/Slots/Fluid/FluidGallerySamples.uss";

        // 参照キャプチャを撮った mock-host の液体の色（W/../e2e/mock-host/fixtures/fluidMasterFixtures.ts）
        // Fluid colors of the mock-host that took the reference captures (W/../e2e/mock-host/fixtures/fluidMasterFixtures.ts)
        public static readonly Color Water = new Color32(0x2A, 0x6F, 0xE0, 0xFF);
        public static readonly Color Oil = new Color32(0x8B, 0x5A, 0x2B, 0xFF);

        public static VisualElement CreateSampleRoot()
        {
            // 上書きが欠けるとレシピ行の見本が画面の形にならないので、開発者へ知らせて素の並びで続ける
            // Without the overrides the recipe-row samples lose the screen shape, so tell the developer and continue unstyled
            var root = SlotGallerySampleSheet.CreateSampleRoot();
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SheetPath);
            if (sheet == null)
            {
                Debug.LogError($"[UiGallery] fluid sample sheet not found: {SheetPath}");
                return root;
            }
            root.styleSheets.Add(sheet);
            return root;
        }
    }
}
