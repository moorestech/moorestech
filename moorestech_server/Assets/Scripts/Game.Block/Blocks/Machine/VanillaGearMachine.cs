using Core.EnergySystem.Gear;
using Core.Item;
using Game.Block.Blocks.Machine.InventoryController;
using Game.Block.Blocks.Machine.SaveLoad;

namespace Game.Block.Blocks.Machine
{
    public class VanillaGearMachine : VanillaMachineBase, IGearConsumer
    {
        public VanillaGearMachine(
            (int blockId, int entityId, long blockHash, VanillaMachineBlockInventory vanillaMachineBlockInventory,
                VanillaMachineSave vanillaMachineSave, VanillaMachineRunProcess vanillaMachineRunProcess,
                ItemStackFactory itemStackFactory) data)
            : base(data.blockId, data.entityId, data.blockHash, data.vanillaMachineBlockInventory,
                data.vanillaMachineSave, data.vanillaMachineRunProcess, data.itemStackFactory)
        {
        }
    }
}