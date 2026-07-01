using System.Collections.Generic;
using UnityEngine;

namespace Game.Gear.Common
{
    public static class GearNetworkStateApplier
    {
        public static GearNetworkInfo ApplyNoGeneratorState(GearNetworkId networkId, IReadOnlyList<IGearEnergyTransformer> transformers, GearRuntimeStateStore runtimeStore)
        {
            var networkInfo = GearNetworkInfo.CreateEmpty();
            runtimeStore.SetNetworkState(new GearNetworkRuntimeState(networkId, 0f, 0f, 0f, false, GearNetworkStopReason.None));

            // generator無しは旧挙動に合わせ、停止ではなく0供給として扱う。
            // A generator-less network follows legacy behavior: zero supply, not rocked.
            foreach (var transformer in transformers)
            {
                var state = new GearRuntimeState(transformer.BlockInstanceId, networkId, new RPM(0), new Torque(0), true, false, GearNetworkStopReason.None);
                runtimeStore.SetGearState(state);
                transformer.SupplyPower(new RPM(0), new Torque(0), true);
            }

            return networkInfo;
        }

        public static GearNetworkInfo ApplyStoppedState(
            GearNetworkId networkId,
            IReadOnlyList<IGearEnergyTransformer> transformers,
            IReadOnlyList<IGearGenerator> generators,
            GearRuntimeStateStore runtimeStore,
            float demandPower,
            float availablePower,
            GearNetworkStopReason stopReason)
        {
            var networkInfo = new GearNetworkInfo(demandPower, availablePower, 0f, stopReason);
            runtimeStore.SetNetworkState(new GearNetworkRuntimeState(networkId, demandPower, availablePower, 0f, true, stopReason));

            // blackout/rockedでは全gearの供給状態を明示的に0へ落とす。
            // During blackout or rocked state, every gear receives explicit zero supply.
            foreach (var transformer in transformers)
            {
                WriteStoppedGearState(networkId, runtimeStore, transformer, stopReason);
                transformer.StopNetwork();
            }

            foreach (var generator in generators)
            {
                WriteStoppedGearState(networkId, runtimeStore, generator, stopReason);
                generator.StopNetwork();
            }

            return networkInfo;
        }

        public static void ApplySupplyState(
            GearNetworkId networkId,
            IReadOnlyList<IGearEnergyTransformer> transformers,
            IReadOnlyList<IGearGenerator> generators,
            IReadOnlyDictionary<Game.Block.Interface.BlockInstanceId, GearRotationInfo> rotationCache,
            GearRuntimeStateStore runtimeStore,
            GearDemandSnapshotStore demandStore,
            IGearGenerator originGenerator,
            float totalDemandPower,
            float totalAvailablePower)
        {
            foreach (var transformer in transformers)
            {
                if (!rotationCache.TryGetValue(transformer.BlockInstanceId, out var info)) continue;
                var rpm = info.GetRpm(originGenerator.GenerateRpm);
                var torque = CalculateSupplyTorque(transformer, info, demandStore, rpm, totalDemandPower, totalAvailablePower);
                WriteSupplyGearState(networkId, runtimeStore, transformer, rpm, torque, info.IsClockwise);
                transformer.SupplyPower(rpm, torque, info.IsClockwise);
            }

            // generator側も同じruntime storeへ、そのtickの確定回転を記録する。
            // Generator states are recorded in the same runtime store for this tick.
            foreach (var generator in generators)
            {
                if (!rotationCache.TryGetValue(generator.BlockInstanceId, out var info)) continue;
                var rpm = info.GetRpm(originGenerator.GenerateRpm);
                var torque = generator.GenerateTorque;
                WriteSupplyGearState(networkId, runtimeStore, generator, rpm, torque, info.IsClockwise);
                generator.SupplyPower(rpm, torque, info.IsClockwise);
            }
        }

        private static Torque CalculateSupplyTorque(
            IGearEnergyTransformer transformer,
            GearRotationInfo info,
            GearDemandSnapshotStore demandStore,
            RPM rpm,
            float totalDemandPower,
            float totalAvailablePower)
        {
            var snapshot = demandStore.GetSnapshot(transformer.BlockInstanceId);
            if (!snapshot.DemandEnabled || totalDemandPower == 0f) return new Torque(0);

            var requiredTorque = transformer.GetRequiredTorque(rpm, info.IsClockwise) * snapshot.DemandRate;
            var supplyTorque = requiredTorque / totalDemandPower * totalAvailablePower;
            if (float.IsNaN(supplyTorque.AsPrimitive())) return new Torque(0);
            return new Torque(Mathf.Min(supplyTorque.AsPrimitive(), requiredTorque.AsPrimitive()));
        }

        private static void WriteStoppedGearState(GearNetworkId networkId, GearRuntimeStateStore runtimeStore, IGearEnergyTransformer transformer, GearNetworkStopReason stopReason)
        {
            var state = new GearRuntimeState(transformer.BlockInstanceId, networkId, new RPM(0), new Torque(0), true, true, stopReason);
            runtimeStore.SetGearState(state);
        }

        private static void WriteSupplyGearState(GearNetworkId networkId, GearRuntimeStateStore runtimeStore, IGearEnergyTransformer transformer, RPM rpm, Torque torque, bool isClockwise)
        {
            var state = new GearRuntimeState(transformer.BlockInstanceId, networkId, rpm, torque, isClockwise, false, GearNetworkStopReason.None);
            runtimeStore.SetGearState(state);
        }
    }
}
