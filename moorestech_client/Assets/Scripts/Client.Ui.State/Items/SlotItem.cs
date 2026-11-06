using Core.Master;

namespace Client.Ui.State.Items
{
    /// <summary>
    /// スロット 1 つの中身（アイテムと個数）。Web の SlotData の写し
    /// One slot's contents (item and count); a port of the Web SlotData
    /// </summary>
    public readonly struct SlotItem
    {
        public readonly ItemId ItemId;
        public readonly int Count;

        public SlotItem(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        // 空スロットの唯一の判定。サーバーは空を（ItemMaster.EmptyItemId, 0 個）の組で送るので個数で見る
        // The single empty-slot predicate; the server sends empty as (ItemMaster.EmptyItemId, 0), so read the count
        public bool IsEmpty => Count <= 0;

        public override string ToString()
        {
            return $"{ItemId.AsPrimitive()}x{Count}";
        }
    }
}
