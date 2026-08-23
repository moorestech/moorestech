using System.Collections.Generic;
using Game.MapGeneration.Pipeline.Visual.Detail;
using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Visual.Surround;

namespace Game.MapGeneration.Pipeline.Visual.Source
{
    /// <summary>
    ///     有効バイオームの並びに対応する見た目設定4種。splatmap生成もdetail生成もこの並び順を前提に添字で引く
    ///     The four visual configs indexed by the enabled-biome order that both splatmap and detail generation assume
    /// </summary>
    public class BiomeVisualSections
    {
        public readonly IReadOnlyList<BiomeDetailConfig> DetailConfigs;

        // splatmapのレイヤー表と重み計算はこの2本を並列配列として受け取る
        // The splatmap layer table and weight computation take these two as parallel arrays
        public readonly string[] MainLayerAddresses;
        public readonly BiomeTextureConfig[] TextureConfigs;

        // 岩周辺の裸地設定。勝者バイオームではなく岩のクラスタ重心のバイオームで引かれる
        // The bare-ground settings around rocks, looked up by the biome at a rock cluster's centroid rather than the pixel's winner
        public readonly SurroundTextureConfig[] SurroundTextureConfigs;

        public BiomeVisualSections(
            string[] mainLayerAddresses, BiomeTextureConfig[] textureConfigs, BiomeDetailConfig[] detailConfigs,
            SurroundTextureConfig[] surroundTextureConfigs)
        {
            MainLayerAddresses = mainLayerAddresses;
            TextureConfigs = textureConfigs;
            DetailConfigs = detailConfigs;
            SurroundTextureConfigs = surroundTextureConfigs;
        }
    }
}
