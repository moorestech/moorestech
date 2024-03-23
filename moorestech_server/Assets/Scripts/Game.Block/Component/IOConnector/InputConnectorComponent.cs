using System;
using System.Collections.Generic;
using System.Linq;
using Game.Block.BlockInventory;
using Game.Block.Interface;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface;
using Game.World.Interface.DataStore;
using UnityEngine;

namespace Game.Block.Component.IOConnector
{
    public class InputConnectorComponent : IBlockComponent
    {
        public IReadOnlyList<IBlockInventory> ConnectInventory => _connectInventory;

        public bool IsDestroy { get; private set; }

        private readonly IBlockConfig _blockConfig;
        private readonly BlockDirection _blockDirection;

        private readonly Vector3Int _blockPos;

        private readonly List<IDisposable> _blockUpdateEvents = new();
        private readonly List<IBlockInventory> _connectInventory = new();
        private readonly IOConnectionSetting _ioConnectionSetting;

        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public InputConnectorComponent(IWorldBlockDatastore worldBlockDatastore, IBlockConfig blockConfig, IWorldBlockUpdateEvent worldBlockUpdateEvent,
            IOConnectionSetting ioConnectionSetting, BlockPositionInfo blockPositionInfo)
        {
            _blockPos = blockPositionInfo.OriginalPos;
            _blockDirection = blockPositionInfo.BlockDirection;
            _worldBlockDatastore = worldBlockDatastore;
            _blockConfig = blockConfig;
            _ioConnectionSetting = ioConnectionSetting;


            var outputPoss = ConvertConnectDirection(_ioConnectionSetting.OutputConnector);
            foreach (var outputPos in outputPoss)
            {
                _blockUpdateEvents.Add(worldBlockUpdateEvent.SubscribePlace(outputPos, b => PlaceBlock(b.Pos)));
                _blockUpdateEvents.Add(worldBlockUpdateEvent.SubscribeRemove(outputPos, RemoveBlock));

                //アウトプット先にブロックがあったら接続を試みる
                if (_worldBlockDatastore.Exists(outputPos))
                {
                    PlaceBlock(outputPos);
                }
            }

            #region Internal

            // 接続先のブロックの接続可能な位置を取得する
            List<Vector3Int> ConvertConnectDirection(ConnectDirection[] connectDirection)
            {
                var blockPosConvertAction = _blockDirection.GetCoordinateConvertAction();

                var convertedPositions = connectDirection.Select(c => blockPosConvertAction(c.ToVector3Int()) + _blockPos);
                return convertedPositions.ToList();
            }

            #endregion
        }

        public void Destroy()
        {
            _connectInventory.Clear();
            _blockUpdateEvents.ForEach(x => x.Dispose());
            _blockUpdateEvents.Clear();
            IsDestroy = true;
        }

        /// <summary>
        ///     ブロックを接続元から接続先に接続できるなら接続する
        ///     その場所にブロックがあるか、
        ///     それぞれインプットとアウトプットの向きはあっているかを確認し、接続する
        /// </summary>
        private void PlaceBlock(Vector3Int destinationPos)
        {
            //接続先にBlockInventoryがなければ処理を終了
            if (!_worldBlockDatastore.TryGetBlock<InputConnectorComponent>(destinationPos, out var destinationInputConnector)) return;
            if (!_worldBlockDatastore.TryGetBlock<IBlockInventory>(destinationPos, out var blockInventory)) return;

            //接続元のブロックデータを取得
            var (_, sourceBlockOutputConnector) = GetConnectionPositions(_ioConnectionSetting, _blockDirection);


            //接続先のブロックデータを取得
            var destinationBlockType = _blockConfig.GetBlockConfig(_worldBlockDatastore.GetBlock(destinationPos).BlockId).Type;

            var destinationSetting = destinationInputConnector._ioConnectionSetting;
            var destinationDirection = _worldBlockDatastore.GetBlockDirection(destinationPos);
            var (destinationBlockInputConnector, _) = GetConnectionPositions(destinationSetting, destinationDirection);

            //接続元の接続可能リストに接続先がなかったら終了
            if (!_ioConnectionSetting.ConnectableBlockType.Contains(destinationBlockType)) return;


            //接続元から接続先へのブロックの距離を取得
            var distance = destinationPos - _blockPos;

            //接続元ブロックに対応するアウトプット座標があるかチェック
            var source = new ConnectDirection(distance);
            if (!sourceBlockOutputConnector.Contains(source)) return;

            //接続先ブロックに対応するインプット座標があるかチェック
            var destination = new ConnectDirection(distance * -1);
            if (!destinationBlockInputConnector.Contains(destination)) return;


            //接続元ブロックと接続先ブロックを接続
            if (!_connectInventory.Contains(blockInventory))
            {
                _connectInventory.Add(blockInventory);
            }

            #region Internal

            // 接続先のブロックの接続可能な位置を取得する
            (List<ConnectDirection> input, List<ConnectDirection> output) GetConnectionPositions(IOConnectionSetting connectionSetting, BlockDirection blockDirection)
            {
                var rawInputConnector = connectionSetting.InputConnector;
                var rawOutputConnector = connectionSetting.OutputConnector;

                var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();

                var inputPoss = rawInputConnector.Select(ConvertConnectDirection).ToList();
                var outputPoss = rawOutputConnector.Select(ConvertConnectDirection).ToList();

                return (inputPoss, outputPoss);

                ConnectDirection ConvertConnectDirection(ConnectDirection connectDirection)
                {
                    var convertedVector = blockPosConvertAction(connectDirection.ToVector3Int());
                    return new ConnectDirection(convertedVector);
                }
            }

            #endregion
        }

        private void RemoveBlock(BlockUpdateProperties updateProperties)
        {
            //削除されたブロックがInputConnectorComponentでない場合、処理を終了する
            if (!_worldBlockDatastore.TryGetBlock<IBlockInventory>(updateProperties.Pos, out var component)) return;

            _connectInventory.Remove(component);
        }
    }
}