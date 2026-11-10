using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.SectionStack;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の詳細の区画の工場。設定が液体の詳細のときだけ区画を作り、他は出さない区画を返す
    /// Factory of the fluid-details section; builds it only when the setting is fluid details, else returns a never-shown section
    /// </summary>
    public sealed class FluidDetailsSectionFactory : ISectionStackSectionFactory
    {
        private readonly GameContentPresentation _content;
        private readonly IBlockFluidSlotsModel _fluids;

        public SectionStackSlot Slot => SectionStackSlot.FluidDetails;

        public FluidDetailsSectionFactory(GameContentPresentation content, IBlockFluidSlotsModel fluids)
        {
            _content = content;
            _fluids = fluids;
        }

        public IBlockInventorySection Create(SectionStackSectionMount mount)
        {
            if (mount.Config.Fluid is DetailsSectionStackFluid details) return new FluidDetailsSectionPresenter(new FluidInventoryContents(details.RowId, _content), _fluids);
            return new NotApplicableSection();
        }
    }
}
