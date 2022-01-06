using System.Collections.Generic;
using Core.Block.BlockInventory;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace World.EventListener
{
    public class BlockRemoveEventToBlockInventoryDisconnect
    {
        private readonly IWorldBlockComponentDatastore<IBlockInventory> _worldBlockInventoryDatastore;

        public BlockRemoveEventToBlockInventoryDisconnect(
            IWorldBlockComponentDatastore<IBlockInventory> worldBlockInventoryDatastore,
            IBlockRemoveEvent blockRemoveEvent)
        {
            _worldBlockInventoryDatastore = worldBlockInventoryDatastore;
            blockRemoveEvent.Subscribe(OnRemoveBlock);
        }

        private void OnRemoveBlock(BlockRemoveEventProperties blockRemoveEvent)
        {
            var x = blockRemoveEvent.Coordinate.X;
            var y = blockRemoveEvent.Coordinate.Y;

            //削除されたブロックがIBlockInventoryでない場合、処理を終了する
            if (!(blockRemoveEvent.Block is IBlockInventory)) return;


            //削除されたブロックの東西南北にあるブロックインベントリを削除する
            var connectOffsetBlockPositions = new List<(int, int)> {(1, 0), (-1, 0), (0, 1), (0, -1)};

            foreach (var (offsetX, offsetY) in connectOffsetBlockPositions)
                //削除されたブロックの周りのブロックがIBlockInventoryを持っている時
                if (_worldBlockInventoryDatastore.ExistsComponentBlock(x + offsetX, y + offsetY))
                    //そのブロックの接続を削除する
                    _worldBlockInventoryDatastore.GetBlock(x + offsetX, y + offsetY)
                        .RemoveOutputConnector((IBlockInventory) blockRemoveEvent.Block);
        }
    }
}