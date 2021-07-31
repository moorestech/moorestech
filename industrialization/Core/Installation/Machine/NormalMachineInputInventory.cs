using System;
using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Config.Installation;
using industrialization.Core.Config.Recipe;
using industrialization.Core.Config.Recipe.Data;
using industrialization.Core.Item;
using industrialization.Core.Util;

namespace industrialization.Core.Installation.Machine
{
    public class NormalMachineInputInventory
    {
        private int _installationId;
        private List<IItemStack> _inputSlot;

        public NormalMachineInputInventory(int installationId)
        {
            _installationId = installationId;
            var data = InstallationConfig.GetInstallationsConfig(installationId);
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
                var recipe = MachineRecipeConfig.GetRecipeData(_installationId, _inputSlot.ToList());
                //実行できるレシピかどうか
                return recipe.RecipeConfirmation(_inputSlot);
            }
        }

        public IMachineRecipeData GetRecipeData()
        {
            return MachineRecipeConfig.GetRecipeData(_installationId, _inputSlot.ToList());
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