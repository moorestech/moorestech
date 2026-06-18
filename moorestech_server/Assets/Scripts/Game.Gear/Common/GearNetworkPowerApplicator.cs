using System.Collections.Generic;
using UnityEngine;

namespace Game.Gear.Common
{
    internal static class GearNetworkPowerApplicator
    {
        public static IGearGenerator FindFastestGenerator(IReadOnlyList<IGearGenerator> generators, out float totalGeneratePower)
        {
            IGearGenerator fastestGenerator = null;
            totalGeneratePower = 0f;

            foreach (var generator in generators)
            {
                totalGeneratePower += generator.GenerateTorque.AsPrimitive() * generator.GenerateRpm.AsPrimitive();
                if (fastestGenerator == null || generator.GenerateRpm > fastestGenerator.GenerateRpm)
                {
                    fastestGenerator = generator;
                }
            }

            return fastestGenerator;
        }

        public static bool HasGeneratorDirectionMismatch(IReadOnlyList<IGearGenerator> generators, IGearGenerator originGenerator, bool rootClockwise, GearNetworkTopologyCache topologyCache)
        {
            foreach (var generator in generators)
            {
                if (generator.BlockInstanceId == originGenerator.BlockInstanceId) continue;
                var node = topologyCache.GetNode(generator);
                if (generator.GenerateIsClockwise != node.GetClockwise(rootClockwise)) return true;
            }

            return false;
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

        public static float BuildTransformerSupplyInfos(IReadOnlyList<IGearEnergyTransformer> transformers, GearNetworkTopologyCache topologyCache, List<GearNetworkSupplyInfo> supplyInfos, float rootRpm, bool rootClockwise)
        {
            supplyInfos.Clear();
            var totalRequiredGearPower = 0f;

            foreach (var transformer in transformers)
            {
                var node = topologyCache.GetNode(transformer);
                var rpm = new RPM(rootRpm * node.RpmRatioFromRoot);
                var isClockwise = node.GetClockwise(rootClockwise);
                var requiredTorque = transformer.GetRequiredTorque(rpm, isClockwise);
                totalRequiredGearPower += requiredTorque.AsPrimitive() * rpm.AsPrimitive();
                supplyInfos.Add(new GearNetworkSupplyInfo(transformer, rpm, isClockwise, requiredTorque));
            }

            return totalRequiredGearPower;
        }

        public static GearNetworkInfo SupplyPowerToNetwork(IReadOnlyList<IGearGenerator> generators, IReadOnlyList<GearNetworkSupplyInfo> supplyInfos, GearNetworkTopologyCache topologyCache, float rootRpm, bool rootClockwise, float totalRequiredGearPower, float totalGeneratePower)
        {
            var operationRate = totalGeneratePower == 0 ? 0 : Mathf.Min(1, totalRequiredGearPower / totalGeneratePower);

            // 全体再計算では transformer と generator の両方へ既存仕様どおり通知する
            // Full recalculation notifies both transformers and generators with the existing formula.
            foreach (var info in supplyInfos)
            {
                var supplyTorque = info.RequiredTorque / totalRequiredGearPower * totalGeneratePower;
                if (float.IsNaN(supplyTorque.AsPrimitive())) supplyTorque = new Torque(0);
                supplyTorque = new Torque(Mathf.Min(supplyTorque.AsPrimitive(), info.RequiredTorque.AsPrimitive()));
                info.Transformer.SupplyPower(info.Rpm, supplyTorque, info.IsClockwise);
            }

            SupplyPowerToGenerators(generators, topologyCache, rootRpm, rootClockwise);
            return new GearNetworkInfo(totalRequiredGearPower, totalGeneratePower, operationRate, GearNetworkStopReason.None);
        }

        public static void SupplyPowerToGenerators(IReadOnlyList<IGearGenerator> generators, GearNetworkTopologyCache topologyCache, float rootRpm, bool rootClockwise)
        {
            foreach (var generator in generators)
            {
                var node = topologyCache.GetNode(generator);
                var rpm = new RPM(rootRpm * node.RpmRatioFromRoot);
                generator.SupplyPower(rpm, generator.GenerateTorque, node.GetClockwise(rootClockwise));
            }
        }

        public static void StopAsEmptyNetwork(IReadOnlyList<IGearEnergyTransformer> transformers)
        {
            foreach (var transformer in transformers) transformer.SupplyPower(new RPM(0), new Torque(0), true);
        }

        public static void StopNetworkComponents(IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators)
        {
            foreach (var transformer in transformers) transformer.StopNetwork();
            foreach (var generator in generators) generator.StopNetwork();
        }

        public static float CalculateRootRpm(IGearGenerator originGenerator, GearNetworkTopologyNode originNode)
        {
            return originNode.RpmRatioFromRoot == 0f ? 0f : originGenerator.GenerateRpm.AsPrimitive() / originNode.RpmRatioFromRoot;
        }
    }
}
