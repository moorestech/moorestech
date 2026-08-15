using Game.MapGeneration.Pipeline.Config;
using static Client.Tests.UnitTest.Terrain.Surround.SurroundWiringTestScene;

namespace Client.Tests.UnitTest.Terrain.Surround
{
    /// <summary>
    ///     結線テストが走る1タイルぶんのTerrainGenerationConfig。有効バイオームは草原1つだけに絞り、
    ///     その樹木配置に根元レイヤーを持つプロトタイプを1本だけ載せる
    ///     The single-tile TerrainGenerationConfig the wiring tests run on: grassland is the only enabled biome,
    ///     and its tree placement carries exactly one prototype owning a root layer
    /// </summary>
    public static class SurroundWiringTestConfig
    {
        public static TerrainGenerationConfig Create()
        {
            var config = new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
                seed = 12345,
                terrainWidth = TileSize,
                terrainLength = TileSize,
                terrainHeight = 600f,
                grasslandEnabled = true,
                forestEnabled = false,
                savannaEnabled = false,
                desertEnabled = false,
                mesaEnabled = false,
                alpineEnabled = false,
                jungleEnabled = false,
                woodsEnabled = false,
            };

            // 根元の重み1は中心画素で他レイヤーを0にする。適用順が岩の前か後かを、その画素の裸地列だけで見分けられる
            // A root weight of 1 zeroes the other layers at the centre pixel, so its bare-ground column alone tells whether trees ran before or after the rocks
            config.grassland.treePlacement = new TreePlacementConfig
            {
                prototypes = new[]
                {
                    new TreePrototypeEntry
                    {
                        mapObjectGuids = new[] { TreeGuid },
                        surroundLayerAddressablePath = TreeRootLayerAddress,
                        surroundLayerWeight = 1f,
                        surroundLayerWidth = TreeSurroundWidth,
                    },
                },
            };

            return config;
        }
    }
}
