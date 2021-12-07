using System.Collections.Generic;
using System.Linq;
using Core.Block.Config;
using Core.Block.RecipeConfig;
using Core.Block.RecipeConfig.Data;
using Core.Item;
using Core.Item.Util;

namespace Core.Block.Machine
{
    public class NormalMachineInputInventory
    {
        private readonly int _blockId;
        private readonly List<IItemStack> _inputSlot;
        private readonly IMachineRecipeConfig _machineRecipeConfig;
        public List<IItemStack> InputSlotWithoutNullItemStack 
        {
            get
            {
                var a = _inputSlot.Where(i => i.Id != ItemConst.NullItemId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }
        
        public List<IItemStack> InputSlot
        {
            get
            {
                //アウトプットスロットをディープコピー
                var a = new List<IItemStack>();
                foreach (var itemStack in _inputSlot)
                {
                    a.Add(itemStack.Clone());
                }
                return a;
            }
        }

        public NormalMachineInputInventory(int BlockId,IBlockConfig blockConfig,IMachineRecipeConfig machineRecipeConfig,ItemStackFactory itemStackFactory)
        {
            _blockId = BlockId;
            _machineRecipeConfig = machineRecipeConfig;
            var data = blockConfig.GetBlocksConfig(BlockId);
            _inputSlot = CreateEmptyItemStacksList.Create(data.InputSlot,itemStackFactory);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (var i = 0; i < _inputSlot.Count; i++)
            {
                if (!_inputSlot[i].IsAllowedToAdd(itemStack)) continue;
                
                //インベントリにアイテムを入れる
                var r = _inputSlot[i].AddItem(itemStack);
                _inputSlot[i] = r.ProcessResultItemStack;
                
                //とった結果のアイテムを返す
                return r.RemainderItemStack;
            }
            return itemStack;
        }
        
        public bool IsAllowedToStartProcess
        {
            get
            {
                //建物IDと現在のインプットスロットからレシピを検索する
                var recipe = _machineRecipeConfig.GetRecipeData(_blockId, _inputSlot.ToList());
                //実行できるレシピかどうか
                return recipe.RecipeConfirmation(_inputSlot);
            }
        }

        public IMachineRecipeData GetRecipeData()
        {
            return _machineRecipeConfig.GetRecipeData(_blockId, _inputSlot.ToList());
        }

        public void ReduceInputSlot(IMachineRecipeData recipe)
        {
            
            //inputスロットからアイテムを減らす
            foreach (var item in recipe.ItemInputs)
            {
                for (var i = 0; i < _inputSlot.Count; i++)
                {
                    if (_inputSlot[i].Id != item.Id || item.Amount > _inputSlot[i].Amount) continue;
                    //アイテムを減らす
                    _inputSlot[i] = _inputSlot[i].SubItem(item.Amount);
                    break;
                }
            }
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _inputSlot[slot] = itemStack;
        }
    }
}