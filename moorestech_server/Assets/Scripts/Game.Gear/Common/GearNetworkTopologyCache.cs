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
        public GearNetworkTopologyNode GetNode(IGearEnergyTransformer transformer) => _nodes[transformer.BlockInstanceId];
        public bool IsRocked(float rootRpm) => _hasDirectionConflict || RpmMismatchTolerance < _maxRpmRatioConflictFromRoot * Math.Abs(rootRpm);

        public void EnsureBuilt(IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators)
        {
            if (!_isDirty) return;
            Rebuild(transformers, generators);
            _isDirty = false;
        }

        public bool TryAddConnectedGear(IGearEnergyTransformer gear)
        {
            if (_isDirty || _nodes.ContainsKey(gear.BlockInstanceId)) return false;
            var connections = CollectConnections(gear);
            if (!TryCalculateNewNode(gear, connections, out var newNode)) return false;

            // 追加gearの接続だけを検証し、既存node集合はそのまま維持する
            // Validate only the added gear edges while preserving the existing node set.
            _networkMemberIds.Add(gear.BlockInstanceId);
            _nodes.Add(gear.BlockInstanceId, newNode);
            _connectionsBySource.Add(gear.BlockInstanceId, connections);
            RegisterReverseConnections(gear, connections);
            ValidateOutgoingConnections(newNode, connections);
            return true;
        }

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
                ValidateOutgoingConnections(currentNode, _connectionsBySource[current.BlockInstanceId]);
                if (!_reverseConnectionsByTarget.TryGetValue(current.BlockInstanceId, out var reverseConnections)) continue;
                foreach (var reverseConnection in reverseConnections) AddOrValidateIncomingConnection(currentNode, reverseConnection);
            }

            if (_nodes.Count != _networkMemberIds.Count) _hasDirectionConflict = true;
        }

        private bool TryCalculateNewNode(IGearEnergyTransformer gear, List<GearConnect> connections, out GearNetworkTopologyNode newNode)
        {
            newNode = default;
            var found = false;
            foreach (var connect in connections)
            {
                if (!_nodes.TryGetValue(connect.Transformer.BlockInstanceId, out var targetNode)) continue;
                var candidate = GearNetworkTopologyCalculator.CalculateSourceNode(targetNode, gear, connect);
                if (!found)
                {
                    newNode = candidate;
                    found = true;
                    continue;
                }

                ValidateExistingNode(newNode, candidate.RpmRatioFromRoot, candidate.IsClockwiseSameAsRoot);
            }

            return found;
        }

        private void ValidateOutgoingConnections(GearNetworkTopologyNode currentNode, List<GearConnect> connections)
        {
            foreach (var connect in connections)
            {
                var target = connect.Transformer;
                if (!_networkMemberIds.Contains(target.BlockInstanceId)) continue;
                var isReverseRotation = connect.Self.IsReverse && connect.Target.IsReverse;
                var targetRatio = GearNetworkTopologyCalculator.CalculateTargetRatio(currentNode, connect, isReverseRotation);
                var targetClockwiseSameAsRoot = isReverseRotation ? !currentNode.IsClockwiseSameAsRoot : currentNode.IsClockwiseSameAsRoot;
                AddOrValidateNode(target, targetRatio, targetClockwiseSameAsRoot);
            }
        }

        private void AddOrValidateIncomingConnection(GearNetworkTopologyNode targetNode, (IGearEnergyTransformer Source, GearConnect Connect) reverseConnection)
        {
            var candidate = GearNetworkTopologyCalculator.CalculateSourceNode(targetNode, reverseConnection.Source, reverseConnection.Connect);
            AddOrValidateNode(reverseConnection.Source, candidate.RpmRatioFromRoot, candidate.IsClockwiseSameAsRoot);
        }

        private void AddOrValidateNode(IGearEnergyTransformer target, float targetRatio, bool targetClockwiseSameAsRoot)
        {
            if (_nodes.TryGetValue(target.BlockInstanceId, out var existingNode))
            {
                ValidateExistingNode(existingNode, targetRatio, targetClockwiseSameAsRoot);
                return;
            }

            _nodes.Add(target.BlockInstanceId, new GearNetworkTopologyNode(target, targetRatio, targetClockwiseSameAsRoot));
            _queue.Enqueue(target);
        }

        private void ValidateExistingNode(GearNetworkTopologyNode existingNode, float targetRatio, bool targetClockwiseSameAsRoot)
        {
            if (existingNode.IsClockwiseSameAsRoot != targetClockwiseSameAsRoot) _hasDirectionConflict = true;
            var ratioDiff = Math.Abs(existingNode.RpmRatioFromRoot - targetRatio);
            if (_maxRpmRatioConflictFromRoot < ratioDiff) _maxRpmRatioConflictFromRoot = ratioDiff;
        }

        private void RegisterMemberIds(IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators)
        {
            foreach (var transformer in transformers) _networkMemberIds.Add(transformer.BlockInstanceId);
            foreach (var generator in generators) _networkMemberIds.Add(generator.BlockInstanceId);
        }

        private void BuildConnectionCache(IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators)
        {
            foreach (var transformer in transformers) RegisterConnections(transformer, CollectConnections(transformer));
            foreach (var generator in generators) RegisterConnections(generator, CollectConnections(generator));
        }

        private void RegisterConnections(IGearEnergyTransformer source, List<GearConnect> connections)
        {
            _connectionsBySource.Add(source.BlockInstanceId, connections);
            RegisterReverseConnections(source, connections);
        }

        private void RegisterReverseConnections(IGearEnergyTransformer source, List<GearConnect> connections)
        {
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

        private static List<GearConnect> CollectConnections(IGearEnergyTransformer source)
        {
            var connections = new List<GearConnect>();
            if (source is IGearConnectCacheProvider provider) provider.AddGearConnectsTo(connections);
            else connections.AddRange(source.GetGearConnects());
            return connections;
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
