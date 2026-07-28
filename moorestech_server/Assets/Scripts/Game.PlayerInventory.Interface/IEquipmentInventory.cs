using Core.Inventory;
using Core.Item.Interface;

namespace Game.PlayerInventory.Interface
{
    /// <summary>
    ///     ツールだけを1枠1個で保持する装備インベントリ
    ///     Equipment inventory that holds only tools, one item per slot
    /// </summary>
    public interface IEquipmentInventory : IOpenableInventory
    {
        // -1は素手を表す
        // -1 means bare hands
        public int SelectedEquipmentIndex { get; }

        public void SetSelectedEquipmentIndex(int index);

        public IItemStack GetSelectedItem();
    }
}
