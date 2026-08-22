using Game.MapGeneration.Pipeline.Config;
using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Placement
{
    // 生成が配置した1件を、見た目ステージが要る全情報（クラスタ・種別込み）でシーン座標に持つ。生成システムの外へは出ない
    // One generated placement with everything the visual stages need (cluster and kind included), in scene space; it never leaves the generation system
    public readonly struct LedgerPlacement
    {
        public readonly string Guid;
        public readonly Vector3 ScenePosition;
        public readonly Vector3 Scale;
        public readonly TerrainSurroundEffectType SurroundEffect;

        // クラスタ無しはnull。-1番兵＋未使用重心は使わない
        // "No cluster" is null; no -1 sentinel plus an unused centroid
        public readonly PlacementCluster? Cluster;

        public LedgerPlacement(string guid, Vector3 scenePosition, Vector3 scale,
            TerrainSurroundEffectType surroundEffect, PlacementCluster? cluster)
        {
            Guid = guid;
            ScenePosition = scenePosition;
            Scale = scale;
            SurroundEffect = surroundEffect;
            Cluster = cluster;
        }
    }
}
