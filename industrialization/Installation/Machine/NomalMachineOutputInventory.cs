using System.Collections.Generic;
using System.Linq;
using industrialization.Config.Installation;
using industrialization.Config.Recipe.Data;
using industrialization.Item;
using industrialization.Util;

namespace industrialization.Installation.Machine
{
    public class NormalMachineOutputInventory
    {
        private List<IItemStack> _outputSlot;
        private IInstallationInventory connectInventory;

        public NormalMachineOutputInventory(int installationId, IInstallationInventory installationInventory)
        {
            connectInventory = installationInventory;
            var data = InstallationConfig.GetInstallationsConfig(installationId);
            _outputSlot = CreateEmptyItemStacksList.Create(data.OutputSlot);
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
                    
                    _outputSlot[i] = _outputSlot[i].AddItem(output.OutputItem).MineItemStack;
                    break;
                }
            }
        }

        void InsertConnectInventory()
        {
            for (int i = 0; i < _outputSlot.Count; i++)
            {
                _outputSlot[i] = connectInventory.InsertItem(_outputSlot[i]);
            }
        }
    }
}