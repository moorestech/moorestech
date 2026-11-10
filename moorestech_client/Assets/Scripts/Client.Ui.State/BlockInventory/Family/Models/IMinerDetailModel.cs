using Client.Ui.State.BlockInventory.Common;
using UniRx;

namespace Client.Ui.State.BlockInventory.Family.Models
{
    /// <summary>
    /// 追跡ブロックの採掘機の詳細（採掘機でなければ Absent）
    /// The tracked block's miner detail (Absent when it is not a miner)
    /// </summary>
    public interface IMinerDetailModel
    {
        IReadOnlyReactiveProperty<SectionContent<MinerDetail>> Current { get; }
    }
}
