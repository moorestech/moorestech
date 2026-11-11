using Client.Ui.State.Commands;
using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバー画面から呼ぶ操作
    /// Operations the hotbar screen calls
    /// </summary>
    public interface IHotbarCommands
    {
        UiCommandResult Select(int slotIndex);
        void Apply(HotbarDropAction action);
    }
}
