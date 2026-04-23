using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;
using UniRx;

namespace Game.Block.Blocks.Machine
{
    // 歯車機械。RPM比で加工速度と消費トルクがスケールする
    // Gear machine. Processing speed and torque consumption scale by RPM ratio
    public class VanillaGearMachineComponent : IBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;
        private readonly GearMachineBlockParam _gearMachineBlockParam;

        public VanillaGearMachineComponent(VanillaMachineProcessorComponent vanillaMachineProcessorComponent, GearEnergyTransformer gearEnergyTransformer, GearMachineBlockParam gearMachineBlockParam)
        {
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
            _gearMachineBlockParam = gearMachineBlockParam;

            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
        }

        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            // 基準電力 = baseTorque × baseRpm。稼働率をこれに乗じて機械プロセッサへ供給
            // Base power = baseTorque × baseRpm. Supply basePower × operatingRate to the machine processor
            var consumption = _gearMachineBlockParam.GearConsumption;
            var basePower = (float)(consumption.BaseTorque * consumption.BaseRpm);
            var operatingRate = _gearEnergyTransformer.CurrentOperatingRate;
            var currentElectricPower = new ElectricPower(basePower * operatingRate);
            _vanillaMachineProcessorComponent.SupplyPower(currentElectricPower);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            BlockException.CheckDestroy(this);
            IsDestroy = true;
        }
    }
}
