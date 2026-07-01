using System.Collections.Generic;
using Game.Block.Interface;
using UnityEngine;
namespace Game.Gear.Common
{
    public class GearNetwork
    {
        public IReadOnlyList<IGearEnergyTransformer> GearTransformers => _gearTransformers;
        public IReadOnlyList<IGearGenerator> GearGenerators => _gearGenerators;
        public GearNetworkInfo CurrentGearNetworkInfo { get; private set; }
        private readonly Dictionary<BlockInstanceId, GearRotationInfo> _rotationCache = new();
        private readonly List<IGearGenerator> _gearGenerators = new();
        private readonly List<IGearEnergyTransformer> _gearTransformers = new();
        private BlockInstanceId _originGeneratorId;
        private bool _hasOriginGenerator;
        private bool _needsRotationRebuild = true;
        public readonly GearNetworkId NetworkId;
        public GearNetwork(GearNetworkId networkId)
        {
            NetworkId = networkId;
            CurrentGearNetworkInfo = GearNetworkInfo.CreateEmpty();
        }
        public void AddGear(IGearEnergyTransformer gear)
        {
            if (gear is IGearGenerator generator) _gearGenerators.Add(generator);
            else _gearTransformers.Add(gear);
            _needsRotationRebuild = true;
        }
        public void RemoveGear(IGearEnergyTransformer gear)
        {
            if (gear is IGearGenerator generator) _gearGenerators.Remove(generator);
            else _gearTransformers.Remove(gear);
            _needsRotationRebuild = true;
        }
        public void ManualUpdate()
        {
            var demandStore = GearDemandSnapshotStore.GetOrCreateForManualUpdate();
            var runtimeStore = GearRuntimeStateStore.GetOrCreateForManualUpdate();
            demandStore.Clear();

            // 旧テスト用の直接更新でもsnapshot経由を必ず通す。
            // Direct test updates still go through the snapshot path.
            foreach (var transformer in _gearTransformers)
            {
                demandStore.SetSnapshot(GearDemandSnapshot.Enabled(transformer.BlockInstanceId));
            }

            Update(demandStore, runtimeStore);
        }
        public void Update(GearDemandSnapshotStore demandStore, GearRuntimeStateStore runtimeStore)
        {
            var originGenerator = FindFastestGenerator();
            if (originGenerator == null)
            {
                CurrentGearNetworkInfo = GearNetworkStateApplier.ApplyNoGeneratorState(NetworkId, _gearTransformers, runtimeStore);
                return;
            }
            // topologyか起点generatorが変わった時だけ回転キャッシュを作り直す。
            // Rebuild rotation cache only when topology or the origin generator changes.
            if (!EnsureRotationCache(originGenerator, runtimeStore))
            {
                GearNetworkFuelUpdater.UpdateFuelGenerators(_gearGenerators, 0f);
                return;
            }
            var totalDemandPower = CalculateDemandPower(originGenerator, demandStore);
            var totalAvailablePower = CalculateAvailablePower();
            if (totalDemandPower > totalAvailablePower)
            {
                CurrentGearNetworkInfo = GearNetworkStateApplier.ApplyStoppedState(NetworkId, _gearTransformers, _gearGenerators, runtimeStore, totalDemandPower, totalAvailablePower, GearNetworkStopReason.OverRequirePower);
                GearNetworkFuelUpdater.UpdateFuelGenerators(_gearGenerators, 0f);
                return;
            }
            var networkLoadRate = totalDemandPower == 0f || totalAvailablePower == 0f ? 0f : Mathf.Min(1f, totalDemandPower / totalAvailablePower);
            CurrentGearNetworkInfo = new GearNetworkInfo(totalDemandPower, totalAvailablePower, networkLoadRate, GearNetworkStopReason.None);
            runtimeStore.SetNetworkState(new GearNetworkRuntimeState(NetworkId, totalDemandPower, totalAvailablePower, networkLoadRate, false, GearNetworkStopReason.None));

            // supply確定後にruntime stateと旧component通知を同期する。
            // After supply is decided, synchronize runtime state and legacy component notifications.
            GearNetworkStateApplier.ApplySupplyState(NetworkId, _gearTransformers, _gearGenerators, _rotationCache, runtimeStore, demandStore, originGenerator, totalDemandPower, totalAvailablePower);
            GearNetworkFuelUpdater.UpdateFuelGenerators(_gearGenerators, networkLoadRate);
        }
        private IGearGenerator FindFastestGenerator()
        {
            IGearGenerator fastest = null;
            foreach (var generator in _gearGenerators)
            {
                if (fastest == null || generator.GenerateRpm > fastest.GenerateRpm) fastest = generator;
            }

            return fastest;
        }

        private bool EnsureRotationCache(IGearGenerator originGenerator, GearRuntimeStateStore runtimeStore)
        {
            var originChanged = !_hasOriginGenerator || _originGeneratorId != originGenerator.BlockInstanceId;
            if (!_needsRotationRebuild && !originChanged) return true;

            _rotationCache.Clear();
            _originGeneratorId = originGenerator.BlockInstanceId;
            _hasOriginGenerator = true;
            var originInfo = new GearRotationInfo(1f, originGenerator.GenerateIsClockwise, originGenerator);
            _rotationCache.Add(originGenerator.BlockInstanceId, originInfo);

            foreach (var connect in originGenerator.GetGearConnects())
            {
                if (BuildRotationCache(connect, originInfo))
                {
                    CurrentGearNetworkInfo = GearNetworkStateApplier.ApplyStoppedState(NetworkId, _gearTransformers, _gearGenerators, runtimeStore, 0f, 0f, GearNetworkStopReason.Rocked);
                    _needsRotationRebuild = true;
                    return false;
                }
            }

            _needsRotationRebuild = false;
            return true;
        }

        private bool BuildRotationCache(GearConnect gearConnect, GearRotationInfo connectedInfo)
        {
            var transformer = gearConnect.Transformer;
            var isClockwise = IsReverseRotation(gearConnect) ? !connectedInfo.IsClockwise : connectedInfo.IsClockwise;
            var rpmRatio = CalculateRpmRatio(gearConnect, connectedInfo);

            // 既存経路と矛盾する回転ならnetworkをrockedにする。
            // If another path disagrees, the network is rocked.
            if (_rotationCache.TryGetValue(transformer.BlockInstanceId, out var cachedInfo))
            {
                return cachedInfo.IsClockwise != isClockwise || Mathf.Abs(cachedInfo.RpmRatio - rpmRatio) > 0.0001f;
            }

            if (transformer is IGearGenerator generator &&
                generator.GenerateIsClockwise != isClockwise &&
                generator.BlockInstanceId != _originGeneratorId)
            {
                return true;
            }

            var currentInfo = new GearRotationInfo(rpmRatio, isClockwise, transformer);
            _rotationCache.Add(transformer.BlockInstanceId, currentInfo);
            foreach (var connect in transformer.GetGearConnects())
            {
                if (BuildRotationCache(connect, currentInfo)) return true;
            }

            return false;
        }

        private static bool IsReverseRotation(GearConnect connect)
        {
            return connect.Self.IsReverse && connect.Target.IsReverse;
        }

        private static float CalculateRpmRatio(GearConnect connect, GearRotationInfo connectedInfo)
        {
            if (connect.Transformer is IGear gear &&
                connectedInfo.EnergyTransformer is IGear connectedGear &&
                IsReverseRotation(connect))
            {
                return connectedInfo.RpmRatio * (float)connectedGear.TeethCount / gear.TeethCount;
            }

            return connectedInfo.RpmRatio;
        }

        private float CalculateDemandPower(IGearGenerator originGenerator, GearDemandSnapshotStore demandStore)
        {
            var totalDemandPower = 0f;
            foreach (var transformer in _gearTransformers)
            {
                if (!_rotationCache.TryGetValue(transformer.BlockInstanceId, out var info)) continue;
                var snapshot = demandStore.GetSnapshot(transformer.BlockInstanceId);
                if (!snapshot.DemandEnabled) continue;

                var rpm = info.GetRpm(originGenerator.GenerateRpm);
                var requiredTorque = transformer.GetRequiredTorque(rpm, info.IsClockwise) * snapshot.DemandRate;
                totalDemandPower += requiredTorque.AsPrimitive() * rpm.AsPrimitive();
            }

            return totalDemandPower;
        }

        private float CalculateAvailablePower()
        {
            var totalAvailablePower = 0f;
            foreach (var generator in _gearGenerators)
            {
                totalAvailablePower += generator.GenerateTorque.AsPrimitive() * generator.GenerateRpm.AsPrimitive();
            }

            return totalAvailablePower;
        }
    }

}
