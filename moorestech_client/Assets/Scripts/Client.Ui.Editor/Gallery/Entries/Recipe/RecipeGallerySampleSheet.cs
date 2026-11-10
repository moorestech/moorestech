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
    /// レシピ族とレシピのボタン・スクロールの見本の組み立て（ギャラリーと Task 8 の並べ比べの撮影が共有する）
    /// Builds samples for the recipe family and the recipe button and scroll (shared by the gallery and the comparison capture)
    /// </summary>
    public static class RecipeGallerySampleSheet
    {
        public const string MachineSelectionKnobClass = "recipe-gallery__machine-selection";
        public const string ResearchKnobClass = "recipe-gallery__research";
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

        public static RecipeRow CreateCraftRow(string testid, int materialCount, int resultCount, bool craftable)
        {
            // Web の CraftRecipeEntry と同じ組: 秒数・保持していない矢印（0）・クラフトボタン
            // Same set as the Web CraftRecipeEntry: duration, the arrow at 0 while not holding, and the craft button
            var row = new RecipeRow(testid, $"{testid}-arrow");
            row.SetDuration(UiText.FromVerbatim("5秒"));
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
            var row = new RecipeRow($"{testid}-row", $"{testid}-arrow");
            row.SetDuration(UiText.FromVerbatim("15秒"));
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

        public static RecipeRow CreateCraftCompareRow(string testid, float rowWidth, string duration, double[] ownedCounts, double[] requiredCounts, int resultCount, bool craftable)
        {
            // Web の撮影で読んだ行と同じ条件で組む: 素材は所持/必要（不足は減光と赤字）、結果は 1 枠に個数
            // Build with the conditions read from the Web shot: materials show owned/required (shortage dims and reds), the result is one slot with its count
            var materials = new List<VisualElement>();
            for (var i = 0; i < ownedCounts.Length; i++)
            {
                var lacking = ownedCounts[i] < requiredCounts[i];
                var slot = new ItemSlot($"{testid}-material-{i}");
                // ツールチップは撮影に写らないので null（アイテム名のまま）
                // The tooltip never shows in the shot, so null keeps the item name
                slot.SetContent(new ItemSlotContent(SlotGallerySampleSheet.Icon($"{testid}-material-{i}", $"Item {i + 1}"), null, new ItemSlotMarks(false, false, lacking), new ItemSlotShortage(ownedCounts[i], requiredCounts[i], lacking, null), null, null));
                materials.Add(slot);
            }
            var row = new RecipeRow(testid, $"{testid}-arrow");
            row.SetDuration(UiText.FromVerbatim(duration));
            row.SetSlots(materials, ItemSlots($"{testid}-result", 1, resultCount));
            row.SetProgress(0f);
            row.SetAction(CreateCraftButton($"{testid}-craft", craftable));
            row.style.width = rowWidth;
            return row;
        }

        public static VisualElement CreateCompareSheet(float machineRowWidth, RecipeRow craftEnabledRow, RecipeRow craftDisabledRow, Color craftGround, Color researchGround)
        {
            // 並べ比べの対象を縦に積む: 機械のレシピ選択の Oil の行・Web と同じ条件のクラフト行 2 状態・クラフトボタン 3 状態・研究ボタン 2 状態
            // Stack the targets: the machine-selection Oil row, craft rows in 2 states built from the Web conditions, craft buttons in 3 states and research buttons in 2 states
            var root = CreateSampleRoot();
            root.AddToClassList("recipe-gallery--compare");
            var machine = AddRow(root, MachineSelectionKnobClass);
            var oil = CreateMachineSelectionOilRow("compare-machine-oil");
            oil.style.width = machineRowWidth;
            machine.Add(oil);
            // クラフト行は Web のインベントリの中央のビューアの地（参照の撮影から読んだ色）の上に置く
            // Craft rows sit on the Web inventory centre viewer ground read from the reference shots
            AddGroundedRow(root, craftGround).Add(craftEnabledRow);
            AddGroundedRow(root, craftGround).Add(craftDisabledRow);
            var craftButtons = AddRow(root, "recipe-gallery__plain");
            craftButtons.Add(CreateCraftButton("compare-craft-enabled", true));
            craftButtons.Add(CreateCraftButton("compare-craft-disabled", false));
            craftButtons.Add(RecipeGalleryHoverDriver.HoverWhenLaidOut(CreateCraftButton("compare-craft-hover", true)));
            // 研究ボタンは Web の研究の詳細ペインの地（参照の撮影から読んだ色）の上に置き、切り抜きの余白 6px も同じ地にする
            // Research buttons sit on the Web research detail pane ground read from the reference shots, with the 6px crop margin on the same ground
            var researchButtons = AddGroundedRow(root, researchGround);
            researchButtons.AddToClassList(ResearchKnobClass);
            researchButtons.Add(CreateResearchButton("compare-research-enabled", true));
            researchButtons.Add(CreateResearchButton("compare-research-disabled", false));
            return root;
        }

        // 切り抜きの余白 6px まで同じ地になるよう、地の色と 8px の内側の余白を持つ段を足す
        // Add a row with the ground color and 8px inner padding so the 6px crop margin shares the ground
        private static VisualElement AddGroundedRow(VisualElement parent, Color ground)
        {
            var row = AddRow(parent, "recipe-gallery__plain");
            row.style.backgroundColor = ground;
            row.style.paddingTop = 8f;
            row.style.paddingBottom = 8f;
            row.style.paddingLeft = 8f;
            row.style.paddingRight = 8f;
            return row;
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
