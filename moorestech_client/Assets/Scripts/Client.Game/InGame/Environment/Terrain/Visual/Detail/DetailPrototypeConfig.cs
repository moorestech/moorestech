using UnityEngine;

namespace Client.Game.InGame.Environment.Terrain.Visual.Detail
{
    /// <summary>
    ///     Unity DetailPrototype の全プロパティを保持する設定。MapMaking DetailPrototypeConfig の移植
    ///     Holds every Unity DetailPrototype property; ported from MapMaking's DetailPrototypeConfig
    /// </summary>
    public class DetailPrototypeConfig
    {
        // 見た目の実体はAddressablesの非同期ロード結果。ファクトリはアドレスだけを埋める
        // The visual assets come from async Addressables loads; the factory fills in only the addresses
        public string prototypeMeshAddressablePath;
        public string prototypeTextureAddressablePath;
        public GameObject prototypeMesh;
        public Texture2D prototypeTexture;

        public bool usePrototypeMesh;
        public DetailRenderMode renderMode;

        public float minWidth;
        public float maxWidth;
        public float minHeight;
        public float maxHeight;

        // 地面法線への追従度・配置のランダムズレ・目標カバー率・ホール端からの余白
        // Alignment to the ground normal, positional jitter, target coverage, and hole-edge padding
        public float alignToGround;
        public float positionJitter;
        public float targetCoverage;
        public float holeEdgePadding;

        public int noiseSeed;
        public float noiseSpread;

        public Color dryColor;
        public Color healthyColor;

        public bool useInstancing;
        public bool useDensityScaling;

        // メッシュ・テクスチャのどちらかが解決済みでなければDetailとして成立しない
        // A detail is only valid once either its mesh or its texture has been resolved
        public bool IsValid =>
            (usePrototypeMesh && prototypeMesh != null) ||
            (!usePrototypeMesh && prototypeTexture != null);

        public void SetPrototypeMesh(GameObject resolvedPrototypeMesh)
        {
            prototypeMesh = resolvedPrototypeMesh;
        }

        public void SetPrototypeTexture(Texture2D resolvedPrototypeTexture)
        {
            prototypeTexture = resolvedPrototypeTexture;
        }

        public DetailPrototype ToDetailPrototype()
        {
            var detailPrototype = new DetailPrototype
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
                detailPrototype.prototype = prototypeMesh;
            else if (prototypeTexture != null)
                detailPrototype.prototypeTexture = prototypeTexture;

            return detailPrototype;
        }
    }
}
