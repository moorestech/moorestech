using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface.Component;
using Game.Block.Interface.ComponentAttribute;
using Game.Context;
using Game.MapObject.Interface;
using Mooresmaster.Model;

namespace Game.Block.Factory.BlockTemplate
{
    [BlockComponent("gearMapObjectMiner")]
    public class VanillaGearMapObjectMinerTemplate : IBlockTemplate
    {
        public IBlock Create(BlockProperties properties)
        {
            var blockParams = properties.BlockParam as GearMapObjectMinerBlockParam;
            var inventory = ServerContext.ItemStackFactory.Create(blockParams.OutputItemSlotCount);
            
            var components = new List<IBlockComponent>
            {
                new VanillaChestComponent(inventory, properties.ComponentManager, properties.BlockOpenableInventoryUpdateEvent),
                new GearMapObjectMinerComponent(
                    blockParams.TeethCount,
                    blockParams.RequireTorque,
                    blockParams.RequiredRpm,
                    blockParams.MiningAreaRange,
                    blockParams.MiningAreaOffset,
                    blockParams.MapObjectMineSettings,
                    ServerContext.MapObjectDatastore,
                    ServerContext.MapObjectFactory,
                    properties.ComponentManager,
                    properties.BlockPositionInfo),
                new GearComponent(blockParams.Gear, properties.ComponentManager)
            };

            return new BlockBase(
                properties.BlockId,
                properties.BlockHash,
                properties.Type,
                components,
                properties.ComponentManager,
                properties.BlockPositionInfo);
        }
    }
}