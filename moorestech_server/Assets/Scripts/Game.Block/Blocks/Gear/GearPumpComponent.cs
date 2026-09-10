using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Block.Blocks.Pump;
using Mooresmaster.Model.BlocksModule;
using UniRx;

namespace Game.Block.Blocks.Gear
{
    /// <summary>
    ///     供給トルクで流体を生成し、汲み上げ中流体を配信する。電力を持たないためCommonMachineのdetailは持たない
    ///     Generates fluid from the supplied gear power and publishes the pumping fluids; it has no power, hence no CommonMachine detail
    /// </summary>
    public class GearPumpComponent : IUpdatableBlockComponent, IBlockStateObservable, IBlockStateDetail
    {
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        private readonly Subject<Unit> _onChangeBlockState = new();
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly PumpFluidOutputComponent _output;
        private readonly List<FluidGenerationEntry> _entries;
        private readonly float _idleTorqueRate;

        private bool _wasGenerating;

        public bool CanGenerateFluid => PumpFluidGenerationUtility.CanGenerateFluid(_entries, _output);

        public GearPumpComponent(GearPumpBlockParam param, GearEnergyTransformer gearEnergyTransformer, PumpFluidOutputComponent output, List<FluidGenerationEntry> entries)
        {
            _gearEnergyTransformer = gearEnergyTransformer;
            _output = output;
            _entries = entries;
            _idleTorqueRate = param.GearConsumption.IdlePowerRate;

            UpdateTorqueRequestRate();
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 稼働率（RPM比 × torqueRate、下限未満で0）を排出量に乗じる
            // Apply operating rate (rpmRatio × torqueRate, zero below minimum) to fluid generation
            PumpFluidGenerationUtility.GenerateFluids(_entries, _gearEnergyTransformer.GetCurrentOperatingRate(), _output);

            UpdateTorqueRequestRate();
            CheckStateAndInvokeEventUpdate();

            #region Internal

            // 生成中は毎tick、待機へ落ちた直後に1回発火する（採掘機と同じ節度）
            // Fires every tick while generating and once on the drop to idle (the miner's cadence)
            void CheckStateAndInvokeEventUpdate()
            {
                var isGenerating = CanGenerateFluid;
                if (isGenerating || _wasGenerating) _onChangeBlockState.OnNext(Unit.Default);
                _wasGenerating = isGenerating;
            }

            #endregion
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            return new[] { PumpStateDetailFactory.CreatePumpDetail(_entries) };
        }

        private void UpdateTorqueRequestRate()
        {
            // 流体を生成できるかどうかで要求トルク倍率を変更要求する
            // Push the torque request rate based on whether fluid can be generated
            _gearEnergyTransformer.SetTorqueRequestRate(CanGenerateFluid ? 1f : _idleTorqueRate);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
            _onChangeBlockState.Dispose();
        }
    }
}
