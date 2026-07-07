using System;
using System.Collections.Generic;
using Core.Master;
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
        // ドメイン固有の追加接続判定（型パラメータで束縛され、両側ブロックで同一が保証される）
        // Domain-specific extra judge (bound by type parameter, guaranteed identical on both sides)
        private static readonly TConnectJudge Judge = new TConnectJudge();

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
        {
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

            // 位置一致した候補を全て評価し、最初に通る組を採用する
            // Evaluate all position-matched candidates and use the first valid pair
            if (!TryFindConnectableCandidate(out var selfConnector, out var targetElementConnector)) return;

            // 接続元ブロックと接続先ブロックを接続
            // Connect source block to target block
            if (!_connectedTargets.ContainsKey(targetComponent))
            {
                var connectedInfo = new ConnectedInfo(selfConnector, targetElementConnector, targetBlock);
                _connectedTargets.Add(targetComponent, connectedInfo);
            }

            #region Internal

            bool TryFindConnectableCandidate(out IBlockConnector validSelfConnector, out IBlockConnector validTargetConnector)
            {
                validSelfConnector = null;
                validTargetConnector = null;
                var candidates = CollectPositionMatchedCandidates();

                // 形状互換表とドメイン判定の両方を通る候補を探す
                // Find a candidate that passes both the shape table and domain judge
                foreach (var candidate in candidates)
                {
                    if (!MasterHolder.BlockMaster.CanConnectConnectorShapes(candidate.selfConnector?.ShapeGuid, candidate.targetConnector?.ShapeGuid)) continue;

                    var judgeContext = new ConnectJudgeContext(candidate.selfConnector, candidate.targetConnector, _blockPositionInfo, targetBlock.BlockPositionInfo);
                    if (!Judge.CanConnect(judgeContext)) continue;

                    validSelfConnector = candidate.selfConnector;
                    validTargetConnector = candidate.targetConnector;
                    return true;
                }

                return false;
            }

            List<(IBlockConnector selfConnector, IBlockConnector targetConnector)> CollectPositionMatchedCandidates()
            {
                var outputConnector = _outputTargetToOutputConnector[outputTargetPos];
                var candidates = new List<(IBlockConnector selfConnector, IBlockConnector targetConnector)>();

                // 対象位置の入力候補だけを集める
                // Collect only input candidates at the target position
                foreach (var targetInput in targetConnector._inputConnectPoss)
                {
                    if (targetInput.Key != outputTargetPos) continue;

                    // 方向無制限入力では自側コネクタだけを確定する
                    // For unrestricted input, resolve only the source connector
                    if (targetInput.Value == null)
                    {
                        candidates.Add((outputConnector.connector, null));
                        return candidates;
                    }

                    // 同じ位置にある全ての候補ペアを評価対象に残す
                    // Keep every candidate pair at the same connector position
                    foreach (var target in targetInput.Value)
                    {
                        if (target.position != outputConnector.position) continue;
                        candidates.Add((outputConnector.connector, target.connector));
                    }
                }

                return candidates;
            }

            #endregion
        }

        private void OnRemoveBlock(BlockRemoveProperties updateProperties)
        {
            // 削除されたブロックがInputConnectorComponentでない場合、処理を終了する
            if (!ServerContext.WorldBlockDatastore.TryGetBlock<TTarget>(updateProperties.Pos, out var component)) return;

            _connectedTargets.Remove(component);
        }
    }
}
