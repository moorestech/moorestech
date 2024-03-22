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
        public bool IsDestroy { get; private set; }
        private readonly IOConnectionSetting _ioConnectionSetting;

        public IReadOnlyList<InputConnectorComponent> ConnectInventory => _connectInventory;
        private readonly List<InputConnectorComponent> _connectInventory = new();
        
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IBlockConfig _blockConfig;
        
        private readonly Vector3Int _blockPos;
        private readonly BlockDirection _blockDirection;
        
        public InputConnectorComponent(IWorldBlockDatastore worldBlockDatastore,IBlockConfig blockConfig,IWorldBlockUpdateEvent worldBlockUpdateEvent,
            IOConnectionSetting ioConnectionSetting, WorldBlockData worldBlockData)
        {
            _blockPos = worldBlockData.OriginalPos;
            _blockDirection = worldBlockData.BlockDirection;
            _worldBlockDatastore = worldBlockDatastore;
            _blockConfig = blockConfig;
            _ioConnectionSetting = ioConnectionSetting;
        }

        public void SetIOSetting()
        {
            var inputPoss = ConvertConnectDirection(_ioConnectionSetting.InputConnector);
            
            foreach (var inputPos in inputPoss)
            {
                ConnectBlock(inputPos, _worldBlockDatastore, _blockConfig);
            }

            #region Internal

            // 接続先のブロックの接続可能な位置を取得する
            List<Vector3Int> ConvertConnectDirection(ConnectDirection[] connectDirection)
            {
                var blockPosConvertAction = _blockDirection.GetCoordinateConvertAction();

                var convertedPositions =
                    connectDirection.Select(c => blockPosConvertAction(c.ToVector3Int()) + _blockPos);
                return convertedPositions.ToList();
            }

            #endregion
        }
        
        public void Destroy()
        {
            _connectInventory.Clear();
            IsDestroy = true;
        }

        /// <summary>
        ///     ブロックを接続元から接続先に接続できるなら接続する
        ///     その場所にブロックがあるか、
        ///     そのブロックのタイプはioConnectionDataDictionaryにあるか、
        ///     それぞれインプットとアウトプットの向きはあっているかを確認し、接続する
        /// </summary>
        private void ConnectBlock(Vector3Int destination,IWorldBlockDatastore worldBlockDatastore,IBlockConfig blockConfig)
        {
            //接続先にBlockInventoryがなければ処理を終了
            if (!worldBlockDatastore.TryGetBlock<InputConnectorComponent>(destination,out var destinationInputConnector)) return;


            //接続元のブロックデータを取得
            var sourceBlock = worldBlockDatastore.GetBlock(_blockPos);
            var sourceBlockType = blockConfig.GetBlockConfig(sourceBlock.BlockId).Type;

            var (_, sourceBlockOutputConnector) = GetConnectionPositions(sourceBlockType, _blockDirection);


            //接続先のブロックデータを取得
            var destinationBlock = worldBlockDatastore.GetBlock(destination);
            var destinationBlockType = blockConfig.GetBlockConfig(destinationBlock.BlockId).Type;
            
            var (destinationBlockInputConnector, _) = GetConnectionPositions(destinationBlockType, worldBlockDatastore.GetBlockDirection(destination));

            //接続元の接続可能リストに接続先がなかったら終了
            if (!_ioConnectionSetting.ConnectableBlockType.Contains(destinationBlockType)) return;


            //接続元から接続先へのブロックの距離を取得
            var distance = destination - _blockPos;

            //接続元ブロックに対応するアウトプット座標があるかチェック
            if (!sourceBlockOutputConnector.Contains(new ConnectDirection(distance))) return;
            //接続先ブロックに対応するインプット座標があるかチェック
            if (!destinationBlockInputConnector.Contains(new ConnectDirection(distance * -1))) return;


            //接続元ブロックと接続先ブロックを接続
            _connectInventory.Add(destinationInputConnector);
        }
        
        /// <summary>
        ///     接続先のブロックの接続可能な位置を取得する
        /// </summary>
        /// <param name="blockType"></param>
        /// <param name="blockDirection"></param>
        /// <returns></returns>
        private static (List<ConnectDirection>, List<ConnectDirection>) GetConnectionPositions(string blockType, BlockDirection blockDirection)
        {
            var rawInputConnector = IOConnectorUtil.IOConnectionData[blockType].InputConnector;
            var rawOutputConnector = IOConnectorUtil.IOConnectionData[blockType].OutputConnector;

            var blockPosConvertAction = blockDirection.GetCoordinateConvertAction();

            var inputPoss = rawInputConnector.Select(ConvertConnectDirection).ToList();
            var outputPoss = rawOutputConnector.Select(ConvertConnectDirection).ToList();

            return (inputPoss, outputPoss);

            #region Internal

            ConnectDirection ConvertConnectDirection(ConnectDirection connectDirection)
            {
                var convertedVector = blockPosConvertAction(connectDirection.ToVector3Int());
                return new ConnectDirection(convertedVector);
            }

            #endregion
        }
    }
}