using System.Collections.Generic;
using System.Linq;
using Core.Block.BlockInventory;
using Core.Block.RecipeConfig.Data;
using Core.Item;
using Core.Item.Util;
using Core.Update;

namespace Core.Block.Machine.Inventory
{
    public class NormalMachineOutputInventory :IUpdate
    {
        private readonly List<IItemStack> _outputSlot;
        private readonly List<IBlockInventory> _connectInventory;
        private readonly ItemStackFactory _itemStackFactory;
        
        public List<IItemStack> OutputSlot
        {
            get
            {
                //アウトプットスロットをディープコピー
                var a = new List<IItemStack>();
                foreach (var itemStack in _outputSlot)
                {
                    a.Add(itemStack.Clone());
                }
                return a;
            }
        }
        public NormalMachineOutputInventory(IBlockInventory connect,int outputSlot,ItemStackFactory itemStackFactory)
        {
            _connectInventory = new List<IBlockInventory> {connect};
            _itemStackFactory = itemStackFactory;
            _outputSlot = CreateEmptyItemStacksList.Create(outputSlot,itemStackFactory);
            GameUpdate.AddUpdateObject(this);
        }

        /// <summary>
        /// アウトプットスロットにアイテムを入れれるかチェック
        /// </summary>
        /// <param name="machineRecipeData"></param>
        /// <returns>スロットに空きがあったらtrue</returns>
        public bool IsAllowedToOutputItem(IMachineRecipeData machineRecipeData)
        {
            foreach (var itemOutput in machineRecipeData.ItemOutputs)
            {
                var isAllowed = _outputSlot.Aggregate(false, (current, slot) => slot.IsAllowedToAdd(itemOutput.OutputItem) || current);

                if (!isAllowed) return false;
            }
            return true;
        }

        public void InsertOutputSlot(IMachineRecipeData machineRecipeData)
        {
            //アウトプットスロットにアイテムを格納する
            foreach (var output in machineRecipeData.ItemOutputs)
            {
                for (int i = 0; i < _outputSlot.Count; i++)
                {
                    if (!_outputSlot[i].IsAllowedToAdd(output.OutputItem)) continue;
                    
                    _outputSlot[i] = _outputSlot[i].AddItem(output.OutputItem).ProcessResultItemStack;
                    break;
                }
            }

        }

        void InsertConnectInventory()
        {
            for (int i = 0; i < _outputSlot.Count; i++)
            {
                foreach (var connect in _connectInventory)
                {
                    _outputSlot[i] = connect.InsertItem(_outputSlot[i]);
                }
            }
        }

        public void AddConnectInventory(IBlockInventory blockInventory)
        {
            _connectInventory.Add(blockInventory);
            //NullInventoryは削除しておく
            for (int i = _connectInventory.Count - 1; i >= 0; i--)
            {
                if (_connectInventory[i] is NullIBlockInventory)
                {
                    _connectInventory.RemoveAt(i);
                }
            }
        }

        public void RemoveConnectInventory(IBlockInventory blockInventory)
        {
            _connectInventory.Remove(blockInventory);
        }

        public void Update()
        {
            InsertConnectInventory();
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _outputSlot[slot] = itemStack;
        }
    }
}