using System.Collections.Generic;
using System.Linq;
using industrialization.Core.Config.Installation;
using industrialization.Core.Config.Recipe.Data;
using industrialization.Core.Item;
using industrialization.Core.Util;

namespace industrialization.Core.Installation.Machine
{
    public class NormalMachineOutputInventory
    {
        private readonly List<IItemStack> _outputSlot;
        private IInstallationInventory _connectInventory;
        public List<IItemStack> OutputSlot 
        {
            get
            {
                var a = _outputSlot.Where(i => i.Id != NullItemStack.NullItemId).ToList();
                a.Sort((a, b) => a.Id - b.Id);
                return a.ToList();
            }
        }

        public NormalMachineOutputInventory(int installationId, IInstallationInventory connect)
        {
            _connectInventory = connect;
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

            InsertConnectInventory();
        }

        void InsertConnectInventory()
        {
            for (int i = 0; i < _outputSlot.Count; i++)
            {
                _outputSlot[i] = _connectInventory.InsertItem(_outputSlot[i]);
            }
        }

        public void ChangeConnectInventory(IInstallationInventory installationInventory)
        {
            _connectInventory = installationInventory;
        }
    }
}