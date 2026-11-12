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

        // 落としはすべてここを通り、何が起きるかは HotbarDropResolver が決める（印や源は判断しない）
        // Every drop goes through here and HotbarDropResolver decides the outcome (marks and sources never decide)
        void Drop(HotbarDragSource source, HotbarDropTarget target);
    }
}
