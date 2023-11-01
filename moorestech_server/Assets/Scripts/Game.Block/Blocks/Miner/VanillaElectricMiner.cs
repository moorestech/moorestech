using Core.EnergySystem.Electric;
using Core.Item;
using Game.Block.Event;

namespace Game.Block.Blocks.Miner
{
    public class VanillaElectricMiner : VanillaMinerBase, IBlockElectricConsumer
    {
        public VanillaElectricMiner((int blockId, int entityId, ulong blockHash, int requestPower, int outputSlotCount, ItemStackFactory itemStackFactory, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent) data) :
            base(data.blockId, data.entityId, data.blockHash, data.requestPower, data.outputSlotCount, data.itemStackFactory, data.openableInventoryUpdateEvent)
        {
        }

        public VanillaElectricMiner((string saveData, int blockId, int entityId, ulong blockHash, int requestPower, int outputSlotCount, ItemStackFactory itemStackFactory, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent) data) :
            base(data.saveData, data.blockId, data.entityId, data.blockHash, data.requestPower, data.outputSlotCount, data.itemStackFactory, data.openableInventoryUpdateEvent)
        {
        }
    }
}