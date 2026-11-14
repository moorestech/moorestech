using Client.Ui.Content;
using Client.Ui.Foundation.Input.OsCursor;
using Client.Ui.Screens.BlockInventory.Parts;
using Client.Ui.State.BlockInventory.Special.Commands;
using Client.Ui.State.Commands;
using Client.Ui.State.Toast;
using Cysharp.Threading.Tasks;

namespace Client.Ui.Screens.BlockInventory.Special.Registration
{
    /// <summary>
    /// 特殊族の View が受ける依存の束（操作・表示素材・alpha の格子の工場・共通部品の指カーソルの口）と、操作の結果の届け先。購読の源は入れない（Presenter の SpecialPresenters が持つ）
    /// Dependency bundle the special views receive (commands, content, alpha's grid factory, the OS cursor port for shared controls) and where command results go; no subscription sources (SpecialPresenters holds them)
    /// </summary>
    public sealed class SpecialViewKit
    {
        private readonly UiCommandResultToaster _toaster;

        public ISpecialBlockCommands Commands { get; }
        public GameContentPresentation Presentation { get; }
        public BlockItemGridFactory Grids { get; }
        public IUiOsCursor OsCursor { get; }

        public SpecialViewKit(ISpecialBlockCommands commands, GameContentPresentation presentation, UiCommandResultToaster toaster, BlockItemGridFactory grids, IUiOsCursor osCursor)
        {
            Commands = commands;
            Presentation = presentation;
            _toaster = toaster;
            Grids = grids;
            OsCursor = osCursor;
        }


        // 操作の結果を待ってトーストの係へ渡す（拒否は警告ログ、良性でなければトースト。楽観更新はしない。D-5）
        // Await the result and hand it to the toaster (rejects log a warning, non-benign ones toast; no optimistic update. D-5)
        public void Run(string commandName, UniTask<UiCommandResult> command)
        {
            command.ContinueWith(result => _toaster.Report(commandName, result)).Forget();
        }
    }
}
