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
        private readonly IWorldBlockInventoryDatastore _blockInventoryDatastore;
        private readonly IWorldBlockDatastore _blockDatastore;
        private readonly IBlockConfig _blockConfig;
        private readonly Dictionary<string, IoConnectionData> _connectionPositions;

        public BlockPlaceEventToBlockInventoryConnect(IWorldBlockInventoryDatastore blockInventoryDatastore,IBlockPlaceEvent blockPlaceEvent, IBlockConfig blockConfig, IWorldBlockDatastore blockDatastore)
        {
            _blockInventoryDatastore = blockInventoryDatastore;
            _blockConfig = blockConfig;
            _blockDatastore = blockDatastore;
            _connectionPositions = new VanillaBlockInventoryConnectionData().Get();
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }
        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            //設置されたブロックが接続が行われないブロック（何の機能もないただのブロックなど）の時はそのまま終了
            var config = _blockConfig.GetBlockConfig(blockPlaceEvent.Block.GetBlockId());
            if (!_connectionPositions.ContainsKey(config.Type)) return;
            
            var (inputConnector, outputConnector) = GetConnectionPositions(config.Type,blockPlaceEvent.BlockDirection);
            
            //接続先のブロックを取得して接続する
            foreach (var pos in inputConnector)
            {
                var connectX = blockPlaceEvent.Coordinate.X + pos.North;
                var connectY = blockPlaceEvent.Coordinate.Y + pos.East;
                //接続先にブロックがなければそのままスルー
                if (!_blockInventoryDatastore.ExistsBlockInventory(connectX, connectY)) continue;
                
                //接続先のブロックのデータを取得
                var outputBlockDirection = _blockDatastore.GetBlockDirection(connectX, connectY);
                var outputBlockType = _blockConfig.GetBlockConfig(_blockDatastore.GetBlock(connectX, connectY).GetBlockId()).Type;
                var (_,outputBlockOutputConnector) = GetConnectionPositions(outputBlockType,outputBlockDirection);
                //接続しようとする方向が接続しようとするブロックのアウトプット可能な向きにない場合はスルー
                if (!outputBlockOutputConnector.Contains(new ConnectionPosition(-connectX,-connectY))) continue;
                
                //接続先のブロックと接続
                _blockInventoryDatastore.GetBlock(blockPlaceEvent.Coordinate.X, blockPlaceEvent.Coordinate.Y).AddConnector(_blockInventoryDatastore.GetBlock(connectX,connectY));
                
            }
            
            //TODO 周り4つのブロックを確認し、そのブロックがインプット可能なブロックであれば接続する
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