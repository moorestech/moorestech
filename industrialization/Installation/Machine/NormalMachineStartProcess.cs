using System.Collections.Generic;
using System.Linq;
using industrialization.Config.Installation;
using industrialization.Config.Recipe;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class NormalMachineStartProcess
    {
        private readonly int _installationId;
        private readonly NormalMachineRunProcess _normalMachineRunProcess;
        public NormalMachineStartProcess(int installationId, NormalMachineRunProcess normalMachineRunProcess)
        {
            _installationId = installationId;
            _normalMachineRunProcess = normalMachineRunProcess;
        }

        public List<IItemStack> StartingProcess(List<IItemStack> inputSlot)
        {
            var recipe = MachineRecipeConfig.GetRecipeData(_installationId, inputSlot.ToList());
            //実行できるレシピがなかったらそのまま返す
            var tmp = recipe.RecipeConfirmation(inputSlot);
            if(!tmp) return inputSlot;
            
            
            //プロセスの実行ができるかどうかを見る
            if (!_normalMachineRunProcess.IsAllowedToStartProcess()) return inputSlot;

            //スタートできるならインベントリ敵にスタートできるか判定し、アイテムを減らす
            foreach (var item in recipe.ItemInputs)
            {
                for (var i = 0; i < inputSlot.Count; i++)
                {
                    if (inputSlot[i].Id != item.Id || item.Amount > inputSlot[i].Amount) continue;
                    //アイテムを減らす
                    inputSlot[i] = inputSlot[i].SubItem(item.Amount);
                    //プロセススタート
                    _normalMachineRunProcess.StartProcess(recipe);
                    break;
                }
            }

            return inputSlot;
        }
    }
}