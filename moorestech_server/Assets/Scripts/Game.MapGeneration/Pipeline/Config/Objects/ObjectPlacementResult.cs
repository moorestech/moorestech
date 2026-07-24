using UnityEngine;

namespace Game.MapGeneration.Pipeline.Config
{
    // 岩クラスターのメタデータ（重心・方向・長さ・ヒーロー岩位置・足跡半径）。
    // Rock-cluster metadata (centroid, direction, length, hero position, footprint radius).
    public struct RockClusterInfo
    {
        public int ClusterId;
        public Vector3 Center;
        public float Angle;
        public float Length;
        public Vector3 HeroCenter;
        public float FootprintRadius;
    }

    // 1件のオブジェクト配置結果。prefab はスキーマ化で mapObjectGuid（文字列）へ置換した。
    // A single object placement result; prefab replaced by mapObjectGuid (string) via schema migration.
    public struct ObjectPlacementResult
    {
        public string MapObjectGuid;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        public float Sink;
        public float MeshRadius;
        public RockClusterInfo ClusterInfo;
    }
}
