using Client.Ui.Content;
using Client.Ui.Screens.BlockInventory.SectionStack;
using Client.Ui.State.BlockInventory.Context;
using Client.Ui.State.BlockInventory.Machine;
using Client.Ui.State.BlockInventory.Machine.Selection;
using Client.Ui.State.SlotCommands.Block;
using Client.Ui.State.Toast;

namespace Client.Ui.Screens.BlockInventory.Machine
{
    /// <summary>
    /// 機械の区画の工場（SectionStackSlot.Machine）。コマンドとトースターは View へ渡し、Presenter は持たない
    /// Factory of the machine section (SectionStackSlot.Machine); commands and the toaster go to the view, never the presenter
    /// </summary>
    public sealed class MachineSectionFactory : ISectionStackSectionFactory
    {
        private readonly IMachineSectionModel _machine;
        private readonly IBlockContextModel _context;
        private readonly IBlockSlotCommands _commands;
        private readonly UiCommandResultToaster _toaster;
        private readonly GameContentPresentation _content;
        private readonly IMachineRecipeSelectCommand _recipeSelect;

        public SectionStackSlot Slot => SectionStackSlot.Machine;

        public MachineSectionFactory(IMachineSectionModel machine, IBlockContextModel context, IBlockSlotCommands commands, UiCommandResultToaster toaster, GameContentPresentation content, IMachineRecipeSelectCommand recipeSelect)
        {
            _machine = machine;
            _context = context;
            _commands = commands;
            _toaster = toaster;
            _content = content;
            // レシピを選ぶコマンドは Task 12 の選択リストが View で使う（D-33）
            // The recipe-select command is used by the Task 12 selection list in the view (D-33)
            _recipeSelect = recipeSelect;
        }

        public IBlockInventorySection Create(SectionStackSectionMount mount)
        {
            return new MachineSectionPresenter(new MachineSectionView(mount.FillsPanelHeight, _content, _commands, _toaster), _machine, _context);
        }
    }
}
