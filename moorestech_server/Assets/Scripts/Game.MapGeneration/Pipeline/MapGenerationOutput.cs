using System.Collections.Generic;
using UnityEngine;

namespace Game.MapGeneration.Pipeline
{
    // 生成パイプラインの結果値オブジェクト。テクスチャ/スプラットは含まない（サーバー非対象）。
    // 座標はすべて生成タイル基準のシーン座標で、スポーン探索の中央化オフセット G は差し引き済み。
    // Result value object of the generation pipeline; no texture/splat (server-irrelevant).
    // All coordinates are scene-space relative to the generated tile, with the spawn-search offset G already removed.
    public class MapGenerationOutput
    {
        public float[] Heights;            // [Resolution*Resolution] 0-1 正規化高さ / normalized height
        public byte[] BiomeIndices;        // [Resolution*Resolution] BiomeType の値 / BiomeType value
        public int Resolution;             // 1辺のセル数 / cells per side
        public Vector3 SpawnPoint;         // シーン座標のスポーン地点 / spawn point in scene space

        // 生成に使ったノイズ窓の原点。クライアントが分類段を再実行するとき同じ窓を指すのに要る。
        // Origin of the noise window used for generation; clients need it to re-run the classification stage on the same window.
        public Vector2 NoiseOrigin;

        // 生成タイルがシーン上で占める原点(= NoiseOrigin - G)。地形の設置位置はこちらで、出力座標もこれを基準にする。
        // Scene-space origin of the generated tile (= NoiseOrigin - G): where the terrain sits, and the basis of every output coordinate.
        public Vector2 SceneOrigin;

        public List<PlacedMapObject> MapObjects = new List<PlacedMapObject>();
        public List<PlacedVein> ItemVeins = new List<PlacedVein>();
        public List<PlacedVein> FluidVeins = new List<PlacedVein>();
    }

    // 木・石など見た目マップオブジェクト1件（GUID + シーン座標）。
    // One visual map object (tree/rock, etc.): GUID plus scene position.
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
