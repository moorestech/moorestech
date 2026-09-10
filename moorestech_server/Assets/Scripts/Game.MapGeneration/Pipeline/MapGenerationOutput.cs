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
        public List<TerrainTileOutput> Tiles = new();  // 格子出力(1タイル以上) / grid output (one or more tiles)
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

    public class PlacedMapObject
    {
        public string MapObjectGuid;
        public Vector3 Position;

        // 配置時のスケール。岩周辺テクスチャの広がりが岩の大きさで決まるため見た目の再構築が読む。
        // The placement scale; the visual rebuild reads it because a rock's surround texture spreads with its size.
        public Vector3 Scale;

        // 配置時の姿勢。斜面法線への傾きとランダムYを配置器が計算しており、落とすと全個体が同じ向きで直立する。
        // The placement rotation; the placers derive the slope tilt and random yaw, and dropping it stands every instance up alike.
        public Quaternion Rotation;
    }

    // 配置点1件ぶんの鉱脈（mapVeins マスタの veinGuid + 点中心の整数 AABB）。
    // One vein per placement point: the mapVeins master veinGuid plus an integer AABB centred on the point.
    // 値型なので保持側は必ず自分のコピーを持ち、出力側のシフトが台帳へ波及しない。
    // A value type gives every holder its own copy, so output-side shifts never reach back into a ledger.
    public readonly struct PlacedVein
    {
        public readonly string VeinGuid;
        public readonly Vector3Int Min;
        public readonly Vector3Int Max;

        public PlacedVein(string veinGuid, Vector3Int min, Vector3Int max)
        {
            VeinGuid = veinGuid;
            Min = min;
            Max = max;
        }

        // シフトは値返しにし、共有インスタンスの破壊的更新を型で塞ぐ。
        // Shifting returns a value, making destructive updates of a shared instance impossible by type.
        public PlacedVein Shifted(Vector3Int offset)
        {
            return new PlacedVein(VeinGuid, Min - offset, Max - offset);
        }
    }
}
