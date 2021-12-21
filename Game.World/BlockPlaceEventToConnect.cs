using Game.World.Interface;
using Game.World.Interface.Event;

namespace World
{
    /// <summary>
    /// ブロックが設置された時、そのブロックの周囲にあるインベントリブロックと接続を行います
    /// </summary>
    public class BlockPlaceEventToConnect
    {
        private readonly IWorldBlockInventoryDatastore _worldBlockInventoryDatastore;

        public BlockPlaceEventToConnect(IWorldBlockInventoryDatastore worldBlockInventoryDatastore,IBlockPlaceEvent blockPlaceEvent)
        {
            _worldBlockInventoryDatastore = worldBlockInventoryDatastore;
            blockPlaceEvent.Subscribe(OnBlockPlace);
        }
        private void OnBlockPlace(BlockPlaceEventProperties blockPlaceEvent)
        {
        }
    }
}