using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine;

namespace Client.Ui.Screens.BlockInventory.Context
{
    /// <summary>
    /// 文脈の区画の工場（SectionStackSlot.Context）。どの blockType でも同じ区画を作り、出すかは区画が決める
    /// Factory of the context section (SectionStackSlot.Context); builds the same section for any blockType, which decides on its own whether to show
    /// </summary>
    public sealed class ContextSectionFactory : ISectionStackSectionFactory
    {
        private readonly GameContentPresentation _content;
        private readonly IBlockContextModel _context;
        private readonly IMachineSectionModel _machine;

        public SectionStackSlot Slot => SectionStackSlot.Context;

        public ContextSectionFactory(GameContentPresentation content, IBlockContextModel context, IMachineSectionModel machine)
        {
            _content = content;
            _context = context;
            _machine = machine;
        }

        public IBlockInventorySection Create(SectionStackSectionMount mount)
        {
            return new ContextSectionPresenter(new BlockContextSectionsView(_content), _context, _machine);
        }
    }
}
