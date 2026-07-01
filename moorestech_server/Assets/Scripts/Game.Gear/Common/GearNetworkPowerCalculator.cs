using System;
using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    public static class GearNetworkPowerCalculator
    {
        public static float CalculateDemandPower(IReadOnlyList<IGearEnergyTransformer> transformers, Dictionary<BlockInstanceId, GearRotationInfo> rotationInfos, GearDemandSnapshotStore demandStore)
        {
            var total = 0f;
            foreach (var transformer in transformers)
            {
                if (!rotationInfos.TryGetValue(transformer.BlockInstanceId, out var info)) continue;
                var snapshot = demandStore.GetOrDefault(transformer.BlockInstanceId);
                var torque = snapshot.DemandEnabled ? transformer.GetRequiredTorque(info.Rpm, info.IsClockwise).AsPrimitive() * snapshot.DemandRate : 0f;
                info.SetRequiredTorque(new Torque(torque));
                total += info.Rpm.AsPrimitive() * torque;
            }
            return total;
        }

        public static float CalculateAvailablePower(IReadOnlyList<IGearGenerator> generators)
        {
            var total = 0f;
            foreach (var generator in generators)
            {
                total += generator.GenerateRpm.AsPrimitive() * generator.GenerateTorque.AsPrimitive();
            }
            return total;
        }

        public static int CreateGeneratorOutputSignature(IReadOnlyList<IGearGenerator> generators)
        {
            var hash = new HashCode();
            foreach (var generator in generators)
            {
                hash.Add(generator.BlockInstanceId);
                hash.Add(generator.GenerateRpm.AsPrimitive());
                hash.Add(generator.GenerateTorque.AsPrimitive());
                hash.Add(generator.GenerateIsClockwise);
            }
            return hash.ToHashCode();
        }
    }
}
