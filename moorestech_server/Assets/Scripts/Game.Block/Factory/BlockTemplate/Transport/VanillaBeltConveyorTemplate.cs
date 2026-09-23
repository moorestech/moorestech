using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Component.ConnectOverride;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate.Transport
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            //TODo UP bletからの入力を受付?
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private BlockSystem GetBlock(Dictionary<string, object> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var beltParam = blockMasterElement.BlockParam as BeltConveyorBlockParam;
            
            var slopeType = beltParam.SlopeType switch
            {
                BeltConveyorBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                BeltConveyorBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
                BeltConveyorBlockParam.SlopeTypeConst.Straight => BeltConveyorSlopeType.Straight
            };
            var connectorComponent = new BlockConnectorComponent<IBlockInventory, DefaultConnectJudge>(
                beltParam.InventoryConnectors.InputConnects, beltParam.InventoryConnectors.OutputConnects,
                blockPositionInfo, new BeltConnectionOverride(blockPositionInfo, slopeType,
                    beltParam.InventoryConnectors));
            var world = Game.Context.ServerContext.GetService<IBeltWorldMutation>();
            var beltComponent = new SegmentBeltComponent(blockInstanceId, blockPositionInfo, slopeType,
                connectorComponent, world, componentStates);

            var components = new List<IBlockComponent>
            {
                beltComponent,
                new SegmentBeltSaveComponent(beltComponent, world),
                connectorComponent
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}
