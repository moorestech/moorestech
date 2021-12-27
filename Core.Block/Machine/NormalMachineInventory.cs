using Core.Item;
using Core.Item.Util;

namespace Core.Block.Machine
{
    public class NormalMachineInventory
    {
        private readonly NormalMachineInputInventory _normalMachineInputInventory;
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;

        public NormalMachineInventory(NormalMachineInputInventory normalMachineInputInventory, NormalMachineOutputInventory normalMachineOutputInventory)
        {
            _normalMachineInputInventory = normalMachineInputInventory;
            _normalMachineOutputInventory = normalMachineOutputInventory;
        }

        public IItemStack GetItem(int slot)
        {
            if (slot < _normalMachineInputInventory.InputSlot.Count)
            {
                return _normalMachineInputInventory.InputSlot[slot];
            }
            slot -= _normalMachineInputInventory.InputSlot.Count;
            return _normalMachineOutputInventory.OutputSlot[slot];
            
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            if (slot < _normalMachineInputInventory.InputSlot.Count)
            {
                _normalMachineInputInventory.SetItem(slot,itemStack);
            }
            else
            {
                slot -= _normalMachineInputInventory.InputSlot.Count;
                _normalMachineOutputInventory.SetItem(slot, itemStack);
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
            if (slot < _normalMachineInputInventory.InputSlot.Count)
            {
                //アイテムIDが同じの時はスタックして余ったものを返す
                var item = _normalMachineInputInventory.InputSlot[slot];
                if (item.Id == itemStack.Id)
                {
                    result = item.AddItem(itemStack);
                    _normalMachineInputInventory.SetItem(slot, result.ProcessResultItemStack);
                    return result.RemainderItemStack;
                }

                //違う場合はそのまま入れ替える
                _normalMachineInputInventory.SetItem(slot, itemStack);
                return item;
            }
            else
            {
                //アウトプットスロットのインデックスに変換する
                slot -= _normalMachineInputInventory.InputSlot.Count;

                var item = _normalMachineOutputInventory.OutputSlot[slot];

                if (item.Id == itemStack.Id)
                {
                    result = item.AddItem(itemStack);
                    _normalMachineOutputInventory.SetItem(slot, result.ProcessResultItemStack);
                    return result.RemainderItemStack;
                }
                _normalMachineOutputInventory.SetItem(slot, itemStack);
                return item;
            }
        }
    }
}