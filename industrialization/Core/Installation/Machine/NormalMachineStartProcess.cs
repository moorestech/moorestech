using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Config.Recipe;
using industrialization.Core.Item;

namespace industrialization.Core.Installation.Machine
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
            //建物IDと現在のインプットスロットからレシピを検索する
            var recipe = MachineRecipeConfig.GetRecipeData(_installationId, inputSlot.ToList());
            
            //実行できるレシピかどうか
            if(!recipe.RecipeConfirmation(inputSlot)) return inputSlot;
            //処理をスタートできるか？
            if (!NormalMachineRunProcess.IsAllowedToStartProcess) return inputSlot;

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