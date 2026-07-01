using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.MapObjectMiner
{
    public class VanillaGearMapObjectMinerComponent : IUpdatableBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaGearMapObjectMinerProcessorComponent _vanillaGearMapObjectMinerProcessorComponent;

        public VanillaGearMapObjectMinerComponent(GearEnergyTransformer gearEnergyTransformer, VanillaGearMapObjectMinerProcessorComponent vanillaGearMapObjectMinerProcessorComponent)
        {
            _vanillaGearMapObjectMinerProcessorComponent = vanillaGearMapObjectMinerProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            _vanillaGearMapObjectMinerProcessorComponent.SupplyPower(_gearEnergyTransformer.GetCurrentSuppliedPower().AsPrimitive());
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
