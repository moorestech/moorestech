using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.Miner
{
    public class VanillaGearMinerComponent : IUpdatableBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaMinerProcessorComponent _vanillaMinerProcessorComponent;

        public VanillaGearMinerComponent(VanillaMinerProcessorComponent vanillaMinerProcessorComponent, GearEnergyTransformer gearEnergyTransformer)
        {
            _vanillaMinerProcessorComponent = vanillaMinerProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            _vanillaMinerProcessorComponent.SupplyPower(_gearEnergyTransformer.GetCurrentSuppliedPower().AsPrimitive());
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
