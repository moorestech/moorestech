using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Commands;
using Client.Ui.State.Routing;
using Client.Ui.State.Toast;

namespace Client.Ui.State.BlockInventory.Panel
{
    public sealed class BlockInventoryPanelCommands : IBlockInventoryPanelCommands
    {
        private readonly IUiStateTransitionRequester _requester;
        private readonly IUiCommandResultToaster _toaster;

        public BlockInventoryPanelCommands(IUiStateTransitionRequester requester, IUiCommandResultToaster toaster)
        {
            _requester = requester;
            _toaster = toaster;
        }

        public void Close()
        {
            // 許可表に拒まれた閉じるはビルドメニューと同じ非良性の拒否としてトーストとログへ出す
            // A close refused by the allow table goes to the toast and the log as the same non-benign refusal as the build menu
            UiCommandResult result = _requester.RequestTransition(UIStateEnum.GameScreen) ? UiCommandAccepted.Instance : UiCommandRejected.Of(UiCommandRejectReason.ScreenCloseRefused);
            _toaster.Report(UiStateTransitionRules.RequestCommandName, result);
        }
    }
}
