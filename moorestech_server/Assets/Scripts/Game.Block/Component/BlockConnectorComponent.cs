using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Block.Interface.Component;
using Game.Block.Interface.ComponentAttribute;
using Game.Context;
using Game.World.Interface.DataStore;
using Mooresmaster.Model.BlockConnectInfoModule;
using UnityEngine;

namespace Game.Block.Component
{
    [DisallowMultiple]
    public class BlockConnectorComponent<TTarget> : IBlockConnectorComponent<TTarget> where TTarget : IBlockComponent
    {
        public IReadOnlyDictionary<TTarget, (IConnectOption selfOption, IConnectOption targetOption)> ConnectedTargets => _connectedTargets;
        private readonly Dictionary<TTarget, (IConnectOption selfOption, IConnectOption targetOption)> _connectedTargets = new();
        
        private readonly List<IDisposable> _blockUpdateEvents = new();
        
        private readonly Dictionary<Vector3Int, List<(Vector3Int position, IConnectOption targetOption)>> _inputConnectPoss = new(); // key インプットコネクターの位置 value そのコネクターと接続できる位置
        private readonly Dictionary<Vector3Int, (Vector3Int position, IConnectOption selfOption)> _outputTargetToOutputConnector = new(); // key アウトプット先の位置 value そのアウトプット先と接続するアウトプットコネクターの位置
        
        public BlockConnectorComponent(BlockConnectInfo inputConnectInfo, BlockConnectInfo outputConnectInfo, BlockPositionInfo blockPositionInfo)
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
                if (ServerContext.WorldBlockDatastore.Exists(outputPos)) OnPlaceBlock(outputPos);
            }
            
            #region Internal
            
            void CreateInputConnectPoss()
            {
                if (inputConnectInfo == null) return;
                foreach (var inputConnectSetting in inputConnectInfo.items)
                {
                    var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();
                    
                    var inputConnectorPos = blockPos + blockPosConvertAction(inputConnectSetting.Offset);
                    var directions = inputConnectSetting.Directions;
                    if (directions == null)
                    {
                        _inputConnectPoss.Add(inputConnectorPos, null);
                        continue;
                    }
                    
                    var targetPositions = directions.Select(c => (blockPosConvertAction(c) + inputConnectorPos, inputConnectSetting.ConnectOption)).ToList();
                    if (!_inputConnectPoss.TryAdd(inputConnectorPos, targetPositions)) _inputConnectPoss[inputConnectorPos] = _inputConnectPoss[inputConnectorPos].Concat(targetPositions).ToList();
                }
            }
            
            void CreateOutputTargetToOutputConnector()
            {
                if (outputConnectInfo == null) return;
                
                foreach (var connectSetting in outputConnectInfo.items)
                {
                    var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();
                    
                    var outputConnectorPos = blockPos + blockPosConvertAction(connectSetting.Offset);
                    var directions = connectSetting.Directions;
                    var targetPoss = directions.Select(c => blockPosConvertAction(c) + outputConnectorPos).ToList();
                    
                    foreach (var targetPos in targetPoss) _outputTargetToOutputConnector.Add(targetPos, (outputConnectorPos, connectSetting.ConnectOption));
                }
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
                        selfOption = outputConnector.selfOption;
                        targetOption = target.targetOption;
                        break;
                    }
            }
            
            if (!isConnect) return;
            
            //接続元ブロックと接続先ブロックを接続
            if (!_connectedTargets.ContainsKey(targetComponent)) _connectedTargets.Add(targetComponent, (selfOption, targetOption));
        }
        
        private void OnRemoveBlock(BlockUpdateProperties updateProperties)
        {
            //削除されたブロックがInputConnectorComponentでない場合、処理を終了する
            if (!ServerContext.WorldBlockDatastore.TryGetBlock<TTarget>(updateProperties.Pos, out var component)) return;
            
            _connectedTargets.Remove(component);
        }
    }
}