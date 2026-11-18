using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.SectionStack;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の行の区画の工場
    /// Factory of the fluid-row section; builds it only for the fluid-row setting, else a never-shown section
    /// </summary>
    public sealed class FluidRowSectionFactory : ISectionStackSectionFactory
    {
        private readonly GameContentPresentation _content;
        private readonly IBlockFluidSlotsModel _fluids;
        private readonly IMachineSectionModel _machine;

        public SectionStackSlot Slot => SectionStackSlot.FluidRow;

        public FluidRowSectionFactory(GameContentPresentation content, IBlockFluidSlotsModel fluids, IMachineSectionModel machine)
        {
            _content = content;
            _fluids = fluids;
            _machine = machine;
        }

        public IBlockInventorySection Create(SectionStackSectionMount mount)
        {
            if (mount.Config.Fluid is RowSectionStackFluid row) return new DetailSectionPresenter<BlockFluidSlotsViewModel>(new FluidRowSectionSource(_fluids, _machine), new FluidRowView(row.RowId, row.ShowProgress, _content));
            return new NotApplicableSection();
        }
    }
}
