using UnityEngine;

namespace Game.MapGeneration.Facade
{
    /// <summary>
    ///     Unity DetailPrototypeを組み立てるための設定値。実アセットの解決とDetailPrototypeへの変換は
    ///     呼び出し側（クライアント）が担い、ここはアドレスと数値パラメータだけを運ぶ
    ///     Settings for assembling a Unity DetailPrototype; the caller (the client) resolves the assets
    ///     and builds the DetailPrototype, so this carries only addresses and numeric parameters
    /// </summary>
    public class DetailPrototypeSpec
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
