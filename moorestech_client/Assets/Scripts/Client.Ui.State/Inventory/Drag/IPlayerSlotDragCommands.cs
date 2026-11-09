using Client.Ui.State.Commands;
using Client.Ui.State.Inventory.Actions;

namespace Client.Ui.State.Inventory.Drag
{
    /// <summary>
    /// ボタンを押したままの進入と分割ドラッグの開始（Web slotActions の onLeftEnter・onRightEnter と splitDrag.begin）。確定は左ボタンの解放で中が行う
    /// Enters with a button held and split-drag begin (Web slotActions onLeftEnter/onRightEnter and splitDrag.begin); the commit runs inside on left release
    /// </summary>
    public interface IPlayerSlotDragCommands
    {
        void BeginSplitDrag(PlayerSlotRef slot);
        void EnterWithLeftButton(PlayerSlotRef slot);
        UiCommandResult EnterWithRightButton(PlayerSlotRef slot);
    }
}
