using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.State;
using Game.Block.Blocks.Machine;
using Game.Block.Blocks.Util;
using Game.EnergySystem;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Blocks.Pump
{
    /// <summary>
    ///     供給電力で流体を生成し、その電力・要求電力・稼働ラベルを同一のラッチ値から配信する（ADR 0010/0051）
    ///     Generates fluid from the supplied power and publishes that power, the request and the state label from one latched value (ADR 0010/0051)
    /// </summary>
    public class ElectricPumpProcessorComponent : IUpdatableBlockComponent, IBlockStateObservable, IBlockStateDetail
    {
        public IObservable<Unit> OnChangeBlockState => _onChangeBlockState;

        // 稼働中は稼働率の上限倍、待機中はidlePowerRate倍の実効要求電力
        // The effective request is the demand cap of the full power while generating and idlePowerRate of it while idle
        public float EffectiveRequestPower => _requiredPower.AsPrimitive() * (IsGenerating(_demandRate) ? _demandRate : _idlePowerRate);

        private readonly Subject<Unit> _onChangeBlockState = new();
        private readonly PumpFluidOutputComponent _output;
        private readonly ElectricPower _requiredPower;
        private readonly float _idlePowerRate;
        private readonly List<FluidGenerationEntry> _entries;

        // tick内供給電力の受け皿
        // Accumulates this tick's supply
        private ElectricPower _suppliedPower;

        // 次tickの電力要求の基準となる稼働率の上限（0なら待機）。Updateの末尾で確定する
        // The demand cap that bases the next tick's request (zero means idle), latched at the end of Update
        private float _demandRate;

        // 分子・分母・稼働率は同じ基準で同時にラッチする（前例 MachineProcessContext.LatchTickPower）
        // The numerator, the denominator and the demand cap are latched together on one basis (precedent: MachineProcessContext.LatchTickPower)
        private PublishedPowerLatch _publishedPower;
        private float _publishedDemandRate;

        public ElectricPumpProcessorComponent(ElectricPumpBlockParam param, PumpFluidOutputComponent output, List<FluidGenerationEntry> entries)
        {
            _output = output;
            _requiredPower = new ElectricPower(Mathf.Max(0.0001f, param.RequiredPower));
            _idlePowerRate = param.IdlePowerRate;
            _entries = entries;

            // 設置直後の1tick目から正しい電力を要求し、初回Update前のstate読み出しでも分母が妥当になるようにする
            // Latch the initial state so the first tick requests the right power and the denominator is sane even if the state is read before the first Update
            _demandRate = PumpFluidGenerationUtility.GenerationDemandRate(_entries, _output);
            _publishedDemandRate = _demandRate;
            _publishedPower = new PublishedPowerLatch(0f, EffectiveRequestPower);
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

            // 供給電力と、それを要求した基準（_demandRate）から導く分母・稼働率を同位置で確定する
            // Latch the supplied power together with the denominator and cap derived from _demandRate, the basis that requested it
            var previousPublishedPower = _publishedPower;
            var wasPublishedGenerating = IsGenerating(_publishedDemandRate);
            _publishedPower = new PublishedPowerLatch(Mathf.Max(0f, _suppliedPower.AsPrimitive()), EffectiveRequestPower);
            _publishedDemandRate = _demandRate;
            _suppliedPower = new ElectricPower(0);

            GenerateFluid();
            CheckStateAndInvokeEventUpdate();

            // 今tickの配信を終えてから、次tickの要求の基準を確定する
            // Latch the basis for the next tick's request only after this tick has been published
            _demandRate = PumpFluidGenerationUtility.GenerationDemandRate(_entries, _output);

            #region Internal

            void GenerateFluid()
            {
                if (!IsGenerating(_publishedDemandRate)) return;

                // 要求電力と同じ稼働率の上限を生成にも掛け、過剰供給があっても搬出できる分しか生成しない
                // Apply the same demand cap as the request to generation, so even an oversupply only generates what can drain
                var supplyRate = Mathf.Clamp01(_publishedPower.CurrentPower / _requiredPower.AsPrimitive());
                PumpFluidGenerationUtility.GenerateFluids(_entries, Mathf.Min(supplyRate, _publishedDemandRate), _output);
            }

            // 生成中は毎tick、待機中は配信値が動いたtickだけ発火する（採掘機の節度＋給電断の通知）
            // Fires every tick while generating and, while idle, only on ticks where a published value moved (the miner's cadence plus a supply-loss notice)
            void CheckStateAndInvokeEventUpdate()
            {
                var publishedPowerMoved = _publishedPower.MovedFrom(previousPublishedPower);
                if (!IsGenerating(_publishedDemandRate) && !wasPublishedGenerating && !publishedPowerMoved) return;

                _onChangeBlockState.OnNext(Unit.Default);
            }

            #endregion
        }

        public BlockStateDetail[] GetBlockStateDetails()
        {
            BlockException.CheckDestroy(this);

            // 稼働状態は「稼働率の上限 > 0（汲み上げ対象あり ∧（空きあり ∨ 直前tickに搬出あり））」の2値。停止中は無い
            // The state is binary: generating while the demand cap is positive (targets exist and there is room or the previous tick drained); there is no halted state
            var stateType = IsGenerating(_publishedDemandRate) ? VanillaMachineBlockStateConst.ProcessingState : VanillaMachineBlockStateConst.IdleState;
            var common = new CommonMachineBlockStateDetail(_publishedPower.CurrentPower, _publishedPower.RequestPower, 0f, stateType, stateType);

            return new[]
            {
                new BlockStateDetail(CommonMachineBlockStateDetail.BlockStateDetailKey, MessagePackSerializer.Serialize(common)),
                PumpStateDetailFactory.CreatePumpDetail(_entries),
            };
        }

        private static bool IsGenerating(float demandRate)
        {
            return 0f < demandRate;
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            IsDestroy = true;
            _onChangeBlockState.Dispose();
        }
    }
}
