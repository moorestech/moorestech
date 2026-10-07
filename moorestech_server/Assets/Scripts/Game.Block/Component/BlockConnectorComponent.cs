using System;
using System.Collections.Generic;
using Game.Block.Component.ConnectionContext;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.Component.ConnectJudge;
using Game.Block.Interface.ComponentAttribute;
using Game.Context;
using Mooresmaster.Model.BlocksModule;
using UniRx;
using UnityEngine;

namespace Game.Block.Component
{
    [DisallowMultiple]
    public class BlockConnectorComponent<TTarget, TConnectContext> : IBlockConnectorComponent<TTarget>
        where TTarget : IBlockComponent
        where TConnectContext : IConnectorContext<TTarget>, new()
    {
        // Contextは型ごとに共有し、worldと配置情報は各コンポーネントが所有する
        // Share context per connector type while each component owns its world and placement data
        private static readonly TConnectContext Context = new();
        internal readonly ConnectorContextData Data;
        private readonly IConnectorWorldLookup _world;
        public IReadOnlyDictionary<TTarget, ConnectedInfo> ConnectedTargets => _connectedTargets;
        private readonly Dictionary<TTarget, ConnectedInfo> _connectedTargets = new();
        private readonly List<IDisposable> _blockUpdateEvents = new();
        private readonly Dictionary<Vector3Int, List<(Vector3Int position, IBlockConnector connector)>> _inputConnectPoss;
        private readonly Dictionary<Vector3Int, (Vector3Int position, IBlockConnector connector)> _outputTargetToOutputConnector;

        public BlockConnectorComponent(IReadOnlyList<IBlockConnector> inputConnectors, IReadOnlyList<IBlockConnector> outputConnectors, BlockPositionInfo blockPositionInfo)
            : this(new ConnectorContextData(inputConnectors, outputConnectors, blockPositionInfo)) { }

        internal BlockConnectorComponent(ConnectorContextData data)
        {
            Data = data;
            _world = ServerContext.WorldBlockDatastore;
            var events = ServerContext.WorldBlockUpdateEvent;
            // 専用接続も通常側の接続先になれるよう、両ポート表を必ず構築する
            // Build both ordinary port tables even when this component also uses specialized connections
            _inputConnectPoss = BlockConnectorConnectPositionCalculator.CalculateConnectorToConnectPosList(data.Inputs, data.Position);
            _outputTargetToOutputConnector = BlockConnectorConnectPositionCalculator.CalculateConnectPosToConnector(data.Outputs, data.Position);
            var positions = new HashSet<Vector3Int>(_outputTargetToOutputConnector.Keys);
            positions.UnionWith(Context.InitializeAndGetOverridelSubsrcibePositions(this, data.Position, data));
            foreach (var position in positions)
            {
                _blockUpdateEvents.Add(events.GetBlockPlaceEvent(position).Subscribe(change => Recalculate(change.BlockData.Block, null)));
                // 撤去通知時はworldに残っている対象を計算から明示的に除く
                // Explicitly exclude the removed block while it is still present in the world during notification
                _blockUpdateEvents.Add(events.GetBlockRemoveEvent(position).Subscribe(change => Recalculate(change.BlockData.Block, change.BlockData.Block)));
            }
            Recalculate(null, null);
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            _connectedTargets.Clear();
            _blockUpdateEvents.ForEach(subscription => subscription.Dispose());
            _blockUpdateEvents.Clear();
            IsDestroy = true;
        }

        private void Recalculate(IBlock targetBlock, IBlock removingBlock)
        {
            var ordinary = CalculateOrdinaryConnections(removingBlock);
            var desired = Context.GetOverride(_connectedTargets, targetBlock, Data, _world, removingBlock, ordinary);
            ConnectorConnectionReconciler.Apply(_connectedTargets, desired);
        }

        private Dictionary<TTarget, ConnectedInfo> CalculateOrdinaryConnections(IBlock removingBlock)
        {
            var desired = new Dictionary<TTarget, ConnectedInfo>();
            foreach (var (targetPosition, output) in _outputTargetToOutputConnector)
            {
                // 接続対象の不在は空セルの通常状態として扱う
                // An absent connection target is the normal state of an empty cell
                var target = _world.GetBlock(targetPosition);
                if (target == null || ReferenceEquals(target, removingBlock)) continue;
                if (!target.ComponentManager.TryGetComponent<BlockConnectorComponent<TTarget, TConnectContext>>(out var connector)) continue;
                if (!target.ComponentManager.TryGetComponent<TTarget>(out var component)) continue;
                if (!connector._inputConnectPoss.TryGetValue(targetPosition, out var acceptedCells)) continue;
                if (!ConnectorPairJudge<TTarget>.TryJudgeConnectorPair(output, acceptedCells, Data.Position, target.BlockPositionInfo,
                        Context, out var selfPort, out var targetPort)) continue;
                if (!desired.ContainsKey(component)) desired.Add(component, new ConnectedInfo(selfPort, targetPort, target));
            }
            return desired;
        }

        public static bool TryJudgeConnect(
            IReadOnlyList<IBlockConnector> selfOutputConnectors, BlockPositionInfo selfPositionInfo,
            IReadOnlyList<IBlockConnector> targetInputConnectors, BlockPositionInfo targetPositionInfo,
            out Vector3Int selfConnectorCell, out Vector3Int targetConnectorCell)
        {
            return ConnectorPairJudge<TTarget>.TryJudgeConnect(selfOutputConnectors, selfPositionInfo,
                targetInputConnectors, targetPositionInfo, Context, out selfConnectorCell, out targetConnectorCell);
        }
    }
}
