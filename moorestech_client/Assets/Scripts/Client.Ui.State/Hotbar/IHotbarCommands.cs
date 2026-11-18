using Client.Ui.State.Hotbar.Logic;

namespace Client.Ui.State.Hotbar
{
    /// <summary>
    /// ホットバー画面から呼ぶ操作
    /// Operations the hotbar screen calls
    /// </summary>
    public interface IHotbarCommands
    {
        // 結果は実装がトースト口へ渡す
        // The implementation hands the result to the toaster
        void Select(int slotIndex);

        // 落としは全部ここを通り結果は Resolver が決める
        // Every drop passes here; Resolver decides the outcome
        void Drop(HotbarDragSource source, HotbarDropTarget target);
    }
}
