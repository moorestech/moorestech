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
        public IReadOnlyDictionary<TTarget, ConnectedInfo> ConnectedTargets => _connectedTargets;
        private readonly Dictionary<TTarget, ConnectedInfo> _connectedTargets = new();
        
        private readonly List<IDisposable> _blockUpdateEvents = new();
        
        private readonly Dictionary<Vector3Int, List<(Vector3Int position, IConnectOption targetOption)>> _inputConnectPoss = new(); // key インプットコネクターの位置 value そのコネクターと接続できる位置
        private readonly Dictionary<Vector3Int, (Vector3Int position, IConnectOption selfOption)> _outputTargetToOutputConnector = new(); // key アウトプット先の位置 value そのアウトプット先と接続するアウトプットコネクターの位置
        
        public BlockConnectorComponent(BlockConnectInfo inputConnectInfo, BlockConnectInfo outputConnectInfo, BlockPositionInfo blockPositionInfo)
        {
            var worldBlockUpdateEvent = ServerContext.WorldBlockUpdateEvent;
            
            _inputConnectPoss = BlockConnectorConnectPositionCalculator.CalculateInputConnectPoss(inputConnectInfo, blockPositionInfo);
            _outputTargetToOutputConnector = BlockConnectorConnectPositionCalculator.CalculateOutputConnectPoss(outputConnectInfo, blockPositionInfo);
            
            foreach (var outputPos in _outputTargetToOutputConnector.Keys)
            {
                _blockUpdateEvents.Add(worldBlockUpdateEvent.SubscribePlace(outputPos, b => OnPlaceBlock(b.Pos)));
                _blockUpdateEvents.Add(worldBlockUpdateEvent.SubscribeRemove(outputPos, OnRemoveBlock));
                
                //アウトプット先にブロックがあったら接続を試みる
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
            if (!_connectedTargets.ContainsKey(targetComponent))
            {
                var block = ServerContext.WorldBlockDatastore.GetBlock(outputTargetPos);
                var connectedInfo = new ConnectedInfo(selfOption, targetOption, block);
                _connectedTargets.Add(targetComponent, connectedInfo);
            }
        }
        
        private void OnRemoveBlock(BlockUpdateProperties updateProperties)
        {
            //削除されたブロックがInputConnectorComponentでない場合、処理を終了する
            if (!ServerContext.WorldBlockDatastore.TryGetBlock<TTarget>(updateProperties.Pos, out var component)) return;
            
            _connectedTargets.Remove(component);
        }
    }
}