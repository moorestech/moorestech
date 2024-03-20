using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Game.Block.BlockInventory;
using Game.Block.Component.IOConnector;
using Game.Block.Interface.BlockConfig;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using UnityEngine;

namespace Game.World.EventHandler.InventoryEvent
{
    /// <summary>
    ///     ブロックが設置された時、そのブロックの周囲にあるインベントリブロックと接続を行います
    /// </summary>
    public class BlockPlaceEventToBlockInventoryConnect
    {
        private readonly IBlockConfig _blockConfig;

        private readonly Dictionary<string, IOConnectionSetting> _ioConnectionDataDictionary = VanillaBlockInventoryConnectionData.IOConnectionData;

        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public BlockPlaceEventToBlockInventoryConnect(IBlockPlaceEvent blockPlaceEvent, IBlockConfig blockConfig,
            IWorldBlockDatastore worldBlockDatastore)
        {
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }

        /// <summary>
        ///     置かれたブロックの東西南北にあるブロックと接続を試みる
        /// </summary>
        /// <param name="blockPlaceEvent"></param>
        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            var connectOffsetBlockPositions = new List<Vector3Int>
            {
                new(1, 0,0), new(-1, 0,0), 
                new(0, 1,0), new(0, -1,0),
                new(0, 0,1), new(0, 0,-1),
            };
            var pos = blockPlaceEvent.Pos;

            foreach (var offset in connectOffsetBlockPositions)
            {
                //接続元を入れ替えて接続を試みる
                ConnectBlock(pos, pos + offset);
                ConnectBlock(pos + offset, pos);
            }
        }


        /// <summary>
        ///     ブロックを接続元から接続先に接続できるなら接続する
        ///     その場所にブロックがあるか、
        ///     そのブロックのタイプはioConnectionDataDictionaryにあるか、
        ///     それぞれインプットとアウトプットの向きはあっているかを確認し、接続する
        /// </summary>
        private void ConnectBlock(Vector3Int source, Vector3Int destination)
        {
            //接続元、接続先にBlockInventoryがなければ処理を終了
            if (!_worldBlockDatastore.ExistsComponentBlock<IBlockInventory>(source) ||
                !_worldBlockDatastore.ExistsComponentBlock<IBlockInventory>(destination)) return;


            //接続元のブロックデータを取得
            var sourceBlock = _worldBlockDatastore.GetBlock(source);
            var sourceBlockType = _blockConfig.GetBlockConfig(sourceBlock.BlockId).Type;
            //接続元のブロックタイプがDictionaryになければ処理を終了
            if (!_ioConnectionDataDictionary.ContainsKey(sourceBlockType)) return;

            var (_, sourceBlockOutputConnector) =
                GetConnectionPositions(
                    sourceBlockType,
                    _worldBlockDatastore.GetBlockDirection(source));


            //接続先のブロックデータを取得
            var destinationBlock = _worldBlockDatastore.GetBlock(destination);
            var destinationBlockType = _blockConfig.GetBlockConfig(destinationBlock.BlockId).Type;
            //接続先のブロックタイプがDictionaryになければ処理を終了
            if (!_ioConnectionDataDictionary.ContainsKey(destinationBlockType)) return;

            var (destinationBlockInputConnector, _) = GetConnectionPositions(destinationBlockType, _worldBlockDatastore.GetBlockDirection(destination));


            //接続元の接続可能リストに接続先がなかったら終了
            if (!_ioConnectionDataDictionary[sourceBlockType].ConnectableBlockType.Contains(destinationBlockType)) return;


            //接続元から接続先へのブロックの距離を取得
            var distance = destination - source;

            //接続元ブロックに対応するアウトプット座標があるかチェック
            if (!sourceBlockOutputConnector.Contains(new ConnectDirection(distance))) return;
            //接続先ブロックに対応するインプット座標があるかチェック
            if (!destinationBlockInputConnector.Contains(new ConnectDirection(distance * -1))) return;


            //接続元ブロックと接続先ブロックを接続
            _worldBlockDatastore.GetBlock<IBlockInventory>(source).AddOutputConnector(
                _worldBlockDatastore.GetBlock<IBlockInventory>(destination));
        }
        
        /// <summary>
        ///     接続先のブロックの接続可能な位置を取得する
        /// </summary>
        /// <param name="blockType"></param>
        /// <param name="blockDirection"></param>
        /// <returns></returns>
        private (List<ConnectDirection>, List<ConnectDirection>) GetConnectionPositions(string blockType, BlockDirection blockDirection)
        {
            var rawInputConnector = _ioConnectionDataDictionary[blockType].InputConnector;
            var rawOutputConnector = _ioConnectionDataDictionary[blockType].OutputConnector;

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