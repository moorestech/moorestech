using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.SectionStack;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の行の区画の工場。設定が液体の行のときだけ区画を作り、他は出さない区画を返す
    /// Factory of the fluid-row section; builds it only when the setting is a fluid row, else returns a never-shown section
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
            if (mount.Config.Fluid is RowSectionStackFluid row) return new FluidRowSectionPresenter(row, new FluidRowView(row.RowId, _content), _fluids, _machine);
            return new NotApplicableSection();
        }
    }
}
