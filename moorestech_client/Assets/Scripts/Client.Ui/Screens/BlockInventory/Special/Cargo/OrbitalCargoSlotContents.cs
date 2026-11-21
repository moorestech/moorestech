using System.Collections.Generic;
using Client.Ui.Components.Slots;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Machine.Slots;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.Items;

namespace Client.Ui.Screens.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 透かしと所持/必要つきの枠を格子へ流す（見せ方は機械の枠と同じ。前例 MachineSlotContents）
    /// Pushes ghosted, held/required slots into a grid (same presentation as machine slots; precedent MachineSlotContents)
    /// </summary>
    public static class OrbitalCargoSlotContents
    {
        public static void Render(BlockItemGrid grid, string gridTestId, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BoundItemSlot> boundSlots, GameContentPresentation content)
        {
            var indices = new List<int>(boundSlots.Count);
            var contents = new List<ItemSlotContent>(boundSlots.Count);
            foreach (var bound in boundSlots)
            {
                indices.Add(bound.Index);
                contents.Add(MachineSlotContents.For(BlockSlotIndexReader.Read(itemSlots, bound.Index, nameof(OrbitalCargoSlotContents), gridTestId), bound, true, content));
            }
            grid.RenderContents(indices, contents);
        }
    }
}
