using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public class VanillaMachine : IBlock, IBlockInventory, IBlockElectric, IOpenableInventory
    {
        private readonly VanillaMachineBlockInventory _vanillaMachineBlockInventory;
        private readonly VanillaMachineSave _vanillaMachineSave;
        private readonly VanillaMachineRunProcess _vanillaMachineRunProcess;
        private readonly ItemStackFactory _itemStackFactory;
        public int EntityId { get; }
        public int BlockId { get; }
        public ulong BlockHash { get; }
        public event Action<ChangedBlockState> OnBlockStateChange;

        public VanillaMachine(int blockId, int entityId, ulong blockHash,
            VanillaMachineBlockInventory vanillaMachineBlockInventory,
            VanillaMachineSave vanillaMachineSave, VanillaMachineRunProcess vanillaMachineRunProcess,ItemStackFactory itemStackFactory)
        {
            BlockId = blockId;
            _vanillaMachineBlockInventory = vanillaMachineBlockInventory;
            _vanillaMachineSave = vanillaMachineSave;
            _vanillaMachineRunProcess = vanillaMachineRunProcess;
            _itemStackFactory = itemStackFactory;
            BlockHash = blockHash;
            EntityId = entityId;
        }

        
        

        #region IBlockInventory

        public IItemStack InsertItem(IItemStack itemStack) { return _vanillaMachineBlockInventory.InsertItem(itemStack); }
        public IItemStack InsertItem(int itemId, int count) { return _vanillaMachineBlockInventory.InsertItem(_itemStackFactory.Create(itemId, count)); }
        public List<IItemStack> InsertItem(List<IItemStack> itemStacks) { return _vanillaMachineBlockInventory.InsertItem(itemStacks); }

        public bool InsertionCheck(List<IItemStack> itemStacks) { return _vanillaMachineBlockInventory.InsertionCheck(itemStacks); }


        public void AddOutputConnector(IBlockInventory blockInventory) { _vanillaMachineBlockInventory.AddConnector(blockInventory); }
        public void RemoveOutputConnector(IBlockInventory blockInventory) { _vanillaMachineBlockInventory.RemoveConnector(blockInventory); }
        
        #endregion


        
        
        

        #region IOpenableInventory implementation
        
        public ReadOnlyCollection<IItemStack> Items => _vanillaMachineBlockInventory.Items;
        public IItemStack GetItem(int slot) { return _vanillaMachineBlockInventory.GetItem(slot); }
        
        public void SetItem(int slot, IItemStack itemStack) { _vanillaMachineBlockInventory.SetItem(slot, itemStack); }
        public void SetItem(int slot, int itemId, int count) { _vanillaMachineBlockInventory.SetItem(slot, _itemStackFactory.Create(itemId, count)); }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _vanillaMachineBlockInventory.ReplaceItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, int itemId, int count) { return ReplaceItem(slot, _itemStackFactory.Create(itemId, count)); }
        
        public int GetSlotSize() { return _vanillaMachineBlockInventory.GetSlotSize(); }
        
        #endregion



        
        
        #region IBlockElectric implementation
        public int RequestPower => _vanillaMachineRunProcess.RequestPower;
        public void SupplyPower(int power) { _vanillaMachineRunProcess.SupplyPower(power); }

        #endregion

        
        

        #region IBlock implementation
        public string GetSaveState() { return _vanillaMachineSave.Save(); }

        #endregion
    }
}