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
    public class ElectricPumpProcessorComponent : IUpdatableBlockComponent, IPumpGenerationState
    {
        private readonly PumpFluidOutputComponent _output;
        private readonly ElectricPower _requiredPower;
        private readonly float _idlePowerRate;
        private readonly List<FluidGenerationEntry> _entries;

        // tick内供給電力の受け皿(Update後0)
        // Holds this tick's supplied power (zeroed after Update)
        private ElectricPower _suppliedPower;

        public bool CanGenerateFluid => PumpFluidGenerationUtility.CanGenerateFluid(_entries, _output);

        // 稼働中は満額、待機中はidlePowerRate倍の実効要求電力
        // The effective request is full while generating and idlePowerRate of it while idle
        public float EffectiveRequestPower => _requiredPower.AsPrimitive() * (CanGenerateFluid ? 1f : _idlePowerRate);

        // state公開用に同一時点で確定させた分子と分母。給電の来ないtickでは分子が0へ落ちる
        // The numerator and denominator latched at one point for the state; the numerator falls to zero on a tick with no supply
        public float CurrentPower { get; private set; }
        public float PublishedRequestPower { get; private set; }

        public ElectricPumpProcessorComponent(ElectricPumpBlockParam param, PumpFluidOutputComponent output, List<FluidGenerationEntry> entries)
        {
            _output = output;
            _requiredPower = new ElectricPower(Mathf.Max(0.0001f, param.RequiredPower));
            _idlePowerRate = param.IdlePowerRate;
            _entries = entries;

            // 初回Update前にstateが読まれても分母が妥当になるよう初期化する
            // Initialize so the denominator is sane even if the state is read before the first Update
            PublishedRequestPower = EffectiveRequestPower;
        }

        // tick内限定の内部経路。供給率から導出済みの実効電力を受け取る
        // Tick-scoped internal path receiving the effective power already derived from the supply rate
        public void SupplyExternalPower(ElectricPower power)
        {
            BlockException.CheckDestroy(this);

            // 複数の電力セグメントから供給され得るため加算する
            // Accumulate power because multiple electric segments may supply this pump
            _suppliedPower += power;
        }

        public void Update()
        {
            BlockException.CheckDestroy(this);

            // 供給電力と、それを算出したのと同じ状態基準の要求電力を同位置で確定する
            // Latch the supplied power together with the request power on the state basis it was derived from
            CurrentPower = Mathf.Max(0f, _suppliedPower.AsPrimitive());
            PublishedRequestPower = EffectiveRequestPower;
            _suppliedPower = new ElectricPower(0);

            var required = Mathf.Max(0.0001f, _requiredPower.AsPrimitive());
            var powerRate = Mathf.Clamp01(CurrentPower / required);

            PumpFluidGenerationUtility.GenerateFluids(_entries, powerRate, _output);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
        }
    }
}
