using System.Linq;
using industrialization.Config;
using industrialization.Inventory;
using industrialization.Item;
using industrialization_tdd.Config.Recipe;

namespace industrialization.Installation.Machine
{
    public class MachineInventory : IInstallationInventory
    {
        private MachineRunProcess _machineRunProcess;
        private IItemStack[] _inputSlot;
        private IItemStack[] _outpuutSlot;
        private int installationId;

        //TODO インプット、アウトプットスロットを取得し実装
        public MachineInventory(int installationId)
        {
            this.installationId = installationId;
            _machineRunProcess = new MachineRunProcess(OutputEvent);
            _inputSlot = new ItemStack[1];
            _outpuutSlot = new ItemStack[1];
        }
        
        /// <summary>
        /// アイテムが入る枠があるかどうか検索し、入るなら入れて返す、入らないならそのままもらったものを返す
        /// </summary>
        /// <param name="itemStack">入れたいアイテム</param>
        /// <returns>余り、枠が無かった等入れようとした結果余ったアイテム</returns>
        public IItemStack InsertItem(IItemStack itemStack)
        {
            for (int i = 0; i < _inputSlot.Length; i++)
            {
                if (_inputSlot[i].ID == itemStack.ID)
                {
                    var r = _inputSlot[i].AddItem(itemStack);
                    _inputSlot[i] = r.MineItemStack;
                    StartProcess();
                    return r.ReceiveItemStack;
                }
            }
            return itemStack;
        }

        void StartProcess()
        {
            if (_machineRunProcess == null) return;
            if(MachineRecipeConfig.GetRecipeData(installationId,_inputSlot.ToList()).RecipeConfirmation(_inputSlot)) return;;
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