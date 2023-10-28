using Core.Util;
using Game.Block.Interface;
using Game.World.Interface.DataStore;

namespace Game.World.Interface.Event
{
    public class BlockRemoveEventProperties
    {
        public readonly IBlock Block;
        public readonly CoreVector2Int CoreVector2Int;

        public BlockRemoveEventProperties(CoreVector2Int coreVector2Int, IBlock block)
        {
            CoreVector2Int = coreVector2Int;
            Block = block;
        }
    }
}