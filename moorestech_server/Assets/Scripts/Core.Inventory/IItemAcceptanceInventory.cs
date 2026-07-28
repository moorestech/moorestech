using Core.Master;

namespace Core.Inventory
{
    /// <summary>
    ///     受け入れるアイテムと1スロットあたりの上限を制限するインベントリが宣言する。
    ///     OpenableInventoryItemDataStoreServiceOption経由でインベントリ自身が制限を守り、Insert系とReplaceItemが強制する。
    ///     直接書き込みのSetItemは強制対象外なので、SetItemを使う側が事前に可否を確認する。
    ///     Declared by inventories that restrict acceptable items and the per-slot cap.
    ///     The inventory itself enforces it through OpenableInventoryItemDataStoreServiceOption on insert and replace.
    ///     SetItem writes directly and is not enforced, so its callers must check acceptance beforehand.
    /// </summary>
    public interface IItemAcceptanceInventory
    {
        // 受け入れないアイテムは1個も書き込まれず、渡したアイテムがそのまま余りとして返る
        // Unacceptable items are never written and the given stack comes back as the remainder
        bool CanAccept(ItemId itemId);

        // 1スロットに保持できる最大個数。超過分は書き込まれず余りとして返る
        // Max count a single slot can hold; the excess is returned as a remainder instead of being written
        int GetMaxCountPerSlot(ItemId itemId);
    }
}
