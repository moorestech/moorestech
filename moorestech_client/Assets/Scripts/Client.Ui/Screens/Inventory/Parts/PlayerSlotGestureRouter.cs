using Client.Ui.Components.Slots;
using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.SlotCommands.Player;
using Client.Ui.State.Toast;

namespace Client.Ui.Screens.Inventory.Parts
{
    /// <summary>
    /// プレイヤーの枠の身振りを型付きコマンドへ振り分け、結果をトーストへ渡す（持ち物と装備で共有）
    /// Routes player-slot gestures to typed commands and reports results (shared by inventory and equipment)
    /// </summary>
    public sealed class PlayerSlotGestureRouter
    {
        private readonly IPlayerSlotCommands _commands;
        private readonly UiCommandResultToaster _toaster;

        public PlayerSlotGestureRouter(IPlayerSlotCommands commands, UiCommandResultToaster toaster)
        {
            _commands = commands;
            _toaster = toaster;
        }

        public void Route(PlayerSlotRef slot, SlotGesture gesture)
        {
            switch (gesture.Kind)
            {
                case SlotGestureKind.LeftDown: Report(_commands.LeftDown(slot, gesture.Shift)); return;
                case SlotGestureKind.RightDown: Report(_commands.RightDown(slot)); return;
                case SlotGestureKind.DoubleClick: Report(_commands.DoubleClick(slot)); return;
                case SlotGestureKind.LeftEnter: _commands.LeftEnter(slot); return;
                case SlotGestureKind.RightEnter: Report(_commands.RightEnter(slot)); return;
                // silent-ok: ホバーはツールチップの宿主（C3-b）が ItemSlot の印から出す / InventoryPanelTooltipTest が見る
                default: return;
            }

            #region Internal

            void Report(UiCommandResult result)
            {
                _toaster.Report(PlayerSlotCommands.CommandName, result);
            }

            #endregion
        }
    }
}
