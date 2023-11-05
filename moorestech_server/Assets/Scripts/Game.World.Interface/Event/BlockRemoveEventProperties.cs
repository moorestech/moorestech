using Game.Block.Interface;
using UnityEngine;

namespace Game.World.Interface.Event
{
    public class BlockRemoveEventProperties
    {
        public readonly IBlock Block;
        public readonly Vector2Int CoreVector2Int;

        public BlockRemoveEventProperties(Vector2Int coreVector2Int, IBlock block)
        {
            CoreVector2Int = coreVector2Int;
            Block = block;
        }
    }
}