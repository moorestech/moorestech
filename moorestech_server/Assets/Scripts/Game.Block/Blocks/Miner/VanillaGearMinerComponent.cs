using Game.Block.Blocks.Gear;
using Game.Gear.Common;

namespace Game.Block.Blocks.Miner
{
    public class VanillaGearMinerComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaMinerProcessorComponent _vanillaMinerProcessorComponent;
        
        public VanillaGearMinerComponent(VanillaMinerProcessorComponent vanillaMinerProcessorComponent)
        {
            _vanillaMinerProcessorComponent = vanillaMinerProcessorComponent;
        }
        
        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}