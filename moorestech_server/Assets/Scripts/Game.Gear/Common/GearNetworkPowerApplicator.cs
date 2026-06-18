using System.Collections.Generic;
using UnityEngine;

namespace Game.Gear.Common
{
    internal static class GearNetworkPowerApplicator
    {
        private const float MaxRootRpm = 1000000f;
        private const int RpmSearchIterations = 24;

        public static float CalculateTotalGeneratePower(IReadOnlyList<IGearGenerator> generators)
        {
            var totalGeneratePower = 0f;
            foreach (var generator in generators)
            {
                totalGeneratePower += generator.GenerateTorque.AsPrimitive() * generator.GenerateRpm.AsPrimitive();
            }

            return totalGeneratePower;
        }

        public static bool TryGetRootClockwise(IGearGenerator referenceGenerator, GearNetworkTopologyCache topologyCache, out bool rootClockwise)
        {
            rootClockwise = true;
            if (referenceGenerator == null) return false;
            var node = topologyCache.GetNode(referenceGenerator);
            rootClockwise = node.GetRootClockwise(referenceGenerator.GenerateIsClockwise);
            return true;
        }

        public static bool HasGeneratorDirectionMismatch(IReadOnlyList<IGearGenerator> generators, bool rootClockwise, GearNetworkTopologyCache topologyCache)
        {
            foreach (var generator in generators)
            {
                var node = topologyCache.GetNode(generator);
                if (generator.GenerateIsClockwise != node.GetClockwise(rootClockwise)) return true;
            }

            return false;
        }

        public static float FindBalancedRootRpm(GearNetworkDemandCache demandCache, float availablePower, bool rootClockwise)
        {
            var low = 0f;
            var high = MaxRootRpm;

            // requiredPower(rootRpm) <= availablePower を満たす最大値を二分探索する
            // Binary-search the highest root RPM whose required power fits supply.
            for (var i = 0; i < RpmSearchIterations; i++)
            {
                var mid = (low + high) * 0.5f;
                var requiredPower = demandCache.CalculateRequiredPower(mid, rootClockwise);
                if (requiredPower <= availablePower) low = mid;
                else high = mid;
            }

            return low;
        }

        public static GearNetworkInfo SupplyPowerToGeneratorsAndCreateInfo(IReadOnlyList<IGearGenerator> generators, GearNetworkTopologyCache topologyCache, float rootRpm, bool rootClockwise, float totalRequiredGearPower, float totalGeneratePower)
        {
            SupplyPowerToGenerators(generators, topologyCache, rootRpm, rootClockwise);
            var operationRate = totalGeneratePower == 0f ? 0f : Mathf.Min(1f, totalRequiredGearPower / totalGeneratePower);
            return new GearNetworkInfo(totalRequiredGearPower, totalGeneratePower, operationRate, GearNetworkStopReason.None);
        }

        public static void StopNetworkComponents(IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators)
        {
            foreach (var transformer in transformers) transformer.StopNetwork();
            foreach (var generator in generators) generator.StopNetwork();
        }

        private static void SupplyPowerToGenerators(IReadOnlyList<IGearGenerator> generators, GearNetworkTopologyCache topologyCache, float rootRpm, bool rootClockwise)
        {
            foreach (var generator in generators)
            {
                var node = topologyCache.GetNode(generator);
                var rpm = new RPM(rootRpm * node.RpmRatioFromRoot);
                var power = generator.GenerateTorque.AsPrimitive() * generator.GenerateRpm.AsPrimitive();
                var torque = rpm.AsPrimitive() <= 0f ? new Torque(0f) : new Torque(power / rpm.AsPrimitive());
                generator.SupplyPower(rpm, torque, node.GetClockwise(rootClockwise));
            }
        }
    }
}
