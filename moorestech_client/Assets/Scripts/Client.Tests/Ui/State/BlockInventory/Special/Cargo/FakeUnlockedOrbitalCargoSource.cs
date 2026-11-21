using System.Collections.Generic;
using Client.Ui.State.BlockInventory.Special.Cargo;
using Core.Master;
using UniRx;

namespace Client.Tests.Ui.State.BlockInventory.Special.Cargo
{
    // 解放済みの積荷の集合を手で差し替える偽物
    // A fake whose unlocked cargo set is swapped by hand
    public sealed class FakeUnlockedOrbitalCargoSource : IUnlockedOrbitalCargoSource
    {
        public readonly ReactiveProperty<IReadOnlyCollection<ItemId>> Unlocked = new(new HashSet<ItemId>());
        public IReadOnlyReactiveProperty<IReadOnlyCollection<ItemId>> UnlockedPayloadItemIds => Unlocked;
    }
}
