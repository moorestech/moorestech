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
        private readonly Queue<IGearEnergyTransformer> _queue = new();

        private bool _isDirty = true;
        private bool _hasDirectionConflict;
        private float _maxRpmRatioConflictFromRoot;

        public bool IsDirty => _isDirty;

        public void MarkDirty()
        {
            _isDirty = true;
        }

        public void EnsureBuilt(IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators)
        {
            if (!_isDirty) return;
            Rebuild(transformers, generators);
            _isDirty = false;
        }

        public GearNetworkTopologyNode GetNode(IGearEnergyTransformer transformer)
        {
            return _nodes[transformer.BlockInstanceId];
        }

        public bool TryAddConnectedGear(IGearEnergyTransformer gear)
        {
            if (_isDirty || _nodes.ContainsKey(gear.BlockInstanceId)) return false;

            // 既存nodeへの接続から新nodeのroot相対値を逆算する
            // Infer the new root-relative node from already known neighbors.
            var connects = gear.GetGearConnects();
            GearNetworkTopologyNode newNode = default;
            var foundConnectedNode = false;
            foreach (var connect in connects)
            {
                if (!_nodes.TryGetValue(connect.Transformer.BlockInstanceId, out var targetNode)) continue;
                var candidateNode = CalculateCurrentNodeFromTarget(gear, connect, targetNode);
                if (!foundConnectedNode)
                {
                    newNode = candidateNode;
                    foundConnectedNode = true;
                    continue;
                }

                ValidateExistingNode(newNode, candidateNode.RpmRatioFromRoot, candidateNode.IsClockwiseSameAsRoot);
            }

            if (!foundConnectedNode) return false;

            foreach (var connect in connects)
            {
                if (!_nodes.TryGetValue(connect.Transformer.BlockInstanceId, out var targetNode)) continue;
                ValidateConnectionFromIncrementalNode(newNode, connect, targetNode);
            }

            _nodes.Add(gear.BlockInstanceId, newNode);
            _networkMemberIds.Add(gear.BlockInstanceId);
            return true;
        }

        public bool IsRocked(float rootRpm)
        {
            return _hasDirectionConflict || RpmMismatchTolerance < _maxRpmRatioConflictFromRoot * Math.Abs(rootRpm);
        }

        private void Rebuild(IReadOnlyList<IGearEnergyTransformer> transformers, IReadOnlyList<IGearGenerator> generators)
        {
            // ネットワーク構成を作り直し、通常tickで使うroot基準の比率を保存する。
            // Rebuild topology and store root-relative ratios used by normal ticks.
            Reset();
            RegisterMemberIds(transformers, generators);
            var root = SelectRoot(transformers, generators);
            if (root == null) return;

            _nodes.Add(root.BlockInstanceId, new GearNetworkTopologyNode(root, 1f, true));
            _queue.Enqueue(root);

            while (_queue.Count > 0)
            {
                var current = _queue.Dequeue();
                var currentNode = _nodes[current.BlockInstanceId];
                foreach (var connect in current.GetGearConnects())
                {
                    AddOrValidateConnection(currentNode, connect);
                }
            }

            if (_nodes.Count != _networkMemberIds.Count) _hasDirectionConflict = true;
        }

        private void AddOrValidateConnection(GearNetworkTopologyNode currentNode, GearConnect connect)
        {
            var target = connect.Transformer;
            if (!_networkMemberIds.Contains(target.BlockInstanceId)) return;

            // 接続先のroot基準rpm比率と回転方向を、既存DFSと同じ式で求める。
            // Calculate target root-relative ratio and direction with the same formula as the old DFS.
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

        private static GearNetworkTopologyNode CalculateCurrentNodeFromTarget(IGearEnergyTransformer current, GearConnect connect, GearNetworkTopologyNode targetNode)
        {
            var isReverseRotation = connect.Self.IsReverse && connect.Target.IsReverse;
            var currentRatio = targetNode.RpmRatioFromRoot;
            if (connect.Transformer is IGear targetGear &&
                current is IGear currentGear &&
                isReverseRotation)
            {
                currentRatio = targetNode.RpmRatioFromRoot * targetGear.TeethCount / currentGear.TeethCount;
            }

            var currentClockwiseSameAsRoot = isReverseRotation
                ? !targetNode.IsClockwiseSameAsRoot
                : targetNode.IsClockwiseSameAsRoot;
            return new GearNetworkTopologyNode(current, currentRatio, currentClockwiseSameAsRoot);
        }

        private void ValidateConnectionFromIncrementalNode(GearNetworkTopologyNode newNode, GearConnect connect, GearNetworkTopologyNode targetNode)
        {
            var isReverseRotation = connect.Self.IsReverse && connect.Target.IsReverse;
            var targetRatio = CalculateTargetRatio(newNode, connect, isReverseRotation);
            var targetClockwiseSameAsRoot = isReverseRotation
                ? !newNode.IsClockwiseSameAsRoot
                : newNode.IsClockwiseSameAsRoot;
            ValidateExistingNode(targetNode, targetRatio, targetClockwiseSameAsRoot);
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

        private void Reset()
        {
            _nodes.Clear();
            _networkMemberIds.Clear();
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
