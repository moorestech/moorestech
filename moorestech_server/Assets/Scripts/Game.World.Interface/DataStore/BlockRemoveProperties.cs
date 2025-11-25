using Game.Block.Interface;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    // ブロック削除イベントのプロパティ
    // Properties for block remove event
    public class BlockRemoveProperties
    {
        public Vector3Int Pos { get; }
        public WorldBlockData BlockData { get; }
        public BlockRemoveReason RemoveReason { get; }
        
        public BlockRemoveProperties(Vector3Int pos, WorldBlockData blockData, BlockRemoveReason removeReason)
        {
            Pos = pos;
            BlockData = blockData;
            RemoveReason = removeReason;
        }
    }
}

