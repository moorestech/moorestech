using System;
using System.Collections.Generic;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SectionStack;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// 標準の本文（固定順の区画の列）を blockType の設定から組む
    /// Builds the standard body (the fixed-order section list) from the blockType settings
    /// </summary>
    public sealed class SectionStackBodyFactory
    {
        private readonly BlockItemGridFactory _grids;
        private readonly SectionStackSlotRegistry _slots;
        private readonly IBlockInventoryPanelSource _panels;

        public SectionStackBodyFactory(BlockItemGridFactory grids, SectionStackSlotRegistry slots, IBlockInventoryPanelSource panels)
        {
            _grids = grids;
            _slots = slots;
            _panels = panels;
        }

        public IBlockInventoryPart Create(string blockType, BlockInventoryBodyMount mount)
        {
            var config = SectionStackConfigTable.Resolve(blockType);
            var sectionMount = new SectionStackSectionMount(config, mount.FillsPanelHeight);
            var sections = new List<IBlockInventorySection>();
            foreach (SectionStackSlot slot in Enum.GetValues(typeof(SectionStackSlot)))
            {
                // 項目グリッドは alpha の持ち物。他の Slot は差し込まれた工場で、無い Slot は飛ばす
                // The item grid is alpha's own; other slots come from plugged factories and empty ones are skipped
                var section = slot == SectionStackSlot.ItemGrid
                    ? new SectionStackItemGridSection(config.ItemGrid, _grids, _panels)
                    : _slots.CreateOrNull(slot, sectionMount);
                if (section != null) sections.Add(section);
            }
            return new SectionStackBody(sections, mount.FillsPanelHeight);
        }
    }
}
