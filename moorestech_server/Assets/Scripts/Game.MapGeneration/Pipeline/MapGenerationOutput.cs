using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Placement;
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

        // pass-1(配置)からpass-2(見た目)への内部受け渡し専用。結果出力(MapInfoJsonBuilder等)には一切写さない。
        // Internal pass-1(placement) to pass-2(visuals) handoff only; never copied into result output (MapInfoJsonBuilder etc.).
        public PlacementLedger Ledger;
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

        // 所属する岩クラスターの識別子。格子全体で一意で、-1 は独立配置（クラスターに属さない）。
        // Identifier of the owning rock cluster, unique across the grid; -1 marks an independent placement.
        public int ClusterId;

        // クラスターの重心をシーン座標のXZで持つ。ClusterId が -1 のときは中心を持たず (0,0)。
        // The cluster centroid as scene-space XZ; a ClusterId of -1 owns no center and stays at (0,0).
        public Vector2 ClusterCenter;
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
