using System.Collections.Generic;
using Client.Ui.Components.Controls;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Recipe
{
    /// <summary>
    /// 並べ比べのレシピ列。機械のレシピ選択の上書きを当てた列に、名前の段と Oil の行の項目を数本積む（バー・逃げ余白・項目の間隔を比べる）
    /// Comparison recipe column; stacks name-band plus Oil-row entries in a column with the machine-selection overrides (to compare the bar, bleed margins and entry gap)
    /// </summary>
    public static class RecipeGalleryCompareList
    {
        public const string EntryNameClass = "recipe-gallery__entry-name";

        public static VisualElement Create(float columnWidth, float columnHeight, int rowCount)
        {
            // 列の寸法は参照の列から読んだ値。行は Web より多く積んでスクロールのバーを出す
            // The column size comes from the reference column; stack more rows than fit so the scrollbar shows
            var root = RecipeGallerySampleSheet.CreateSampleRoot();
            var column = new VisualElement();
            column.AddToClassList(RecipeGallerySampleSheet.MachineSelectionKnobClass);
            column.style.width = columnWidth;
            column.style.height = columnHeight;
            var list = new RecipeListScrollView("compare-machine-list");
            list.style.flexGrow = 1;
            // Web の 1 項目は「名前 → 継承バッジ → RecipeRow」なので、名前の段（12px・行高 1・下 4px）を行の上に置く。参照にバッジは写っていない
            // A Web entry is name, inheritance badge, then RecipeRow, so put the name band (12px, line-height 1, 4px below) above the row; no badge shows in the reference
            var entries = new List<VisualElement>();
            for (var i = 0; i < rowCount; i++)
            {
                var entry = new VisualElement();
                var name = new Label("Oil");
                name.AddToClassList(EntryNameClass);
                entry.Add(name);
                entry.Add(RecipeGallerySampleSheet.CreateMachineSelectionOilRow($"compare-machine-list-{i}"));
                entries.Add(entry);
            }
            list.SetEntries(entries);
            column.Add(list);
            root.Add(column);
            return root;
        }
    }
}
