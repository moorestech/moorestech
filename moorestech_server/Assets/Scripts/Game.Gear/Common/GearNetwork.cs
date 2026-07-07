using System;
using System.Collections.Generic;
using Game.Block.Interface;
using Game.Gear.Tick;

namespace Game.Gear.Common
{
    // 噛み合いでつながったgearの連結成分。需給計算はGearTickUpdater起点のRunTickでのみ行う。
    // 各gearの現在値(実RPM/トルク/向き)は保持せず、符号付き原点RPM比×原点RPMから TryResolveRotation で導出する。
    // A connected component of meshed gears; supply-demand runs only through RunTick. Per-gear values are derived via TryResolveRotation, not stored.
    public class GearNetwork
    {
        public IReadOnlyList<IGearEnergyTransformer> GearTransformers => _gearTransformers;
        public IReadOnlyList<IGearGenerator> GearGenerators => _gearGenerators;
        public readonly GearNetworkId NetworkId;

        // 毎tickの燃料消費等が必要なgeneratorを含むか。GearTickUpdaterの燃料更新対象の判定に使う
        // Whether this network contains generators needing per-tick fuel updates; used by GearTickUpdater
        public bool HasContinuousTickGenerator => _continuousTickGeneratorCount > 0;

        private readonly List<IGearEnergyTransformer> _gearTransformers = new();
        private readonly List<IGearGenerator> _gearGenerators = new();
        private int _continuousTickGeneratorCount;

        // topologyと原点が不変な間再利用するtraversal cache。topology変更・原点交代・generator消失でnull化される
        // Traversal cache reused while topology and origin stay unchanged; nulled on topology change, origin switch, or loss of generators
        private GearNetworkRotationCache _rotationCache;

        public GearNetwork(GearNetworkId networkId)
        {
            NetworkId = networkId;
        }

        // 現在の需給集約情報。実体はGearRuntimeStateStoreのnetwork単位stateから組み立てる
        // Current aggregate supply-demand info, assembled from the store's per-network state
        public GearNetworkInfo CurrentGearNetworkInfo
        {
            get
            {
                var state = GearRuntimeStateStore.Instance.GetNetworkState(NetworkId);
                return new GearNetworkInfo(state.DemandPower, state.AvailablePower, state.NetworkLoadRate, state.StopReason);
            }
        }

        public void AddGear(IGearEnergyTransformer gear)
        {
            switch (gear)
            {
                case IGearGenerator generator:
                    _gearGenerators.Add(generator);
                    if (generator.RequiresContinuousTick) _continuousTickGeneratorCount++;
                    break;
                default:
                    _gearTransformers.Add(gear);
                    break;
            }
        }

        public void RemoveGear(IGearEnergyTransformer gear)
        {
            switch (gear)
            {
                case IGearGenerator generator:
                    _gearGenerators.Remove(generator);
                    if (generator.RequiresContinuousTick) _continuousTickGeneratorCount--;
                    break;
                default:
                    _gearTransformers.Remove(gear);
                    break;
            }
        }

        public void MarkTopologyDirty()
        {
            _rotationCache = null;
        }

        // gearの実RPMと絶対回転方向を符号付き原点RPM比から導出する。停止時はRPM0（向きは維持）。
        // 未計算・cache外・generator不在なら false を返し、呼び出し側はゼロ扱いにする。
        // Derive a gear's actual RPM and absolute direction from its signed ratio; RPM is 0 while stopped (direction kept).
        // Returns false when uncomputed / outside the cache / no generator, so callers treat it as zero.
        public bool TryResolveRotation(BlockInstanceId blockInstanceId, out RPM rpm, out bool isClockwise)
        {
            rpm = new RPM(0);
            isClockwise = true;
            if (_rotationCache == null) return false;
            if (!_rotationCache.TryGetRotation(blockInstanceId, out var rotation)) return false;

            var origin = _rotationCache.Origin;
            isClockwise = rotation.IsSameDirectionAsOrigin ? origin.GenerateIsClockwise : !origin.GenerateIsClockwise;

            var state = GearRuntimeStateStore.Instance.GetNetworkState(NetworkId);
            rpm = state.IsStopped ? new RPM(0) : new RPM(Math.Abs(rotation.SignedRpmRatio) * origin.GenerateRpm.AsPrimitive());
            return true;
        }

        // 1tick分の需給計算。traversal cacheを再構築した場合trueを返す（診断カウンタ用）
        // Run one tick of supply-demand; returns true when the traversal cache was rebuilt (for diagnostics)
        public bool RunTick(GearDemandSnapshot demandSnapshot, GearRuntimeStateStore store)
        {
            // 最も速いgeneratorを原点として選定する
            // Pick the fastest generator as the traversal origin
            IGearGenerator originGenerator = null;
            foreach (var generator in _gearGenerators)
            {
                if (originGenerator == null || generator.GenerateRpm > originGenerator.GenerateRpm) originGenerator = generator;
            }

            // generatorが無い場合は空状態を書き、回転定義を破棄する（gearは導出でゼロになる）
            // Without any generator, write the empty state and drop the rotation cache (gears derive to zero)
            if (originGenerator == null)
            {
                _rotationCache = null;
                store.SetNetworkState(NetworkId, new GearNetworkRuntimeState(false, GearNetworkStopReason.None, 0f, 0f, 0f));
                return false;
            }

            // topology変更または原点交代時のみtraversalを再構築する
            // Rebuild the traversal only when topology changed or the origin generator switched
            var rebuilt = false;
            if (_rotationCache == null || _rotationCache.OriginBlockInstanceId != originGenerator.BlockInstanceId)
            {
                _rotationCache = GearRotationTraversalBuilder.Build(originGenerator);
                rebuilt = true;
            }

            // 符号衝突（逆回転）または現在RPMでの歯車比矛盾があればロック停止する
            // Lock the network on a sign conflict (reverse rotation) or a gear-ratio conflict at the current rpm
            if (_rotationCache.HasDirectionConflict || _rotationCache.IsRpmConflicted(originGenerator.GenerateRpm.AsPrimitive()))
            {
                GearNetworkPowerCalculator.StopNetwork(this, store, GearNetworkStopReason.Rocked, 0f, 0f);
                return rebuilt;
            }

            GearNetworkPowerCalculator.CalculateAndDistribute(this, _rotationCache, demandSnapshot, store);
            return rebuilt;
        }

        // 毎tick駆動が必要なgeneratorの燃料消費・出力更新。確定済みの負荷率を渡す
        // Per-tick fuel consumption and output update for continuous generators, fed the settled load rate
        public void ConsumeGeneratorTicks(GearRuntimeStateStore store)
        {
            var networkLoadRate = store.GetNetworkState(NetworkId).NetworkLoadRate;
            foreach (var generator in _gearGenerators)
            {
                // 同tickの破断sweep等で破壊済みのgeneratorはスキップする（topologyからの除去は次tickのflushで行われる）
                // Skip generators already destroyed this tick (e.g. by the breakage sweep); topology removal happens at the next flush
                if (generator.IsDestroy) continue;
                if (generator.RequiresContinuousTick) generator.ConsumeGeneratorTick(networkLoadRate);
            }
        }

        // テスト互換用の即時再計算。本番経路はGearTickUpdaterのみが入口となる
        // Immediate recalculation kept for test compatibility; production flows only through GearTickUpdater
        public void ManualUpdate()
        {
            RunTick(GearDemandSnapshotUpdater.SharedSnapshot, GearRuntimeStateStore.Instance);
        }
    }
}
