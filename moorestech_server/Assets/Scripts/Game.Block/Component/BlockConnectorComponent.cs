using System;
using System.Collections.Generic;
using Game.Block.Component.ConnectionContext;
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
        internal IConnectorContext<TTarget> Context { get; }

        public IReadOnlyDictionary<TTarget, ConnectedInfo> ConnectedTargets => _connectedTargets;
        private readonly Dictionary<TTarget, ConnectedInfo> _connectedTargets = new();

        private readonly List<IDisposable> _blockUpdateEvents = new();
        private readonly BlockPositionInfo _blockPositionInfo;

        // key: インプットコネクターの位置
        // value: 接続可能位置とIBlockConnector
        private readonly Dictionary<Vector3Int, List<(Vector3Int position, IBlockConnector connector)>> _inputConnectPoss;

        // key: アウトプット先の位置
        // value: アウトプットコネクターの位置とIBlockConnector
        private readonly Dictionary<Vector3Int, (Vector3Int position, IBlockConnector connector)> _outputTargetToOutputConnector;

        public BlockConnectorComponent(IReadOnlyList<IBlockConnector> inputConnectors, IReadOnlyList<IBlockConnector> outputConnectors, BlockPositionInfo blockPositionInfo)
            : this(inputConnectors, outputConnectors, blockPositionInfo, new DefaultConnectorContext<TTarget>()) { }

        internal BlockConnectorComponent(IReadOnlyList<IBlockConnector> inputConnectors, IReadOnlyList<IBlockConnector> outputConnectors,
            BlockPositionInfo blockPositionInfo, IConnectorContext<TTarget> context)
        {
            Context = context;
            var worldBlockUpdateEvent = ServerContext.WorldBlockUpdateEvent;

            _blockPositionInfo = blockPositionInfo;
            _inputConnectPoss = BlockConnectorConnectPositionCalculator.CalculateConnectorToConnectPosList(inputConnectors, blockPositionInfo);
            _outputTargetToOutputConnector = BlockConnectorConnectPositionCalculator.CalculateConnectPosToConnector(outputConnectors, blockPositionInfo);

            foreach (var outputPos in _outputTargetToOutputConnector.Keys)
            {
                _blockUpdateEvents.Add(worldBlockUpdateEvent.GetBlockPlaceEvent(outputPos).Subscribe(b => OnPlaceBlock(b.Pos)));
                _blockUpdateEvents.Add(worldBlockUpdateEvent.GetBlockRemoveEvent(outputPos).Subscribe(OnRemoveBlock));

                // アウトプット先にブロックがあったら接続を試みる
                // If there is a block at the output destination, try to connect
                if (ServerContext.WorldBlockDatastore.Exists(outputPos)) OnPlaceBlock(outputPos);
            }
        }

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            Context.Dispose();
            _connectedTargets.Clear();
            _blockUpdateEvents.ForEach(x => x.Dispose());
            _blockUpdateEvents.Clear();
            IsDestroy = true;
        }

        /// <summary>
        ///     ブロックを接続元から接続先に接続できるなら接続する
        ///     位置一致 → 形状互換表 → ドメイン判定の3段で接続可否を決める
        ///     Connect source to target if possible: position match, then shape table, then domain judge
        /// </summary>
        private void OnPlaceBlock(Vector3Int outputTargetPos)
        {
            // 接続先に同型のコネクタコンポーネントとターゲットがなければ処理を終了
            // Exit if the target lacks a same-typed connector component and target component
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            if (!worldBlockDatastore.TryGetBlock(outputTargetPos, out BlockConnectorComponent<TTarget, TConnectJudge> targetConnector)) return;
            if (!worldBlockDatastore.TryGetBlock<TTarget>(outputTargetPos, out var targetComponent)) return;

            var targetBlock = ServerContext.WorldBlockDatastore.GetBlock(outputTargetPos);
            // 専用コンテキストがedge単位で接続する
            // The specialized context owns connections at shared edges
            if (Context.HandlesOverride(targetBlock)) return;

            // 位置一致した候補を全て評価し、最初に通る組を採用する
            // Evaluate all position-matched candidates and use the first valid pair
            if (!targetConnector._inputConnectPoss.TryGetValue(outputTargetPos, out var targetAcceptedCells)) return;
            if (!ConnectorPairJudge<TConnectJudge>.TryJudgeConnectorPair(_outputTargetToOutputConnector[outputTargetPos], targetAcceptedCells, _blockPositionInfo, targetBlock.BlockPositionInfo, out var selfConnector, out var targetElementConnector)) return;

            // 接続元ブロックと接続先ブロックを接続
            // Connect source block to target block
            if (!_connectedTargets.ContainsKey(targetComponent))
            {
                var connectedInfo = new ConnectedInfo(selfConnector, targetElementConnector, targetBlock);
                _connectedTargets.Add(targetComponent, connectedInfo);
            }
        }

        public static bool TryJudgeConnect(
            IReadOnlyList<IBlockConnector> selfOutputConnectors, BlockPositionInfo selfPositionInfo,
            IReadOnlyList<IBlockConnector> targetInputConnectors, BlockPositionInfo targetPositionInfo,
            out Vector3Int selfConnectorCell, out Vector3Int targetConnectorCell)
        {
            return ConnectorPairJudge<TConnectJudge>.TryJudgeConnect(selfOutputConnectors, selfPositionInfo,
                targetInputConnectors, targetPositionInfo, out selfConnectorCell, out targetConnectorCell);
        }

        // 差分の適用先は既存辞書を保持する
        // Apply deltas without replacing the existing dictionary
        internal void RemoveConnection(TTarget target) => _connectedTargets.Remove(target);
        internal void SetConnection(TTarget target, ConnectedInfo connection)
        {
            if (IsDestroy)
            {
                Debug.LogError("Cannot connect a destroyed source component.");
                return;
            }
            _connectedTargets[target] = connection;
        }

        private void OnRemoveBlock(BlockRemoveProperties updateProperties)
        {
            // 削除されたブロックがInputConnectorComponentでない場合、処理を終了する
            if (!ServerContext.WorldBlockDatastore.TryGetBlock<TTarget>(updateProperties.Pos, out var component)) return;

            _connectedTargets.Remove(component);
        }
    }
}
