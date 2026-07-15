using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// クラスター単位のメタデータ。岩群の重心・方向・長さを保持し、
    /// TreePlacement・Mudテクスチャがクラスター単位で処理するために使用。
    /// </summary>
    public struct RockClusterInfo
    {
        public int ClusterId;
        // クラスター重心（ワールド座標）
        public Vector3 Center;
        // 長軸方向の角度（ラジアン）
        public float Angle;
        // 長軸方向の全長（m）
        public float Length;
        // ヒーロー岩の位置（ワールド座標）。Secondary/Rubbleの従属配置基準
        public Vector3 HeroCenter;
        // クラスター内の全メンバーの足跡半径（従属配置の回避距離に使用）
        public float FootprintRadius;
    }

    /// <summary>
    /// 1つのプレハブ配置情報。ObjectPlacementGenerator → TerrainApplier へ渡される。
    /// </summary>
    public struct ObjectPlacementResult
    {
        public GameObject Prefab;
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;
        // TerrainApplierが地面スナップ時に使用する沈み込み量
        public float Sink;
        // プレハブメッシュのXZ平面での概算半径（スケール適用済み）。
        // RubblePatchがメッシュ外周に瓦礫を配置するために使用
        public float MeshRadius;
        // クラスターモードで生成された場合のクラスター情報（-1はクラスター外）
        public RockClusterInfo ClusterInfo;
    }
}
