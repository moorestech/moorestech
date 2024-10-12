using Game.Gear.Common;

namespace Game.Block.Blocks.Miner
{
    public class VanillaGearMinerComponent : IGearEnergyTransformer
    {
        
        
        private readonly VanillaMinerProcessorComponent _vanillaMinerProcessorComponent;
        
        public VanillaGearMinerComponent(BlockInstanceId blockInstanceId, ElectricPower requestEnergy, VanillaMinerProcessorComponent vanillaMinerProcessorComponent)
        {
            _vanillaMinerProcessorComponent = vanillaMinerProcessorComponent;
            BlockInstanceId = blockInstanceId;
            RequestEnergy = requestEnergy;
        }
        
        public void SupplyEnergy(ElectricPower power)
        {
            _vanillaMinerProcessorComponent.SupplyEnergy(power);
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}