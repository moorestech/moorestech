using Client.Ui.State.BlockInventory.Panel;
using UniRx;

namespace Client.Tests.Ui.State.BlockInventory.PanelModel
{
    /// <summary>
    /// パネルの状態の偽の源（Task 5・Screens の L1 でも使う）
    /// Fake panel-state source (also used by the Task 5 and Screens L1 tests)
    /// </summary>
    public sealed class FakePanelSource : IBlockInventoryPanelSource
    {
        private readonly ReactiveProperty<BlockInventoryPanelState> _state;

        public FakePanelSource(ReactiveProperty<BlockInventoryPanelState> state)
        {
            _state = state;
        }

        public IReadOnlyReactiveProperty<BlockInventoryPanelState> Current => _state;
    }
}
