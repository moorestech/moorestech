using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;

namespace Client.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// パネル・詳細・枠・液体・カタログから値を組む
    /// Builds values from panel, detail, slots, fluids, catalog
    /// </summary>
    public static class MachineSectionViewModelBuilder
    {
        public static MachineSectionViewModel Build(OpenBlockInventoryPanel panel, MachineDetail detail, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluidSlots, MachineRecipeCatalog catalog, int moduleStackLimit)
        {
            // 行はブロックと選択が同じ間は前回の列（状態更新ごとに組まない。D-39）
            // Rows stay the previous list while the block and selection are unchanged (not rebuilt per state update, D-39)
            var rows = catalog.RowsFor(panel.BlockGuid, detail.Selection);
            var slotView = MachineSlotGhosts.BuildMachineSlotView(detail.Binding, itemSlots, fluidSlots);
            var bands = detail.Binding.Layout.Split(itemSlots.Count);
            return new MachineSectionViewModel(panel.BlockInstanceId, detail, itemSlots, fluidSlots, slotView, bands, rows, moduleStackLimit, catalog);
        }
    }
}
