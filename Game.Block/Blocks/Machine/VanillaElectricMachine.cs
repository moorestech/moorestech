using Game.Block.Blocks.Machine.InventoryController;
using Game.Block.Blocks.Machine.SaveLoad;
using Core.EnergySystem.Electric;
using Core.Item;

namespace Game.Block.Blocks.Machine
{
    public class VanillaElectricMachine : VanillaMachineBase, IBlockElectricConsumer
    {
        public VanillaElectricMachine((int blockId, int entityId, ulong blockHash, VanillaMachineBlockInventory vanillaMachineBlockInventory, VanillaMachineSave vanillaMachineSave, VanillaMachineRunProcess vanillaMachineRunProcess, ItemStackFactory itemStackFactory) data) 
            : base(data.blockId, data.entityId, data.blockHash, data.vanillaMachineBlockInventory, data.vanillaMachineSave,data. vanillaMachineRunProcess, data.itemStackFactory)
        {
        }
    }
}