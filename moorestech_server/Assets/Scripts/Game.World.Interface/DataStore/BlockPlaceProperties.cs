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

        // セーブデータ初期ロード由来の設置か。クライアント向けブロードキャスト抑制に使う
        // Whether this placement comes from initial save load; used to suppress client broadcasts
        public bool IsInitialLoad { get; }

        public BlockPlaceProperties(Vector3Int pos, WorldBlockData blockData, bool isInitialLoad)
        {
            Pos = pos;
            BlockData = blockData;
            IsInitialLoad = isInitialLoad;
        }
    }
}

