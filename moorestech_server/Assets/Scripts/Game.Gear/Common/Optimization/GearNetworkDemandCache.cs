using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gear.Common
{
    internal class GearNetworkDemandCache
    {
        private readonly List<GearNetworkDemandTerm> _terms = new();
        private readonly List<GearNetworkZeroDemand> _zeroDemands = new();
        private readonly List<GearNetworkFallbackDemand> _fallbackDemands = new();
        private readonly Dictionary<GearNetworkDemandKey, int> _termIndexes = new();

        public bool IsBuilt { get; private set; }

        public void Invalidate()
        {
            IsBuilt = false;
            _terms.Clear();
            _zeroDemands.Clear();
            _fallbackDemands.Clear();
            _termIndexes.Clear();
        }

        public void Rebuild(IReadOnlyList<IGearEnergyTransformer> transformers, GearNetworkTopologyCache topologyCache)
        {
            Invalidate();
            foreach (var transformer in transformers) AddTransformer(transformer, topologyCache.GetNode(transformer));
            IsBuilt = true;
        }

        public void AddTransformer(IGearEnergyTransformer transformer, GearNetworkTopologyNode node)
        {
            if (transformer is not IGearPowerConsumptionProfileProvider provider)
            {
                _fallbackDemands.Add(new GearNetworkFallbackDemand(transformer, node));
                return;
            }

            if (!provider.TryGetGearPowerConsumptionProfile(out var profile) || !profile.HasDemand || node.RpmRatioFromRoot <= 0f)
            {
                _zeroDemands.Add(new GearNetworkZeroDemand(transformer, node));
                return;
            }

            // 同一の消費式とRPM比は1項にまとめ、探索と配布の重複計算を減らす
            // Merge identical demand formulas and RPM ratios to reduce repeated search and supply work.
            var key = new GearNetworkDemandKey(profile, node.RpmRatioFromRoot);
            if (!_termIndexes.TryGetValue(key, out var index))
            {
                index = _terms.Count;
                _termIndexes.Add(key, index);
                _terms.Add(new GearNetworkDemandTerm(profile, node.RpmRatioFromRoot));
            }

            _terms[index].AddMember(transformer, node.IsClockwiseSameAsRoot);
        }

        public float CalculateRequiredPower(float rootRpm, bool rootClockwise)
        {
            var totalRequiredPower = 0f;
            foreach (var term in _terms) totalRequiredPower += term.CalculateRequiredPower(rootRpm);
            foreach (var fallback in _fallbackDemands) totalRequiredPower += fallback.CalculateRequiredPower(rootRpm, rootClockwise);
            return totalRequiredPower;
        }

        public float SupplyPowerToTransformers(float rootRpm, bool rootClockwise)
        {
            var totalRequiredPower = 0f;
            foreach (var term in _terms) totalRequiredPower += term.SupplyPower(rootRpm, rootClockwise);
            foreach (var zeroDemand in _zeroDemands) zeroDemand.SupplyPower(rootRpm, rootClockwise);
            foreach (var fallback in _fallbackDemands) totalRequiredPower += fallback.SupplyPower(rootRpm, rootClockwise);
            return totalRequiredPower;
        }

        private sealed class GearNetworkDemandTerm
        {
            private readonly List<GearNetworkDemandMember> _members = new();
            private readonly float _rpmRatioFromRoot;
            private readonly float _minimumRootRpm;
            private readonly float _baseRootRpm;
            private readonly float _underTorqueCoefficient;
            private readonly float _overTorqueCoefficient;
            private readonly float _underExponent;
            private readonly float _overExponent;

            public GearNetworkDemandTerm(GearPowerConsumptionProfile profile, float rpmRatioFromRoot)
            {
                _rpmRatioFromRoot = rpmRatioFromRoot;
                _minimumRootRpm = profile.MinimumRpm / rpmRatioFromRoot;
                _baseRootRpm = profile.BaseRpm / rpmRatioFromRoot;
                _underExponent = profile.TorqueExponentUnder;
                _overExponent = profile.TorqueExponentOver;
                _underTorqueCoefficient = profile.BaseTorque * Mathf.Pow(rpmRatioFromRoot / profile.BaseRpm, _underExponent);
                _overTorqueCoefficient = profile.BaseTorque * Mathf.Pow(rpmRatioFromRoot / profile.BaseRpm, _overExponent);
            }

            public void AddMember(IGearEnergyTransformer transformer, bool isClockwiseSameAsRoot)
            {
                _members.Add(new GearNetworkDemandMember(transformer, _rpmRatioFromRoot, isClockwiseSameAsRoot));
            }

            public float CalculateRequiredPower(float rootRpm)
            {
                var torque = CalculateRequiredTorque(rootRpm);
                return torque * rootRpm * _rpmRatioFromRoot * _members.Count;
            }

            public float SupplyPower(float rootRpm, bool rootClockwise)
            {
                var rpm = new RPM(rootRpm * _rpmRatioFromRoot);
                var requiredTorque = new Torque(CalculateRequiredTorque(rootRpm));
                foreach (var member in _members) member.SupplyPower(rpm, requiredTorque, rootClockwise);
                return requiredTorque.AsPrimitive() * rpm.AsPrimitive() * _members.Count;
            }

            private float CalculateRequiredTorque(float rootRpm)
            {
                if (rootRpm < _minimumRootRpm) return 0f;
                var coefficient = rootRpm <= _baseRootRpm ? _underTorqueCoefficient : _overTorqueCoefficient;
                var exponent = rootRpm <= _baseRootRpm ? _underExponent : _overExponent;
                return coefficient * Mathf.Pow(rootRpm, exponent);
            }
        }

        private readonly struct GearNetworkDemandMember
        {
            public readonly IGearEnergyTransformer Transformer;
            public readonly float RpmRatioFromRoot;
            public readonly bool IsClockwiseSameAsRoot;

            public GearNetworkDemandMember(IGearEnergyTransformer transformer, float rpmRatioFromRoot, bool isClockwiseSameAsRoot)
            {
                Transformer = transformer;
                RpmRatioFromRoot = rpmRatioFromRoot;
                IsClockwiseSameAsRoot = isClockwiseSameAsRoot;
            }

            public void SupplyPower(RPM rpm, Torque torque, bool rootClockwise)
            {
                var clockwise = IsClockwiseSameAsRoot ? rootClockwise : !rootClockwise;
                Transformer.SupplyPower(rpm, torque, clockwise);
            }
        }

        private readonly struct GearNetworkZeroDemand
        {
            private readonly GearNetworkDemandMember _member;

            public GearNetworkZeroDemand(IGearEnergyTransformer transformer, GearNetworkTopologyNode node)
            {
                _member = new GearNetworkDemandMember(transformer, node.RpmRatioFromRoot, node.IsClockwiseSameAsRoot);
            }

            public void SupplyPower(float rootRpm, bool rootClockwise)
            {
                _member.SupplyPower(new RPM(rootRpm * _member.RpmRatioFromRoot), new Torque(0f), rootClockwise);
            }
        }

        private readonly struct GearNetworkFallbackDemand
        {
            private readonly GearNetworkDemandMember _member;

            public GearNetworkFallbackDemand(IGearEnergyTransformer transformer, GearNetworkTopologyNode node)
            {
                _member = new GearNetworkDemandMember(transformer, node.RpmRatioFromRoot, node.IsClockwiseSameAsRoot);
            }

            public float CalculateRequiredPower(float rootRpm, bool rootClockwise)
            {
                var rpm = new RPM(rootRpm * _member.RpmRatioFromRoot);
                var clockwise = _member.IsClockwiseSameAsRoot ? rootClockwise : !rootClockwise;
                return _member.Transformer.GetRequiredTorque(rpm, clockwise).AsPrimitive() * rpm.AsPrimitive();
            }

            public float SupplyPower(float rootRpm, bool rootClockwise)
            {
                var rpm = new RPM(rootRpm * _member.RpmRatioFromRoot);
                var clockwise = _member.IsClockwiseSameAsRoot ? rootClockwise : !rootClockwise;
                var requiredTorque = _member.Transformer.GetRequiredTorque(rpm, clockwise);
                _member.Transformer.SupplyPower(rpm, requiredTorque, clockwise);
                return requiredTorque.AsPrimitive() * rpm.AsPrimitive();
            }
        }

        private readonly struct GearNetworkDemandKey : IEquatable<GearNetworkDemandKey>
        {
            private readonly GearPowerConsumptionProfile _profile;
            private readonly float _rpmRatioFromRoot;

            public GearNetworkDemandKey(GearPowerConsumptionProfile profile, float rpmRatioFromRoot)
            {
                _profile = profile;
                _rpmRatioFromRoot = rpmRatioFromRoot;
            }

            public bool Equals(GearNetworkDemandKey other)
            {
                return _profile.MinimumRpm.Equals(other._profile.MinimumRpm) &&
                       _profile.BaseRpm.Equals(other._profile.BaseRpm) &&
                       _profile.BaseTorque.Equals(other._profile.BaseTorque) &&
                       _profile.TorqueExponentUnder.Equals(other._profile.TorqueExponentUnder) &&
                       _profile.TorqueExponentOver.Equals(other._profile.TorqueExponentOver) &&
                       _rpmRatioFromRoot.Equals(other._rpmRatioFromRoot);
            }

            public override bool Equals(object obj) => obj is GearNetworkDemandKey other && Equals(other);

            public override int GetHashCode() => HashCode.Combine(_profile.MinimumRpm, _profile.BaseRpm, _profile.BaseTorque, _profile.TorqueExponentUnder, _profile.TorqueExponentOver, _rpmRatioFromRoot);
        }
    }
}
