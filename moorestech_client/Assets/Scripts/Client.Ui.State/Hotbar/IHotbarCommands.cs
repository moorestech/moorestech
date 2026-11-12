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

        // 落としは全部ここを通り結果は Resolver が決める
        // Every drop passes here; Resolver decides the outcome
        void Drop(HotbarDragSource source, HotbarDropTarget target);
    }
}
