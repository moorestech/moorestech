using System.Collections.Generic;
using Core.Block.Config.LoadConfig.Param;
using Core.Block.Event;
using Core.EnergySystem.Electric;
using Core.Item;

namespace Core.Block.Blocks.PowerGenerator
{
    public class VanillaElectricGenerator : VanillaPowerGeneratorBase,IElectricGenerator
    {
        public VanillaElectricGenerator((int blockId, int entityId, ulong blockHash, int fuelItemSlot, ItemStackFactory itemStackFactory, Dictionary<int, FuelSetting> fuelSettings, IBlockOpenableInventoryUpdateEvent blockInventoryUpdate) data) : 
            base(data.blockId, data.entityId, data.blockHash, data.fuelItemSlot, data.itemStackFactory, data.fuelSettings, data.blockInventoryUpdate)
        {
        }

        public VanillaElectricGenerator((string state,int blockId, int entityId, ulong blockHash,  int fuelItemSlot, ItemStackFactory itemStackFactory, Dictionary<int, FuelSetting> fuelSettings, IBlockOpenableInventoryUpdateEvent blockInventoryUpdate) data) : 
            base(data.state, data.blockId, data.entityId, data.blockHash, data.fuelItemSlot, data.itemStackFactory, data.fuelSettings, data.blockInventoryUpdate)
        {
        }
    }
}