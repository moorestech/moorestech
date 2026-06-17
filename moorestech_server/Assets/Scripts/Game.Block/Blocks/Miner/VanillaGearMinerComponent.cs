using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using UniRx;

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
            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
        }

        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            SupplyCurrentGearPower();
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 安定ネットワークではgear通知が来ないため、processorへ継続供給だけ再投入する
            // Stable gear networks skip update events, so keep feeding the processor from cached gear state
            SupplyCurrentGearPower();
        }

        private void SupplyCurrentGearPower()
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
