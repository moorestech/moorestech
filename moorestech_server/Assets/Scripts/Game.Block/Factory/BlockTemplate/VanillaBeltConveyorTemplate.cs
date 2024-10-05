using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaBeltConveyorTemplate : IBlockTemplate
    {
        public const string SlopeUpBeltConveyor = "gear belt conveyor up";
        public const string SlopeDownBeltConveyor = "gear belt conveyor down";
        public const string Hueru = "gear belt conveyor hueru";
        public const string Kieru = "gear belt conveyor kieru";
        
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var beltParam = blockMasterElement.BlockParam as BeltConveyorBlockParam;
            var blockName = blockMasterElement.Name;
            
            var connectorComponent = BlockTemplateUtil.CreateInventoryConnector(beltParam.InventoryConnectors, blockPositionInfo);
            var beltComponent = new VanillaBeltConveyorComponent(beltParam.BeltConveyorItemCount, beltParam.TimeOfItemEnterToExit, connectorComponent, blockName);
            var components = new List<IBlockComponent>
            {
                beltComponent,
                connectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
        
        public IBlock Load(string state, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            //TODo UP bletからの入力を受付?
            var beltParam = blockMasterElement.BlockParam as BeltConveyorBlockParam;
            var blockName = blockMasterElement.Name;
            
            var connectorComponent = BlockTemplateUtil.CreateInventoryConnector(beltParam.InventoryConnectors, blockPositionInfo);
            var beltComponent = new VanillaBeltConveyorComponent(state, beltParam.BeltConveyorItemCount, beltParam.TimeOfItemEnterToExit, connectorComponent, blockName);
            var components = new List<IBlockComponent>
            {
                beltComponent,
                connectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
    }
}