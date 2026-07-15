using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// バイオームごとの岩石・小物（Object）配置設定。
    /// clusterEntries で Primary→Secondary→RubblePatch の階層配置を、
    /// entries で独立散布を管理する。
    /// </summary>
    [System.Serializable]
    public class BiomeObjectConfig
    {
        /// <summary>
        /// Independent（独立散布）用のオブジェクト配置エントリ。
        /// Poisson Disk または旧バックボーンクラスターで配置する。
        /// </summary>
        [System.Serializable]
        public class ObjectEntry
        {
            // =================================================================
            // プレハブ＋密度
            // =================================================================
            [Header("プレハブ＋密度")]
            [Label("プレハブ")]
            public GameObject[] prefabs;
            [Label("密度")]
            [Range(0f, 10f)] public float density = 1f;

            // =================================================================
            // 外観
            // =================================================================
            [Header("外観")]
            [Label("スケール範囲")]
            public Vector2 scaleRange = new Vector2(0.8f, 1.2f);
            [Label("傾斜追従")]
            [Range(0f, 1f)] public float slopeAlignment = 0f;
            [Label("埋め込み範囲")]
            public Vector2 sinkRange = Vector2.zero;

            // =================================================================
            // ノイズ変調
            // =================================================================
            [Header("ノイズ変調")]
            [Label("ノイズ種別")]
            public MapNoiseType noiseType = MapNoiseType.None;
            [Label("周波数")]
            [Range(0.1f, 50f)] public float noiseFrequency = 10f;
            [Label("振幅")]
            [Range(-50f, 50f)] public float noiseAmplitude = 1f;
            [Label("閾値")]
            [Range(0f, 1f)] public float noiseThreshold = 0.5f;

            // =================================================================
            // 傾斜フィルタ
            // =================================================================
            [Header("傾斜フィルタ")]
            [Label("傾斜フィルタ有効")]
            public bool useSlopeFilter;
            [Label("傾斜下限(°)")]
            [Range(0f, 90f)] public float slopeMin = 0f;
            [Label("傾斜上限(°)")]
            [Range(0f, 90f)] public float slopeMax = 90f;
            [Label("傾斜スムーズ幅")]
            [Range(0f, 20f)] public float slopeSmoothness = 4f;

            // =================================================================
            // クラスターモード（旧バックボーン互換）
            // =================================================================
            [Header("クラスターモード")]
            [Label("クラスターモード有効")]
            public bool useClusterMode;
            [Label("クラスター数")]
            [Range(1, 50)] public int clusterCount = 8;
            [Label("メンバー数")]
            [Range(1, 15)] public int objectsPerCluster = 4;
            [Label("クラスター半径(m)")]
            [Range(1f, 50f)] public float clusterRadius = 12f;

            // =================================================================
            // Tree距離制約
            // =================================================================
            [Header("Tree距離制約")]
            [Label("Tree最小距離")]
            public float minDistanceFromTree;
            [Label("Tree最大距離")]
            public float maxDistanceFromTree;
        }

        // Primary→Secondary→RubblePatch 階層配置
        [Header("クラスター配置")]
        [Label("クラスターエントリ")]
        public ObjectClusterEntry[] clusterEntries = new ObjectClusterEntry[0];

        // 独立散布
        [Header("独立配置")]
        [Label("独立エントリ")]
        public ObjectEntry[] entries = new ObjectEntry[0];

        [Header("配置アルゴリズム")]
        [Label("オブジェクトアルゴリズム設定")]
        public ObjectAlgorithmConfig algorithmConfig = new ObjectAlgorithmConfig();

        [Header("岩周辺テクスチャ")]
        [Label("裸地テクスチャ設定")]
        public ObjectSurroundTextureConfig surroundTextureConfig = new ObjectSurroundTextureConfig();

        // バイオーム境界からこの距離(m)以内にはオブジェクトを配置しない
        [Header("境界マージン")]
        [Label("境界マージン(m)")]
        [Range(0f, 20f)] public float borderMargin = 0f;
    }
}
