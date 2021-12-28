using System.Collections.Generic;
using Core.Block.BlockInventory;
using Core.Block.Machine.Inventory;
using Core.Block.Machine.InventoryController;
using Core.Block.Machine.SaveLoad;
using Core.Electric;
using Core.Inventory;
using Core.Item;

namespace Core.Block.Machine
{
    /// <summary>
    /// 機械を表すクラス
    /// 具体的な処理は各コンポーネントに任せて、このクラスはInterfaceの実装だけを行う
    /// </summary>
    public class NormalMachine : IBlock,IBlockInventory,IBlockElectric,IInventory
    {
        private readonly NormalMachineInputInventory _normalMachineInputInventory;
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;
        private readonly NormalMachineBlockInventory _normalMachineBlockInventory;
        private readonly NormalMachineInventory _normalMachineInventory;
        private readonly NormalMachineSave _normalMachineSave;
        private readonly NormalMachineRunProcess _normalMachineRunProcess;
        
        public List<IItemStack> InputSlotWithoutEmptyItemStack => _normalMachineInputInventory.InputSlotWithoutEmptyItemStack;
        public List<IItemStack> OutputSlotWithoutEmptyItemStack => _normalMachineOutputInventory.OutputSlotWithoutEmptyItemStack;
        
        private readonly int _blockId;
        private readonly int _intId;
        public NormalMachine(int blockId, int intId,
            NormalMachineInputInventory normalMachineInputInventory,
            NormalMachineOutputInventory normalMachineOutputInventory, 
            NormalMachineBlockInventory normalMachineBlockInventory, 
            NormalMachineInventory normalMachineInventory, 
            NormalMachineSave normalMachineSave, NormalMachineRunProcess normalMachineRunProcess)
        {
            _normalMachineInputInventory = normalMachineInputInventory;
            _normalMachineOutputInventory = normalMachineOutputInventory;
            _normalMachineBlockInventory = normalMachineBlockInventory;
            _normalMachineInventory = normalMachineInventory;
            _normalMachineSave = normalMachineSave;
            _normalMachineRunProcess = normalMachineRunProcess;
            _blockId = blockId;
            _intId = intId;
        }
        
        // IBlockInventoryのインターフェース実装
        public IItemStack InsertItem(IItemStack itemStack) { return _normalMachineBlockInventory.InsertItem(itemStack); }
        public void AddConnector(IBlockInventory blockInventory) { _normalMachineBlockInventory.AddConnector(blockInventory); }
        public void RemoveConnector(IBlockInventory blockInventory) { _normalMachineBlockInventory.RemoveConnector(blockInventory); }


        //IInventoryのインターフェース実装
        public IItemStack GetItem(int slot) { return _normalMachineInventory.GetItem(slot); }
        public void SetItem(int slot, IItemStack itemStack) { _normalMachineInventory.SetItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _normalMachineInventory.ReplaceItem(slot, itemStack); }
        
        
        //IBlockElectricのインターフェース実装
        public int GetRequestPower(){return _normalMachineRunProcess.GetRequestPower();}
        public void SupplyPower(int power){_normalMachineRunProcess.SupplyPower(power);}
        
        //IBlockのインターフェース実装
        public int GetIntId(){return _intId;}
        public int GetBlockId() { return _blockId; }
        public string GetSaveState()
        {
            return _normalMachineSave.Save();
        }
    }
}