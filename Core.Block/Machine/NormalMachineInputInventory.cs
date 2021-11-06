using System.Collections.Generic;
using System.Linq;
using Core.Config.Installation;
using Core.Config.Recipe;
using Core.Config.Recipe.Data;
using Core.Item;
using Core.Util;

namespace Core.Block.Machine
{
    public class NormalMachineInputInventory
    {
        private readonly int _blockId;
        private readonly List<IItemStack> _inputSlot;
        public List<IItemStack> InputSlot 
        {
            get
            {
                var a = _inputSlot.Where(i => i.Id != BlockConst.NullBlockId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }

        public NormalMachineInputInventory(int BlockId)
        {
            _blockId = BlockId;
            var data = BlockConfig.GetBlocksConfig(BlockId);
            _inputSlot = CreateEmptyItemStacksList.Create(data.InputSlot);
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (var i = 0; i < _inputSlot.Count; i++)
            {
                if (!_inputSlot[i].IsAllowedToAdd(itemStack)) continue;
                
                //インベントリにアイテムを入れる
                var r = _inputSlot[i].AddItem(itemStack);
                _inputSlot[i] = r.MineItemStack;
                
                //とった結果のアイテムを返す
                return r.ReceiveItemStack;
            }
            return itemStack;
        }
        
        public bool IsAllowedToStartProcess
        {
            get
            {
                //建物IDと現在のインプットスロットからレシピを検索する
                var recipe = MachineRecipeConfig.GetRecipeData(_blockId, _inputSlot.ToList());
                //実行できるレシピかどうか
                return recipe.RecipeConfirmation(_inputSlot);
            }
        }

        public IMachineRecipeData GetRecipeData()
        {
            return MachineRecipeConfig.GetRecipeData(_blockId, _inputSlot.ToList());
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
    }
}