using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    public class GearNetwork
    {
        private const int UninitializedSignature = int.MinValue;

        public readonly GearNetworkId NetworkId;
        public IReadOnlyList<IGearEnergyTransformer> GearTransformers => _gearTransformers;
        public IReadOnlyList<IGearGenerator> GearGenerators => _gearGenerators;
        public GearNetworkInfo CurrentGearNetworkInfo { get; private set; }
        public bool RequiresDemandSnapshotRefresh { get; private set; } = true;

        private readonly Dictionary<BlockInstanceId, GearRotationInfo> _rotationInfos = new();
        private readonly List<IGearEnergyTransformer> _gearTransformers = new();
        private readonly List<IGearGenerator> _gearGenerators = new();
        private bool _topologyDirty = true;
        private bool _rotationCacheValid;
        private IGearGenerator _originGenerator;
        private int _lastOutputSignature = UninitializedSignature;
        private float _lastNetworkLoadRate;

        public GearNetwork(GearNetworkId networkId)
        {
            NetworkId = networkId;
        }

        public void AddGear(IGearEnergyTransformer gear)
        {
            if (gear is IGearGenerator generator) _gearGenerators.Add(generator);
            else _gearTransformers.Add(gear);
            MarkTopologyDirty();
        }

        public void RemoveGear(IGearEnergyTransformer gear)
        {
            if (gear is IGearGenerator generator) _gearGenerators.Remove(generator);
            else _gearTransformers.Remove(gear);
            _rotationInfos.Remove(gear.BlockInstanceId);
            MarkTopologyDirty();
        }

        internal void UpdateNetwork(GearDemandSnapshotStore demandStore, GearRuntimeStateStore runtimeStore)
        {
            var outputSignature = GearNetworkPowerCalculator.CreateGeneratorOutputSignature(_gearGenerators);
            if (CanReusePreviousSupply(outputSignature))
            {
                ConsumeFuelGenerators(_lastNetworkLoadRate);
                return;
            }

            if (_gearGenerators.Count == 0)
            {
                MarkNoGeneratorCacheClean();
                WriteNoGeneratorState(runtimeStore);
                _lastOutputSignature = outputSignature;
                return;
            }

            if (!EnsureRotationCache())
            {
                var stoppedAvailablePower = GearNetworkPowerCalculator.CalculateAvailablePower(_gearGenerators);
                WriteStoppedState(runtimeStore, 0f, stoppedAvailablePower, GearNetworkStopReason.Rocked);
                ConsumeFuelGenerators(0f);
                _lastOutputSignature = outputSignature;
                return;
            }

            var demandPower = GearNetworkPowerCalculator.CalculateDemandPower(_gearTransformers, _rotationInfos, demandStore);
            var availablePower = GearNetworkPowerCalculator.CalculateAvailablePower(_gearGenerators);
            if (demandPower > availablePower)
            {
                WriteStoppedState(runtimeStore, demandPower, availablePower, GearNetworkStopReason.OverRequirePower);
                ConsumeFuelGenerators(0f);
                _lastOutputSignature = outputSignature;
                return;
            }

            var loadRate = demandPower == 0f || availablePower == 0f ? 0f : demandPower / availablePower;
            WriteSuppliedState(runtimeStore, demandPower, availablePower, loadRate);
            ConsumeFuelGenerators(loadRate);
            _lastOutputSignature = outputSignature;
        }

        public void MarkDemandSnapshotRefreshed()
        {
            RequiresDemandSnapshotRefresh = false;
        }

        private void MarkTopologyDirty()
        {
            _topologyDirty = true;
            _rotationCacheValid = false;
            RequiresDemandSnapshotRefresh = true;
            _lastOutputSignature = UninitializedSignature;
        }

        private void MarkNoGeneratorCacheClean()
        {
            _originGenerator = null;
            _rotationInfos.Clear();
            _topologyDirty = false;
            _rotationCacheValid = false;
        }

        private bool CanReusePreviousSupply(int outputSignature)
        {
            if (_topologyDirty) return false;
            if (_lastOutputSignature == UninitializedSignature) return false;
            return _lastOutputSignature == outputSignature;
        }

        private bool EnsureRotationCache()
        {
            var fastest = SelectFastestGenerator();
            if (!_topologyDirty && _originGenerator == fastest && _rotationCacheValid)
            {
                RefreshRpmCache(fastest);
                return true;
            }

            _originGenerator = fastest;
            _rotationCacheValid = GearNetworkRotationCacheBuilder.Rebuild(_rotationInfos, fastest);
            _topologyDirty = false;
            if (!_rotationCacheValid) return false;

            RefreshRpmCache(fastest);
            return true;
        }

        private IGearGenerator SelectFastestGenerator()
        {
            IGearGenerator fastest = null;
            foreach (var generator in _gearGenerators)
            {
                if (fastest == null || generator.GenerateRpm > fastest.GenerateRpm) fastest = generator;
            }
            return fastest;
        }

        private void RefreshRpmCache(IGearGenerator origin)
        {
            foreach (var info in _rotationInfos.Values)
            {
                info.SetRpm(origin.GenerateRpm * info.RpmRatio);
            }
        }

        private void WriteSuppliedState(GearRuntimeStateStore runtimeStore, float demandPower, float availablePower, float loadRate)
        {
            CurrentGearNetworkInfo = new GearNetworkInfo(demandPower, availablePower, loadRate, GearNetworkStopReason.None);
            GearNetworkRuntimeStateWriter.WriteSupplied(CreateWriteContext(runtimeStore, demandPower, availablePower, loadRate));
            _lastNetworkLoadRate = loadRate;
        }

        private void WriteStoppedState(GearRuntimeStateStore runtimeStore, float demandPower, float availablePower, GearNetworkStopReason stopReason)
        {
            CurrentGearNetworkInfo = new GearNetworkInfo(demandPower, availablePower, 0f, stopReason);
            GearNetworkRuntimeStateWriter.WriteStopped(CreateWriteContext(runtimeStore, demandPower, availablePower, 0f), stopReason);
            _lastNetworkLoadRate = 0f;
        }

        private void WriteNoGeneratorState(GearRuntimeStateStore runtimeStore)
        {
            CurrentGearNetworkInfo = GearNetworkInfo.CreateEmpty();
            GearNetworkRuntimeStateWriter.WriteNoGenerator(CreateWriteContext(runtimeStore, 0f, 0f, 0f));
            _lastNetworkLoadRate = 0f;
        }

        private GearNetworkWriteContext CreateWriteContext(GearRuntimeStateStore runtimeStore, float demandPower, float availablePower, float loadRate)
        {
            return new GearNetworkWriteContext(NetworkId, _gearTransformers, _gearGenerators, _rotationInfos, runtimeStore, demandPower, availablePower, loadRate);
        }

        private void ConsumeFuelGenerators(float networkLoadRate)
        {
            foreach (var generator in _gearGenerators)
            {
                if (generator is IGearTickFuelConsumer fuelGenerator)
                {
                    fuelGenerator.UpdateFromGearTick(networkLoadRate);
                }
            }
        }
    }
}
