using System;
using Core.Master;

namespace Game.Block.Interface
{
    public interface IBlockFactory
    {
        public IBlock Create(BlockId blockId, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo);
        public IBlock Load(Guid blockGuid, BlockInstanceId blockInstanceId, string state, BlockPositionInfo blockPositionInfo);
    }
}