using System.Linq;
using industrialization.Config;
using industrialization.Config.Recipe;
using industrialization.Inventory;
using industrialization.Item;

namespace industrialization.Installation.Machine
{
    public class MachineInventory : IInstallationInventory
    {
        private MachineRunProcess _machineRunProcess;
        private IInstallationInventory _connectInventory;
        private IItemStack[] _inputSlot;
        private IItemStack[] _outpuutSlot;
        private int installationId;

        //TODO インプット、アウトプットスロットを取得し実装
        public MachineInventory(int installationId, IInstallationInventory connectInventory)
        {
            this.installationId = installationId;
            _connectInventory = connectInventory;
            _inputSlot = ItemStackFactory.CreateEmptyItemStacksArray(1);
            _outpuutSlot = ItemStackFactory.CreateEmptyItemStacksArray(1);
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
                if (_inputSlot[i].Id == itemStack.Id)
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
            //プロセスをスタートできるか判定
            if (_machineRunProcess != null && _machineRunProcess.IsProcessing()) return;
            var recipe = MachineRecipeConfig.GetRecipeData(installationId, _inputSlot.ToList());
            if(MachineRecipeConfig.GetRecipeData(
                installationId,_inputSlot.ToList()).
                RecipeConfirmation(_inputSlot)) return;
            //TODO アウトプットスロットに空きがあるかチェック

            //スタートできるならスタートし、アイテムを減らす
            _machineRunProcess = new MachineRunProcess(OutputEvent,recipe);
            //TODO アイテムを減らす処理
        }

        void OutputEvent(ItemStack output)
        {
            //アウトプットスロットに受け取ったアイテムを入れる
            for (int i = 0; i < _outpuutSlot.Length; i++)
            {
                if (_outpuutSlot[i].Id == output.Id)
                {
                    _outpuutSlot[i] = _outpuutSlot[i].AddItem(output).MineItemStack;
                }
            }
        }

        public InventoryData GetInventory()
        {
            throw new System.NotImplementedException();
        }
    }
}