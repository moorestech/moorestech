using System.Collections.Generic;
using Core.Item.Interface;
using Core.Master;

namespace Game.PlayerInventory.Interface
{
    /// <summary>
    ///     items.json ルートの initialEquipmentItems を装備スロット順のスタック列へ解決する（マスタは読むだけ）
    ///     Resolves items.json root initialEquipmentItems into a slot-ordered stack list (master is read only)
    /// </summary>
    public static class InitialEquipmentMasterUtil
    {
        public static List<IItemStack> CreateInitialEquipmentStacks(IItemStackFactory itemStackFactory)
        {
            var stacks = new List<IItemStack>();
            foreach (var element in MasterHolder.ItemMaster.Items.InitialEquipmentItems)
            {
                var itemId = MasterHolder.ItemMaster.GetItemId(element.ItemGuid);
                stacks.Add(itemStackFactory.Create(itemId, element.ItemCount));
            }
            return stacks;
        }
    }
}
