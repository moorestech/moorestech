using Game.Block.Blocks.Gear;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;
using UniRx;

namespace Game.Block.Blocks.MapObjectMiner
{
    public class VanillaGearMapObjectMinerComponent : IBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaGearMapObjectMinerProcessorComponent _vanillaGearMapObjectMinerProcessorComponent;
        private readonly GearMapObjectMinerBlockParam _gearMinerBlockParam;

        public VanillaGearMapObjectMinerComponent(GearEnergyTransformer gearEnergyTransformer, GearMapObjectMinerBlockParam gearMinerBlockParam, VanillaGearMapObjectMinerProcessorComponent vanillaGearMapObjectMinerProcessorComponent)
        {
            _gearMinerBlockParam = gearMinerBlockParam;
            _vanillaGearMapObjectMinerProcessorComponent = vanillaGearMapObjectMinerProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
        }

        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            // 基準電力 = baseTorque × baseRpm。稼働率をこれに乗じて採掘プロセッサへ供給
            // Base power = baseTorque × baseRpm. Supply basePower × operatingRate to the miner processor
            var consumption = _gearMinerBlockParam.GearConsumption;
            var basePower = (float)(consumption.BaseTorque * consumption.BaseRpm);
            var operatingRate = _gearEnergyTransformer.CurrentOperatingRate;
            var currentElectricPower = new ElectricPower(basePower * operatingRate);
            _vanillaGearMapObjectMinerProcessorComponent.SupplyPower(currentElectricPower);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
