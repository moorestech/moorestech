using System;
using System.Collections.Generic;
using Core.Master;
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

            #region Internal

            void OnPlaceBlock(Vector3Int changedPosition)
            {
                TryAddDefaultTarget(changedPosition);
                _connectionOverride.ApplyTo(_connectedTargets);
            }

            void TryAddDefaultTarget(Vector3Int outputTargetPos)
            {
                if (!_outputTargetToOutputConnector.TryGetValue(outputTargetPos, out var selfOutput)) return;
                var world = ServerContext.WorldBlockDatastore;
                if (!world.TryGetBlock(outputTargetPos, out BlockConnectorComponent<TTarget, TConnectJudge> targetConnector)) return;
                if (!world.TryGetBlock<TTarget>(outputTargetPos, out var targetComponent)) return;
                var targetBlock = world.GetBlock(outputTargetPos);
                if (!targetConnector._inputConnectPoss.TryGetValue(outputTargetPos, out var targetAcceptedCells)) return;
                if (!TryJudgeConnectorPair(selfOutput, targetAcceptedCells,
                        _blockPositionInfo, targetBlock.BlockPositionInfo, Judge,
                        out var selfConnector, out var targetElementConnector)) return;
                if (!_connectedTargets.ContainsKey(targetComponent))
                    _connectedTargets.Add(targetComponent,
                        new ConnectedInfo(selfConnector, targetElementConnector, targetBlock));
            }

            #endregion
        }

        public bool IsDestroy { get; private set; }
        public void Destroy()
        {
            _connectedTargets.Clear();
            _blockUpdateEvents.ForEach(x => x.Dispose());
            _blockUpdateEvents.Clear();
            IsDestroy = true;
        }

        // 歯車プレビューと実Worldは従来のペア判定を共有する。
        // Gear preview and World connection share the legacy pair matcher.
        public static bool TryJudgeConnect(IReadOnlyList<IBlockConnector> selfOutputConnectors,
            BlockPositionInfo selfPositionInfo, IReadOnlyList<IBlockConnector> targetInputConnectors,
            BlockPositionInfo targetPositionInfo, out Vector3Int selfConnectorCell,
            out Vector3Int targetConnectorCell)
        {
            selfConnectorCell = Vector3Int.zero;
            targetConnectorCell = Vector3Int.zero;
            var outputs = BlockConnectorConnectPositionCalculator.CalculateConnectPosToConnector(
                selfOutputConnectors, selfPositionInfo);
            var inputs = BlockConnectorConnectPositionCalculator.CalculateConnectorToConnectPosList(
                targetInputConnectors, targetPositionInfo);
            foreach (var (targetPos, selfOutput) in outputs)
            {
                if (!inputs.TryGetValue(targetPos, out var accepted)) continue;
                if (!TryJudgeConnectorPair(selfOutput, accepted, selfPositionInfo,
                        targetPositionInfo, Judge, out _, out _)) continue;
                selfConnectorCell = selfOutput.position;
                targetConnectorCell = targetPos;
                return true;
            }
            return false;
        }

        private static bool TryJudgeConnectorPair(
            (Vector3Int position, IBlockConnector connector) output,
            List<(Vector3Int position, IBlockConnector connector)> accepted,
            BlockPositionInfo selfPosition, BlockPositionInfo targetPosition,
            IConnectorConnectJudge judge, out IBlockConnector selfConnector,
            out IBlockConnector targetConnector)
        {
            selfConnector = null;
            targetConnector = null;
            if (accepted == null)
                return Accept(output.connector, null, out selfConnector, out targetConnector);
            foreach (var candidate in accepted)
            {
                if (candidate.position != output.position) continue;
                if (Accept(output.connector, candidate.connector,
                        out selfConnector, out targetConnector)) return true;
            }
            return false;

            #region Internal

            bool Accept(IBlockConnector source, IBlockConnector target,
                out IBlockConnector validSelfConnector, out IBlockConnector validTargetConnector)
            {
                validSelfConnector = null;
                validTargetConnector = null;
                if (!MasterHolder.BlockMaster.CanConnectConnectorShapes(source?.ShapeGuid, target?.ShapeGuid)) return false;
                if (!judge.CanConnect(new ConnectJudgeContext(source, target, selfPosition, targetPosition))) return false;
                validSelfConnector = source;
                validTargetConnector = target;
                return true;
            }

            #endregion
        }

        private void OnRemoveBlock(BlockRemoveProperties updateProperties)
        {
            if (!ServerContext.WorldBlockDatastore.TryGetBlock<TTarget>(updateProperties.Pos, out var component)) return;
            _connectedTargets.Remove(component);
        }
    }
}
