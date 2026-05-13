using Game.Block.Blocks.Gear;
using Game.Block.Interface.Component;
using UniRx;

namespace Game.Block.Blocks.MapObjectMiner
{
    public class VanillaGearMapObjectMinerComponent : IBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaGearMapObjectMinerProcessorComponent _vanillaGearMapObjectMinerProcessorComponent;

        public VanillaGearMapObjectMinerComponent(GearEnergyTransformer gearEnergyTransformer, VanillaGearMapObjectMinerProcessorComponent vanillaGearMapObjectMinerProcessorComponent)
        {
            _vanillaGearMapObjectMinerProcessorComponent = vanillaGearMapObjectMinerProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
        }

        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            _vanillaGearMapObjectMinerProcessorComponent.SupplyPower(_gearEnergyTransformer.GetCurrentSuppliedPower().AsPrimitive());
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
