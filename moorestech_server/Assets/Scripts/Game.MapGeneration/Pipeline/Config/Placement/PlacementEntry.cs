using UnityEngine;

namespace Game.MapGeneration.Pipeline.Config
{
    // Tree/Object/Ore 配置の統一出力。prefab 参照は mapObjectGuid（Ore は veinGuid）文字列へ置換した。
    // Unified placement output; prefab replaced by mapObjectGuid (veinGuid for ore) string.
    public struct PlacementEntry
    {
        // 配置対象の GUID 文字列（Tree/Object=mapObjectGuid、Ore=veinGuid）。
        // Target GUID string (mapObjectGuid for tree/object, veinGuid for ore).
        public string MapObjectGuid;
        public Vector3 WorldPosition;
        public Quaternion Rotation;
        public Vector3 Scale;
        public float Sink;
        public RockClusterInfo? Cluster;
    }
}
