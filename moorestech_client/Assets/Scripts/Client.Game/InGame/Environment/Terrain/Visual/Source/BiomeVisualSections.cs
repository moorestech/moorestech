using System.Collections.Generic;
using Client.Game.InGame.Environment.Terrain.Visual.Detail;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;

namespace Client.Game.InGame.Environment.Terrain.Visual.Source
{
    /// <summary>
    ///     有効バイオームの並びに対応する見た目設定3種。splatmap生成もdetail生成もこの並び順を前提に添字で引く
    ///     The three visual configs indexed by the enabled-biome order that both splatmap and detail generation assume
    /// </summary>
    public class BiomeVisualSections
    {
        public readonly IReadOnlyList<BiomeDetailConfig> DetailConfigs;

        // splatmapのレイヤー表と重み計算はこの2本を並列配列として受け取る
        // The splatmap layer table and weight computation take these two as parallel arrays
        public readonly string[] MainLayerAddresses;
        public readonly BiomeTextureConfig[] TextureConfigs;

        public BiomeVisualSections(string[] mainLayerAddresses, BiomeTextureConfig[] textureConfigs, BiomeDetailConfig[] detailConfigs)
        {
            MainLayerAddresses = mainLayerAddresses;
            TextureConfigs = textureConfigs;
            DetailConfigs = detailConfigs;
        }
    }
}
