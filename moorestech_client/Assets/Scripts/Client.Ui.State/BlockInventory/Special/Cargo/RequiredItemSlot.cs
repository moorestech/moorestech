using Core.Master;

namespace Client.Ui.State.BlockInventory.Special.Cargo
{
    /// <summary>
    /// 1 枠に入るべき品と必要な数（枠はサーバーの絶対添字）
    /// The item that belongs in one slot and its required count (the slot is a server absolute index)
    /// </summary>
    public readonly struct RequiredItemSlot
    {
        public readonly int Slot;
        public readonly ItemId ItemId;
        public readonly int Count;

        public RequiredItemSlot(int slot, ItemId itemId, int count)
        {
            Slot = slot;
            ItemId = itemId;
            Count = count;
        }
    }
}
