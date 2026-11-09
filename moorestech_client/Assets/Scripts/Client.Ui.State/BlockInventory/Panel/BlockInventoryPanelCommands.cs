using Client.Game.InGame.UI.UIState;
using Client.Ui.State.Routing;
using UnityEngine;

namespace Client.Ui.State.BlockInventory.Panel
{
    public sealed class BlockInventoryPanelCommands : IBlockInventoryPanelCommands
    {
        private readonly IUiStateTransitionRequester _requester;

        public BlockInventoryPanelCommands(IUiStateTransitionRequester requester)
        {
            _requester = requester;
        }

        public void Close()
        {
            // 許可表で拒まれた閉じるは何もしない。理由は開発者ログへ（口の側でも現状態をログに出す）
            // A close refused by the allow table does nothing; the reason goes to the developer log (the port logs the current state too)
            if (!_requester.RequestTransition(UIStateEnum.GameScreen)) Debug.LogWarning("[BlockInventoryPanelCommands] close refused by UiStateTransitionRules");
        }
    }
}
