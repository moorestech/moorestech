using System;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Fluid;
using Game.Gear.Common;
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
        private readonly GearPumpFluidOutputComponent _output;

        //指摘　独自のGearPumpFluidOutputComponentを定義するのではなく、FluidPipeComponentで代用することはできないか？
        public GearPumpComponent(GearPumpBlockParam param, GearEnergyTransformer gearEnergyTransformer, GearPumpFluidOutputComponent output)
        {
            _param = param;
            _gearEnergyTransformer = gearEnergyTransformer;
            _output = output;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // Calculate power rate based on current RPM and Torque vs requirements
            var requiredRpm = new RPM(Math.Max(0.0001f, _param.RequiredRpm));
            var requiredTorque = new Torque(Math.Max(0.0001f, _param.RequireTorque));
            var supplied = _gearEnergyTransformer.CalcMachineSupplyPower(requiredRpm, requiredTorque);
            var requiredPower = requiredRpm.AsPrimitive() * requiredTorque.AsPrimitive();
            var powerRate = requiredPower <= 0 ? 0f : supplied.AsPrimitive() / requiredPower;
            
            //指摘　ここはClampにして
            if (powerRate < 0) powerRate = 0;
            if (powerRate > 1) powerRate = 1;

            // Generate fluids scaled by powerRate
            var dt = (float)GameUpdater.UpdateSecondTime;
            foreach (var gen in _param.GenerateFluid)
            {
                if (gen.GenerateTime <= 0) continue;
                var perSec = gen.Amount / Math.Max(0.0001f, gen.GenerateTime);
                var add = perSec * powerRate * dt;
                if (add <= 0) continue;

                var fluidId = MasterHolder.FluidMaster.GetFluidId(gen.FluidGuid);
                var stack = new FluidStack(add, fluidId);
                _output.Tank.AddLiquid(stack, FluidContainer.Empty);
            }
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}

