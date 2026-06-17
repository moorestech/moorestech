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

        private readonly List<IGearGenerator> _gearGenerators = new();
        private readonly List<IGearEnergyTransformer> _gearTransformers = new();
        private readonly List<GearNetworkSupplyInfo> _transformerSupplyInfos = new();
        private readonly GearNetworkTopologyCache _topologyCache = new();
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

            _topologyCache.MarkDirty();
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

            _topologyCache.MarkDirty();
        }

        public void ManualUpdate()
        {
            // 最大RPMのgeneratorを起点にする既存仕様を維持する。
            // Keep the existing rule that the highest-RPM generator is the origin.
            var fastestOriginGenerator = FindFastestGenerator(out var totalGeneratePower);
            if (fastestOriginGenerator == null)
            {
                StopAsEmptyNetwork();
                return;
            }

            _topologyCache.EnsureBuilt(_gearTransformers, _gearGenerators);
            var originNode = _topologyCache.GetNode(fastestOriginGenerator);
            var rootRpm = CalculateRootRpm(fastestOriginGenerator, originNode);
            var rootClockwise = originNode.GetRootClockwise(fastestOriginGenerator.GenerateIsClockwise);

            // 構造矛盾とgenerator回転方向矛盾は既存と同じRocked扱いにする。
            // Preserve Rocked behavior for topology conflicts and generator direction mismatches.
            if (_topologyCache.IsRocked(rootRpm) || HasGeneratorDirectionMismatch(fastestOriginGenerator, rootClockwise))
            {
                CurrentGearNetworkInfo = new GearNetworkInfo(0, 0, 0, GearNetworkStopReason.Rocked);
                StopNetworkComponents();
                return;
            }

            var totalRequiredGearPower = BuildTransformerSupplyInfos(rootRpm, rootClockwise);
            if (totalRequiredGearPower > totalGeneratePower)
            {
                CurrentGearNetworkInfo = new GearNetworkInfo(totalRequiredGearPower, totalGeneratePower, 0f, GearNetworkStopReason.OverRequirePower);
                StopNetworkComponents();
                return;
            }

            SupplyPowerToNetwork(rootRpm, rootClockwise, totalRequiredGearPower, totalGeneratePower);
        }

        private IGearGenerator FindFastestGenerator(out float totalGeneratePower)
        {
            IGearGenerator fastestOriginGenerator = null;
            totalGeneratePower = 0f;

            foreach (var generator in _gearGenerators)
            {
                totalGeneratePower += generator.GenerateTorque.AsPrimitive() * generator.GenerateRpm.AsPrimitive();
                if (fastestOriginGenerator == null || generator.GenerateRpm > fastestOriginGenerator.GenerateRpm)
                {
                    fastestOriginGenerator = generator;
                }
            }

            return fastestOriginGenerator;
        }

        private bool HasGeneratorDirectionMismatch(IGearGenerator fastestOriginGenerator, bool rootClockwise)
        {
            foreach (var generator in _gearGenerators)
            {
                if (generator.BlockInstanceId == fastestOriginGenerator.BlockInstanceId) continue;
                var node = _topologyCache.GetNode(generator);
                if (generator.GenerateIsClockwise != node.GetClockwise(rootClockwise)) return true;
            }

            return false;
        }

        private float BuildTransformerSupplyInfos(float rootRpm, bool rootClockwise)
        {
            _transformerSupplyInfos.Clear();
            var totalRequiredGearPower = 0f;

            foreach (var transformer in _gearTransformers)
            {
                var node = _topologyCache.GetNode(transformer);
                var rpm = new RPM(rootRpm * node.RpmRatioFromRoot);
                var isClockwise = node.GetClockwise(rootClockwise);
                var requiredTorque = transformer.GetRequiredTorque(rpm, isClockwise);
                totalRequiredGearPower += requiredTorque.AsPrimitive() * rpm.AsPrimitive();
                _transformerSupplyInfos.Add(new GearNetworkSupplyInfo(transformer, rpm, isClockwise, requiredTorque));
            }

            return totalRequiredGearPower;
        }

        private void SupplyPowerToNetwork(float rootRpm, bool rootClockwise, float totalRequiredGearPower, float totalGeneratePower)
        {
            var operationRate = totalGeneratePower == 0 ? 0 : Mathf.Min(1, totalRequiredGearPower / totalGeneratePower);
            CurrentGearNetworkInfo = new GearNetworkInfo(totalRequiredGearPower, totalGeneratePower, operationRate, GearNetworkStopReason.None);

            // transformerへのtorque配分式は既存のpower配分仕様をそのまま使う。
            // Keep the existing power-based torque distribution formula for transformers.
            foreach (var info in _transformerSupplyInfos)
            {
                var supplyTorque = info.RequiredTorque / totalRequiredGearPower * totalGeneratePower;
                if (float.IsNaN(supplyTorque.AsPrimitive())) supplyTorque = new Torque(0);
                supplyTorque = new Torque(Mathf.Min(supplyTorque.AsPrimitive(), info.RequiredTorque.AsPrimitive()));
                info.Transformer.SupplyPower(info.Rpm, supplyTorque, info.IsClockwise);
            }

            foreach (var generator in _gearGenerators)
            {
                var node = _topologyCache.GetNode(generator);
                var rpm = new RPM(rootRpm * node.RpmRatioFromRoot);
                generator.SupplyPower(rpm, generator.GenerateTorque, node.GetClockwise(rootClockwise));
            }
        }

        private void StopAsEmptyNetwork()
        {
            CurrentGearNetworkInfo = GearNetworkInfo.CreateEmpty();
            foreach (var transformer in _gearTransformers) transformer.SupplyPower(new RPM(0), new Torque(0), true);
        }

        private void StopNetworkComponents()
        {
            foreach (var transformer in _gearTransformers) transformer.StopNetwork();
            foreach (var generator in _gearGenerators) generator.StopNetwork();
        }

        private static float CalculateRootRpm(IGearGenerator originGenerator, GearNetworkTopologyNode originNode)
        {
            return originNode.RpmRatioFromRoot == 0f ? 0f : originGenerator.GenerateRpm.AsPrimitive() / originNode.RpmRatioFromRoot;
        }
    }
}
