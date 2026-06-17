using System.Collections.Generic;

namespace Core.Inventory
{
    public class InventorySlotList
    {
        public IReadOnlyList<int> Slots => _slots;
        public int Count => _slots.Count;

        private readonly List<int> _slots = new();
        private readonly Dictionary<int, int> _positionBySlot = new();

        public void Add(int slot)
        {
            if (_positionBySlot.ContainsKey(slot)) return;

            // 末尾追加で順序管理コストを一定に抑える
            // Append to keep index maintenance constant time
            _positionBySlot[slot] = _slots.Count;
            _slots.Add(slot);
        }

        public void Remove(int slot)
        {
            if (!_positionBySlot.TryGetValue(slot, out var position)) return;

            // 末尾要素と入れ替えて削除を一定時間にする
            // Swap with the tail so removal stays constant time
            var lastIndex = _slots.Count - 1;
            var lastSlot = _slots[lastIndex];
            _slots[position] = lastSlot;
            _positionBySlot[lastSlot] = position;
            _slots.RemoveAt(lastIndex);
            _positionBySlot.Remove(slot);
        }

        public int GetLast()
        {
            return _slots[^1];
        }

        public void Clear()
        {
            _slots.Clear();
            _positionBySlot.Clear();
        }
    }
}
