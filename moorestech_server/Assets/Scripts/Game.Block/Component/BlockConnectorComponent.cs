using System;
using System.Collections.Generic;
using Game.Block.Interface;
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
        public IReadOnlyDictionary<TTarget, ConnectedInfo> ConnectedTargets => _connectedTargets;
        private readonly Dictionary<TTarget, ConnectedInfo> _connectedTargets = new();

        private readonly List<IDisposable> _blockUpdateEvents = new();

        // key: インプットコネクターの位置
        // value: そのコネクターと接続できる位置とIBlockConnector
        private readonly Dictionary<Vector3Int, List<(Vector3Int position, IBlockConnector element)>> _inputConnectPoss;

        // key: アウトプット先の位置
        // value: そのアウトプット先と接続するアウトプットコネクターの位置とIBlockConnector
        private readonly Dictionary<Vector3Int, (Vector3Int position, IBlockConnector element)> _outputTargetToOutputConnector;

        public BlockConnectorComponent(IBlockConnector[] inputConnects, IBlockConnector[] outputConnects, BlockPositionInfo blockPositionInfo)
        {
            var worldBlockUpdateEvent = ServerContext.WorldBlockUpdateEvent;

            _inputConnectPoss = BlockConnectorConnectPositionCalculator.CalculateConnectorToConnectPosList(inputConnects, blockPositionInfo);
            _outputTargetToOutputConnector = BlockConnectorConnectPositionCalculator.CalculateConnectPosToConnector(outputConnects, blockPositionInfo);

            foreach (var outputPos in _outputTargetToOutputConnector.Keys)
            {
                _blockUpdateEvents.Add(worldBlockUpdateEvent.SubscribePlace(outputPos, b => OnPlaceBlock(b.Pos)));
                _blockUpdateEvents.Add(worldBlockUpdateEvent.SubscribeRemove(outputPos, OnRemoveBlock));

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
        ///     その場所にブロックがあるか、
        ///     それぞれインプットとアウトプットの向きはあっているかを確認し、接続する
        /// </summary>
        private void OnPlaceBlock(Vector3Int outputTargetPos)
        {
            // 接続先にBlockInventoryがなければ処理を終了
            // Exit if no BlockInventory at connection target
            var worldBlockDatastore = ServerContext.WorldBlockDatastore;
            if (!worldBlockDatastore.TryGetBlock(outputTargetPos, out BlockConnectorComponent<TTarget> targetConnector)) return;
            if (!worldBlockDatastore.TryGetBlock<TTarget>(outputTargetPos, out var targetComponent)) return;

            // アウトプット先にターゲットのインプットオブジェクトがあるかどうかをチェックする
            // Check if target's input object exists at output destination
            var isConnect = false;
            IBlockConnector selfElement = null;
            IBlockConnector targetElement = null;
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
                        selfElement = outputConnector.element;
                        targetElement = target.element;
                        break;
                    }
            }

            if (!isConnect) return;

            // 接続元ブロックと接続先ブロックを接続
            // Connect source block to target block
            if (!_connectedTargets.ContainsKey(targetComponent))
            {
                var block = ServerContext.WorldBlockDatastore.GetBlock(outputTargetPos);
                var connectedInfo = new ConnectedInfo(selfElement, targetElement, block);
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
