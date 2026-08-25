using UnityEngine;

namespace Game.MapGeneration.Pipeline.Visual.Detail
{
    /// <summary>
    ///     Detail1本ぶんの見た目設定を生成システム内部で持つ形。外へ出るときはFacadeのDetailPrototypeSpecへ写す
    ///     生成の入力（マスタ由来のPOCO）と境界DTOを同じ型にすると、境界の都合が生成側へ逆流する
    ///     One detail's visual settings as the generation system holds them internally; crossing outwards copies into the Facade's DetailPrototypeSpec
    ///     Sharing one type between generation's input (a master-derived POCO) and the boundary DTO would let boundary concerns flow back into generation
    /// </summary>
    public class DetailPrototypeRuntimeConfig
    {
        // 見た目の実体はAddressablesの非同期ロード結果。ここはアドレスだけを持つ
        // The visual assets come from async Addressables loads; this holds only the addresses
        public string prototypeMeshAddressablePath;
        public string prototypeTextureAddressablePath;

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
    }
}
