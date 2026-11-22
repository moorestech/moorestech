using System.Collections.Generic;
using Core.Master;
using UniRx;

namespace Client.Ui.State.BlockInventory.Special.Cargo
{
    // 解放済みの積荷の集合（詳細の Model が購読して一覧を組み直す）
    // The set of unlocked cargo kinds; the detail model subscribes and rebuilds the list
    public interface IUnlockedOrbitalCargoSource
    {
        IReadOnlyReactiveProperty<IReadOnlyCollection<ItemId>> UnlockedPayloadItemIds { get; }
    }
}
