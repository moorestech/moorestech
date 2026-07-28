using Core.Master;

namespace Core.Inventory
{
    // 受け入れ可能なアイテムを制限したいインベントリが宣言する。移動サービスがこれを尊重する
    // Declared by inventories that restrict acceptable items; move services honor it
    public interface IItemAcceptanceInventory
    {
        bool CanAccept(ItemId itemId);
        int GetMaxCountPerSlot(ItemId itemId);
    }
}
