using UnityEngine;
using UnityEngine.Serialization;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// Primary(大岩)クラスター + 任意数のSecondary配置グループを一体管理する設定。
    /// secondaries[] に Ring/Saddle モードのグループを自由に追加できる。
    /// </summary>
    [System.Serializable]
    public class ObjectClusterEntry
    {
        // =============================================================
        // Primary（大岩クラスター）— Poisson中心 + 極座標メンバー配置
        // =============================================================
        [Header("Primary（大岩）")]
        [Label("プレハブ")]
        [FormerlySerializedAs("primaryPrefabs")]
        public GameObject[] primary;

        [Label("密度")]
        [Range(0f, 10f)] public float density = 1f;

        [Label("スケール範囲")]
        public Vector2 scaleRange = new Vector2(0.8f, 1.2f);

        [Label("傾斜追従")]
        [Range(0f, 1f)] public float slopeAlignment;

        [Label("埋め込み範囲")]
        public Vector2 sinkRange = Vector2.zero;

        // ノイズ変調 — クラスター中心の空間フィルタリング
        [Header("ノイズ変調")]
        [Label("ノイズ種別")]
        public MapNoiseType noiseType = MapNoiseType.None;
        [Label("周波数")]
        [Range(0.1f, 50f)] public float noiseFrequency = 10f;
        [Label("振幅")]
        [Range(-50f, 50f)] public float noiseAmplitude = 1f;
        [Label("閾値")]
        [Range(0f, 1f)] public float noiseThreshold = 0.5f;

        // クラスター設定
        [Header("クラスター設定")]
        [Label("クラスター数")]
        [Range(1, 50)] public int clusterCount = 8;
        [Label("メンバー数")]
        [Range(1, 15)] public int objectsPerCluster = 4;
        [Label("クラスター半径(m)")]
        [Range(1f, 50f)] public float clusterRadius = 12f;

        // Tree距離制約
        [Header("Tree距離制約")]
        [Label("Tree最小距離")]
        public float minDistanceFromTree;

        // =============================================================
        // 従属配置グループ（Ring/Saddle を任意数追加）
        // =============================================================
        [Header("従属配置グループ")]
        [Label("従属グループ")]
        public ObjectClusterSecondary[] secondaries;
    }
}
