using Core.Block.BlockInventory;
using Core.Electric;
using Core.Item;

namespace Core.Block.PowerGenerator
{
    //TODO アイテムを挿入して発電するシステムを作る
    public class VanillaPowerGenerator : IBlock,IPowerGenerator,IBlockInventory
    {
        private readonly int _blockId;
        private readonly int _intId;

        public VanillaPowerGenerator(int blockId, int intId)
        {
            _blockId = blockId;
            _intId = intId;
        }

        public int OutputPower()
        {
            return 100;
        }

        public string GetSaveState()
        {
            return "";
        }

        public int GetIntId()
        {
            return _intId;
        }

        public int GetBlockId()
        {
            return _blockId;
        }

        public IItemStack InsertItem(IItemStack itemStack)
        {
            throw new System.NotImplementedException();
        }

        public void AddConnector(IBlockInventory blockInventory)
        {
            throw new System.NotImplementedException();
        }

        public void RemoveConnector(IBlockInventory blockInventory)
        {
            throw new System.NotImplementedException();
        }
    }
}