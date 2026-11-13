using System.Collections.Generic;
using Client.Ui.Components.Slots;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using UnityEngine.UIElements;

namespace Client.Ui.Content
{
    /// <summary>
    /// 機械レシピの行の枠を組む口（選択行とビューアが共有）
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
            // アイテムの後ろに液体。押下の購読は使い手
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
