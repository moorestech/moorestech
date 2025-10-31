using System;
using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks;
using Game.Block.Blocks.BaseCamp;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class BaseCampBlockTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams = null)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private IBlock GetBlock(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var blockParam = (BaseCampBlockParam)blockMasterElement.BlockParam;
            
            var baseCampComponent = componentStates == null ?
                new BaseCampComponent(blockInstanceId, blockParam) :
                new BaseCampComponent(componentStates, blockInstanceId, blockParam);
            
            var components = new List<IBlockComponent>
            {
                baseCampComponent
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}