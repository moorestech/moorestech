using Game.Block.Interface;
using UnityEngine;

namespace Game.World.Interface.Event
{
    public class BlockRemoveEventProperties
    {
        public readonly IBlock Block;
        public readonly Vector3Int Pos;

        public BlockRemoveEventProperties(Vector3Int pos, IBlock block)
        {
            Pos = pos;
            Block = block;
        }
    }
}