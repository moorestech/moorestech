using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.ItemShooter;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaItemShooterTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var itemShooter = blockMasterElement.BlockParam as ItemShooterBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(itemShooter.InventoryConnectors, blockPositionInfo);

            var settings = CreateSettings(itemShooter);
            var service = new ItemShooterComponentService(inputConnectorComponent, settings);
            var chestComponent = new ItemShooterComponent(service);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }
        
        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var itemShooter = blockMasterElement.BlockParam as ItemShooterBlockParam;
            var inputConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(itemShooter.InventoryConnectors, blockPositionInfo);

            var settings = CreateSettings(itemShooter);
            var service = new ItemShooterComponentService(inputConnectorComponent, settings);
            var chestComponent = new ItemShooterComponent(componentStates, service);
            var components = new List<IBlockComponent>
            {
                chestComponent,
                inputConnectorComponent,
            };
            
            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        #region Internal

        private static ItemShooterComponentSettings CreateSettings(ItemShooterBlockParam param)
        {
            var slope = param.SlopeType switch
            {
                ItemShooterBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                ItemShooterBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
                _ => BeltConveyorSlopeType.Straight
            };

            return new ItemShooterComponentSettings(
                param.InventoryItemNum,
                (float)param.InitialShootSpeed,
                (float)param.ItemShootSpeed,
                (float)param.Acceleration,
                slope);
        }

        #endregion
    }
}
