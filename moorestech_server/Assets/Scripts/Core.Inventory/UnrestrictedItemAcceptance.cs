using Core.Master;

namespace Core.Inventory
{
    /// <summary>
    ///     受入制限を持たないインベントリが使う既定の判定。すべてのアイテムを無制限に受け入れる。
    ///     Default acceptance used by inventories without restrictions; accepts every item without a cap.
    /// </summary>
    public class UnrestrictedItemAcceptance : IItemAcceptanceInventory
    {
        public static readonly UnrestrictedItemAcceptance Instance = new();

        private UnrestrictedItemAcceptance()
        {
        }

        public bool CanAccept(ItemId itemId)
        {
            return true;
        }

        public int GetMaxCountPerSlot(ItemId itemId)
        {
            // アイテム自身のスタック上限だけが効くようにする
            // Let only the item's own stack limit take effect
            return int.MaxValue;
        }
    }
}
