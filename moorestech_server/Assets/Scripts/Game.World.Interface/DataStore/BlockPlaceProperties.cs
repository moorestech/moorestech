using Game.Block.Interface;
using UnityEngine;

namespace Game.World.Interface.DataStore
{
    // ブロック配置イベントのプロパティ
    // Properties for block place event
    public class BlockPlaceProperties
    {
        public Vector3Int Pos { get; }
        public WorldBlockData BlockData { get; }
        public bool IsInitialLoad { get; }

        public BlockPlaceProperties(Vector3Int pos, WorldBlockData blockData)
            : this(pos, blockData, false)
        {
        }

        public BlockPlaceProperties(Vector3Int pos, WorldBlockData blockData, bool isInitialLoad)
        {
            Pos = pos;
            BlockData = blockData;
            IsInitialLoad = isInitialLoad;
        }
    }
}

