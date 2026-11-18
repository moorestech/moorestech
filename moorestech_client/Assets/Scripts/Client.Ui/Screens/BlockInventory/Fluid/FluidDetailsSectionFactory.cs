using System.Collections.Generic;
using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Fluid;
using Client.Ui.State.BlockInventory.Logic.Common;
using Client.Ui.State.BlockInventory.SectionStack;

namespace Client.Ui.Screens.BlockInventory.Fluid
{
    /// <summary>
    /// 液体の詳細の区画の工場
    /// Factory of the fluid-details section; builds it only for the fluid-details setting, else a never-shown section
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
            if (mount.Config.Fluid is DetailsSectionStackFluid details) return new DetailSectionPresenter<IReadOnlyList<BlockFluidSlotState>>(new FluidDetailsSectionSource(_fluids), new FluidInventoryContents(details.RowId, _content));
            return new NotApplicableSection();
        }
    }
}
