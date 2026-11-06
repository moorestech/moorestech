using Core.Master;

namespace Client.Ui.State.BlockInventory.Logic.Common
{
    /// <summary>
    /// ブロック在庫の 1 スロットの中身（Web の SlotData）。Task 11 で SlotItem へ畳む暫定。部品 API の ItemSlotContent とは別物
    /// One block-inventory slot's content (Web SlotData); interim until Task 11 folds it into SlotItem; distinct from the component API's ItemSlotContent
    /// </summary>
    public readonly struct BlockItemSlotState
    {
        public ItemId ItemId { get; }
        public int Count { get; }

        public BlockItemSlotState(ItemId itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        // 空 ID や 0 個は実物とみなさない（Web の hasRealItem）
        // An empty id or zero count is not a real item (Web hasRealItem)
        public bool HasRealItem => !ItemId.Equals(ItemMaster.EmptyItemId) && Count > 0;
    }
}
