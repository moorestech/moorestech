using System;
using System.Collections.Generic;
using Core.Master;
using Game.Block.Factory.BlockTemplate;

namespace Game.Block.Interface
{
    public interface IBlockFactory
    {
        public IBlock Create(BlockId blockId, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams);
        public IBlock Load(Guid blockGuid, BlockInstanceId blockInstanceId, Dictionary<string,string> state, BlockPositionInfo blockPositionInfo);
        
        public void RegisterTemplateIBlock(string key, IBlockTemplate block);
    }
}