using System.Collections.Generic;
using Client.Ui.Components.Slots;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using UnityEngine.UIElements;

namespace Client.Ui.Content
{
    /// <summary>
    /// 機械レシピの行の素材・結果の枠を組む唯一の口。機械 UI の選択行とレシピビューアの機械の 1 件が共有する
    /// The single builder of a machine recipe row's material and result slots, shared by the machine UI selection row and the recipe viewer's machine entry
    /// </summary>
    public static class MachineRecipeRowContentBuilder
    {
        private static readonly ItemSlotMarks NoMarks = new(false, false, false);

        public static MachineRecipeSideSlots Materials(string rowId, MachineRecipeData recipe, GameContentPresentation content)
        {
            return Side($"{rowId}-input", recipe.InputItems, recipe.InputFluids, content);
        }

        public static MachineRecipeSideSlots Results(string rowId, MachineRecipeData recipe, GameContentPresentation content)
        {
            return Side($"{rowId}-output", recipe.OutputItems, recipe.OutputFluids, content);
        }

        private static MachineRecipeSideSlots Side(string sideId, IReadOnlyList<ItemAmount> items, IReadOnlyList<RecipeFluidAmount> fluids, GameContentPresentation content)
        {
            // アイテム（必要数）の後ろに液体（量の帯）。押下の購読は使い手が決める
            // Items (required counts) followed by fluids (amount band); the user decides which presses to subscribe
            var elements = new List<VisualElement>(items.Count + fluids.Count);
            var itemSlots = new List<MachineRecipeItemSlot>(items.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var itemSlot = new ItemSlot($"{sideId}-item-{i}");
                itemSlot.SetContent(new ItemSlotContent(content.Item(items[i].ItemId), items[i].Count, NoMarks, null, null, null));
                elements.Add(itemSlot);
                itemSlots.Add(new MachineRecipeItemSlot(itemSlot, items[i].ItemId));
            }
            for (var i = 0; i < fluids.Count; i++)
            {
                var fluidSlot = new FluidAmountSlot($"{sideId}-fluid-{i}");
                fluidSlot.SetFluid(content.Fluid(fluids[i].FluidId), SlotAmountBadge.Show(fluids[i].Amount));
                elements.Add(fluidSlot);
            }
            return new MachineRecipeSideSlots(elements, itemSlots);
        }
    }
}
