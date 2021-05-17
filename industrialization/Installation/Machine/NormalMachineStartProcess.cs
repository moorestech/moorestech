using System.Collections.Generic;
using System.Linq;
using industrialization.Config.Installation;
using industrialization.Config.Recipe;
using industrialization.Config.Recipe.Data;
using industrialization.Item;
using industrialization.Test.Generate;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class NormalMachineStartProcess
    {
        private readonly int _installationId;
        public readonly NormalMachineRunProcess NormalMachineRunProcess;
        public NormalMachineStartProcess(int installationId, NormalMachineRunProcess normalMachineRunProcess)
        {
            _installationId = installationId;
            NormalMachineRunProcess = normalMachineRunProcess;
        }

        public List<IItemStack> StartingProcess(List<IItemStack> inputSlot)
        {
            var recipe = MachineRecipeConfig.GetRecipeData(_installationId, inputSlot.ToList());
            //実行できるレシピがなかったらそのまま返す
            if(!recipe.RecipeConfirmation(inputSlot)) return inputSlot;
            
            //プロセスの実行ができるかどうかを見る
            if (!NormalMachineRunProcess.IsAllowedToStartProcess()) return inputSlot;

            //inputスロットからアイテムを減らす
            foreach (var item in recipe.ItemInputs)
            {
                for (var i = 0; i < inputSlot.Count; i++)
                {
                    if (inputSlot[i].Id != item.Id || item.Amount > inputSlot[i].Amount) continue;
                    //アイテムを減らす
                    inputSlot[i] = inputSlot[i].SubItem(item.Amount);
                    break;
                }
            }
            //プロセススタート
            NormalMachineRunProcess.StartProcess(recipe);

            return inputSlot;
        }
    }
}