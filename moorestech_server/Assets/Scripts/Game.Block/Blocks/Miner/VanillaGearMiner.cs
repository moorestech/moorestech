using Core.EnergySystem.Gear;
using Core.Item.Interface;
using Game.Block.Component;
using Game.Block.Event;
using Game.Block.Interface;

namespace Game.Block.Blocks.Miner
{
    public class VanillaGearMiner : VanillaMinerBase, IGearConsumer
    {
        public VanillaGearMiner(
            (int blockId, int entityId, long blockHash, int requestPower, int outputSlotCount, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent, BlockPositionInfo blockPositionInfo) data) :
            base(data.blockId, data.entityId, data.blockHash, data.requestPower, data.outputSlotCount, data.openableInventoryUpdateEvent, data.blockPositionInfo)
        {
        }

        public VanillaGearMiner(
            (string saveData, int blockId, int entityId, long blockHash, int requestPower, int outputSlotCount, BlockOpenableInventoryUpdateEvent openableInventoryUpdateEvent, BlockPositionInfo blockPositionInfo) data)
            :
            base(data.saveData, data.blockId, data.entityId, data.blockHash, data.requestPower, data.outputSlotCount, data.openableInventoryUpdateEvent, data.blockPositionInfo)
        {
        }
    }
}