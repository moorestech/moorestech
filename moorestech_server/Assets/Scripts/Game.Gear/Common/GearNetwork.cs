using System.Collections.Generic;

namespace Game.Gear.Common
{
    public class GearNetwork
    {
        public IReadOnlyList<IGearEnergyTransformer> GearTransformers => _gearTransformers;
        public IReadOnlyList<IGearGenerator> GearGenerators => _gearGenerators;
        public GearNetworkInfo CurrentGearNetworkInfo { get; private set; }

        private readonly List<IGearGenerator> _gearGenerators = new();
        private readonly List<IGearEnergyTransformer> _gearTransformers = new();
        private readonly GearNetworkTopologyCache _topologyCache = new();
        private readonly GearNetworkStableStateCache _stableStateCache = new();
        private readonly GearNetworkDemandCache _demandCache = new();

        private float _cachedTotalGeneratePower;
        private bool _calculationDirty = true;
        private bool _generatorOutputDirty = true;

        public readonly GearNetworkId NetworkId;

        public GearNetwork(GearNetworkId networkId)
        {
            NetworkId = networkId;
        }

        public void AddGear(IGearEnergyTransformer gear)
        {
            var isGenerator = gear is IGearGenerator;
            AddToMemberList(gear);
            if (TryApplyIncrementalAdd(gear, isGenerator)) return;
            MarkFullRebuildRequired();
        }

        public void RemoveGear(IGearEnergyTransformer gear)
        {
            RemoveFromMemberList(gear);
            MarkFullRebuildRequired();
        }

        public void MarkGeneratorOutputDirty()
        {
            _generatorOutputDirty = true;
            _calculationDirty = true;
            _stableStateCache.Invalidate();
        }

        public void ManualUpdate()
        {
            var topologyDirty = _topologyCache.IsDirty;
            if (_stableStateCache.CanSkipUpdate(topologyDirty, _calculationDirty, _generatorOutputDirty)) return;

            var referenceGenerator = GetReferenceGenerator();
            var totalGeneratePower = GetTotalGeneratePower();
            if (referenceGenerator == null)
            {
                StopAsNoPower(totalGeneratePower);
                _topologyCache.MarkCleanWithoutRebuild();
                StoreStableState(totalGeneratePower);
                return;
            }

            EnsureTopologyAndDemandCache(topologyDirty);
            if (totalGeneratePower <= 0f)
            {
                StopAsNoPower(totalGeneratePower);
                StoreStableState(totalGeneratePower);
                return;
            }

            if (!GearNetworkPowerApplicator.TryGetRootClockwise(referenceGenerator, _topologyCache, out var rootClockwise))
            {
                StopAsRocked(totalGeneratePower);
                return;
            }

            // 供給powerに釣り合う最大root RPMを、集約済み消費cacheから探す
            // Find the highest balanced root RPM from the aggregated demand cache.
            var rootRpm = GearNetworkPowerApplicator.FindBalancedRootRpm(_demandCache, totalGeneratePower, rootClockwise);
            if (_topologyCache.IsRocked(rootRpm) || GearNetworkPowerApplicator.HasGeneratorDirectionMismatch(_gearGenerators, rootClockwise, _topologyCache))
            {
                StopAsRocked(totalGeneratePower);
                return;
            }

            var requiredPower = _demandCache.SupplyPowerToTransformers(rootRpm, rootClockwise);
            CurrentGearNetworkInfo = GearNetworkPowerApplicator.SupplyPowerToGeneratorsAndCreateInfo(_gearGenerators, _topologyCache, rootRpm, rootClockwise, requiredPower, totalGeneratePower);
            StoreStableState(totalGeneratePower);
        }

        private bool TryApplyIncrementalAdd(IGearEnergyTransformer gear, bool isGenerator)
        {
            if (!_demandCache.IsBuilt) return false;
            if (!_topologyCache.TryAddConnectedGear(gear)) return false;
            if (!isGenerator) _demandCache.AddTransformer(gear, _topologyCache.GetNode(gear));

            // topologyは維持し、power/RPMだけ次のupdateで必ず再評価する
            // Keep topology clean and force only power/RPM recalculation next update.
            _calculationDirty = true;
            if (isGenerator) _generatorOutputDirty = true;
            _stableStateCache.Invalidate();
            return true;
        }

        private void EnsureTopologyAndDemandCache(bool topologyWasDirty)
        {
            _topologyCache.EnsureBuilt(_gearTransformers, _gearGenerators);
            if (!topologyWasDirty && _demandCache.IsBuilt) return;
            _demandCache.Rebuild(_gearTransformers, _topologyCache);
        }

        private float GetTotalGeneratePower()
        {
            if (!_generatorOutputDirty) return _cachedTotalGeneratePower;
            _cachedTotalGeneratePower = GearNetworkPowerApplicator.CalculateTotalGeneratePower(_gearGenerators);
            return _cachedTotalGeneratePower;
        }

        private void MarkFullRebuildRequired()
        {
            _topologyCache.MarkDirty();
            _demandCache.Invalidate();
            _calculationDirty = true;
            _generatorOutputDirty = true;
            _stableStateCache.Invalidate();
        }

        private void AddToMemberList(IGearEnergyTransformer gear)
        {
            if (gear is IGearGenerator generator) _gearGenerators.Add(generator);
            else _gearTransformers.Add(gear);
        }

        private void RemoveFromMemberList(IGearEnergyTransformer gear)
        {
            if (gear is IGearGenerator generator) _gearGenerators.Remove(generator);
            else _gearTransformers.Remove(gear);
        }

        private IGearGenerator GetReferenceGenerator()
        {
            return _gearGenerators.Count == 0 ? null : _gearGenerators[0];
        }

        private void StopAsNoPower(float totalGeneratePower)
        {
            CurrentGearNetworkInfo = new GearNetworkInfo(0f, totalGeneratePower, 0f, GearNetworkStopReason.None);
            GearNetworkPowerApplicator.StopNetworkComponents(_gearTransformers, _gearGenerators);
        }

        private void StopAsRocked(float totalGeneratePower)
        {
            CurrentGearNetworkInfo = new GearNetworkInfo(0f, totalGeneratePower, 0f, GearNetworkStopReason.Rocked);
            GearNetworkPowerApplicator.StopNetworkComponents(_gearTransformers, _gearGenerators);
            StoreStableState(totalGeneratePower);
        }

        private void StoreStableState(float totalGeneratePower)
        {
            _cachedTotalGeneratePower = totalGeneratePower;
            _generatorOutputDirty = false;
            _calculationDirty = false;
            _stableStateCache.Store();
        }
    }
}
