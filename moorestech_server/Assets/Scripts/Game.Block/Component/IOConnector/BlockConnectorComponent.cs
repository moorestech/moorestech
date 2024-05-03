using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Game.Block.Interface;
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

        private readonly Dictionary<Vector3Int,ConnectSettings> _outputConnectSettings = new();
        private readonly Dictionary<Vector3Int,ConnectSettings> _inputConnectSettings = new();
        
        public BlockConnectorComponent(List<ConnectSettings> outputConnectSettings, List<ConnectSettings> inputConnectSettings,BlockPositionInfo blockPositionInfo)
        {
            var blockPos = blockPositionInfo.OriginalPos;
            var blockDirection = blockPositionInfo.BlockDirection;
            var worldBlockUpdateEvent = ServerContext.WorldBlockUpdateEvent;
            
            var outputPoss = ConvertConnectionPos(_outputConnectSettings,outputConnectSettings);
            foreach (var outputPos in outputPoss)
            {
                _blockUpdateEvents.Add(worldBlockUpdateEvent.SubscribePlace(outputPos, b => OnPlaceBlock(b.Pos)));
                _blockUpdateEvents.Add(worldBlockUpdateEvent.SubscribeRemove(outputPos, OnRemoveBlock));

                //アウトプット先にブロックがあったら接続を試みる
                if (ServerContext.WorldBlockDatastore.Exists(outputPos))
                {
                    OnPlaceBlock(outputPos);
                }
            }
            
            ConvertConnectionPos(_inputConnectSettings,inputConnectSettings);

            #region Internal

            // 接続先のブロックの接続可能な位置を取得する
            List<Vector3Int> ConvertConnectionPos(Dictionary<Vector3Int,ConnectSettings> addTarget,List<ConnectSettings> connectSettings)
            {
                var result = new List<Vector3Int>();
                foreach (var connectSetting in connectSettings)
                {
                    var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();
                    
                    var convertedOffset = blockPosConvertAction(connectSetting.ConnectorPosOffset);
                    if (connectSetting.ConnectDirection.Count == 0)
                    {
                        addTarget.Add(blockPos, connectSetting);
                        continue;
                    }

                    var targetPoss = connectSetting.ConnectDirection.Select(c => blockPosConvertAction(c) + blockPos + convertedOffset).ToList();
                    result.AddRange(targetPoss);
                    foreach (var targetPos in targetPoss)
                    {
                        addTarget.Add(targetPos, connectSetting);
                    }
                }
                
                return result;
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
        ///     それぞれインプットとアウトプットの向きはあっているかを確認し、接続する
        /// </summary>
        private void OnPlaceBlock(Vector3Int outputTargetPos)
        {
            //接続先にBlockInventoryがなければ処理を終了
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            if (!worldBlockDatastore.TryGetBlock<BlockConnectorComponent<TTarget>>(outputTargetPos, out var targetConnector)) return;
            if (!worldBlockDatastore.TryGetBlock<TTarget>(outputTargetPos, out var targetComponent)) return;
            
            // アウトプット先にターゲットのインプットオブジェクトがあるかどうかをチェックする
            var isConnectable = false;
            foreach (var targetInputPos in targetConnector._inputConnectSettings)      
            {
                if (targetInputPos.Key == outputTargetPos)
                {
                    isConnectable = true;
                }
            }
            if (!isConnectable)
            {
                return;
            }
            
            //TODo インプット方向に制限があったら、その制限の方向に自分のアウトプットコネクターがあるかどうかをチェックする
            if
            
            
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

    public class ConnectSettings
    {
        public Vector3Int ConnectorPosOffset; // 原点からみたコネクターの場所のオフセット
        public List<Vector3Int> ConnectDirection; // インプットされる方向、もしくはアウトプットする方向
    }
}