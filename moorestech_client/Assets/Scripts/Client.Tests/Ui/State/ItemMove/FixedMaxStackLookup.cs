using System;
using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;
using UniRx;

namespace Client.Tests.Ui.State.ItemMove
{
    // どのアイテムにも同じスタック上限を返す。Web テストの maxStack 固定値の写し
    // Returns the same stack limit for every item; mirrors the fixed maxStack in the Web tests
    public sealed class FixedMaxStackLookup : IItemStackLevelLookup
    {
        private readonly int _maxStack;

        public FixedMaxStackLookup(int maxStack)
        {
            _maxStack = maxStack;
        }

        public IReadOnlyDictionary<Guid, int> UnlockedLevels => new Dictionary<Guid, int>();
        public IObservable<(Guid itemGuid, int level)> OnStackLevelUnlocked => Observable.Never<(Guid itemGuid, int level)>();
        public int GetMaxStack(ItemId itemId) => _maxStack;
        public int GetUnlockedLevel(Guid itemGuid) => 1;
    }
}
