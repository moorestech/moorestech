using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return GetBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            //TODo UP bletからの入力を受付?
            return GetBlock(state, blockMasterElement, blockInstanceId, blockPositionInfo);
        }
        
        private BlockSystem GetBlock(string state, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var beltParam = blockMasterElement.BlockParam as BeltConveyorBlockParam;
            
            var slopeType = beltParam.SlopeType switch
            {
                ItemShooterBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                ItemShooterBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
                ItemShooterBlockParam.SlopeTypeConst.Straight => BeltConveyorSlopeType.Straight
            };
            var connectorComponent = BlockTemplateUtil.CreateInventoryConnector(beltParam.InventoryConnectors, blockPositionInfo);
            var itemFactory = new CommonBeltConveyorInventoryItemFactory();
            var beltComponent = state == null ? 
                new VanillaBeltConveyorComponent(beltParam.BeltConveyorItemCount, beltParam.TimeOfItemEnterToExit, connectorComponent, slopeType, itemFactory) : 
                new VanillaBeltConveyorComponent(state, beltParam.BeltConveyorItemCount, beltParam.TimeOfItemEnterToExit, connectorComponent, slopeType, itemFactory);
            
            
            var components = new List<IBlockComponent>
            {
                beltComponent,
                connectorComponent
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}