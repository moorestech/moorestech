using Core.Block.BlockInventory;
using Core.Block.Blocks.Machine.InventoryController;
using Core.Block.Blocks.Machine.SaveLoad;
using Core.Electric;
using Core.Inventory;
using Core.Item;

namespace Core.Block.Blocks.Machine
{
    /// <summary>
    /// 機械を表すクラス
    /// 具体的な処理は各コンポーネントに任せて、このクラスはInterfaceの実装だけを行う
    /// </summary>
    public class VanillaMachine : IBlock,IBlockInventory,IBlockElectric,IInventory
    {
        private readonly VanillaMachineBlockInventory _vanillaMachineBlockInventory;
        private readonly VanillaMachineInventory _vanillaMachineInventory;
        private readonly VanillaMachineSave _vanillaMachineSave;
        private readonly VanillaMachineRunProcess _vanillaMachineRunProcess;
        
        
        private readonly int _blockId;
        private readonly int _intId;
        public VanillaMachine(int blockId, int intId,
            VanillaMachineBlockInventory vanillaMachineBlockInventory, 
            VanillaMachineInventory vanillaMachineInventory, 
            VanillaMachineSave vanillaMachineSave, VanillaMachineRunProcess vanillaMachineRunProcess)
        {
            _vanillaMachineBlockInventory = vanillaMachineBlockInventory;
            _vanillaMachineInventory = vanillaMachineInventory;
            _vanillaMachineSave = vanillaMachineSave;
            _vanillaMachineRunProcess = vanillaMachineRunProcess;
            _blockId = blockId;
            _intId = intId;
        }
        
        // IBlockInventoryのインターフェース実装
        public IItemStack InsertItem(IItemStack itemStack) { return _vanillaMachineBlockInventory.InsertItem(itemStack); }
        public void AddOutputConnector(IBlockInventory blockInventory) { _vanillaMachineBlockInventory.AddConnector(blockInventory); }
        public void RemoveOutputConnector(IBlockInventory blockInventory) { _vanillaMachineBlockInventory.RemoveConnector(blockInventory); }


        //IInventoryのインターフェース実装
        public IItemStack GetItem(int slot) { return _vanillaMachineInventory.GetItem(slot); }
        public void SetItem(int slot, IItemStack itemStack) { _vanillaMachineInventory.SetItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _vanillaMachineInventory.ReplaceItem(slot, itemStack); }
        
        
        //IBlockElectricのインターフェース実装
        public int GetRequestPower(){return _vanillaMachineRunProcess.GetRequestPower();}
        public void SupplyPower(int power){_vanillaMachineRunProcess.SupplyPower(power);}
        
        //IBlockのインターフェース実装
        public int GetIntId(){return _intId;}
        public int GetBlockId() { return _blockId; }
        public string GetSaveState()
        {
            return _vanillaMachineSave.Save();
        }
    }
}