using Client.Ui.State.Commands;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバーの画面から呼ぶ操作（Web の hotbar.select・assign・swap・clear）
    /// Operations the hotbar screen calls (Web hotbar.select, assign, swap, clear)
    /// </summary>
    public interface IHotbarCommands
    {
        UiCommandResult Select(int slotIndex);
        void Apply(HotbarDropAction action);
    }
}
