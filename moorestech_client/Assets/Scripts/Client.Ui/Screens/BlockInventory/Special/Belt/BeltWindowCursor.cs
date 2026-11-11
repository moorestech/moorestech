using Client.Input.ArrowKeys;
using Client.Ui.State.BlockInventory.Logic.Belt;
using Client.Ui.State.BlockInventory.Special.Details;

namespace Client.Ui.Screens.BlockInventory.Special.Belt
{
    /// <summary>
    /// 選んだ窓の tick（既定 180。←/→ で巡回し端で止まる。Web の useState と cycleBeltWindow）
    /// Selected window ticks (default 180; ←/→ cycle and stop at the ends; Web useState and cycleBeltWindow)
    /// </summary>
    public sealed class BeltWindowCursor
    {
        public int SelectedTicks { get; private set; } = BeltThroughputLogic.DefaultBeltWindowTicks;

        public void Step(BeltThroughputDetail belt, HorizontalArrowKey key)
        {
            var step = key == HorizontalArrowKey.Left ? BeltWindowStep.Previous : BeltWindowStep.Next;
            SelectedTicks = BeltThroughputLogic.CycleBeltWindow(belt.Windows, SelectedTicks, step);
        }

        // 区画がパネルから外れたら既定へ戻す（Web のコンポーネントの破棄と同じ）
        // Return to the default when the section leaves the panel (same as the Web component unmount)
        public void Reset()
        {
            SelectedTicks = BeltThroughputLogic.DefaultBeltWindowTicks;
        }
    }
}
