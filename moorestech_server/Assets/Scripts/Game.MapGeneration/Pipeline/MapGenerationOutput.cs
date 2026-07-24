using System.Collections.Generic;
using UnityEngine;

namespace Game.MapGeneration.Pipeline
{
    // 生成パイプラインの結果値オブジェクト。テクスチャ/スプラットは含まない（サーバー非対象）。
    // Result value object of the generation pipeline; no texture/splat (server-irrelevant).
    public class MapGenerationOutput
    {
        public float[] Heights;            // [Resolution*Resolution] 0-1 正規化高さ / normalized height
        public byte[] BiomeIndices;        // [Resolution*Resolution] BiomeType の値 / BiomeType value
        public int Resolution;             // 1辺のセル数 / cells per side
        public Vector3 SpawnPoint;         // ワールド座標のスポーン地点 / spawn point in world space
        public List<PlacedMapObject> MapObjects = new List<PlacedMapObject>();
        public List<PlacedVein> ItemVeins = new List<PlacedVein>();
    }

    // 木・石など見た目マップオブジェクト1件（GUID + ワールド座標）。
    // One visual map object (tree/rock, etc.): GUID plus world position.
    public class PlacedMapObject
    {
        public string MapObjectGuid;
        public Vector3 Position;
    }

    // 鉱脈クラスター1件（mapVeins マスタの veinGuid + 整数 AABB）。
    // One vein cluster: mapVeins master veinGuid plus an integer AABB.
    public class PlacedVein
    {
        public string VeinGuid;
        public Vector3Int Min;
        public Vector3Int Max;
    }
}
