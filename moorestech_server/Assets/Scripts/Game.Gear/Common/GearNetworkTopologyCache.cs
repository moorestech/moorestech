using System;
using System.Collections.Generic;
using Game.Block.Interface;

namespace Game.Gear.Common
{
    internal class GearNetworkTopologyCache
    {
        private const float RpmMismatchTolerance = 0.1f;

        private readonly Dictionary<BlockInstanceId, GearNetworkTopologyNode> _nodes = new();
        private readonly HashSet<BlockInstanceId> _networkMemberIds = new();
        private readonly Dictionary<BlockInstanceId, List<GearConnect>> _connectionsBySource = new();
        private readonly Dictionary<BlockInstanceId, List<(IGearEnergyTransformer Source, GearConnect Connect)>> _reverseConnectionsByTarget = new();
        private readonly Queue<IGearEnergyTransformer> _queue = new();

        private bool _isDirty = true;
        private bool _hasDirectionConflict;
        private float _maxRpmRatioConflictFromRoot;

        public bool IsDirty => _isDirty;

        public void MarkDirty() => _isDirty = true;
        public void MarkCleanWithoutRebuild() => _isDirty = false;

        public void EnsureBuilt(IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators)
        {
            if (!_isDirty) return;
            Rebuild(transformers, generators);
            _isDirty = false;
        }

        public GearNetworkTopologyNode GetNode(IGearEnergyTransformer transformer) => _nodes[transformer.BlockInstanceId];

        public bool IsRocked(float rootRpm) => _hasDirectionConflict || RpmMismatchTolerance < _maxRpmRatioConflictFromRoot * Math.Abs(rootRpm);

        private void Rebuild(IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators)
        {
            Reset();
            RegisterMemberIds(transformers, generators);
            BuildConnectionCache(transformers, generators);
            var root = SelectRoot(transformers, generators);
            if (root == null) return;

            // root基準のRPM比と回転方向をBFSで保存する
            // Store root-relative RPM ratios and rotation direction by BFS.
            _nodes.Add(root.BlockInstanceId, new GearNetworkTopologyNode(root, 1f, true));
            _queue.Enqueue(root);

            while (_queue.Count > 0)
            {
                var current = _queue.Dequeue();
                var currentNode = _nodes[current.BlockInstanceId];
                foreach (var connect in _connectionsBySource[current.BlockInstanceId])
                {
                    AddOrValidateOutgoingConnection(currentNode, connect);
                }

                if (!_reverseConnectionsByTarget.TryGetValue(current.BlockInstanceId, out var reverseConnections)) continue;
                foreach (var reverseConnection in reverseConnections)
                {
                    AddOrValidateIncomingConnection(currentNode, reverseConnection);
                }
            }

            if (_nodes.Count != _networkMemberIds.Count) _hasDirectionConflict = true;
        }

        private void AddOrValidateOutgoingConnection(GearNetworkTopologyNode currentNode, GearConnect connect)
        {
            var target = connect.Transformer;
            if (!_networkMemberIds.Contains(target.BlockInstanceId)) return;

            // 接続先のroot基準値を既存DFSと同じ式で求める
            // Calculate the target root-relative values with the same rule as the old DFS.
            var isReverseRotation = connect.Self.IsReverse && connect.Target.IsReverse;
            var targetRatio = CalculateTargetRatio(currentNode, connect, isReverseRotation);
            var targetClockwiseSameAsRoot = isReverseRotation
                ? !currentNode.IsClockwiseSameAsRoot
                : currentNode.IsClockwiseSameAsRoot;

            if (_nodes.TryGetValue(target.BlockInstanceId, out var existingNode))
            {
                ValidateExistingNode(existingNode, targetRatio, targetClockwiseSameAsRoot);
                return;
            }

            _nodes.Add(target.BlockInstanceId, new GearNetworkTopologyNode(target, targetRatio, targetClockwiseSameAsRoot));
            _queue.Enqueue(target);
        }

        private void AddOrValidateIncomingConnection(GearNetworkTopologyNode targetNode, (IGearEnergyTransformer Source, GearConnect Connect) reverseConnection)
        {
            var source = reverseConnection.Source;
            var connect = reverseConnection.Connect;

            // rootがどこでも同じ連結成分を復元できるよう逆向きにも値を解く
            // Solve the inverse edge so any root can recover the same connected component.
            var isReverseRotation = connect.Self.IsReverse && connect.Target.IsReverse;
            var sourceRatio = CalculateSourceRatio(targetNode, source, connect, isReverseRotation);
            var sourceClockwiseSameAsRoot = isReverseRotation
                ? !targetNode.IsClockwiseSameAsRoot
                : targetNode.IsClockwiseSameAsRoot;

            if (_nodes.TryGetValue(source.BlockInstanceId, out var existingNode))
            {
                ValidateExistingNode(existingNode, sourceRatio, sourceClockwiseSameAsRoot);
                return;
            }

            _nodes.Add(source.BlockInstanceId, new GearNetworkTopologyNode(source, sourceRatio, sourceClockwiseSameAsRoot));
            _queue.Enqueue(source);
        }

        private static float CalculateTargetRatio(GearNetworkTopologyNode currentNode, GearConnect connect, bool isReverseRotation)
        {
            if (connect.Transformer is IGear targetGear &&
                currentNode.Transformer is IGear currentGear &&
                isReverseRotation)
            {
                return currentNode.RpmRatioFromRoot * currentGear.TeethCount / targetGear.TeethCount;
            }

            return currentNode.RpmRatioFromRoot;
        }

        private static float CalculateSourceRatio(GearNetworkTopologyNode targetNode, IGearEnergyTransformer source, GearConnect connect, bool isReverseRotation)
        {
            if (connect.Transformer is IGear targetGear &&
                source is IGear sourceGear &&
                isReverseRotation)
            {
                return targetNode.RpmRatioFromRoot * targetGear.TeethCount / sourceGear.TeethCount;
            }

            return targetNode.RpmRatioFromRoot;
        }

        private void ValidateExistingNode(GearNetworkTopologyNode existingNode, float targetRatio, bool targetClockwiseSameAsRoot)
        {
            if (existingNode.IsClockwiseSameAsRoot != targetClockwiseSameAsRoot)
            {
                _hasDirectionConflict = true;
            }

            var ratioDiff = Math.Abs(existingNode.RpmRatioFromRoot - targetRatio);
            if (_maxRpmRatioConflictFromRoot < ratioDiff)
            {
                _maxRpmRatioConflictFromRoot = ratioDiff;
            }
        }

        private void RegisterMemberIds(IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators)
        {
            foreach (var transformer in transformers) _networkMemberIds.Add(transformer.BlockInstanceId);
            foreach (var generator in generators) _networkMemberIds.Add(generator.BlockInstanceId);
        }

        private void BuildConnectionCache(IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators)
        {
            foreach (var transformer in transformers) RegisterConnections(transformer);
            foreach (var generator in generators) RegisterConnections(generator);
        }

        private void RegisterConnections(IGearEnergyTransformer source)
        {
            var connections = source.GetGearConnects();
            _connectionsBySource.Add(source.BlockInstanceId, connections);
            foreach (var connect in connections)
            {
                var targetId = connect.Transformer.BlockInstanceId;
                if (!_networkMemberIds.Contains(targetId)) continue;
                if (!_reverseConnectionsByTarget.TryGetValue(targetId, out var reverseConnections))
                {
                    reverseConnections = new List<(IGearEnergyTransformer Source, GearConnect Connect)>();
                    _reverseConnectionsByTarget.Add(targetId, reverseConnections);
                }

                reverseConnections.Add((source, connect));
            }
        }

        private void Reset()
        {
            _nodes.Clear();
            _networkMemberIds.Clear();
            _connectionsBySource.Clear();
            _reverseConnectionsByTarget.Clear();
            _queue.Clear();
            _hasDirectionConflict = false;
            _maxRpmRatioConflictFromRoot = 0f;
        }

        private static IGearEnergyTransformer SelectRoot(IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators)
        {
            if (generators.Count > 0) return generators[0];
            return transformers.Count > 0 ? transformers[0] : null;
        }
    }
}
