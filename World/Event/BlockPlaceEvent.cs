using Core.Block;
using World.DataStore;

namespace World.Event
{
    public static class BlockPlaceEvent
    {
        public delegate void PutBlockEvent(Coordinate coordinate,IBlock block);
        private static event PutBlockEvent OnBlockPutEvent;

        public static void Subscribe(PutBlockEvent @event) { OnBlockPutEvent += @event; }
        public static void UnSubscribe(PutBlockEvent @event) { OnBlockPutEvent -= @event; }

        internal static void OnBlockPutEventInvoke(Coordinate coordinate,IBlock block)
        {
            OnBlockPutEvent(coordinate, block);
        }
    }
}