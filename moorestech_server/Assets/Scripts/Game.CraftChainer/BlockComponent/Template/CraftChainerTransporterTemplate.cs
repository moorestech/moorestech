using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Factory.BlockTemplate;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.CraftChainer.BlockComponent.Template
{
    public class CraftChainerTransporterTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo, BlockCreateParam[] createParams)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private BlockSystem GetBlock(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var transporterParam = blockMasterElement.BlockParam as CraftChainerTransporterBlockParam;
            
            var transporterComponent = componentStates == null ?
                new CraftChainerTransporterComponent() :
                new CraftChainerTransporterComponent(componentStates);
            
            var slopeType = transporterParam.SlopeType switch
            {
                ItemShooterBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                ItemShooterBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
                ItemShooterBlockParam.SlopeTypeConst.Straight => BeltConveyorSlopeType.Straight
            };
            var connectorComponent = BlockTemplateUtil.CreateInventoryConnector(transporterParam.InventoryConnectors, blockPositionInfo);
            var beltConveyorConnector = new CraftChainerTransporterInserter(connectorComponent, transporterComponent.NodeId);
            var itemCount = transporterParam.TransporterConveyorItemCount;
            var time = transporterParam.TimeOfItemEnterToExit;
            
            var beltComponent = componentStates == null ?
                new VanillaBeltConveyorComponent(itemCount, time, beltConveyorConnector, slopeType) :
                new VanillaBeltConveyorComponent(componentStates, itemCount, time, beltConveyorConnector, slopeType, transporterParam.InventoryConnectors);
            
            var components = new List<IBlockComponent>
            {
                beltComponent,
                connectorComponent,
                transporterComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}