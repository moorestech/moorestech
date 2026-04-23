using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using UniRx;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    /// 歯車機械を表すクラス。RPM比で加工速度と消費トルクがスケールする
    /// Gear machine. Processing speed and torque consumption scale by RPM ratio
    /// </summary>
    public class VanillaGearMachineComponent : IBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;

        public VanillaGearMachineComponent(VanillaMachineProcessorComponent vanillaMachineProcessorComponent, GearEnergyTransformer gearEnergyTransformer)
        {
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;

            _gearEnergyTransformer.OnGearUpdate.Subscribe(OnGearUpdate);
        }

        private void OnGearUpdate(GearUpdateType gearUpdateType)
        {
            _vanillaMachineProcessorComponent.SupplyPower(_gearEnergyTransformer.GetCurrentSuppliedPower());
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            BlockException.CheckDestroy(this);
            IsDestroy = true;
        }
    }
}
