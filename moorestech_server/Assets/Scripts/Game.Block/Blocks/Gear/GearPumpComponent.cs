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
        private readonly GearPumpBlockParam _param;
        private readonly GearEnergyTransformer _gearEnergyTransformer;
        private readonly PumpFluidOutputComponent _output;

        public GearPumpComponent(GearPumpBlockParam param, GearEnergyTransformer gearEnergyTransformer, PumpFluidOutputComponent output)
        {
            _param = param;
            _gearEnergyTransformer = gearEnergyTransformer;
            _output = output;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 稼働率（RPM比 × torqueRate、下限未満で0）を排出量に乗じる
            // Apply operating rate (rpmRatio × torqueRate, zero below minimum) to fluid generation
            PumpFluidGenerationUtility.GenerateFluids(
                _param.GenerateFluid.items,
                _gearEnergyTransformer.GetCurrentOperatingRate(),
                _output);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
