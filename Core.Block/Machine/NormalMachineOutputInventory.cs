using System.Collections.Generic;
using System.Linq;
using Core.Block.Config;
using Core.Block.RecipeConfig.Data;
using Core.Item;
using Core.Item.Util;
using Core.Update;

namespace Core.Block.Machine
{
    public class NormalMachineOutputInventory :IUpdate
    {
        private readonly List<IItemStack> _outputSlot;
        private IBlockInventory _connectInventory;
        public List<IItemStack> OutputSlotWithoutNullItemStack 
        {
            get
            {
                var a = _outputSlot.Where(i => i.Id != ItemConst.NullItemId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }
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

        public NormalMachineOutputInventory(int blockId, IBlockInventory connect)
        {
            _connectInventory = connect;
            var data = BlockConfig.GetBlocksConfig(blockId);
            _outputSlot = CreateEmptyItemStacksList.Create(data.OutputSlot);
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
                _outputSlot[i] = _connectInventory.InsertItem(_outputSlot[i]);
            }
        }

        public void ChangeConnectInventory(IBlockInventory blockInventory)
        {
            _connectInventory = blockInventory;
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