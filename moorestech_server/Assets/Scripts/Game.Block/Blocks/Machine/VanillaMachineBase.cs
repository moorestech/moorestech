using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.EnergySystem;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.BlockInventory;
using Game.Block.Blocks.Machine.InventoryController;
using Game.Block.Blocks.Machine.SaveLoad;
using Game.Block.Component.IOConnector;
using Game.Block.Interface;
using Game.Block.Interface.State;
using Game.Context;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     機械を表すクラス
    ///     具体的な処理は各コンポーネントに任せて、このクラスはInterfaceの実装だけを行う
    /// </summary>
    public abstract class VanillaMachineBase : IBlock, IBlockInventory, IEnergyConsumer, IOpenableInventory
    {
        private readonly BlockComponentManager _blockComponentManager = new();

        private readonly VanillaMachineBlockInventory _vanillaMachineBlockInventory;
        private readonly VanillaMachineRunProcess _vanillaMachineRunProcess;
        private readonly VanillaMachineSave _vanillaMachineSave;

        protected VanillaMachineBase(int blockId, int entityId, long blockHash,
            VanillaMachineBlockInventory vanillaMachineBlockInventory,
            VanillaMachineSave vanillaMachineSave, VanillaMachineRunProcess vanillaMachineRunProcess, BlockPositionInfo blockPositionInfo, BlockConnectorComponent<IBlockInventory> blockConnectorComponent)
        {
            BlockId = blockId;
            _vanillaMachineBlockInventory = vanillaMachineBlockInventory;
            _vanillaMachineSave = vanillaMachineSave;
            _vanillaMachineRunProcess = vanillaMachineRunProcess;
            BlockPositionInfo = blockPositionInfo;
            BlockHash = blockHash;
            EntityId = entityId;

            _blockComponentManager.AddComponent(blockConnectorComponent);
        }

        public int EntityId { get; }
        public int BlockId { get; }
        public long BlockHash { get; }

        public IBlockComponentManager ComponentManager => _blockComponentManager;

        public BlockPositionInfo BlockPositionInfo { get; }
        public IObservable<ChangedBlockState> BlockStateChange => _vanillaMachineRunProcess.ChangeState;

        #region IBlock implementation

        public string GetSaveState()
        {
            return _vanillaMachineSave.Save();
        }

        #endregion



        public bool Equals(IBlock other)
        {
            if (other is null) return false;
            return EntityId == other.EntityId && BlockId == other.BlockId && BlockHash == other.BlockHash;
        }

        public override bool Equals(object obj)
        {
            return obj is IBlock other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(EntityId, BlockId, BlockHash);
        }


        #region IBlockInventory

        public IItemStack InsertItem(IItemStack itemStack)
        {
            return _vanillaMachineBlockInventory.InsertItem(itemStack);
        }

        public IItemStack InsertItem(int itemId, int count)
        {
            var item = ServerContext.ItemStackFactory.Create(itemId, count);
            return _vanillaMachineBlockInventory.InsertItem(item);
        }

        public List<IItemStack> InsertItem(List<IItemStack> itemStacks)
        {
            return _vanillaMachineBlockInventory.InsertItem(itemStacks);
        }

        public bool InsertionCheck(List<IItemStack> itemStacks)
        {
            return _vanillaMachineBlockInventory.InsertionCheck(itemStacks);
        }

        #endregion


        #region IOpenableInventory implementation

        public ReadOnlyCollection<IItemStack> Items => _vanillaMachineBlockInventory.Items;

        public IItemStack GetItem(int slot)
        {
            return _vanillaMachineBlockInventory.GetItem(slot);
        }

        public void SetItem(int slot, IItemStack itemStack)
        {
            _vanillaMachineBlockInventory.SetItem(slot, itemStack);
        }

        public void SetItem(int slot, int itemId, int count)
        {
            var item = ServerContext.ItemStackFactory.Create(itemId, count);
            _vanillaMachineBlockInventory.SetItem(slot, item);
        }

        public IItemStack ReplaceItem(int slot, IItemStack itemStack)
        {
            return _vanillaMachineBlockInventory.ReplaceItem(slot, itemStack);
        }

        public IItemStack ReplaceItem(int slot, int itemId, int count)
        {
            var item = ServerContext.ItemStackFactory.Create(itemId, count);
            return ReplaceItem(slot, item);
        }

        public int GetSlotSize()
        {
            return _vanillaMachineBlockInventory.GetSlotSize();
        }

        #endregion


        #region IBlockElectric implementation

        public int RequestEnergy => _vanillaMachineRunProcess.RequestPower;

        public void SupplyEnergy(int power)
        {
            _vanillaMachineRunProcess.SupplyPower(power);
        }

        #endregion
    }
}