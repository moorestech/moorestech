using System.Collections.Generic;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    /// Generates fluid based on supplied electric power and pushes it into an inner tank.
    /// </summary>
    public class ElectricPumpProcessorComponent : IUpdatableBlockComponent
    {
        private readonly PumpFluidOutputComponent _output;
        private readonly ElectricPower _requiredPower;
        private readonly List<FluidGenerationEntry> _entries;
        private ElectricPower _currentPower;
        public bool CanGenerateFluid => _entries.Count > 0 && _output.CanAcceptGeneratedFluid;

        public ElectricPumpProcessorComponent(ElectricPumpBlockParam param, PumpFluidOutputComponent output, BlockPositionInfo blockPositionInfo)
        {
            _output = output;
            _requiredPower = new ElectricPower(Mathf.Max(0.0001f, param.RequiredPower));
            _entries = PumpFluidGenerationUtility.ResolveGenerationEntries(param.GenerateFluid.items, blockPositionInfo.OriginalPos);
        }

        public void SupplyPower(ElectricPower power)
        {
            BlockException.CheckDestroy(this);
            _currentPower = power;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            var required = Mathf.Max(0.0001f, _requiredPower.AsPrimitive());
            var supplied = Mathf.Max(0f, _currentPower.AsPrimitive());
            var powerRate = required <= 0f ? 0f : Mathf.Clamp01(supplied / required);

            PumpFluidGenerationUtility.GenerateFluids(_entries, powerRate, _output);

            _currentPower = new ElectricPower(0);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
