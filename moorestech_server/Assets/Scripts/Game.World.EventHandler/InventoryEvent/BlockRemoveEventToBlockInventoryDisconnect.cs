using System.Collections.Generic;
using Game.Block.BlockInventory;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;
using UnityEngine;

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
            var removePos = blockRemoveEvent.Pos;

            //削除されたブロックがIBlockInventoryでない場合、処理を終了する
            if (blockRemoveEvent.Block is not IBlockInventory block) return;


            //削除されたブロックの東西南北にあるブロックインベントリを削除する
            var connectOffsetBlockPositions = new List<Vector3Int>
            {
                new(1, 0,0), new (-1, 0,0), 
                new(0, 1,0), new(0, -1,0),
                new(0, 0,1), new(0, 0,-1),
            };

            foreach (var offsetpos in connectOffsetBlockPositions)
                //削除されたブロックの周りのブロックがIBlockInventoryを持っている時
                if (_worldBlockDatastore.ExistsComponent<IBlockInventory>(offsetpos + removePos))
                    //そのブロックの接続を削除する
                    Debug.Log("TODO remove output connector");
                    //_worldBlockDatastore.GetBlock<IBlockInventory>(offsetpos + removePos).RemoveOutputConnector(block);
        }
    }
}