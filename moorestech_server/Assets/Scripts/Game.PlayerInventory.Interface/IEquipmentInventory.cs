using Core.Inventory;
using Core.Item.Interface;

namespace Game.PlayerInventory.Interface
{
    /// <summary>
    ///     選択中スロットが手持ちアイテムになる装備インベントリ
    ///     Equipment inventory whose selected slot becomes the held item
    /// </summary>
    public interface IEquipmentInventory : IOpenableInventory
    {
        // 常に 0..スロット数-1 の実スロットを指す
        // Always points at a real slot in 0..slotCount-1
        public int SelectedEquipmentIndex { get; }

        public void SetSelectedEquipmentIndex(int index);

        public IItemStack GetSelectedItem();
    }
}
