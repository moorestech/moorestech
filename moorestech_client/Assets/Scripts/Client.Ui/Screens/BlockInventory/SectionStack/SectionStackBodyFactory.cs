using System;
using System.Collections.Generic;
using Client.Ui.Screens.BlockInventory.Machine;
using Client.Ui.Screens.BlockInventory.Parts;
using UniRx;
using Client.Ui.Screens.BlockInventory.Registry;
using Client.Ui.State.BlockInventory.Panel;
using Client.Ui.State.BlockInventory.SectionStack;

namespace Client.Ui.Screens.BlockInventory.SectionStack
{
    /// <summary>
    /// ・画面IDから標準本文を構築
    /// ・区画の順序は固定
    /// Build the standard body from its screen ID.
    /// Keep sections in fixed order.
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

        public IBlockInventoryPart Create(string blockScreen, BlockInventoryBodyMount mount)
        {
            var config = SectionStackConfigTable.Resolve(blockScreen);
            var sectionMount = new SectionStackSectionMount(config, mount.FillsPanelHeight);
            // 機械欄の表示番号は同じ本文のグリッドにだけ渡す
            // Share machine slot visibility only with the grid in this body
            var machine = _slots.CreateOrNull(SectionStackSlot.Machine, sectionMount);
            var visibleMachineSlots = (machine as MachineSectionPresenter)?.VisibleItemSlots
                ?? new ReactiveProperty<IReadOnlyList<int>>(Array.Empty<int>());
            var sections = new List<IBlockInventorySection>();
            foreach (SectionStackSlot slot in Enum.GetValues(typeof(SectionStackSlot)))
            {
                // 項目グリッドは alpha の持ち物。他の Slot は差し込まれた工場で、無い Slot は飛ばす
                // The item grid is alpha's own; other slots come from plugged factories and empty ones are skipped
                var section = slot == SectionStackSlot.ItemGrid
                    ? new SectionStackItemGridSection(config.ItemGrid, _grids, _panels, visibleMachineSlots)
                    : slot == SectionStackSlot.Machine ? machine : _slots.CreateOrNull(slot, sectionMount);
                if (section != null) sections.Add(section);
            }
            return new SectionStackBody(sections, mount.FillsPanelHeight);
        }
    }
}
