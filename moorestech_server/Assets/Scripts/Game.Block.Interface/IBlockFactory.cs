using System;
using Core.Master;
using Game.Block.Factory.BlockTemplate;

namespace Game.Block.Interface
{
    public interface IBlockFactory
    {
        public IBlock Create(BlockId blockId, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo);
        public IBlock Load(Guid blockGuid, BlockInstanceId blockInstanceId, string state, BlockPositionInfo blockPositionInfo);
        
        public void RegisterTemplateIBlock(string key, IBlockTemplate block);
    }
}