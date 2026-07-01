using Game.Block.Blocks.Gear;
using Game.Block.Interface;
using Game.Block.Interface.Component;

namespace Game.Block.Blocks.Machine
{
    /// <summary>
    /// 歯車機械を表すクラス。RPM比で加工速度と消費トルクがスケールする
    /// Gear machine. Processing speed and torque consumption scale by RPM ratio
    /// </summary>
    public class VanillaGearMachineComponent : IUpdatableBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;

        public VanillaGearMachineComponent(VanillaMachineProcessorComponent vanillaMachineProcessorComponent, GearEnergyTransformer gearEnergyTransformer)
        {
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);
            _vanillaMachineProcessorComponent.SupplyPower(_gearEnergyTransformer.GetCurrentSuppliedPower().AsPrimitive());
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            BlockException.CheckDestroy(this);
            IsDestroy = true;
        }
    }
}
