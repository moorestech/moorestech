using Game.Block.Interface;
using Game.EnergySystem;

namespace Game.Block.Blocks.Miner
{
    public class VanillaElectricMinerComponent : IElectricConsumer
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy { get; }
        
        private readonly VanillaMinerProcessorComponent _vanillaMinerProcessorComponent;
        
        public VanillaElectricMinerComponent(BlockInstanceId blockInstanceId, ElectricPower requestEnergy, VanillaMinerProcessorComponent vanillaMinerProcessorComponent)
        {
            _vanillaMinerProcessorComponent = vanillaMinerProcessorComponent;
            BlockInstanceId = blockInstanceId;
            RequestEnergy = requestEnergy;
        }
        
        public void SupplyEnergy(ElectricPower power)
        {
            _vanillaMinerProcessorComponent.SupplyPower(power);
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}