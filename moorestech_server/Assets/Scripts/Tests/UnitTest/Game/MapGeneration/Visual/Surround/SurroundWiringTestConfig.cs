using Game.MapGeneration.Pipeline.Config;
using static Tests.UnitTest.Game.MapGeneration.Visual.Surround.SurroundWiringTestScene;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Surround
{
    /// <summary>
    ///     結線テストが走る1タイルぶんのTerrainGenerationConfig。有効バイオームは草原1つだけに絞り、
    ///     その樹木配置に根元レイヤーを持つプロトタイプを載せる
    ///     The single-tile TerrainGenerationConfig the wiring tests run on: grassland is the only enabled biome,
    ///     and its tree placement carries prototypes owning a root layer
    /// </summary>
    public static class SurroundWiringTestConfig
    {
        public static TerrainGenerationConfig Create()
        {
            var config = new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
                detailResolution = 1024,
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
                    CreateRootPrototype(TreeGuid),

                    // 岩のguidも同じ設定で載せる。テーブルに無いと振り分けの取り違えがTryGetの失敗で黙って救われる
                    // The rock's guid carries the same settings: absent from the table, a mis-sorted rock would be rescued by a failed lookup
                    // そうなると「岩は木として塗られない」の検証がSplitではなくテーブルの穴を見ているだけになる
                    // The rock-never-paints check would then watch that hole rather than Split
                    CreateRootPrototype(StoneGuid),
                },
            };

            return config;
        }

        private static TreePrototypeEntry CreateRootPrototype(string mapObjectGuid)
        {
            return new TreePrototypeEntry
            {
                mapObjectGuids = new[] { mapObjectGuid },
                surroundLayerAddressablePath = TreeRootLayerAddress,
                surroundLayerWeight = 1f,
                surroundLayerWidth = TreeSurroundWidth,
            };
        }
    }
}
