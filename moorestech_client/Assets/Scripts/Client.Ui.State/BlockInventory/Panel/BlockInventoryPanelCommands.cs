using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Commands;
using Client.Ui.State.Routing;

namespace Client.Ui.State.BlockInventory.Panel
{
    public sealed class BlockInventoryPanelCommands : IBlockInventoryPanelCommands
    {
        private readonly IUiStateTransitionRequester _requester;

        public BlockInventoryPanelCommands(IUiStateTransitionRequester requester)
        {
            _requester = requester;
        }

        public UiCommandResult Close()
        {
            // 許可表に拒まれた閉じるはビルドメニューと同じ非良性の拒否で返し、View が Report でトーストとログへ出す
            // A close refused by the allow table returns the same non-benign refusal as the build menu; the view reports it to the toast and the log
            if (!_requester.RequestTransition(UIStateEnum.GameScreen)) return UiCommandRejected.Of(UiCommandRejectReason.ScreenCloseRefused);
            return UiCommandAccepted.Instance;
        }
    }
}
