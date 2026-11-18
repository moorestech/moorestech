using Client.Ui.Components.Slots;
using Client.Ui.State.Inventory.Actions;
using Client.Ui.State.SlotCommands.Player;

namespace Client.Ui.Screens.Inventory.Parts
{
    /// <summary>
    /// 枠の身振りをコマンドへ振り分ける（結果のトーストはコマンド側）
    /// Routes slot gestures to commands (the commands report results to toast)
    /// </summary>
    public sealed class PlayerSlotGestureRouter
    {
        private readonly IPlayerSlotCommands _commands;

        public PlayerSlotGestureRouter(IPlayerSlotCommands commands)
        {
            _commands = commands;
        }

        public void Route(PlayerSlotRef slot, SlotGesture gesture)
        {
            switch (gesture.Kind)
            {
                case SlotGestureKind.LeftDown: _commands.LeftDown(slot, gesture.Shift); return;
                case SlotGestureKind.RightDown: _commands.RightDown(slot); return;
                case SlotGestureKind.DoubleClick: _commands.DoubleClick(slot); return;
                case SlotGestureKind.LeftEnter: _commands.LeftEnter(slot); return;
                case SlotGestureKind.RightEnter: _commands.RightEnter(slot); return;
                // silent-ok: ホバーはツールチップの宿主（C3-b）が ItemSlot の印から出す / InventoryPanelTooltipTest が見る
                default: return;
            }
        }
    }
}
