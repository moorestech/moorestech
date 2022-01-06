using Core.Block.Blocks.Machine.Inventory;
using Core.Item;
using Core.Item.Util;

namespace Core.Block.Blocks.Machine.InventoryController
{
    public class VanillaMachineInventory
    {
        private readonly VanillaMachineInputInventory _vanillaMachineInputInventory;
        private readonly VanillaMachineOutputInventory _vanillaMachineOutputInventory;

        public VanillaMachineInventory(VanillaMachineInputInventory vanillaMachineInputInventory,
            VanillaMachineOutputInventory vanillaMachineOutputInventory)
        {
            _vanillaMachineInputInventory = vanillaMachineInputInventory;
            _vanillaMachineOutputInventory = vanillaMachineOutputInventory;
        }

        public IItemStack GetItem(int slot)
        {
            if (slot < _vanillaMachineInputInventory.InputSlot.Count)
            {
                return _vanillaMachineInputInventory.InputSlot[slot];
            }

            slot -= _vanillaMachineInputInventory.InputSlot.Count;
            return _vanillaMachineOutputInventory.OutputSlot[slot];
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            if (slot < _vanillaMachineInputInventory.InputSlot.Count)
            {
                _vanillaMachineInputInventory.SetItem(slot, itemStack);
            }
            else
            {
                slot -= _vanillaMachineInputInventory.InputSlot.Count;
                _vanillaMachineOutputInventory.SetItem(slot, itemStack);
            }
        }

        /// <summary>
        /// アイテムの置き換えを実行しますが、同じアイテムIDの場合はそのまま現在のアイテムにスタックされ、スタックしきらなかったらその分を返します。
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="itemStack"></param>
        /// <returns></returns>
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            ItemProcessResult result;
            if (slot < _vanillaMachineInputInventory.InputSlot.Count)
            {
                //アイテムIDが同じの時はスタックして余ったものを返す
                var item = _vanillaMachineInputInventory.InputSlot[slot];
                if (item.Id == itemStack.Id)
                {
                    result = item.AddItem(itemStack);
                    _vanillaMachineInputInventory.SetItem(slot, result.ProcessResultItemStack);
                    return result.RemainderItemStack;
                }

                //違う場合はそのまま入れ替える
                _vanillaMachineInputInventory.SetItem(slot, itemStack);
                return item;
            }
            else
            {
                //アウトプットスロットのインデックスに変換する
                slot -= _vanillaMachineInputInventory.InputSlot.Count;

                var item = _vanillaMachineOutputInventory.OutputSlot[slot];

                if (item.Id == itemStack.Id)
                {
                    result = item.AddItem(itemStack);
                    _vanillaMachineOutputInventory.SetItem(slot, result.ProcessResultItemStack);
                    return result.RemainderItemStack;
                }

                _vanillaMachineOutputInventory.SetItem(slot, itemStack);
                return item;
            }
        }
    }
}