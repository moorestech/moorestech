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

            // アウトプット先にターゲットのインプットオブジェクトがあるかどうかをチェックする
            // Check if target's input object exists at output destination
            var isConnect = false;
            IBlockConnector selfConnector = null;
            IBlockConnector targetElementConnector = null;
            foreach (var targetInput in targetConnector._inputConnectPoss)
            {
                // アウトプット先に、インプットのコネクターがあるかどうかをチェックする
                if (targetInput.Key != outputTargetPos) continue;

                // インプットがどこからでも接続できるならそのまま接続
                if (targetInput.Value == null)
                {
                    isConnect = true;
                    break;
                }

                // インプット先に制限がある場合、その座標にアウトプットのコネクターがあるかをチェックする
                var outputConnector = _outputTargetToOutputConnector[outputTargetPos];

                // インプット先にアウトプットのコネクターがある場合は接続できる
                foreach (var target in targetInput.Value)
                    if (target.position == outputConnector.position)
                    {
                        isConnect = true;
                        selfConnector = outputConnector.connector;
                        targetElementConnector = target.connector;
                        break;
                    }
            }

            if (!isConnect) return;

            var targetBlock = ServerContext.WorldBlockDatastore.GetBlock(outputTargetPos);

            // 形状互換表チェック（未設定コネクタ・未特定コネクタはワイルドカード）
            // Shape table check (unset shapes and unresolved connectors are wildcard)
            if (!MasterHolder.BlockMaster.CanConnectConnectorShapes(selfConnector?.ShapeGuid, targetElementConnector?.ShapeGuid)) return;

            // ドメイン固有の追加判定
            // Domain-specific extra judge
            var judgeContext = new ConnectJudgeContext(selfConnector, targetElementConnector, _blockPositionInfo, targetBlock.BlockPositionInfo);
            if (!Judge.CanConnect(judgeContext)) return;

            // 接続元ブロックと接続先ブロックを接続
            // Connect source block to target block
            if (!_connectedTargets.ContainsKey(targetComponent))
            {
                var connectedInfo = new ConnectedInfo(selfConnector, targetElementConnector, targetBlock);
                _connectedTargets.Add(targetComponent, connectedInfo);
            }
        }

        private void OnRemoveBlock(BlockRemoveProperties updateProperties)
        {
            // 削除されたブロックがInputConnectorComponentでない場合、処理を終了する
            if (!ServerContext.WorldBlockDatastore.TryGetBlock<TTarget>(updateProperties.Pos, out var component)) return;

            _connectedTargets.Remove(component);
        }
    }
}
