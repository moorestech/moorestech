using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Blocks.Pump;
using Mooresmaster.Model.BlocksModule;

namespace Game.Block.Blocks.Gear
{
    /// <summary>
    /// Generates fluid into an inner tank based on supplied gear power.
    /// </summary>
    public class GearPumpComponent : IUpdatableBlockComponent
    {
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly PumpFluidOutputComponent _output;
        private readonly List<FluidGenerationEntry> _entries;
        private readonly float _idleTorqueRate;

        public GearPumpComponent(GearPumpBlockParam param, GearEnergyTransformer gearEnergyTransformer, PumpFluidOutputComponent output, BlockPositionInfo blockPositionInfo)
        {
            _gearEnergyTransformer = gearEnergyTransformer;
            _output = output;
            _entries = PumpFluidGenerationUtility.ResolveGenerationEntries(param.GenerateFluid.items, blockPositionInfo.OriginalPos);
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
        }

        private void UpdateTorqueRequestRate()
        {
            // 流体を生成できるかどうかで要求トルク倍率を変更要求する
            // Push the torque request rate based on whether fluid can be generated
            var canGenerateFluid = _entries.Count > 0 && _output.CanAcceptGeneratedFluid;
            _gearEnergyTransformer.SetTorqueRequestRate(canGenerateFluid ? 1f : _idleTorqueRate);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
