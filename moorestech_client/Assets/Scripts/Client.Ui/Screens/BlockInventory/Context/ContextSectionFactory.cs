using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 文脈の区画の工場
    /// Factory of the context section (SectionStackSlot.Context); builds it for any blockType, which decides whether to show
    /// </summary>
    public sealed class ContextSectionFactory : ISectionStackSectionFactory
    {
        private readonly GameContentPresentation _content;
        private readonly IBlockContextModel _context;
        private readonly IMachineSectionModel _machine;
        private readonly IUiOsCursor _osCursor;
        private readonly ICleanRoomYieldImprovementCommands _improvementCommands;

        public SectionStackSlot Slot => SectionStackSlot.Context;

        public ContextSectionFactory(GameContentPresentation content, IBlockContextModel context, IMachineSectionModel machine, IUiOsCursor osCursor, ICleanRoomYieldImprovementCommands improvementCommands)
        {
            _content = content;
            _context = context;
            _machine = machine;
            _osCursor = osCursor;
            _improvementCommands = improvementCommands;
        }

        public IBlockInventorySection Create(SectionStackSectionMount mount)
        {
            return new ContextSectionPresenter(new BlockContextSectionsView(_content, _osCursor, _improvementCommands), _context, _machine);
        }
    }
}
