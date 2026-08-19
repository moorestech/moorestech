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
    public class VanillaGearMachineComponent : IUpdatableBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly VanillaMachineProcessorComponent _vanillaMachineProcessorComponent;

        public VanillaGearMachineComponent(VanillaMachineProcessorComponent vanillaMachineProcessorComponent, GearEnergyTransformer gearEnergyTransformer)
        {
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;

            // 加工状態の変化に応じて要求トルク倍率を変更要求する
            // Push the torque request rate whenever the processing state changes
            _vanillaMachineProcessorComponent.OnChangeBlockState.Subscribe(_ => UpdateTorqueRequestRate());
            UpdateTorqueRequestRate();
        }

        // gear網の確定済み供給値を毎tick取り直し、加工判定より前にprocessorへ渡す
        // Re-read the network's settled supply each tick and feed the processor before it processes
        public void Update()
        {
            BlockException.CheckDestroy(this);
            _vanillaMachineProcessorComponent.SupplyExternalPower(_gearEnergyTransformer.GetCurrentSuppliedPower().AsPrimitive());
        }

        private void UpdateTorqueRequestRate()
        {
            // 表示の分母・加工速度と同じ導出（EffectiveRequestPowerRate）をそのまま歯車網への要求へ反映する
            // Feed the gear network the same derivation used for the display denominator and processing speed (EffectiveRequestPowerRate)
            _gearEnergyTransformer.SetTorqueRequestRate(_vanillaMachineProcessorComponent.EffectiveRequestPowerRate);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            BlockException.CheckDestroy(this);
            IsDestroy = true;
        }
    }
}
