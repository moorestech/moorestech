using Core.Block.BlockInventory;
using Core.Electric;
using Core.Item;

namespace Core.Block.Blocks.Miner
{
    public class VanillaMiner: IBlock,IBlockElectric,IBlockInventory
    {
        private readonly int _blockId;
        private readonly int _intId;

        public VanillaMiner(int blockId, int intId)
        {
            _blockId = blockId;
            _intId = intId;
        }

        public int GetRequestPower()
        {
            throw new System.NotImplementedException();
        }

        public void SupplyPower(int power)
        {
            throw new System.NotImplementedException();
        }

        public string GetSaveState()
        {
            throw new System.NotImplementedException();
        }


        public void AddOutputConnector(IBlockInventory blockInventory)
        {
            throw new System.NotImplementedException();
        }

        public void RemoveOutputConnector(IBlockInventory blockInventory)
        {
            throw new System.NotImplementedException();
        }
        
        public IItemStack InsertItem(IItemStack itemStack) { return itemStack; }
        public int GetIntId() { return _intId; }
        public int GetBlockId() { return _blockId; }
    }
}