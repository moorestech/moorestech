using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// バイオームごとの草花（Detail）配置設定。
    /// DetailPlacementGenerator が密度マップ生成時に参照する。
    /// </summary>
    [System.Serializable]
    public class BiomeDetailConfig
    {
        [System.Serializable]
        public class DetailEntry
        {
            // =================================================================
            // Step 1: プロトタイプ — 使用する草花メッシュ/テクスチャの定義
            // =================================================================
            [Header("Step 1: プロトタイプ")]
            [Label("プロトタイプ設定")]
            public DetailPrototypeConfig prototypeConfig = new DetailPrototypeConfig();

            // =================================================================
            // Step 2: 密度 — biomeWeightにweightを掛けて基本密度を算出
            // =================================================================
            [Header("Step 2: 密度")]
            [Label("基本ウェイト")]
            [Range(0f, 1f)] public float weight = 1f;
            // 密度がこの範囲外ならスキップ。狭い範囲でまばらな配置を実現
            [Label("ウェイト閾値")]
            public Vector2 weightRange = new Vector2(0f, 1f);
            [Label("最大密度")]
            [Range(1, 16)] public int maxDensity = 16;
            // 他の DetailEntry が既に配置済みの場所を避ける
            [Label("他Detailを避ける")]
            public bool occludedByOthers;

            // =================================================================
            // Step 3: ノイズスタック — 密度に空間的ばらつきを加える
            // =================================================================
            [Header("Step 3: ノイズスタック")]
            [Label("密度ノイズ")]
            public DetailNoiseStack noiseStack = new DetailNoiseStack();

            // =================================================================
            // Step 4: 地形フィルタ — 傾斜/曲率/角度で配置を絞り込む
            // =================================================================
            [Header("Step 4: 傾斜フィルタ")]
            [Label("傾斜")]
            public DetailFilter slopeFilter = DetailFilter.Slope();

            [Header("Step 4: 曲率フィルタ")]
            [Label("曲率")]
            public DetailFilter curvatureFilter = DetailFilter.Curvature();

            [Header("Step 4: 角度フィルタ")]
            [Label("角度（方位）")]
            public DetailFilter angleFilter = DetailFilter.Angle();

            // =================================================================
            // Step 5: 距離フィルタ — Tree/Objectとの近接度で配置を制御
            // =================================================================
            [Header("Step 5: 距離フィルタ")]
            // 最近接 Tree までの距離で配置制御（木の根元に下草を集中させる等）
            [Label("Tree距離")]
            public DetailFilter treeDistanceFilter = DetailFilter.Distance();
            // 最近接 Object までの距離で配置制御
            [Label("Object距離")]
            public DetailFilter objectDistanceFilter = DetailFilter.Distance();

            // =================================================================
            // Step 6: テクスチャフィルタ — Splatmapの特定レイヤー上に限定配置
            // =================================================================
            [Header("Step 6: テクスチャフィルタ")]
            [Label("テクスチャ")]
            public DetailTextureFilter textureFilter = new DetailTextureFilter();
        }

        public DetailEntry[] entries = new DetailEntry[0];

        [Header("配置閾値")]
        [Label("フィルタ棄却閾値")]
        [Range(0.001f, 0.1f)] public float filterRejectThreshold = 0.01f;

        // バイオーム境界からこの距離(m)以内にはDetailを配置しない
        [Header("境界マージン")]
        [Label("境界マージン(m)")]
        [Range(0f, 20f)] public float borderMargin = 0f;
    }
}
