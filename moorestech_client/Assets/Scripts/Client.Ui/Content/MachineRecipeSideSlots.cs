using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Client.Ui.Content
{
    /// <summary>
    /// 行の片側の枠と、アイテムの枠と ID の組
    /// One side (materials or results) of a machine recipe row; Elements in display order, Items pair each item slot with its ID
    /// </summary>
    public readonly struct MachineRecipeSideSlots
    {
        public readonly IReadOnlyList<VisualElement> Elements;
        public readonly IReadOnlyList<MachineRecipeItemSlot> Items;

        public MachineRecipeSideSlots(IReadOnlyList<VisualElement> elements, IReadOnlyList<MachineRecipeItemSlot> items)
        {
            Elements = elements;
            Items = items;
        }
    }
}
