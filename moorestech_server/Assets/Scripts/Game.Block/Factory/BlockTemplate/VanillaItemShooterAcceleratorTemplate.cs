using System.Collections.Generic;
using Game.Block.Blocks;
using Game.Block.Blocks.BeltConveyor;
using Game.Block.Blocks.ItemShooter;
using Game.Block.Component;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Factory.BlockTemplate
{
    public class VanillaItemShooterAcceleratorTemplate : IBlockTemplate
    {
        public IBlock New(BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateBlock(null, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        public IBlock Load(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            return CreateBlock(componentStates, blockMasterElement, blockInstanceId, blockPositionInfo);
        }

        private static IBlock CreateBlock(Dictionary<string, string> componentStates, BlockMasterElement blockMasterElement, BlockInstanceId blockInstanceId, BlockPositionInfo blockPositionInfo)
        {
            var acceleratorParam = blockMasterElement.BlockParam as ItemShooterAcceleratorBlockParam;

            var inventoryConnectorComponent = BlockTemplateUtil.CreateInventoryConnector(acceleratorParam.InventoryConnectors, blockPositionInfo);
            var gearConnectorComponent = new BlockConnectorComponent<IGearEnergyTransformer>(
                acceleratorParam.Gear.GearConnects,
                acceleratorParam.Gear.GearConnects,
                blockPositionInfo);

            var settings = CreateSettings(acceleratorParam);
            var service = new ItemShooterComponentService(inventoryConnectorComponent, settings);
            var itemShooterComponent = componentStates == null
                ? new ItemShooterComponent(service)
                : new ItemShooterComponent(componentStates, service);

            var acceleratorComponent = new ItemShooterAcceleratorComponent(service, acceleratorParam, blockInstanceId, gearConnectorComponent);

            var components = new List<IBlockComponent>
            {
                acceleratorComponent,
                itemShooterComponent,
                gearConnectorComponent,
                inventoryConnectorComponent
            };

            return new BlockSystem(blockInstanceId, blockMasterElement.BlockGuid, components, blockPositionInfo);
        }

        #region Internal

        private static ItemShooterComponentSettings CreateSettings(ItemShooterAcceleratorBlockParam param)
        {
            var slope = param.SlopeType switch
            {
                ItemShooterAcceleratorBlockParam.SlopeTypeConst.Up => BeltConveyorSlopeType.Up,
                ItemShooterAcceleratorBlockParam.SlopeTypeConst.Down => BeltConveyorSlopeType.Down,
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
