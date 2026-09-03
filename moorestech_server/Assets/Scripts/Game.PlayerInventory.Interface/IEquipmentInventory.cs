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
        // 選択位置が無い時に指す既定スロット。新規プレイヤーと無指定復元がここへ揃う
        // The default slot used when there is no selection; new players and unspecified restores share it
        public const int DefaultSelectedIndex = 0;

        // 常に 0..スロット数-1 の実スロットを指す
        // Always points at a real slot in 0..slotCount-1
        public int SelectedEquipmentIndex { get; }

        public void SetSelectedEquipmentIndex(int index);

        public IItemStack GetSelectedItem();
    }
}
