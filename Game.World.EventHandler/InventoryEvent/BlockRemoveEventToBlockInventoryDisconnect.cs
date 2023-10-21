using System.Collections.Generic;
using Game.Block.BlockInventory;
using Game.World.Interface.DataStore;
using Game.World.Interface.Event;

namespace Game.World.EventHandler.InventoryEvent
{
    /// <summary>
    ///     
    /// </summary>
    public class BlockRemoveEventToBlockInventoryDisconnect
    {
        private readonly IWorldBlockDatastore _worldBlockDatastore;

        public BlockRemoveEventToBlockInventoryDisconnect(IBlockRemoveEvent blockRemoveEvent, IWorldBlockDatastore worldBlockDatastore)
        {
            _worldBlockDatastore = worldBlockDatastore;
            blockRemoveEvent.Subscribe(OnRemoveBlock);
        }

        private void OnRemoveBlock(BlockRemoveEventProperties blockRemoveEvent)
        {
            var x = blockRemoveEvent.Coordinate.X;
            var y = blockRemoveEvent.Coordinate.Y;

            //IBlockInventory
            if (!(blockRemoveEvent.Block is IBlockInventory)) return;


            
            var connectOffsetBlockPositions = new List<(int, int)> { (1, 0), (-1, 0), (0, 1), (0, -1) };

            foreach (var (offsetX, offsetY) in connectOffsetBlockPositions)
                //IBlockInventory
                if (_worldBlockDatastore.ExistsComponentBlock<IBlockInventory>(x + offsetX, y + offsetY))
                    
                    _worldBlockDatastore.GetBlock<IBlockInventory>(x + offsetX, y + offsetY)
                        .RemoveOutputConnector((IBlockInventory)blockRemoveEvent.Block);
        }
    }
}