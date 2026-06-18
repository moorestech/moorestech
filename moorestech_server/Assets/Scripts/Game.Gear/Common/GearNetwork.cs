using System.Collections.Generic;
using UnityEngine;

namespace Game.Gear.Common
{
    public class GearNetwork
    {
        public IReadOnlyList<IGearEnergyTransformer> GearTransformers => _gearTransformers;
        public IReadOnlyList<IGearGenerator> GearGenerators => _gearGenerators;
        public GearNetworkInfo CurrentGearNetworkInfo { get; private set; }

        private readonly List<IGearGenerator> _gearGenerators = new();
        private readonly List<IGearEnergyTransformer> _gearTransformers = new();
        private readonly List<GearNetworkSupplyInfo> _transformerSupplyInfos = new();
        private readonly List<IGearGenerator> _pendingIncrementalGenerators = new();
        private readonly GearNetworkTopologyCache _topologyCache = new();
        private readonly GearNetworkStableStateCache _stableStateCache = new();
        private IGearGenerator _cachedFastestGenerator;
        private float _cachedTotalGeneratePower;
        private bool _canUseIncrementalGeneratorAdd;
        public readonly GearNetworkId NetworkId;

        public GearNetwork(GearNetworkId networkId)
        {
            NetworkId = networkId;
        }

        public void AddGear(IGearEnergyTransformer gear)
        {
            switch (gear)
            {
                case IGearGenerator generator:
                    _gearGenerators.Add(generator);
                    break;
                default:
                    _gearTransformers.Add(gear);
                    break;
            }

            if (TryTrackIncrementalGeneratorAdd(gear)) return;
            MarkFullRebuildRequired();
        }

        private bool TryTrackIncrementalGeneratorAdd(IGearEnergyTransformer gear)
        {
            if (gear is not IGearGenerator generator) return false;
            if (!_canUseIncrementalGeneratorAdd || _cachedFastestGenerator == null) return false;
            if (generator.GenerateRpm > _cachedFastestGenerator.GenerateRpm) return false;
            if (!_topologyCache.TryAddConnectedGear(generator)) return false;

            // 最大RPMが不変なら既存消費は変わらないため、追加generatorだけを保留する
            // If max RPM is unchanged, existing demand is stable, so defer only the added generator.
            _cachedTotalGeneratePower += generator.GenerateTorque.AsPrimitive() * generator.GenerateRpm.AsPrimitive();
            _pendingIncrementalGenerators.Add(generator);
            return true;
        }

        private void MarkFullRebuildRequired()
        {
            _pendingIncrementalGenerators.Clear();
            _canUseIncrementalGeneratorAdd = false;
            _topologyCache.MarkDirty();
            _stableStateCache.Invalidate();
        }

        public void RemoveGear(IGearEnergyTransformer gear)
        {
            switch (gear)
            {
                case IGearGenerator generator:
                    _gearGenerators.Remove(generator);
                    break;
                default:
                    _gearTransformers.Remove(gear);
                    break;
            }

            MarkFullRebuildRequired();
        }

        public void ManualUpdate()
        {
            if (TryApplyIncrementalGeneratorAdds()) return;
            var topologyDirty = _topologyCache.IsDirty;

            // 既存仕様どおり最大RPMのgeneratorを起点にする
            // Keep the existing rule that the highest-RPM generator is the origin.
            var fastestGenerator = GearNetworkPowerApplicator.FindFastestGenerator(_gearGenerators, out var totalGeneratePower);
            if (_stableStateCache.CanSkipUpdate(_gearGenerators, fastestGenerator, totalGeneratePower, topologyDirty)) return;

            if (fastestGenerator == null)
            {
                StopAsEmptyNetwork();
                StoreStableState(null, totalGeneratePower);
                return;
            }

            _topologyCache.EnsureBuilt(_gearTransformers, _gearGenerators);
            var originNode = _topologyCache.GetNode(fastestGenerator);
            var rootRpm = GearNetworkPowerApplicator.CalculateRootRpm(fastestGenerator, originNode);
            var rootClockwise = originNode.GetRootClockwise(fastestGenerator.GenerateIsClockwise);

            // 構造矛盾とgenerator方向矛盾は従来どおりRockedにする
            // Preserve Rocked behavior for topology conflicts and generator direction mismatches.
            if (_topologyCache.IsRocked(rootRpm) || GearNetworkPowerApplicator.HasGeneratorDirectionMismatch(_gearGenerators, fastestGenerator, rootClockwise, _topologyCache))
            {
                CurrentGearNetworkInfo = new GearNetworkInfo(0, 0, 0, GearNetworkStopReason.Rocked);
                StopNetworkComponents();
                StoreStableState(fastestGenerator, totalGeneratePower);
                return;
            }

            var requiredPower = GearNetworkPowerApplicator.BuildTransformerSupplyInfos(_gearTransformers, _topologyCache, _transformerSupplyInfos, rootRpm, rootClockwise);
            if (requiredPower > totalGeneratePower)
            {
                CurrentGearNetworkInfo = new GearNetworkInfo(requiredPower, totalGeneratePower, 0f, GearNetworkStopReason.OverRequirePower);
                StopNetworkComponents();
                StoreStableState(fastestGenerator, totalGeneratePower);
                return;
            }

            CurrentGearNetworkInfo = GearNetworkPowerApplicator.SupplyPowerToNetwork(_gearGenerators, _transformerSupplyInfos, _topologyCache, rootRpm, rootClockwise, requiredPower, totalGeneratePower);
            StoreStableState(fastestGenerator, totalGeneratePower);
        }

        private bool TryApplyIncrementalGeneratorAdds()
        {
            if (_pendingIncrementalGenerators.Count == 0) return false;
            if (!ValidateGeneratorStateForIncrementalAdd()) return false;
            var originNode = _topologyCache.GetNode(_cachedFastestGenerator);
            var rootRpm = GearNetworkPowerApplicator.CalculateRootRpm(_cachedFastestGenerator, originNode);
            var rootClockwise = originNode.GetRootClockwise(_cachedFastestGenerator.GenerateIsClockwise);

            if (_topologyCache.IsRocked(rootRpm) || GearNetworkPowerApplicator.HasGeneratorDirectionMismatch(_pendingIncrementalGenerators, rootClockwise, _topologyCache))
            {
                CurrentGearNetworkInfo = new GearNetworkInfo(0, 0, 0, GearNetworkStopReason.Rocked);
                StopNetworkComponents();
                StoreIncrementalStableState();
                return true;
            }

            var requiredPower = CurrentGearNetworkInfo.TotalRequiredGearPower;
            if (requiredPower > _cachedTotalGeneratePower)
            {
                CurrentGearNetworkInfo = new GearNetworkInfo(requiredPower, _cachedTotalGeneratePower, 0f, GearNetworkStopReason.OverRequirePower);
                StopNetworkComponents();
                StoreIncrementalStableState();
                return true;
            }

            var operationRate = _cachedTotalGeneratePower == 0 ? 0 : Mathf.Min(1, requiredPower / _cachedTotalGeneratePower);
            CurrentGearNetworkInfo = new GearNetworkInfo(requiredPower, _cachedTotalGeneratePower, operationRate, GearNetworkStopReason.None);
            GearNetworkPowerApplicator.SupplyPowerToGenerators(_pendingIncrementalGenerators, _topologyCache, rootRpm, rootClockwise);
            StoreIncrementalStableState();
            return true;
        }

        private bool ValidateGeneratorStateForIncrementalAdd()
        {
            var fastestGenerator = GearNetworkPowerApplicator.FindFastestGenerator(_gearGenerators, out var totalGeneratePower);
            if (fastestGenerator != null &&
                fastestGenerator.BlockInstanceId == _cachedFastestGenerator.BlockInstanceId &&
                Mathf.Abs(totalGeneratePower - _cachedTotalGeneratePower) <= 0.0001f) return true;

            // 既存generator出力が変わった場合は安全側で全体再計算へ戻す
            // If existing generator output changed, fall back to the full recalculation path.
            MarkFullRebuildRequired();
            return false;
        }

        private void StoreIncrementalStableState()
        {
            _stableStateCache.StoreIncrementalGeneratorAdds(_pendingIncrementalGenerators, _cachedFastestGenerator, _cachedTotalGeneratePower);
            _pendingIncrementalGenerators.Clear();
            _canUseIncrementalGeneratorAdd = CurrentGearNetworkInfo.StopReason == GearNetworkStopReason.None && !_topologyCache.IsDirty;
        }

        private void StoreStableState(IGearGenerator fastestGenerator, float totalGeneratePower)
        {
            _cachedFastestGenerator = fastestGenerator;
            _cachedTotalGeneratePower = totalGeneratePower;
            _pendingIncrementalGenerators.Clear();
            _canUseIncrementalGeneratorAdd = fastestGenerator != null && CurrentGearNetworkInfo.StopReason == GearNetworkStopReason.None && !_topologyCache.IsDirty;
            _stableStateCache.Store(_gearGenerators, fastestGenerator, totalGeneratePower);
        }

        private void StopAsEmptyNetwork()
        {
            CurrentGearNetworkInfo = GearNetworkInfo.CreateEmpty();
            GearNetworkPowerApplicator.StopAsEmptyNetwork(_gearTransformers);
        }

        private void StopNetworkComponents()
        {
            GearNetworkPowerApplicator.StopNetworkComponents(_gearTransformers, _gearGenerators);
        }
    }
}
