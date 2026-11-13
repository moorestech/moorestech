using System.Collections.Generic;
using UnityEngine.UIElements;

namespace Client.Ui.Content
{
    /// <summary>
    /// 機械レシピの行の片側（素材か結果）の枠。Elements は並べる順、Items はアイテムの枠と ID の組
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
