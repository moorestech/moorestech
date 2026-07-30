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
        public const int BareHandsIndex = -1;

        // -1は素手を表す
        // -1 means bare hands
        public int SelectedEquipmentIndex { get; }

        public void SetSelectedEquipmentIndex(int index);

        public IItemStack GetSelectedItem();
    }
}
