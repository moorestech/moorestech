using System.Collections.Generic;
using Client.Ui.Components.Controls;
using Client.Ui.Components.Recipe;
using Client.Ui.Components.Slots;
using Client.Ui.Editor.Gallery.Entries.Slots;
using Client.Ui.State.Text;
using Mooresmaster.Localization.Generated;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Client.Ui.Editor.Gallery.Entries.Recipe
{
    /// <summary>
    /// レシピ部品の見本の組み立て
    /// Builds recipe component samples
    /// </summary>
    public static class RecipeGallerySampleSheet
    {
        public const string MachineSelectionKnobClass = "recipe-gallery__machine-selection";
        public const string ResearchKnobClass = "recipe-gallery__research";
        private const string EntryNameClass = "recipe-gallery__entry-name";
        private const string SheetPath = "Assets/Scripts/Client.Ui.Editor/Gallery/Entries/Recipe/RecipeGallerySamples.uss";

        public static VisualElement CreateSampleRoot()
        {
            // 画面の上書きが欠けると Web と比べられないので、ギャラリーを開いた開発者へ知らせて素の並びで続ける
            // Without the screen overrides nothing can be compared, so tell the developer who opened the gallery and continue unstyled
            var root = new VisualElement();
            root.AddToClassList("recipe-gallery");
            var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(SheetPath);
            if (sheet == null)
            {
                Debug.LogError($"[UiGallery] recipe sample sheet not found: {SheetPath}");
                return root;
            }
            root.styleSheets.Add(sheet);
            return root;
        }

        public static VisualElement AddRow(VisualElement parent, string knobClass)
        {
            var row = new VisualElement();
            row.AddToClassList("recipe-gallery__row");
            row.AddToClassList(knobClass);
            parent.Add(row);
            return row;
        }

        public static VisualElement AddPlainRow(VisualElement parent)
        {
            // つまみ無しの行（前例 SlotGallerySampleSheet.AddPlainRow）
            // A row without knobs (precedent SlotGallerySampleSheet.AddPlainRow)
            var row = new VisualElement();
            row.AddToClassList("recipe-gallery__row");
            parent.Add(row);
            return row;
        }

        public static RecipeRow CreateCraftRow(string testid, int materialCount, int resultCount, bool craftable)
        {
            // Web の CraftRecipeEntry と同じ組: 秒数・保持していない矢印（0）・クラフトボタン
            // Same set as the Web CraftRecipeEntry: duration, the arrow at 0 while not holding, and the craft button
            var row = new RecipeRow(testid, $"{testid}-arrow", UiText.FromVerbatim("5秒"));
            row.SetSlots(ItemSlots($"{testid}-material", materialCount, 3), ItemSlots($"{testid}-result", resultCount, 1));
            row.SetProgress(0f);
            row.SetAction(CreateCraftButton($"{testid}-craft", craftable));
            return row;
        }

        public static RecipeRow CreateMachineSelectionOilRow(string testid)
        {
            // 参照 4-block-machine-recipe-selection.png の Oil の行: 液体 10 → 液体 1,000・15秒・静止の矢印・操作なし
            // The Oil row of 4-block-machine-recipe-selection.png: fluid 10 to fluid 1,000, 15 seconds, idle arrow, no action
            var input = new FluidAmountSlot($"{testid}-input-fluid-0");
            input.SetFluid(SlotGallerySampleSheet.Icon("gallery-fluid-input", "Oil"), SlotAmountBadge.Show(10));
            var output = new FluidAmountSlot($"{testid}-output-fluid-0");
            output.SetFluid(SlotGallerySampleSheet.Icon("gallery-fluid-output", "Heavy Oil"), SlotAmountBadge.Show(1000));
            var row = new RecipeRow($"{testid}-row", $"{testid}-arrow", UiText.FromVerbatim("15秒"));
            row.SetSlots(new VisualElement[] { input }, new VisualElement[] { output });
            row.SetIdle();
            return row;
        }

        public static RecipeActionButton CreateCraftButton(string testid, bool enabled)
        {
            var button = new RecipeActionButton(testid, new UiText(LocalizationKeys.Ui.Recipe.CraftButtonLabel), RecipeActionLabelWeight.Medium);
            button.SetEnabled(enabled);
            return button;
        }

        public static RecipeActionButton CreateResearchButton(string testid, bool enabled)
        {
            // 研究の上書き（色・寸法・字）は置く側の祖先の ResearchKnobClass が当てる
            // The research overrides (colors, size, type) come from the ResearchKnobClass ancestor where it is placed
            var button = new RecipeActionButton(testid, new UiText(LocalizationKeys.Ui.Research.Action), RecipeActionLabelWeight.Regular);
            button.SetEnabled(enabled);
            return button;
        }

        public static RecipeRow CreateLackingCraftRow(string testid)
        {
            // Web の撮影と同じ無効の行: 素材 0/2（不足は減光と赤字）・100/1、結果 1 枠に 1、0.2秒
            // The disabled row from the Web shot: materials 0/2 (shortage dims and reds) and 100/1, one result of 1, 0.2 seconds
            var materials = new List<VisualElement> { ShortageSlot($"{testid}-material-0", 0, 2), ShortageSlot($"{testid}-material-1", 100, 1) };
            var row = new RecipeRow(testid, $"{testid}-arrow", UiText.FromVerbatim("0.2秒"));
            row.SetSlots(materials, ItemSlots($"{testid}-result", 1, 1));
            row.SetProgress(0f);
            row.SetAction(CreateCraftButton($"{testid}-craft", false));
            return row;
        }

        public static VisualElement CreateMachineSelectionEntry(string testid)
        {
            // Web の 1 項目は「名前 → 継承バッジ → RecipeRow」。名前の段（12px・行高 1・下 4px）を Oil の行の上に置く
            // A Web entry is name, inheritance badge, then RecipeRow; put the name band (12px, line-height 1, 4px below) above the Oil row
            var entry = new VisualElement();
            var name = new Label("Oil");
            name.AddToClassList(EntryNameClass);
            entry.Add(name);
            entry.Add(CreateMachineSelectionOilRow(testid));
            return entry;
        }

        private static ItemSlot ShortageSlot(string testid, double owned, double required)
        {
            // ツールチップは見本に要らないので null（アイテム名のまま）
            // The sample needs no tooltip, so null keeps the item name
            var lacking = owned < required;
            var slot = new ItemSlot(testid);
            slot.SetContent(new ItemSlotContent(SlotGallerySampleSheet.Icon(testid, testid), null, new ItemSlotMarks(false, false, lacking), new ItemSlotShortage(owned, required, lacking, null), null, null));
            return slot;
        }

        private static IReadOnlyList<VisualElement> ItemSlots(string prefix, int count, int amount)
        {
            var slots = new List<VisualElement>();
            for (var i = 0; i < count; i++)
            {
                var slot = new ItemSlot($"{prefix}-{i}");
                slot.SetContent(new ItemSlotContent(SlotGallerySampleSheet.Icon($"{prefix}-{i}", $"Item {i + 1}"), amount, new ItemSlotMarks(false, false, false), null, null, null));
                slots.Add(slot);
            }
            return slots;
        }
    }
}
