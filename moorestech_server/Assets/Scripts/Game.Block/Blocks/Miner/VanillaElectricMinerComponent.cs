using Game.Block.Interface;
using Game.EnergySystem;

namespace Game.Block.Blocks.Miner
{
    public class VanillaElectricMinerComponent : IElectricConsumer
    {
        public BlockInstanceId BlockInstanceId { get; }
        public ElectricPower RequestEnergy => new(_vanillaMinerProcessorComponent.RequestEnergy);
        
        private readonly VanillaMinerProcessorComponent _vanillaMinerProcessorComponent;
        
        public VanillaElectricMinerComponent(BlockInstanceId blockInstanceId, VanillaMinerProcessorComponent vanillaMinerProcessorComponent)
        {
            _vanillaMinerProcessorComponent = vanillaMinerProcessorComponent;
            BlockInstanceId = blockInstanceId;
        }
        
        public void SupplyEnergy(ElectricPower power)
        {
            _vanillaMinerProcessorComponent.SupplyPower(power.AsPrimitive());
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
