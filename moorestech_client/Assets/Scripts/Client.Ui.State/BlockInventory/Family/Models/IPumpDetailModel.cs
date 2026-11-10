using Client.Ui.State.BlockInventory.Common;
using UniRx;

namespace Client.Ui.State.BlockInventory.Family.Models
{
    /// <summary>
    /// 追跡ブロックのポンプの詳細（ポンプでなければ Absent）
    /// The tracked block's pump detail (Absent when it is not a pump)
    /// </summary>
    public interface IPumpDetailModel
    {
        IReadOnlyReactiveProperty<SectionContent<PumpDetail>> Current { get; }
    }
}
