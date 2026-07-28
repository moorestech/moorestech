using Core.Master;

namespace Core.Inventory
{
    /// <summary>
    ///     受け入れるアイテムと1スロットあたりの上限を制限するインベントリが宣言する。
    ///     OpenableInventoryItemDataStoreServiceOption経由でインベントリ自身が制限を守り、Insert系とReplaceItemが強制する。
    ///     直接書き込みのSetItemとSetItemWithoutEventは強制対象外なので、呼ぶ側が事前に可否を確認する。
    ///     セーブロード復元はSetItemWithoutEventを通るため、復元側で違反アイテムの行き先を明示的に決めること。
    ///     Declared by inventories that restrict acceptable items and the per-slot cap.
    ///     The inventory itself enforces it through OpenableInventoryItemDataStoreServiceOption on insert and replace.
    ///     SetItem and SetItemWithoutEvent write directly and are not enforced, so their callers must check acceptance first.
    ///     Save loading goes through SetItemWithoutEvent, so the restore path must decide explicitly where violating items go.
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
