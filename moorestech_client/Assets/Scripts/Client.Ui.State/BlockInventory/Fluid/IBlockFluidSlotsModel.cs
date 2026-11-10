using UniRx;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 追跡ブロックの液体。閉・貨車では空の列
    /// 約束: 通知のたび新しい VM を配る
    /// Fluids of the tracked block (tank, fluid platform, pipe); empty list and no progress when closed or on a freight car
    /// Contract: a new view model is published on each of the tracker's two notifications (a new reference even when equal)
    /// </summary>
    public interface IBlockFluidSlotsModel
    {
        IReadOnlyReactiveProperty<BlockFluidSlotsViewModel> Current { get; }
    }
}
