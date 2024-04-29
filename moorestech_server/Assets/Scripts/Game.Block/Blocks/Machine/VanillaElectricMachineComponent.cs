using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Core.Inventory;
using Core.Item.Interface;
using Game.Block.Blocks.Machine.InventoryController;
using Game.Block.Blocks.Machine.SaveLoad;
using Game.Block.Component;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Context;
using Game.EnergySystem;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    ///     機械を表すクラス
    ///     具体的な処理は各コンポーネントに任せて、このクラスはInterfaceの実装だけを行う
    /// TODO この処理周辺のコンポーネントの分離をいい感じにする
    /// </summary>
    public class VanillaElectricMachineComponent : IBlockInventory, IElectricConsumer, IOpenableBlockInventoryComponent, IBlockStateChange, IBlockSaveState
    {
        public IObservable<ChangedBlockState> BlockStateChange => _vanillaMachineRunProcess.ChangeState;
        
        public bool IsDestroy { get; private set; }
        
        private readonly VanillaMachineBlockInventory _vanillaMachineBlockInventory;
        private readonly VanillaMachineRunProcess _vanillaMachineRunProcess;
        private readonly VanillaMachineSave _vanillaMachineSave;

        public VanillaElectricMachineComponent(int entityId,
            VanillaMachineBlockInventory vanillaMachineBlockInventory,
            VanillaMachineSave vanillaMachineSave, VanillaMachineRunProcess vanillaMachineRunProcess)
        {
            _vanillaMachineBlockInventory = vanillaMachineBlockInventory;
            _vanillaMachineSave = vanillaMachineSave;
            _vanillaMachineRunProcess = vanillaMachineRunProcess;
            EntityId = entityId;
        }

        public int EntityId { get; }


        #region IBlock implementation

        public string GetSaveState()
        {
            return _vanillaMachineSave.Save();
        }

        #endregion

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

        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}