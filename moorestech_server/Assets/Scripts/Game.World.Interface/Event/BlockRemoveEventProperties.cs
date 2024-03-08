using Game.Block.Interface;
using UnityEngine;

namespace Game.World.Interface.Event
{
    public class BlockRemoveEventProperties
    {
        public readonly Vector2Int Pos;
        public readonly IBlock Block;

        public BlockRemoveEventProperties(Vector2Int pos, IBlock block)
        {
            Pos = pos;
            Block = block;
        }
    }
}