using Server.Core.EnergySystem.Electric;
using Server.Core.Item;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;

namespace Game.Block.Blocks.Miner
{
    public class VanillaElectricMiner : VanillaMinerBase, IBlockElectricConsumer
    {
        public VanillaElectricMiner(
            (int blockId, int entityId, long blockHash, int requestPower, int outputSlotCount, ItemStackFactory
                itemStackFactory, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent, BlockPositionInfo blockPositionInfo, ComponentFactory componentFactory) data) :
            base(data.blockId, data.entityId, data.blockHash, data.requestPower, data.outputSlotCount,
                data.itemStackFactory, data.openableInventoryUpdateEvent, data.blockPositionInfo, data.componentFactory)
        {
        }

        public VanillaElectricMiner(
            (string saveData, int blockId, int entityId, long blockHash, int requestPower, int outputSlotCount,
                ItemStackFactory itemStackFactory, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent, BlockPositionInfo blockPositionInfo, ComponentFactory componentFactory) data)
            :
            base(data.saveData, data.blockId, data.entityId, data.blockHash, data.requestPower, data.outputSlotCount,
                data.itemStackFactory, data.openableInventoryUpdateEvent, data.blockPositionInfo, data.componentFactory)
        {
        }
    }
}