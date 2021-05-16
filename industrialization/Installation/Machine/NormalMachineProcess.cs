using System.Collections.Generic;
using System.Linq;
using industrialization.Config.Installation;
using industrialization.Config.Recipe;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class NormalMachineProcess
    {
        private int _installationId;
        public NormalMachineProcess(int installationId)
        {
            this._installationId = installationId;
        }

        //TODO プロセスをスタートする
        public List<IItemStack> StartProcess(List<IItemStack> inputSlot)
        {
            var recipe = MachineRecipeConfig.GetRecipeData(_installationId, inputSlot.ToList());
            var tmp = recipe.RecipeConfirmation(inputSlot);
            if(!tmp) return inputSlot;
            
            
            //TODO アウトプットスロットに空きがあるかチェック

            //スタートできるなら加工をスタートし、アイテムを減らす
            foreach (var item in recipe.ItemInputs)
            {
                for (int i = 0; i < inputSlot.Count; i++)
                {
                    if (inputSlot[i].Id == item.Id &&  item.Amount <=inputSlot[i].Amount)
                    {
                        inputSlot[i] = inputSlot[i].SubItem(item.Amount);
                        //TODO プロセススタート
                        break;
                    }
                }
            }

            return inputSlot;
        }
    }
}