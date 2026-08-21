using System;
using Client.Game.InGame.Environment.Terrain.Visual.Detail;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Game.MapGeneration.Pipeline.Biomes;
using Mooresmaster.Model.GenerationModule;
using GenDetailConfig = Mooresmaster.Model.BiomeDetailConfigModule.BiomeDetailConfig;
using GenObjectConfig = Mooresmaster.Model.BiomeObjectConfigModule.BiomeObjectConfig;
using GenTextureConfig = Mooresmaster.Model.BiomeTextureConfigModule.BiomeTextureConfig;

namespace Client.Game.InGame.Environment.Terrain.Visual.Source
{
    /// <summary>
    ///     BiomeTypeからGenerationMasterの見た目セクションを引く唯一の対応表。サーバーの実行時Configは
    ///     見た目を持たないため、ここだけがマスタの生成型を直接読む
    ///     The single lookup from BiomeType to GenerationMaster's visual sections; the server's runtime config
    ///     carries no visuals, so this is the only place reading the master's generated types directly
    /// </summary>
    public static class BiomeVisualSectionTable
    {
        public static BiomeVisualSections Resolve(Generation generation, BiomeType[] biomeTypes)
        {
            // 見た目セクションはVanillaGeneratorのalgorithmParamにしか存在しない
            // The visual sections exist only on VanillaGenerator's algorithmParam
            if (generation.AlgorithmParam is not VanillaGeneratorAlgorithmParam vanillaParam)
                throw new InvalidOperationException(
                    "[BiomeVisualSectionTable] Terrain visuals require a VanillaGenerator algorithmParam.");

            var mainLayerAddresses = new string[biomeTypes.Length];
            var textureConfigs = new BiomeTextureConfig[biomeTypes.Length];
            var detailConfigs = new BiomeDetailConfig[biomeTypes.Length];
            var surroundTextureConfigs = new SurroundTextureConfig[biomeTypes.Length];

            for (var index = 0; index < biomeTypes.Length; index++)
                switch (biomeTypes[index])
                {
                    case BiomeType.Grassland:
                        Fill(index, vanillaParam.Grassland.TerrainLayerAddressablePath, vanillaParam.Grassland.TextureConfig,
                            vanillaParam.Grassland.DetailConfig, vanillaParam.Grassland.ObjectConfig);
                        break;
                    case BiomeType.Forest:
                        Fill(index, vanillaParam.Forest.TerrainLayerAddressablePath, vanillaParam.Forest.TextureConfig,
                            vanillaParam.Forest.DetailConfig, vanillaParam.Forest.ObjectConfig);
                        break;
                    case BiomeType.Savanna:
                        Fill(index, vanillaParam.Savanna.TerrainLayerAddressablePath, vanillaParam.Savanna.TextureConfig,
                            vanillaParam.Savanna.DetailConfig, vanillaParam.Savanna.ObjectConfig);
                        break;
                    case BiomeType.Desert:
                        Fill(index, vanillaParam.Desert.TerrainLayerAddressablePath, vanillaParam.Desert.TextureConfig,
                            vanillaParam.Desert.DetailConfig, vanillaParam.Desert.ObjectConfig);
                        break;
                    case BiomeType.Mesa:
                        Fill(index, vanillaParam.Mesa.TerrainLayerAddressablePath, vanillaParam.Mesa.TextureConfig,
                            vanillaParam.Mesa.DetailConfig, vanillaParam.Mesa.ObjectConfig);
                        break;
                    case BiomeType.Alpine:
                        Fill(index, vanillaParam.Alpine.TerrainLayerAddressablePath, vanillaParam.Alpine.TextureConfig,
                            vanillaParam.Alpine.DetailConfig, vanillaParam.Alpine.ObjectConfig);
                        break;
                    case BiomeType.Jungle:
                        Fill(index, vanillaParam.Jungle.TerrainLayerAddressablePath, vanillaParam.Jungle.TextureConfig,
                            vanillaParam.Jungle.DetailConfig, vanillaParam.Jungle.ObjectConfig);
                        break;
                    case BiomeType.Woods:
                        Fill(index, vanillaParam.Woods.TerrainLayerAddressablePath, vanillaParam.Woods.TextureConfig,
                            vanillaParam.Woods.DetailConfig, vanillaParam.Woods.ObjectConfig);
                        break;

                    // Ocean/Beachは構造バイオームで有効バイオーム列には現れない。届いたなら分類側の契約が壊れている
                    // Ocean and Beach are structural biomes absent from the enabled list; arriving here means the classification contract broke
                    default:
                        throw new InvalidOperationException(
                            $"[BiomeVisualSectionTable] '{biomeTypes[index]}' has no visual section in generation master.");
                }

            return new BiomeVisualSections(mainLayerAddresses, textureConfigs, detailConfigs, surroundTextureConfigs);

            #region Internal

            void Fill(
                int index, string terrainLayerAddress, GenTextureConfig generatedTextureConfig,
                GenDetailConfig generatedDetailConfig, GenObjectConfig generatedObjectConfig)
            {
                mainLayerAddresses[index] = terrainLayerAddress;
                textureConfigs[index] = SplatTextureConfigFactory.Build(generatedTextureConfig);
                detailConfigs[index] = DetailRuntimeConfigFactory.Build(generatedDetailConfig);
                surroundTextureConfigs[index] = SurroundTextureConfigFactory.Build(generatedObjectConfig);
            }

            #endregion
        }
    }
}
