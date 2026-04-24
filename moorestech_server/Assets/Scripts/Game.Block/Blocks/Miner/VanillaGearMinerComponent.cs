using Game.Block.Blocks.Gear;
using Game.Block.Interface.Component;
using UniRx;

namespace Game.Block.Blocks.Miner
{
    public class VanillaGearMinerComponent : IBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaMinerProcessorComponent _vanillaMinerProcessorComponent;

        public VanillaGearMinerComponent(VanillaMinerProcessorComponent vanillaMinerProcessorComponent, GearEnergyTransformer gearEnergyTransformer)
        {
            _vanillaMinerProcessorComponent = vanillaMinerProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
        }

        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            _vanillaMinerProcessorComponent.SupplyPower(_gearEnergyTransformer.GetCurrentSuppliedPower().AsPrimitive());
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
