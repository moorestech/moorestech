using System.Collections.Generic;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using UnityEngine.UIElements;

namespace Client.Ui.Screens.BlockInventory.Machine.RecipeSelection
{
    /// <summary>
    /// レシピ選択行の素材・結果の枠
    /// Material and result slots of a selection row: items then fluids; slot presses are not subscribed, the row is the target
    /// </summary>
    public static class MachineRecipeRowContentBuilder
    {
        private static readonly ItemSlotMarks NoMarks = new(false, false, false);

        public static IReadOnlyList<VisualElement> Materials(string rowId, MachineRecipeData recipe, GameContentPresentation content)
        {
            return Slots($"{rowId}-input", recipe.InputItems, recipe.InputFluids, content);
        }

        public static IReadOnlyList<VisualElement> Results(string rowId, MachineRecipeData recipe, GameContentPresentation content)
        {
            return Slots($"{rowId}-output", recipe.OutputItems, recipe.OutputFluids, content);
        }

        private static List<VisualElement> Slots(string sideId, IReadOnlyList<ItemAmount> items, IReadOnlyList<RecipeFluidAmount> fluids, GameContentPresentation content)
        {
            // アイテムは ItemSlot、液体は帯
            // Items are ItemSlots with counts; fluids are FluidAmountSlots with the amount band
            var slots = new List<VisualElement>(items.Count + fluids.Count);
            for (var i = 0; i < items.Count; i++)
            {
                var itemSlot = new ItemSlot($"{sideId}-item-{i}");
                itemSlot.SetContent(new ItemSlotContent(content.Item(items[i].ItemId), items[i].Count, NoMarks, null, null, null));
                slots.Add(itemSlot);
            }
            for (var i = 0; i < fluids.Count; i++)
            {
                var fluidSlot = new FluidAmountSlot($"{sideId}-fluid-{i}");
                fluidSlot.SetFluid(content.Fluid(fluids[i].FluidId), SlotAmountBadge.Show(fluids[i].Amount));
                slots.Add(fluidSlot);
            }
            return slots;
        }
    }
}
