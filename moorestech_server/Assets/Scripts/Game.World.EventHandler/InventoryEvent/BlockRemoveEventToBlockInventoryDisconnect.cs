using System.Collections.Generic;
using Game.Block.BlockInventory;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler.InventoryEvent
{
    /// <summary>
    ///     ブロックが削除されたとき、そのブロックと接続しているブロックを削除する
    /// </summary>
    public class BlockRemoveEventToBlockInventoryDisconnect
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public BlockRemoveEventToBlockInventoryDisconnect(IBlockRemoveEvent blockRemoveEvent,
            IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
            blockRemoveEvent.Subscribe(OnRemoveBlock);
        }

        private void OnRemoveBlock(BlockRemoveEventProperties blockRemoveEvent)
        {
            var x = blockRemoveEvent.CoreVector2Int.x;
            var y = blockRemoveEvent.CoreVector2Int.y;

            //削除されたブロックがIBlockInventoryでない場合、処理を終了する
            if (blockRemoveEvent.Block is not IBlockInventory block) return;


            //削除されたブロックの東西南北にあるブロックインベントリを削除する
            var connectOffsetBlockPositions = new List<(int, int)> { (1, 0), (-1, 0), (0, 1), (0, -1) };

            foreach (var (offsetX, offsetY) in connectOffsetBlockPositions)
                //削除されたブロックの周りのブロックがIBlockInventoryを持っている時
                if (_worldBlockDatastore.ExistsComponentBlock<IBlockInventory>(x + offsetX, y + offsetY))
                    //そのブロックの接続を削除する
                    _worldBlockDatastore.GetBlock<IBlockInventory>(x + offsetX, y + offsetY)
                        .RemoveOutputConnector(block);
        }
    }
}