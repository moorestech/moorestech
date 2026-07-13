using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// バイオームごとのテクスチャレイヤー設定。
    /// SplatmapGenerator が傾斜フィルタとノイズ変調を適用してブレンドウェイトを決定する。
    /// </summary>
    [System.Serializable]
    public class BiomeTextureConfig
    {
        [System.Serializable]
        public class TextureEntry
        {
            // =================================================================
            // Step 1: レイヤー＋基本ウェイト — 傾斜・ノイズ適用前の初期値
            // =================================================================
            [Header("Step 1: レイヤー")]
            public TerrainLayer layer;
            [Range(0f, 1f)] public float weight = 1f;

            // =================================================================
            // Step 2: 傾斜フィルタ — 崖面テクスチャ等の切り替え
            // =================================================================
            [Header("Step 2: 傾斜フィルタ")]
            public bool useSlopeFilter;
            [Range(0f, 90f)] public float slopeMin = 0f;
            [Range(0f, 90f)] public float slopeMax = 90f;
            [Range(0f, 20f)] public float slopeSmoothness = 4f;

            // =================================================================
            // Step 3: 高度フィルタ — 標高に応じた切り替え（雪線・海岸線など）
            // =================================================================
            [Header("Step 3: 高度フィルタ")]
            public bool useHeightFilter;
            [Range(0f, 1f)] public float heightMin = 0f;
            [Range(0f, 1f)] public float heightMax = 1f;
            [Range(0f, 0.1f)] public float heightSmoothness = 0.02f;

            // =================================================================
            // Step 4: 曲率フィルタ — 谷底（凹）/尾根（凸）のテクスチャ変化
            // =================================================================
            [Header("Step 4: 曲率フィルタ")]
            public bool useCurvatureFilter;
            [Range(-1f, 1f)] public float curvatureMin = -1f;
            [Range(-1f, 1f)] public float curvatureMax = 1f;
            [Range(0f, 1f)] public float curvatureSmoothness = 0.2f;

            // =================================================================
            // Step 5: ノイズ変調 — ウェイトに自然なばらつきを付与
            // =================================================================
            [Header("Step 5: ノイズ変調")]
            public MapNoiseType noiseType = MapNoiseType.None;
            [Range(0.1f, 50f)] public float noiseFrequency = 10f;
            [Range(-50f, 50f)] public float noiseAmplitude = 1f;
        }

        public TextureEntry[] entries = new TextureEntry[0];
    }
}
