using System;
using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    internal class GearNetworkStableStateCache
    {
        private const float FloatTolerance = 0.0001f;

        private readonly Dictionary<BlockInstanceId, GearGeneratorStateSnapshot> _generatorSnapshots = new();

        private bool _hasSnapshot;
        private bool _hasFastestGenerator;
        private BlockInstanceId _fastestGeneratorId;
        private float _totalGeneratePower;

        public bool CanSkipUpdate(IReadOnlyList<IGearGenerator> generators, IGearGenerator fastestGenerator, float totalGeneratePower, bool topologyDirty)
        {
            if (topologyDirty || !_hasSnapshot) return false;
            if (Math.Abs(_totalGeneratePower - totalGeneratePower) > FloatTolerance) return false;

            var hasFastestGenerator = fastestGenerator != null;
            if (_hasFastestGenerator != hasFastestGenerator) return false;
            if (hasFastestGenerator && !_fastestGeneratorId.Equals(fastestGenerator.BlockInstanceId)) return false;
            if (_generatorSnapshots.Count != generators.Count) return false;

            // generator出力が完全に同じならネットワーク結果も同じなので再計算しない
            // Skip the network pass when every generator output still matches the saved snapshot
            foreach (var generator in generators)
            {
                if (!_generatorSnapshots.TryGetValue(generator.BlockInstanceId, out var snapshot)) return false;
                if (!snapshot.Matches(generator)) return false;
            }

            return true;
        }

        public void Store(IReadOnlyList<IGearGenerator> generators, IGearGenerator fastestGenerator, float totalGeneratePower)
        {
            _generatorSnapshots.Clear();
            foreach (var generator in generators)
            {
                _generatorSnapshots.Add(generator.BlockInstanceId, new GearGeneratorStateSnapshot(generator));
            }

            _hasSnapshot = true;
            _hasFastestGenerator = fastestGenerator != null;
            _fastestGeneratorId = _hasFastestGenerator ? fastestGenerator.BlockInstanceId : new BlockInstanceId(0);
            _totalGeneratePower = totalGeneratePower;
        }

        public void Invalidate()
        {
            _hasSnapshot = false;
            _generatorSnapshots.Clear();
        }

        private readonly struct GearGeneratorStateSnapshot
        {
            private readonly float _rpm;
            private readonly float _torque;
            private readonly bool _isClockwise;

            public GearGeneratorStateSnapshot(IGearGenerator generator)
            {
                _rpm = generator.GenerateRpm.AsPrimitive();
                _torque = generator.GenerateTorque.AsPrimitive();
                _isClockwise = generator.GenerateIsClockwise;
            }

            public bool Matches(IGearGenerator generator)
            {
                return Math.Abs(_rpm - generator.GenerateRpm.AsPrimitive()) <= FloatTolerance &&
                       Math.Abs(_torque - generator.GenerateTorque.AsPrimitive()) <= FloatTolerance &&
                       _isClockwise == generator.GenerateIsClockwise;
            }
        }
    }
}
