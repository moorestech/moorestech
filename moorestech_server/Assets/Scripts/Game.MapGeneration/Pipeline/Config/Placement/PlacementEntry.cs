using UnityEngine;

namespace Game.MapGeneration.Pipeline.Config
{
    // Tree/Object/Ore 配置の統一出力。prefab 参照は mapObjectGuid（Ore は veinGuid）文字列へ置換した。
    // Unified placement output; prefab replaced by mapObjectGuid (veinGuid for ore) string.
    // 生成は種別別ファクトリー経由だけに絞る。オブジェクト初期化子を塞ぎSurroundEffectの付け忘れをコンパイルエラーにする
    // Construction is narrowed to the per-kind factories; blocking object initializers turns a missing SurroundEffect into a compile error
    public readonly struct PlacementEntry
    {
        // 配置対象の GUID 文字列（Tree/Object=mapObjectGuid、Ore=veinGuid）。
        // Target GUID string (mapObjectGuid for tree/object, veinGuid for ore).
        public readonly string MapObjectGuid;
        public readonly Vector3 WorldPosition;
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;
        public readonly float Sink;

        // 所属クラスタ。クラスタを組まない配置はnull
        // The owning cluster; a placement that forms no cluster is null
        public readonly RockClusterInfo? Cluster;

        // terrainSurroundEffectTypeを写す。見た目ステージのみ読む
        // Copies terrainSurroundEffectType; only the visual stages read it
        public readonly TerrainSurroundEffectType SurroundEffect;

        PlacementEntry(
            string mapObjectGuid, Vector3 worldPosition, Quaternion rotation, Vector3 scale, float sink,
            RockClusterInfo? cluster, TerrainSurroundEffectType surroundEffect)
        {
            MapObjectGuid = mapObjectGuid;
            WorldPosition = worldPosition;
            Rotation = rotation;
            Scale = scale;
            Sink = sink;
            Cluster = cluster;
            SurroundEffect = surroundEffect;
        }

        // 木はクラスタを組まないので、クラスタ引数そのものを持たせない
        // A tree never forms a cluster, so no cluster argument exists here at all
        public static PlacementEntry CreateTree(
            string mapObjectGuid, Vector3 worldPosition, Quaternion rotation, Vector3 scale, float sink,
            TerrainSurroundEffectType surroundEffect) =>
            new PlacementEntry(mapObjectGuid, worldPosition, rotation, scale, sink, null, surroundEffect);

        // オブジェクトは所属クラスタを必ず宣言する。独立散布はnullを渡す
        // An object always declares its cluster membership; independent scatter passes null
        public static PlacementEntry CreateObject(
            string mapObjectGuid, Vector3 worldPosition, Quaternion rotation, Vector3 scale, float sink,
            RockClusterInfo? cluster, TerrainSurroundEffectType surroundEffect) =>
            new PlacementEntry(mapObjectGuid, worldPosition, rotation, scale, sink, cluster, surroundEffect);

        // 鉱脈は位置からAABBを組むだけで、回転・スケール・沈み込みもクラスタも持たない
        // A vein only builds its AABB from the position; it carries no rotation, scale, sink or cluster
        public static PlacementEntry CreateVein(
            string veinGuid, Vector3 worldPosition, TerrainSurroundEffectType surroundEffect) =>
            new PlacementEntry(veinGuid, worldPosition, Quaternion.identity, Vector3.one, 0f, null, surroundEffect);

        // 位置とクラスタ重心は同じ座標系に居るため、必ず同じシフトで一緒に動かす
        // The position and the cluster centroid share one frame, so they always move together by the same shift
        public PlacementEntry Shifted(Vector3 shift)
        {
            var shiftedCluster = Cluster;
            if (shiftedCluster.HasValue)
            {
                var cluster = shiftedCluster.Value;
                cluster.Center += shift;
                shiftedCluster = cluster;
            }

            return new PlacementEntry(
                MapObjectGuid, WorldPosition + shift, Rotation, Scale, Sink, shiftedCluster, SurroundEffect);
        }
    }
}
