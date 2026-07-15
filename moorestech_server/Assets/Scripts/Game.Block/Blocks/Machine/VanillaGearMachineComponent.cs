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
        private readonly float _idleTorqueRate;

        public VanillaGearMachineComponent(VanillaMachineProcessorComponent vanillaMachineProcessorComponent, GearEnergyTransformer gearEnergyTransformer, float idleTorqueRate)
        {
            _vanillaMachineProcessorComponent = vanillaMachineProcessorComponent;
            _gearEnergyTransformer = gearEnergyTransformer;
            _idleTorqueRate = idleTorqueRate;

            // 加工状態の変化に応じて要求トルク倍率を変更要求する
            // Push the torque request rate whenever the processing state changes
            _vanillaMachineProcessorComponent.OnChangeBlockState.Subscribe(_ => UpdateTorqueRequestRate());
            UpdateTorqueRequestRate();
        }

        // GearRuntimeStateStore由来の現在供給値を毎tick取り直し、加工判定より前にprocessorへ渡す
        // Re-read the current supply from GearRuntimeStateStore each tick and feed the processor before it processes
        public void Update()
        {
            BlockException.CheckDestroy(this);
            _vanillaMachineProcessorComponent.SupplyPower(_gearEnergyTransformer.GetCurrentSuppliedPower().AsPrimitive());
        }

        private void UpdateTorqueRequestRate()
        {
            var isProcessing = _vanillaMachineProcessorComponent.CurrentState == ProcessState.Processing;
            _gearEnergyTransformer.SetTorqueRequestRate(isProcessing ? 1f : _idleTorqueRate);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            BlockException.CheckDestroy(this);
            IsDestroy = true;
        }
    }
}
