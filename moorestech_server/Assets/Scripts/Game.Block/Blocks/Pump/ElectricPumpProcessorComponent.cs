using System;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.EnergySystem;
using Game.Fluid;
using Mooresmaster.Model.BlocksModule;
using UnityEngine;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    /// Generates fluid based on supplied electric power and pushes it into an inner tank.
    /// </summary>
    public class ElectricPumpProcessorComponent : IUpdatableBlockComponent
    {
        private readonly ElectricPumpBlockParam _param;
        private readonly PumpFluidOutputComponent _output;
        private readonly ElectricPower _requiredPower;
        private ElectricPower _currentPower;

        public ElectricPumpProcessorComponent(ElectricPumpBlockParam param, PumpFluidOutputComponent output)
        {
            _param = param;
            _output = output;
            _requiredPower = new ElectricPower(Mathf.Max(0.0001f, param.RequiredPower));
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

            // 電力比率に応じて生成テーブルを走査し、内部タンクへ蓄える
            PumpFluidGenerationUtility.GenerateFluids(
                _param.GenerateFluid.items,
                powerRate,
                _output);

            _currentPower = new ElectricPower(0);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
