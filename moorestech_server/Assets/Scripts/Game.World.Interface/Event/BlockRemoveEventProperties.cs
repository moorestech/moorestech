using Game.Block.Interface;
using UnityEngine;

namespace Game.World.Interface.Event
{
    public class BlockRemoveEventProperties
    {
        public readonly Vector3Int Pos;
        public readonly IBlock Block;

        public BlockRemoveEventProperties(Vector3Int pos, IBlock block)
        {
            Pos = pos;
            Block = block;
        }
    }
}