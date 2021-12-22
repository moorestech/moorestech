using System.Collections.Generic;
using System.Linq;
using Core.Block.BlockInventory;
using Core.Block.Config;
using Game.World.Interface;
using Game.World.Interface.Event;

namespace World
{
    /// <summary>
    /// ブロックが設置された時、そのブロックの周囲にあるインベントリブロックと接続を行います
    /// </summary>
    public class BlockPlaceEventToBlockInventoryConnect
    {
        private readonly IWorldBlockInventoryDatastore _worldBlockInventoryDatastore;
        private readonly IWorldBlockDatastore _worldBlockDatastore;
        private readonly IBlockConfig _blockConfig;
        private readonly Dictionary<string, IoConnectionData> _connectionPositions;

        public BlockPlaceEventToBlockInventoryConnect(IWorldBlockInventoryDatastore worldBlockInventoryDatastore,IBlockPlaceEvent blockPlaceEvent, IBlockConfig blockConfig, IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockInventoryDatastore = worldBlockInventoryDatastore;
            _blockConfig = blockConfig;
            _worldBlockDatastore = worldBlockDatastore;
            _connectionPositions = new VanillaBlockInventoryConnectionData().Get();
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }
        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            //設置されたブロックが接続が行われないブロック（何の機能もないただのブロックなど）の時はそのまま終了
            var config = _blockConfig.GetBlockConfig(blockPlaceEvent.Block.GetBlockId());
            if (!_connectionPositions.ContainsKey(config.Type)) return;
            
            var (inputConnector, outputConnector) = GetConnectionPositions(config.Type,blockPlaceEvent.BlockDirection);
            
            //コネクターを設定
            SetOutputConnector(blockPlaceEvent, outputConnector);
            SetInputConnector(blockPlaceEvent, inputConnector);
        }


        /// <summary>
        /// ブロックのアウトプット先を取得して接続する
        /// </summary>
        private void SetOutputConnector(BlockPlaceEventProperties blockPlaceEvent, List<ConnectionPosition> outputConnector)
        {
            foreach (var pos in outputConnector)
            {
                var connectX = blockPlaceEvent.Coordinate.X + pos.North;
                var connectY = blockPlaceEvent.Coordinate.Y + pos.East;
                //接続先にブロックがなければそのまま次へ
                if (!_worldBlockInventoryDatastore.ExistsBlockInventory(connectX, connectY)) continue;


                //接続先のブロックのデータを取得
                var outputBlockDirection = _worldBlockDatastore.GetBlockDirection(connectX, connectY);
                var outputBlockType =
                    _blockConfig.GetBlockConfig(_worldBlockDatastore.GetBlock(connectX, connectY).GetBlockId()).Type;
                var (_, outputBlockOutputConnector) = GetConnectionPositions(outputBlockType, outputBlockDirection);


                //おいたブロックが接続しようとするブロックのアウトプット可能な向きにない場合は次へ
                if (!outputBlockOutputConnector.Contains(new ConnectionPosition(-connectX, -connectY))) continue;

                //接続先のブロックと接続
                _worldBlockInventoryDatastore.GetBlock(blockPlaceEvent.Coordinate.X, blockPlaceEvent.Coordinate.Y)
                    .AddConnector(_worldBlockInventoryDatastore.GetBlock(connectX, connectY));
            }
        }
        /// <summary>
        /// ブロックのインプット元を取得して接続する
        /// </summary>
        private void SetInputConnector(BlockPlaceEventProperties blockPlaceEvent, List<ConnectionPosition> inputConnector)
        {
            foreach (var pos in inputConnector)
            {
                var connectX = blockPlaceEvent.Coordinate.X + pos.North;
                var connectY = blockPlaceEvent.Coordinate.Y + pos.East;
                if (!_worldBlockInventoryDatastore.ExistsBlockInventory(connectX, connectY)) continue;

                //接続先のブロックのデータを取得
                var inputBlockDirection = _worldBlockDatastore.GetBlockDirection(connectX, connectY);
                var inputBlockType =
                    _blockConfig.GetBlockConfig(_worldBlockDatastore.GetBlock(connectX, connectY).GetBlockId()).Type;
                var (inputBlockInputConnector, _) = GetConnectionPositions(inputBlockType, inputBlockDirection);

                //おいたブロックが接続しようとするブロックのインプット可能な向きにない場合は次へ
                if (!inputBlockInputConnector.Contains(new ConnectionPosition(-connectX, -connectY))) continue;

                //接続元のブロックと接続
                _worldBlockInventoryDatastore.GetBlock(connectX, connectY).AddConnector(
                    _worldBlockInventoryDatastore.GetBlock(blockPlaceEvent.Coordinate.X, blockPlaceEvent.Coordinate.Y));
            }
        }


        private void ConnectBlock(int sourceX, int sourceY, int destinationX, int destinationY)
        {
            //接続元のブロックデータを取得
            var sourceBlock = _worldBlockDatastore.GetBlock(sourceX, sourceY);

        }
        
        /// <summary>
        /// 接続先のブロックの接続可能な位置を取得する
        /// </summary>
        /// <param name="blockType"></param>
        /// <param name="blockDirection"></param>
        /// <returns></returns>
        private (List<ConnectionPosition>,List<ConnectionPosition>) GetConnectionPositions(string blockType,BlockDirection blockDirection)
        {
            var rawInputConnector = _connectionPositions[blockType].InputConnector;
            var rawOutputConnector = _connectionPositions[blockType].OutputConnector;
            var inputConnectionPositions = new List<ConnectionPosition>();
            var outputConnectionPositions = new List<ConnectionPosition>();

            //デフォルトは北向きなので、北向き以外の時は値を変更
            switch (blockDirection)
            {
                case BlockDirection.North:
                    inputConnectionPositions = rawInputConnector.ToList();
                    outputConnectionPositions = rawOutputConnector.ToList();
                    break;
                case BlockDirection.East:
                    inputConnectionPositions = rawInputConnector.Select(p => new ConnectionPosition(-p.East,p.North)).ToList();
                    outputConnectionPositions = rawOutputConnector.Select(p => new ConnectionPosition(-p.East,p.North)).ToList();
                    break;
                case BlockDirection.South:
                    inputConnectionPositions = rawInputConnector.Select(p => new ConnectionPosition(-p.North,-p.East)).ToList();
                    outputConnectionPositions = rawOutputConnector.Select(p => new ConnectionPosition(-p.North,-p.East)).ToList();
                    break;
                case BlockDirection.West:
                    inputConnectionPositions = rawInputConnector.Select(p => new ConnectionPosition(p.East,-p.North)).ToList();
                    outputConnectionPositions = rawOutputConnector.Select(p => new ConnectionPosition(p.East,-p.North)).ToList();
                    break;
            }
            
            return (inputConnectionPositions,outputConnectionPositions);
        }
    }
}