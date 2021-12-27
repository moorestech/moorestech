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
    /// 「機械」というオブジェクト(ドメイン)事態の責務が大きすぎて、クラス自体の責務も大きくなってしまっているう
    /// 単純に別のクラスに分けるのも手かも知れないが、本質的な解決になるのだろうか？
    /// 現状箱のままにしておくが、今後機械関係のクラスに修正をする場合、この機械のクラス全体をリファクタする必要があるような気がする
    /// </summary>
    public class NormalMachine : IBlock,IBlockInventory,IBlockElectric,IInventory
    {
        private readonly NormalMachineInputInventory _normalMachineInputInventory;
        private readonly NormalMachineOutputInventory _normalMachineOutputInventory;
        private readonly NormalMachineBlockInventory _normalMachineBlockInventory;
        private readonly NormalMachineInventory _normalMachineInventory;
        private readonly NormalMachineSave _normalMachineSave;
        
        public List<IItemStack> InputSlotWithoutEmptyItemStack => _normalMachineInputInventory.InputSlotWithoutEmptyItemStack;
        public List<IItemStack> OutputSlotWithoutEmptyItemStack => _normalMachineOutputInventory.OutputSlotWithoutEmptyItemStack;
        
        private readonly int _blockId;
        private readonly int _intId;
        public NormalMachine(int blockId, int intId,
            NormalMachineInputInventory normalMachineInputInventory,
            NormalMachineOutputInventory normalMachineOutputInventory, 
            NormalMachineBlockInventory normalMachineBlockInventory, 
            NormalMachineInventory normalMachineInventory, 
            NormalMachineSave normalMachineSave)
        {
            _normalMachineInputInventory = normalMachineInputInventory;
            _normalMachineOutputInventory = normalMachineOutputInventory;
            _normalMachineBlockInventory = normalMachineBlockInventory;
            _normalMachineInventory = normalMachineInventory;
            _normalMachineSave = normalMachineSave;
            _blockId = blockId;
            _intId = intId;
        }
        
        public IItemStack InsertItem(IItemStack itemStack) { return _normalMachineBlockInventory.InsertItem(itemStack); }
        public void AddConnector(IBlockInventory blockInventory) { _normalMachineBlockInventory.AddConnector(blockInventory); }
        public void RemoveConnector(IBlockInventory blockInventory) { _normalMachineBlockInventory.RemoveConnector(blockInventory); }


        public IItemStack GetItem(int slot) { return _normalMachineInventory.GetItem(slot); }
        public void SetItem(int slot, IItemStack itemStack) { _normalMachineInventory.SetItem(slot, itemStack); }
        public IItemStack ReplaceItem(int slot, IItemStack itemStack) { return _normalMachineInventory.ReplaceItem(slot, itemStack); }
        


        private const int RequestPower = 100;
        private int _nowPower = 0;
        public int GetRequestPower(){return RequestPower;}
        public void SupplyPower(int power){_nowPower = power;}
        public int GetIntId(){return _intId;}
        public int GetBlockId() { return _blockId; }
        public string GetSaveState()
        {
            return _normalMachineSave.Save();
        }
    }
}