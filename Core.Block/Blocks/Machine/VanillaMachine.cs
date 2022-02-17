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
    public class VanillaMachine : IBlock, IBlockInventory, IBlockElectric, IInventory
    {
        private readonly VanillaMachineBlockInventory _vanillaMachineBlockInventory;
        private readonly VanillaMachineInventory _vanillaMachineInventory;
        private readonly VanillaMachineSave _vanillaMachineSave;
        private readonly VanillaMachineRunProcess _vanillaMachineRunProcess;
        private readonly ItemStackFactory _itemStackFactory;


        private readonly int _blockId;
        private readonly int _intId;

        public VanillaMachine(int blockId, int intId,
            VanillaMachineBlockInventory vanillaMachineBlockInventory,
            VanillaMachineInventory vanillaMachineInventory,
            VanillaMachineSave vanillaMachineSave, VanillaMachineRunProcess vanillaMachineRunProcess,ItemStackFactory itemStackFactory)
        {
            _vanillaMachineBlockInventory = vanillaMachineBlockInventory;
            _vanillaMachineInventory = vanillaMachineInventory;
            _vanillaMachineSave = vanillaMachineSave;
            _vanillaMachineRunProcess = vanillaMachineRunProcess;
            _itemStackFactory = itemStackFactory;
            _blockId = blockId;
            _intId = intId;
        }

        
        

        #region IBlockInventory

        public IItemStack InsertItem(IItemStack itemStack) { return _vanillaMachineBlockInventory.InsertItem(itemStack); }
        public IItemStack InsertItem(int itemId, int count) { return _vanillaMachineBlockInventory.InsertItem(_itemStackFactory.Create(itemId, count)); }
        

        public void AddOutputConnector(IBlockInventory blockInventory) { _vanillaMachineBlockInventory.AddConnector(blockInventory); }
        public void RemoveOutputConnector(IBlockInventory blockInventory) { _vanillaMachineBlockInventory.RemoveConnector(blockInventory); }
        
        #endregion


        
        
        

        #region IInventory implementation
        public IItemStack GetItem(int slot) { return _vanillaMachineInventory.GetItem(slot); }
        
        public void SetItem(int slot, IItemStack itemStack) { _vanillaMachineInventory.SetItem(slot, itemStack); }
        public void SetItem(int slot, int itemId, int count) { _vanillaMachineInventory.SetItem(slot, _itemStackFactory.Create(itemId, count)); }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _vanillaMachineInventory.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return ReplaceItem(slot, _itemStackFactory.Create(itemId, count)); }
        
        public int GetSlotSize() { return _vanillaMachineInventory.GetSlotSize(); }
        
        #endregion



        
        
        #region IBlockElectric implementation
        public int GetRequestPower() { return _vanillaMachineRunProcess.GetRequestPower(); }
        public void SupplyPower(int power) { _vanillaMachineRunProcess.SupplyPower(power); }

        #endregion

        
        

        #region IBlock implementation
        
        public int GetIntId() { return _intId; }
        public int GetBlockId() { return _blockId; }
        public string GetSaveState() { return _vanillaMachineSave.Save(); }

        #endregion
    }
}