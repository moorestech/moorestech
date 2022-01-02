using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Core.Block.RecipeConfig;
using Core.Block.RecipeConfig.Data;
using Core.Item;
using Core.Item.Util;

namespace Core.Block.Machine.Inventory
{
    public class VanillaMachineInputInventory
    {
        private readonly int _blockId;
        private readonly List<IItemStack> _inputSlot;
        private readonly IMachineRecipeConfig _machineRecipeConfig;
        
        public ReadOnlyCollection<IItemStack> InputSlot => new(_inputSlot);

        public VanillaMachineInputInventory(int blockId,int inputSlot,IMachineRecipeConfig machineRecipeConfig,ItemStackFactory itemStackFactory)
        {
            _blockId = blockId;
            _machineRecipeConfig = machineRecipeConfig;
            _inputSlot = CreateEmptyItemStacksList.Create(inputSlot,itemStackFactory);
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
                    if (_inputSlot[i].Id != item.Id || item.Count > _inputSlot[i].Count) continue;
                    //アイテムを減らす
                    _inputSlot[i] = _inputSlot[i].SubItem(item.Count);
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