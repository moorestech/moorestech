using System;
using Core.Master;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Blocks.Pump;
using Game.EnergySystem;
using Game.Fluid;
using Game.Gear.Common;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

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

            // Calculate power rate based on current RPM and Torque vs requirements
            var requiredRpm = Mathf.Max(0.0001f, _param.RequiredRpm);
            var requiredTorque = Mathf.Max(0.0001f, _param.RequireTorque);
            var supplied = _gearEnergyTransformer.CalcMachineSupplyPower(new RPM(requiredRpm), new Torque(requiredTorque));
            var powerRate = supplied.AsPrimitive() / (requiredRpm * requiredTorque);
            powerRate = Mathf.Clamp01(powerRate);

            // Generate fluids scaled by powerRate
            PumpFluidGenerationUtility.GenerateFluids(
                _param.GenerateFluid.items,
                powerRate,
                _output);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
