using System.Collections.Generic;
using System.Collections.ObjectModel;
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


        public ReadOnlyCollection<IItemStack> Items
        {
            get
            {
                var items = new List<IItemStack>();
                items.AddRange(_vanillaMachineInputInventory.InputSlot);
                items.AddRange(_vanillaMachineOutputInventory.OutputSlot);
                return new ReadOnlyCollection<IItemStack>(items);
            }
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

        public int GetSlotSize()
        {
            return _vanillaMachineInputInventory.InputSlot.Count + _vanillaMachineOutputInventory.OutputSlot.Count;
        }

        /// <summary>
        /// ?????????????????????????????????????????????????????????????????????ID????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????????
        /// </summary>
        /// <param name="slot"></param>
        /// <param name="itemStack"></param>
        /// <returns></returns>
        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            ItemProcessResult result;
            if (slot < _vanillaMachineInputInventory.InputSlot.Count)
            {
                //????????????ID????????????????????????????????????????????????????????????
                var item = _vanillaMachineInputInventory.InputSlot[slot];
                if (item.Id == itemStack.Id)
                {
                    result = item.AddItem(itemStack);
                    _vanillaMachineInputInventory.SetItem(slot, result.ProcessResultItemStack);
                    return result.RemainderItemStack;
                }

                //??????????????????????????????????????????
                _vanillaMachineInputInventory.SetItem(slot, itemStack);
                return item;
            }
            else
            {
                //??????????????????????????????????????????????????????????????????
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