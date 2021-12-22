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
        private readonly IWorldBlockInventoryDatastore _blockDatastore;
        private readonly IBlockConfig _blockConfig;
        private readonly Dictionary<string, ConnectionPosition[]> _connectionPositions;

        public BlockPlaceEventToBlockInventoryConnect(IWorldBlockInventoryDatastore blockDatastore,IBlockPlaceEvent blockPlaceEvent, IBlockConfig blockConfig)
        {
            _blockDatastore = blockDatastore;
            _blockConfig = blockConfig;
            _connectionPositions = new VanillaBlockInventoryConnectionData().Get();
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }
        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
            var config = _blockConfig.GetBlockConfig(blockPlaceEvent.Block.GetBlockId());
            //設置されたブロックが接続が行われないブロック（何の機能もないただのブロックなど）の時はそのまま終了
            if (!_connectionPositions.ContainsKey(config.Type)) return;
            
            //接続先のブロックの位置を取得し、設置されたブロックの方角に応じて接続先のブロックの位置を調整
            var connectionPositions = _connectionPositions[config.Type];
            //デフォルトは北向きなので、北向き以外の時は値を変更
            switch (blockPlaceEvent.BlockDirection)
            {
                case BlockDirection.East:
                    connectionPositions = connectionPositions.Select(p => new ConnectionPosition(-p.East,p.North)).ToArray();
                    break;
                case BlockDirection.South:
                    connectionPositions = connectionPositions.Select(p => new ConnectionPosition(-p.North,-p.East)).ToArray();
                    break;
                case BlockDirection.West:
                    connectionPositions = connectionPositions.Select(p => new ConnectionPosition(p.East,-p.North)).ToArray();
                    break;
            }
            
            //接続先のブロックを取得して接続する
            foreach (var pos in connectionPositions)
            {
                var co = blockPlaceEvent.Coordinate;
                ((IBlockInventory)blockPlaceEvent.Block).AddConnector(_blockDatastore.GetBlock(co.X + pos.North, co.Y + pos.East));
            }
            
        }
    }
}