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
        public readonly Quaternion Rotation;
        public readonly Vector3 Scale;
        public readonly TerrainSurroundEffectType SurroundEffect;
        public readonly int ClusterId;
        public readonly Vector2 ClusterCenter;

        public LedgerPlacement(string guid, Vector3 scenePosition, Quaternion rotation, Vector3 scale,
            TerrainSurroundEffectType surroundEffect, int clusterId, Vector2 clusterCenter)
        {
            Guid = guid;
            ScenePosition = scenePosition;
            Rotation = rotation;
            Scale = scale;
            SurroundEffect = surroundEffect;
            ClusterId = clusterId;
            ClusterCenter = clusterCenter;
        }
    }
}
