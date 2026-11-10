using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;

namespace Client.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// パネル・機械の詳細・枠・液体・カタログから機械の区画の値を組む
    /// Builds the machine section's values from the panel, machine detail, slots, fluids and catalog
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
            return new MachineSectionViewModel(panel.BlockInstanceId, panel.BlockGuid, detail, itemSlots, fluidSlots, slotView, bands, rows, moduleStackLimit, catalog);
        }
    }
}
