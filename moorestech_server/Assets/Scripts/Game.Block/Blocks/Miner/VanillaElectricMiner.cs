using Core.EnergySystem.Electric;
using Core.Item;
using Game.Block.Event;
using Game.Block.Interface;

namespace Game.Block.Blocks.Miner
{
    public class VanillaElectricMiner : VanillaMinerBase, IBlockElectricConsumer
    {
        public VanillaElectricMiner(
            (int blockId, int entityId, long blockHash, int requestPower, int outputSlotCount, ItemStackFactory
                itemStackFactory, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent,BlockPositionInfo blockPositionInfo) data) :
            base(data.blockId, data.entityId, data.blockHash, data.requestPower, data.outputSlotCount,
                data.itemStackFactory, data.openableInventoryUpdateEvent,data.blockPositionInfo)
        {
        }

        public VanillaElectricMiner(
            (string saveData, int blockId, int entityId, long blockHash, int requestPower, int outputSlotCount,
                ItemStackFactory itemStackFactory, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent,BlockPositionInfo blockPositionInfo) data)
            :
            base(data.saveData, data.blockId, data.entityId, data.blockHash, data.requestPower, data.outputSlotCount,
                data.itemStackFactory, data.openableInventoryUpdateEvent,data.blockPositionInfo)
        {
        }
    }
}