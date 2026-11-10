using UniRx;

namespace Client.Ui.State.BlockInventory.Fluid
{
    /// <summary>
    /// 追跡ブロック（タンク・液体プラットフォーム・パイプ）の液体。閉・貨車では空の列・進捗なし
    /// 約束: 追跡の 2 つの通知（追跡替え・状態変化）のたびに新しい VM を配る（同値でも参照を変えて流す）
    /// Fluids of the tracked block (tank, fluid platform, pipe); empty list and no progress when closed or on a freight car
    /// Contract: a new view model is published on each of the tracker's two notifications (a new reference even when equal)
    /// </summary>
    public interface IBlockFluidSlotsModel
    {
        IReadOnlyReactiveProperty<BlockFluidSlotsViewModel> Current { get; }
    }
}
