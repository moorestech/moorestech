using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.Block.Interface.ComponentAttribute;
using Game.Context;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Block.Component
{
    [DisallowMultiple]
    public class BlockConnectorComponent<TTarget> : IBlockConnectorComponent<TTarget> where TTarget : IBlockComponent
    {
        private readonly List<IDisposable> _blockUpdateEvents = new();
        private readonly Dictionary<TTarget, (IConnectOption selfOption, IConnectOption targetOption)> _connectTargets = new();

        private readonly Dictionary<Vector3Int, (List<Vector3Int> positions, IConnectOption targetOption)> _inputConnectPoss = new(); // key インプットコネクターの位置 value そのコネクターと接続できる位置
        private readonly Dictionary<Vector3Int, (Vector3Int position, IConnectOption selfOption)> _outputTargetToOutputConnector = new(); // key アウトプット先の位置 value そのアウトプット先と接続するアウトプットコネクターの位置

        public BlockConnectorComponent(List<ConnectSettings> inputConnectSettings, List<ConnectSettings> outputConnectSettings, BlockPositionInfo blockPositionInfo)
        {
            var blockPos = blockPositionInfo.OriginalPos;
            var blockDirection = blockPositionInfo.BlockDirection;
            var worldBlockUpdateEvent = ServerContext.WorldBlockUpdateEvent;

            CreateInputConnectPoss();
            CreateOutputTargetToOutputConnector();

            foreach (var outputPos in _outputTargetToOutputConnector.Keys)
            {
                _blockUpdateEvents.Add(worldBlockUpdateEvent.SubscribePlace(outputPos, b => OnPlaceBlock(b.Pos)));
                _blockUpdateEvents.Add(worldBlockUpdateEvent.SubscribeRemove(outputPos, OnRemoveBlock));

                //アウトプット先にブロックがあったら接続を試みる
                if (ServerContext.WorldBlockDatastore.Exists(outputPos))
                {
                    OnPlaceBlock(outputPos);
                }
            }

            #region Internal

            void CreateInputConnectPoss()
            {
                if (inputConnectSettings == null)
                {
                    return;
                }
                foreach (var inputConnectSetting in inputConnectSettings)
                {
                    var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();

                    var inputConnectorPos = blockPos + blockPosConvertAction(inputConnectSetting.ConnectorPosOffset);
                    List<Vector3Int> directions = inputConnectSetting.ConnectorDirections;
                    if (directions == null)
                    {
                        _inputConnectPoss.Add(inputConnectorPos, (null, inputConnectSetting.Option));
                        continue;
                    }

                    var targetPositions = directions.Select(c => blockPosConvertAction(c) + inputConnectorPos).ToList();
                    _inputConnectPoss.Add(inputConnectorPos, (targetPositions, inputConnectSetting.Option));
                }
            }

            void CreateOutputTargetToOutputConnector()
            {
                if (outputConnectSettings == null)
                {
                    return;
                }

                foreach (var connectSetting in outputConnectSettings)
                {
                    var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();

                    var outputConnectorPos = blockPos + blockPosConvertAction(connectSetting.ConnectorPosOffset);
                    List<Vector3Int> directions = connectSetting.ConnectorDirections;
                    var targetPoss = directions.Select(c => blockPosConvertAction(c) + outputConnectorPos).ToList();

                    foreach (var targetPos in targetPoss)
                    {
                        _outputTargetToOutputConnector.Add(targetPos, (outputConnectorPos, connectSetting.Option));
                    }
                }
            }

            #endregion
        }
        public IReadOnlyDictionary<TTarget, (IConnectOption selfOption, IConnectOption targetOption)> ConnectTargets => _connectTargets;

        public bool IsDestroy { get; private set; }

        public void Destroy()
        {
            _connectTargets.Clear();
            _blockUpdateEvents.ForEach(x => x.Dispose());
            _blockUpdateEvents.Clear();
            IsDestroy = true;
        }

        /// <summary>
        ///     ブロックを接続元から接続先に接続できるなら接続する
        ///     その場所にブロックがあるか、
        ///     それぞれインプットとアウトプットの向きはあっているかを確認し、接続する TODO ここのドキュメントを書く
        /// </summary>
        private void OnPlaceBlock(Vector3Int outputTargetPos)
        {
            //接続先にBlockInventoryがなければ処理を終了
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            if (!worldBlockDatastore.TryGetBlock(outputTargetPos, out BlockConnectorComponent<TTarget> targetConnector)) return;
            if (!worldBlockDatastore.TryGetBlock<TTarget>(outputTargetPos, out var targetComponent)) return;

            // アウトプット先にターゲットのインプットオブジェクトがあるかどうかをチェックする
            var isConnect = false;
            IConnectOption selfOption = null;
            IConnectOption targetOption = null;
            foreach (KeyValuePair<Vector3Int, (List<Vector3Int> positions, IConnectOption targetOption)> targetInput in targetConnector._inputConnectPoss)
            {
                // アウトプット先に、インプットのコネクターがあるかどうかをチェックする
                if (targetInput.Key != outputTargetPos) continue;

                // インプットがどこからでも接続できるならそのまま接続
                if (targetInput.Value.positions == null)
                {
                    isConnect = true;
                    break;
                }

                // インプット先に制限がある場合、その座標にアウトプットのコネクターがあるかをチェックする
                var outputConnector = _outputTargetToOutputConnector[outputTargetPos];

                // インプット先にアウトプットのコネクターがある場合は接続できる
                if (targetInput.Value.positions.Any(inputTargetPosition => inputTargetPosition == outputConnector.position))
                {
                    isConnect = true;
                    selfOption = outputConnector.selfOption;
                    targetOption = targetInput.Value.targetOption;
                    break;
                }
            }
            if (!isConnect)
            {
                return;
            }

            //接続元ブロックと接続先ブロックを接続
            if (!_connectTargets.ContainsKey(targetComponent))
            {
                _connectTargets.Add(targetComponent, (selfOption, targetOption));
            }
        }

        private void OnRemoveBlock(BlockUpdateProperties updateProperties)
        {
            //削除されたブロックがInputConnectorComponentでない場合、処理を終了する
            if (!ServerContext.WorldBlockDatastore.TryGetBlock<TTarget>(updateProperties.Pos, out var component)) return;

            _connectTargets.Remove(component);
        }
    }
}