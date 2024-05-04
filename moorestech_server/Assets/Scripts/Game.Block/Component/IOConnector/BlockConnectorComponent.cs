using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.Block.Interface.Component;
using Game.Block.Interface.ComponentAttribute;
using Game.Context;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Block.Component.IOConnector
{
    [DisallowMultiple]
    public class BlockConnectorComponent<TTarget> : IBlockComponent where TTarget : IBlockComponent
    {
        public IReadOnlyList<TTarget> ConnectTargets => _connectTargets;

        public bool IsDestroy { get; private set; }
        
        private readonly List<IDisposable> _blockUpdateEvents = new();
        private readonly List<TTarget> _connectTargets = new();

        private readonly Dictionary<Vector3Int,List<Vector3Int>> _inputConnectPoss = new(); // key インプットコネクターの位置 value そのコネクターと接続できる位置
        private readonly Dictionary<Vector3Int,Vector3Int> _outputTargetToOutputConnector = new(); // key アウトプット先の位置 value そのアウトプット先と接続するアウトプットコネクターの位置

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
                    Debug.Log("Manual OnPlaceBlock " + outputPos);
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
                    var directions = inputConnectSetting.ConnectorDirections;
                    if (directions == null)
                    {
                        _inputConnectPoss.Add(inputConnectorPos, null);
                        continue;
                    }
                    
                    var targetPoss = directions.Select(c => blockPosConvertAction(c) + inputConnectorPos).ToList();
                    _inputConnectPoss.Add(inputConnectorPos, targetPoss);
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
                    var directions = connectSetting.ConnectorDirections;
                    var targetPoss = directions.Select(c => blockPosConvertAction(c) + outputConnectorPos).ToList();
                    
                    foreach (var targetPos in targetPoss)
                    {
                        _outputTargetToOutputConnector.Add(targetPos, outputConnectorPos);
                    }
                }
            }

            #endregion
        }

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
            Debug.Log("OnPlaceBlock " + outputTargetPos);
            //接続先にBlockInventoryがなければ処理を終了
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            if (!worldBlockDatastore.TryGetBlock<BlockConnectorComponent<TTarget>>(outputTargetPos, out var targetConnector)) return;
            if (!worldBlockDatastore.TryGetBlock<TTarget>(outputTargetPos, out var targetComponent)) return;
            
            // アウトプット先にターゲットのインプットオブジェクトがあるかどうかをチェックする
            var isConnect = false;
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
                var outputConnectorPos = _outputTargetToOutputConnector[outputTargetPos];
                
                // インプット先にアウトプットのコネクターがある場合は接続できる
                if (targetInput.Value.Any(inputTargetPosition => inputTargetPosition == outputConnectorPos))
                {
                    isConnect = true;
                    break;
                }
            }
            if (!isConnect)
            {
                return;
            }

            //接続元ブロックと接続先ブロックを接続
            if (!_connectTargets.Contains(targetComponent))
            {
                _connectTargets.Add(targetComponent);
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