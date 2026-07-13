using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// Tree/Object配置の統一出力struct。
    /// 各ジェネレーターがper-biome呼び出しで返す配置1件分のデータ。
    /// オーケストレーターがTreeInstance/ObjectPlacementResultに変換してUnity APIに渡す。
    /// </summary>
    public struct PlacementEntry
    {
        // 抽選済みのプレハブ参照（PrototypeIndex経由ではなく直接参照）
        public GameObject Prefab;
        // ワールド空間座標
        public Vector3 WorldPosition;
        public Quaternion Rotation;
        public Vector3 Scale;
        // 地面への沈み込み量（m）
        public float Sink;
        // 岩クラスター情報（岩配置のみ非null、Tree系はnull）
        public RockClusterInfo? Cluster;
    }
}
