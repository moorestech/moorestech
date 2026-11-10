using System;
using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.Logic.Detail;
using Client.Ui.State.BlockInventory.Logic.Machine;
using Client.Ui.State.BlockInventory.Machine.Detail;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.Items;
using Client.Ui.State.Recipe.Data;
using Game.Block.Interface;

namespace Client.Ui.State.BlockInventory.Machine
{
    /// <summary>
    /// 開いた機械の区画が描く値
    /// Values the open machine section draws: detail, slots, fluids, ghosts, bands, rows, module limit
    /// </summary>
    public sealed class MachineSectionViewModel
    {
        public BlockInstanceId BlockInstanceId { get; }
        public MachineDetail Detail { get; }
        public IReadOnlyList<SlotItem> ItemSlots { get; }
        public IReadOnlyList<BlockFluidSlotState> FluidSlots { get; }
        public MachineSlotView SlotView { get; }
        public MachineSlotBands Bands { get; }
        public IReadOnlyList<MachineRecipeSelectionRow> Rows { get; }
        public int ModuleStackLimit { get; }

        private readonly MachineRecipeCatalog _catalog;

        public MachineSectionViewModel(BlockInstanceId blockInstanceId, MachineDetail detail, IReadOnlyList<SlotItem> itemSlots, IReadOnlyList<BlockFluidSlotState> fluidSlots, MachineSlotView slotView, MachineSlotBands bands, IReadOnlyList<MachineRecipeSelectionRow> rows, int moduleStackLimit, MachineRecipeCatalog catalog)
        {
            BlockInstanceId = blockInstanceId;
            Detail = detail;
            ItemSlots = itemSlots;
            FluidSlots = fluidSlots;
            SlotView = slotView;
            Bands = bands;
            Rows = rows;
            ModuleStackLimit = moduleStackLimit;
            _catalog = catalog;
        }

        public bool TryGetSelectedRow(out MachineRecipeSelectionRow row)
        {
            foreach (var candidate in Rows)
            {
                if (!candidate.Selected) continue;
                row = candidate;
                return true;
            }
            row = null;
            return false;
        }

        // 同種の機械を続けて開いても前の値と区別するため、個体 ID で一致を見る
        // Match by instance ID so the previous value is told apart even when the same machine type is reopened
        public bool IsFor(OpenBlockInventoryPanel panel)
        {
            return panel.BlockInstanceId == BlockInstanceId;
        }

        public bool TryGetRecipe(Guid recipeGuid, out MachineRecipeData recipe)
        {
            return _catalog.TryGetRecipe(recipeGuid, out recipe);
        }
    }
}
