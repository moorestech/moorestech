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
        private readonly HashSet<ItemId> _unknownItems = new();

        public FixedMaxStackLookup(int maxStack)
        {
            _maxStack = maxStack;
        }

        public IReadOnlyDictionary<Guid, int> UnlockedLevels => new Dictionary<Guid, int>();
        public IObservable<(Guid itemGuid, int level)> OnStackLevelUnlocked => Observable.Never<(Guid itemGuid, int level)>();
        public bool ExistsItem(ItemId itemId) => !_unknownItems.Contains(itemId);
        public int GetMaxStack(ItemId itemId) => _unknownItems.Contains(itemId) ? throw new InvalidOperationException($"unknown ItemId {itemId}") : _maxStack;

        // マスタに無い ID の扱いを試すため、その ID を未知にする（本物は GetMaxStack が例外になる）
        // Marks an ID unknown to exercise IDs absent from the master (the real lookup throws in GetMaxStack)
        public void SetUnknown(ItemId itemId)
        {
            _unknownItems.Add(itemId);
        }
        public int GetUnlockedLevel(Guid itemGuid) => 1;
    }
}
