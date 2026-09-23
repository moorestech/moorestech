using System;
using System.Collections.Generic;
using Game.Block.Component.ConnectOverride;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.ComponentAttribute;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Component
{
    [DisallowMultiple]
    public class BlockConnectorComponent<TTarget, TConnectJudge> : IBlockConnectorComponent<TTarget>
        where TTarget : IBlockComponent
        where TConnectJudge : IConnectorConnectJudge, new()
    {
        private static readonly TConnectJudge Judge = new TConnectJudge();
        public IReadOnlyDictionary<TTarget, ConnectedInfo> ConnectedTargets => _connectedTargets;
        private readonly Dictionary<TTarget, ConnectedInfo> _connectedTargets = new();
        private readonly List<IDisposable> _blockUpdateEvents = new();
        private readonly BlockPositionInfo _blockPositionInfo;
        private readonly IConnectorConnectionOverride<TTarget> _connectionOverride;
        private readonly Dictionary<Vector3Int, List<(Vector3Int position, IBlockConnector connector)>> _inputConnectPoss;
        private readonly Dictionary<Vector3Int, (Vector3Int position, IBlockConnector connector)> _outputTargetToOutputConnector;

        public BlockConnectorComponent(IReadOnlyList<IBlockConnector> inputConnectors,
            IReadOnlyList<IBlockConnector> outputConnectors, BlockPositionInfo blockPositionInfo)
            : this(inputConnectors, outputConnectors, blockPositionInfo,
                new DefaultConnectionOverride<TTarget>()) { }

        internal BlockConnectorComponent(IReadOnlyList<IBlockConnector> inputConnectors,
            IReadOnlyList<IBlockConnector> outputConnectors, BlockPositionInfo blockPositionInfo,
            IConnectorConnectionOverride<TTarget> connectionOverride)
        {
            _blockPositionInfo = blockPositionInfo;
            _connectionOverride = connectionOverride;
            _inputConnectPoss = BlockConnectorConnectPositionCalculator.CalculateConnectorToConnectPosList(inputConnectors, blockPositionInfo);
            _outputTargetToOutputConnector = BlockConnectorConnectPositionCalculator.CalculateConnectPosToConnector(outputConnectors, blockPositionInfo);
            var worldEvent = ServerContext.WorldBlockUpdateEvent;
            var positions = new HashSet<Vector3Int>(_outputTargetToOutputConnector.Keys);
            var completionPositions = new HashSet<Vector3Int>(connectionOverride.ObservationPositions);
            foreach (var position in completionPositions) positions.Add(position);
            foreach (var position in positions)
            {
                _blockUpdateEvents.Add(worldEvent.GetBlockPlaceEvent(position).Subscribe(b => OnPlaceBlock(b.Pos)));
                _blockUpdateEvents.Add(worldEvent.GetBlockRemoveEvent(position).Subscribe(OnRemoveBlock));
                if (completionPositions.Contains(position))
                    _blockUpdateEvents.Add(worldEvent.GetBlockRemovalCompletedEvent(position).Subscribe(_ =>
                        _connectionOverride.ApplyTo(_connectedTargets)));
                if (ServerContext.WorldBlockDatastore.Exists(position)) OnPlaceBlock(position);
            }
            _connectionOverride.ApplyTo(_connectedTargets);
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            _connectedTargets.Clear();
            _blockUpdateEvents.ForEach(x => x.Dispose());
            _blockUpdateEvents.Clear();
            IsDestroy = true;
        }

        private void OnPlaceBlock(Vector3Int changedPosition)
        {
            TryAddDefaultTarget(changedPosition);
            _connectionOverride.ApplyTo(_connectedTargets);
        }

        private void TryAddDefaultTarget(Vector3Int outputTargetPos)
        {
            if (!_outputTargetToOutputConnector.TryGetValue(outputTargetPos, out var selfOutput)) return;
            var world = ServerContext.WorldBlockDatastore;
            if (!world.TryGetBlock(outputTargetPos, out BlockConnectorComponent<TTarget, TConnectJudge> targetConnector)) return;
            if (!world.TryGetBlock<TTarget>(outputTargetPos, out var targetComponent)) return;
            var targetBlock = world.GetBlock(outputTargetPos);
            if (!targetConnector._inputConnectPoss.TryGetValue(outputTargetPos, out var targetAcceptedCells)) return;
            if (!BlockConnectorCandidateMatcher.TryJudgeConnectorPair(selfOutput, targetAcceptedCells,
                    _blockPositionInfo, targetBlock.BlockPositionInfo, Judge,
                    out var selfConnector, out var targetElementConnector)) return;
            if (!_connectedTargets.ContainsKey(targetComponent))
                _connectedTargets.Add(targetComponent,
                    new ConnectedInfo(selfConnector, targetElementConnector, targetBlock));
        }

        // 歯車プレビューと実Worldは従来のペア判定を共有する。
        // Gear preview and World connection share the legacy pair matcher.
        public static bool TryJudgeConnect(IReadOnlyList<IBlockConnector> selfOutputConnectors,
            BlockPositionInfo selfPositionInfo, IReadOnlyList<IBlockConnector> targetInputConnectors,
            BlockPositionInfo targetPositionInfo, out Vector3Int selfConnectorCell,
            out Vector3Int targetConnectorCell)
        {
            return BlockConnectorCandidateMatcher.TryJudgeConnect(selfOutputConnectors, selfPositionInfo,
                targetInputConnectors, targetPositionInfo, Judge,
                out selfConnectorCell, out targetConnectorCell);
        }

        private void OnRemoveBlock(BlockRemoveProperties updateProperties)
        {
            if (!ServerContext.WorldBlockDatastore.TryGetBlock<TTarget>(updateProperties.Pos, out var component)) return;
            _connectedTargets.Remove(component);
        }
    }
}
