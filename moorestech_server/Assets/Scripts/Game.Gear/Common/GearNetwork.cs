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
        private readonly List<GearNetworkSupplyInfo> _transformerSupplyInfos = new();
        private readonly GearNetworkTopologyCache _topologyCache = new();
        private readonly GearNetworkStableStateCache _stableStateCache = new();
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

            MarkFullRebuildRequired();
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
            var topologyDirty = _topologyCache.IsDirty;
            var referenceGenerator = GetReferenceGenerator();
            var totalGeneratePower = GearNetworkPowerApplicator.CalculateTotalGeneratePower(_gearGenerators);
            if (_stableStateCache.CanSkipUpdate(_gearGenerators, referenceGenerator, totalGeneratePower, topologyDirty)) return;

            if (referenceGenerator == null)
            {
                StopAsNoPower(totalGeneratePower);
                _topologyCache.MarkCleanWithoutRebuild();
                StoreStableState(referenceGenerator, totalGeneratePower);
                return;
            }

            if (totalGeneratePower <= 0f)
            {
                _topologyCache.EnsureBuilt(_gearTransformers, _gearGenerators);
                StopAsNoPower(totalGeneratePower);
                StoreStableState(referenceGenerator, totalGeneratePower);
                return;
            }

            _topologyCache.EnsureBuilt(_gearTransformers, _gearGenerators);
            if (!GearNetworkPowerApplicator.TryGetRootClockwise(referenceGenerator, _topologyCache, out var rootClockwise))
            {
                StopAsRocked(referenceGenerator, totalGeneratePower);
                return;
            }

            // 供給可能powerから消費側が釣り合う最大root RPMを探す
            // Find the highest root RPM whose consumer demand fits available power.
            var rootRpm = GearNetworkPowerApplicator.FindBalancedRootRpm(_gearTransformers, _topologyCache, totalGeneratePower, rootClockwise);
            if (_topologyCache.IsRocked(rootRpm) || GearNetworkPowerApplicator.HasGeneratorDirectionMismatch(_gearGenerators, rootClockwise, _topologyCache))
            {
                StopAsRocked(referenceGenerator, totalGeneratePower);
                return;
            }

            var requiredPower = GearNetworkPowerApplicator.BuildTransformerSupplyInfos(_gearTransformers, _topologyCache, _transformerSupplyInfos, rootRpm, rootClockwise);
            CurrentGearNetworkInfo = GearNetworkPowerApplicator.SupplyPowerToNetwork(_gearGenerators, _transformerSupplyInfos, _topologyCache, rootRpm, rootClockwise, requiredPower, totalGeneratePower);
            StoreStableState(referenceGenerator, totalGeneratePower);
        }

        private void MarkFullRebuildRequired()
        {
            _topologyCache.MarkDirty();
            _stableStateCache.Invalidate();
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

        private void StopAsRocked(IGearGenerator referenceGenerator, float totalGeneratePower)
        {
            CurrentGearNetworkInfo = new GearNetworkInfo(0f, totalGeneratePower, 0f, GearNetworkStopReason.Rocked);
            GearNetworkPowerApplicator.StopNetworkComponents(_gearTransformers, _gearGenerators);
            StoreStableState(referenceGenerator, totalGeneratePower);
        }

        private void StoreStableState(IGearGenerator referenceGenerator, float totalGeneratePower)
        {
            _stableStateCache.Store(_gearGenerators, referenceGenerator, totalGeneratePower);
        }
    }
}
