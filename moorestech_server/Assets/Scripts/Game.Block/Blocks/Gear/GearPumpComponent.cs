using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Blocks.Pump;

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

        public GearPumpComponent(List<FluidGenerationEntry> entries, GearEnergyTransformer gearEnergyTransformer, PumpFluidOutputComponent output)
        {
            _gearEnergyTransformer = gearEnergyTransformer;
            _output = output;
            _entries = entries;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 稼働率（RPM比 × torqueRate、下限未満で0）を排出量に乗じる
            // Apply operating rate (rpmRatio × torqueRate, zero below minimum) to fluid generation
            PumpFluidGenerationUtility.GenerateFluids(_entries, _gearEnergyTransformer.GetCurrentOperatingRate(), _output);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
