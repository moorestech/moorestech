using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Special.Commands;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    /// <summary>
    /// 特殊族の View が受ける依存の束（操作・表示素材・alpha の格子の工場・共通部品の指カーソルの口）。操作の結果は Commands がトーストへ渡す。購読の源は入れない（Presenter の SpecialPresenters が持つ）
    /// Dependency bundle the special views receive (commands, content, alpha's grid factory, the OS cursor port for shared controls); the commands hand results to the toaster; no subscription sources (SpecialPresenters holds them)
    /// </summary>
    public sealed class SpecialViewKit
    {
        public ISpecialBlockCommands Commands { get; }
        public GameContentPresentation Presentation { get; }
        public BlockItemGridFactory Grids { get; }
        public IUiOsCursor OsCursor { get; }

        public SpecialViewKit(ISpecialBlockCommands commands, GameContentPresentation presentation, BlockItemGridFactory grids, IUiOsCursor osCursor)
        {
            Commands = commands;
            Presentation = presentation;
            Grids = grids;
            OsCursor = osCursor;
        }
    }
}
