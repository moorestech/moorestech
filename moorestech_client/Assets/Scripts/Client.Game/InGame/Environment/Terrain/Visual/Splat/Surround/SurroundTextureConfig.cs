namespace Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround
{
    /// <summary>
    ///     岩の周りを裸地テクスチャへ寄せる設定。MapMaking ObjectSurroundTextureConfig の移植で、
    ///     TerrainLayer参照だけがアドレス文字列に置き換わっている
    ///     Settings pulling the ground around a rock towards a bare texture; ported from MapMaking's
    ///     ObjectSurroundTextureConfig with only the TerrainLayer reference replaced by an address string
    /// </summary>
    public class SurroundTextureConfig
    {
        public bool enabled;

        // 空文字は未設定。移植元の surroundLayer == null と同じくMudフォールバックへ倒れる
        // An empty string means unset and falls back to Mud, exactly as the source's null surroundLayer did
        public string surroundLayerAddressablePath;

        // コア領域: 岩の直下を強く裸地化する帯
        // Core zone: the band right under the rock that goes strongly bare
        public float coreRadius;
        public float coreBlendMin;
        public float coreBlendMax;

        // 遷移帯: コアの外で元のテクスチャへ戻していく帯
        // Transition band: the ring outside the core fading back to the original texture
        public float transitionRadius;
        public float transitionBlendMin;
        public float transitionBlendMax;

        // 2層Perlinの周波数と低周波側の混合比
        // The two Perlin layers' frequencies and the low-frequency share of the mix
        public float noiseLowFrequency;
        public float noiseHighFrequency;
        public float noiseLowWeight;

        // 岩メッシュの基底幅。転送Scaleと掛けてフットプリント半径になる
        // The rock mesh's base width; multiplied by the transferred scale it becomes the footprint radius
        public float rockMeshBaseSize;

        // クラスタに属さない単体の岩だけが使う裸地
        // The bare patch only a rock outside any cluster uses
        public float singleRockRadius;
        public float singleRockBlend;
    }
}
