using UnityEngine;

namespace MapGenerator.Pipeline.Config
{
    /// <summary>
    /// Unity DetailPrototype の全プロパティをラップする設定クラス。
    /// MicroVerse の DetailPrototypeSerializable に相当。
    /// Inspector で設定したパラメータから DetailPrototype を生成する。
    /// </summary>
    [System.Serializable]
    public class DetailPrototypeConfig
    {
        [Header("見た目")]
        [Label("メッシュプレハブ")]
        public GameObject prototypeMesh;
        [Label("テクスチャ")]
        public Texture2D prototypeTexture;
        [Label("メッシュを使用")]
        public bool usePrototypeMesh;
        [Label("描画モード")]
        public DetailRenderMode renderMode = DetailRenderMode.Grass;

        [Header("サイズ")]
        [Label("幅（最小）")]
        [Range(0.1f, 10f)] public float minWidth = 0.5f;
        [Label("幅（最大）")]
        [Range(0.1f, 10f)] public float maxWidth = 1.5f;
        [Label("高さ（最小）")]
        [Range(0.1f, 10f)] public float minHeight = 0.5f;
        [Label("高さ（最大）")]
        [Range(0.1f, 10f)] public float maxHeight = 1.5f;

        [Header("配置")]
        // 地面法線に合わせる度合い。0=垂直、1=完全に法線に沿う
        [Label("地面への整列")]
        [Range(0f, 1f)] public float alignToGround = 0f;
        // 配置位置のランダムズレ。0=グリッド、1=完全ランダム
        [Label("位置ジッター")]
        [Range(0f, 1f)] public float positionJitter = 1f;
        [Label("目標カバー率")]
        [Range(0f, 1f)] public float targetCoverage = 1f;
        // テレインホール端からの距離（幅に対する割合）
        [Label("ホール端パディング")]
        [Range(0f, 1f)] public float holeEdgePadding = 0f;
        [Label("ノイズシード")]
        public int noiseSeed = 0;
        [Label("ノイズスプレッド")]
        [Range(0f, 50f)] public float noiseSpread = 0.1f;

        [Header("色")]
        [Label("枯れ色")]
        public Color dryColor = new Color(0.80f, 0.70f, 0.30f, 1f);
        [Label("健康色")]
        public Color healthyColor = new Color(0.26f, 0.97f, 0.16f, 1f);

        [Header("レンダリング")]
        [Label("GPU インスタンシング")]
        public bool useInstancing = true;
        [Label("密度スケーリング")]
        public bool useDensityScaling = false;

        /// <summary>
        /// この設定から Unity の DetailPrototype を生成する。
        /// TerrainApplier が terrainData.detailPrototypes に渡す。
        /// </summary>
        public DetailPrototype ToDetailPrototype()
        {
            var dp = new DetailPrototype
            {
                renderMode = renderMode,
                minWidth = minWidth,
                maxWidth = maxWidth,
                minHeight = minHeight,
                maxHeight = maxHeight,
                noiseSeed = noiseSeed,
                noiseSpread = noiseSpread,
                dryColor = dryColor,
                healthyColor = healthyColor,
                useInstancing = useInstancing,
                usePrototypeMesh = usePrototypeMesh,
                alignToGround = alignToGround,
                positionJitter = positionJitter,
                targetCoverage = targetCoverage,
                holeEdgePadding = holeEdgePadding,
                useDensityScaling = useDensityScaling,
            };

            if (usePrototypeMesh && prototypeMesh != null)
                dp.prototype = prototypeMesh;
            else if (prototypeTexture != null)
                dp.prototypeTexture = prototypeTexture;

            return dp;
        }

        /// <summary>
        /// プロトタイプが有効か（メッシュまたはテクスチャが設定されているか）。
        /// </summary>
        public bool IsValid =>
            (usePrototypeMesh && prototypeMesh != null) ||
            (!usePrototypeMesh && prototypeTexture != null);
    }
}
