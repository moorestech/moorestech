using System.Linq;
using industrialization.Config;
using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation.Machine
{
    public class MachineInventory : IInstallationInventory
    {
        private MachineRunProcess machineRunProcess;
        private IItemStack[] InputSlot;
        private IItemStack[] OutpuutSlot;
        private int installationId;

        //TODO インプット、アウトプットスロットを取得し実装
        public MachineInventory(int installationId)
        {
            this.installationId = installationId;
            machineRunProcess = new MachineRunProcess(OutputEvent);
            InputSlot = new ItemStack[1];
            OutpuutSlot = new ItemStack[1];
        }
        
        /// <summary>
        /// アイテムが入る枠があるかどうか検索し、入るなら入れて返す、入らないならそのままもらったものを返す
        /// </summary>
        /// <param name="itemStack">入れたいアイテム</param>
        /// <returns>余り、枠が無かった等入れようとした結果余ったアイテム</returns>
        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (int i = 0; i < InputSlot.Length; i++)
            {
                if (InputSlot[i].ID == itemStack.ID)
                {
                    var r = InputSlot[i].addItem(itemStack);
                    InputSlot[i] = r.MineItemStack;
                    StartProcess();
                    return r.ReceiveItemStack;
                }
            }
            return itemStack;
        }

        void StartProcess()
        {
            if (machineRunProcess == null || MachineRecipeConfig.GetRecipeData(installationId,InputSlot).RecipeConfirmation(InputSlot))
            {
                
            }
        }

        void OutputEvent(ItemStack output)
        {
            
        }

        public InventoryData GetInventory()
        {
            throw new System.NotImplementedException();
        }
    }
}