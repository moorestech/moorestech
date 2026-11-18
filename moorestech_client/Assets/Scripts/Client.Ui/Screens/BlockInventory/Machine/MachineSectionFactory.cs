using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Client.Ui.State.SlotCommands.Block;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画の工場
    /// Factory of the machine section (SectionStackSlot.Machine); commands go to the view, never the presenter
    /// </summary>
    public sealed class MachineSectionFactory : ISectionStackSectionFactory
    {
        private readonly IMachineSectionModel _machine;
        private readonly IBlockContextModel _context;
        private readonly IBlockSlotCommands _commands;
        private readonly GameContentPresentation _content;
        private readonly IMachineRecipeSelectCommand _recipeSelect;
        private readonly IUiOsCursor _osCursor;

        public SectionStackSlot Slot => SectionStackSlot.Machine;

        public MachineSectionFactory(IMachineSectionModel machine, IBlockContextModel context, IBlockSlotCommands commands, GameContentPresentation content, IMachineRecipeSelectCommand recipeSelect, IUiOsCursor osCursor)
        {
            _machine = machine;
            _context = context;
            _commands = commands;
            _content = content;
            _recipeSelect = recipeSelect;
            _osCursor = osCursor;
        }

        public IBlockInventorySection Create(SectionStackSectionMount mount)
        {
            return new MachineSectionPresenter(new MachineSectionView(mount.FillsPanelHeight, _content, _commands, _osCursor, _recipeSelect), _machine, _context);
        }
    }
}
