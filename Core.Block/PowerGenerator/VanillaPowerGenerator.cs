using System.Collections.Generic;
using Core.Block.BlockInventory;
using Core.Electric;
using Core.Item;
using Core.Item.Util;
using Core.Update;

namespace Core.Block.PowerGenerator
{
    //TODO アイテムを挿入して発電するシステムを作る
    public class VanillaPowerGenerator : IBlock,IPowerGenerator,IBlockInventory,IUpdate
    {
        private readonly int _blockId;
        private readonly int _intId;
        
        private int _fuelItemId = ItemConst.NullItemId;
        private readonly List<IItemStack> _fuelItemStacks;

        public VanillaPowerGenerator(int blockId, int intId,int fuelItemSlot,ItemStackFactory itemStackFactory)
        {
            _blockId = blockId;
            _intId = intId;
            _fuelItemStacks = CreateEmptyItemStacksList.Create(fuelItemSlot,itemStackFactory);
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

        public void Update()
        {
            
        }
    }
}