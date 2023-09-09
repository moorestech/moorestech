using System.Collections.Generic;
using System.Linq;
using Core.Block.BlockInventory;
using Core.Block.Config;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler.InventoryEvent
{
    /// <summary>
    /// ブロックが設置された時、そのブロックの周囲にあるインベントリブロックと接続を行います
    /// </summary>
    public class BlockPlaceEventToBlockInventoryConnect
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IBlockConfig _blockConfig;
        private readonly Dictionary<string, IoConnectionData> _ioConnectionDataDictionary = VanillaBlockInventoryConnectionData.IoConnectionData;

        public BlockPlaceEventToBlockInventoryConnect(IBlockPlaceEvent blockPlaceEvent, IBlockConfig blockConfig, IWorldBlockDatastore worldBlockDatastore)
        {
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }

        /// <summary>
        /// 置かれたブロックの東西南北にあるブロックと接続を試みる
        /// </summary>
        /// <param name="blockPlaceEvent"></param>
        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            var connectOffsetBlockPositions = new List<(int, int)>() {(1, 0), (-1, 0), (0, 1), (0, -1)};
            int x = blockPlaceEvent.Coordinate.X;
            int y = blockPlaceEvent.Coordinate.Y;

            foreach (var (offsetX, offsetY) in connectOffsetBlockPositions)
            {
                //接続元を入れ替えて接続を試みる
                ConnectBlock(x, y, x + offsetX, y + offsetY);
                ConnectBlock(x + offsetX, y + offsetY, x, y);
            }
        }

        
        
        /// <summary>
        /// ブロックを接続元から接続先に接続できるなら接続する
        /// その場所にブロックがあるか、
        /// そのブロックのタイプはioConnectionDataDictionaryにあるか、
        /// それぞれインプットとアウトプットの向きはあっているかを確認し、接続する
        /// </summary>
        private void ConnectBlock(int sourceX, int sourceY, int destinationX, int destinationY)
        {
            //接続元、接続先にBlockInventoryがなければ処理を終了
            if (!_worldBlockDatastore.ExistsComponentBlock<IBlockInventory>(sourceX, sourceY) ||
                !_worldBlockDatastore.ExistsComponentBlock<IBlockInventory>(destinationX, destinationY)) return;


            //接続元のブロックデータを取得
            var sourceBlock = _worldBlockDatastore.GetBlock(sourceX, sourceY);
            var sourceBlockType = _blockConfig.GetBlockConfig(sourceBlock.BlockId).Type;
            //接続元のブロックタイプがDictionaryになければ処理を終了
            if (!_ioConnectionDataDictionary.ContainsKey(sourceBlockType)) return;

            var (_, sourceBlockOutputConnector) =
                GetConnectionPositions(
                    sourceBlockType,
                    _worldBlockDatastore.GetBlockDirection(sourceX, sourceY));


            //接続先のブロックデータを取得
            var destinationBlock = _worldBlockDatastore.GetBlock(destinationX, destinationY);
            var destinationBlockType = _blockConfig.GetBlockConfig(destinationBlock.BlockId).Type;
            //接続先のブロックタイプがDictionaryになければ処理を終了
            if (!_ioConnectionDataDictionary.ContainsKey(destinationBlockType)) return;

            var (destinationBlockInputConnector, _) =
                GetConnectionPositions(
                    destinationBlockType,
                    _worldBlockDatastore.GetBlockDirection(destinationX, destinationY));
            
            
            //接続元の接続可能リストに接続先がなかったら終了
            if (!_ioConnectionDataDictionary[sourceBlockType].ConnectableBlockType.Contains(destinationBlockType)) return;



                //接続元から接続先へのブロックの距離を取得
            var distanceX = destinationX - sourceX;
            var distanceY = destinationY - sourceY;

            //接続元ブロックに対応するアウトプット座標があるかチェック
            if (!sourceBlockOutputConnector.Contains(new ConnectDirection(distanceY, distanceX))) return;
            //接続先ブロックに対応するインプット座標があるかチェック
            if (!destinationBlockInputConnector.Contains(new ConnectDirection(-distanceY,-distanceX))) return;


            //接続元ブロックと接続先ブロックを接続
            _worldBlockDatastore.GetBlock<IBlockInventory>(sourceX, sourceY).AddOutputConnector(
                _worldBlockDatastore.GetBlock<IBlockInventory>(destinationX, destinationY));
        }

        /// <summary>
        /// 接続先のブロックの接続可能な位置を取得する
        /// </summary>
        /// <param name="blockType"></param>
        /// <param name="blockDirection"></param>
        /// <returns></returns>
        private (List<ConnectDirection>, List<ConnectDirection>) GetConnectionPositions(string blockType,
            BlockDirection blockDirection)
        {
            var rawInputConnector = _ioConnectionDataDictionary[blockType].InputConnector;
            var rawOutputConnector = _ioConnectionDataDictionary[blockType].OutputConnector;
            var inputConnectionPositions = new List<ConnectDirection>();
            var outputConnectionPositions = new List<ConnectDirection>();

            //デフォルトは北向きなので、北向き以外の時は値を変更
            switch (blockDirection)
            {
                case BlockDirection.North:
                    inputConnectionPositions = rawInputConnector.ToList();
                    outputConnectionPositions = rawOutputConnector.ToList();
                    break;
                case BlockDirection.East:
                    inputConnectionPositions =
                        rawInputConnector.Select(p => new ConnectDirection(-p.East, p.North)).ToList();
                    outputConnectionPositions = rawOutputConnector.Select(p => new ConnectDirection(-p.East, p.North))
                        .ToList();
                    break;
                case BlockDirection.South:
                    inputConnectionPositions = rawInputConnector.Select(p => new ConnectDirection(-p.North, -p.East))
                        .ToList();
                    outputConnectionPositions =
                        rawOutputConnector.Select(p => new ConnectDirection(-p.North, -p.East)).ToList();
                    break;
                case BlockDirection.West:
                    inputConnectionPositions =
                        rawInputConnector.Select(p => new ConnectDirection(p.East, -p.North)).ToList();
                    outputConnectionPositions = rawOutputConnector.Select(p => new ConnectDirection(p.East, -p.North))
                        .ToList();
                    break;
            }

            return (inputConnectionPositions, outputConnectionPositions);
        }
    }
}