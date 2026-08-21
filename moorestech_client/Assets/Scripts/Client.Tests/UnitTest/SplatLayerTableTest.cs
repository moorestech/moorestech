using System;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
using Client.Game.InGame.Environment.Terrain.Visual.Splat.Surround;
using Client.Tests.UnitTest.Terrain.Surround;
using Game.MapGeneration.Pipeline.Config;
using Game.MapGeneration.Pipeline.Jobs;
using NUnit.Framework;
using Unity.Collections;

namespace Client.Tests.UnitTest
{
    /// <summary>
    ///     splatmapのレイヤー並びとTextureEntryParamsのスライス割り当てを検証する。
    ///     並びはsplatmapの列順そのもので、ずれると全バイオームのテクスチャが入れ替わる。
    ///     Verifies the splatmap layer order and the TextureEntryParams slice assignment.
    ///     The order is the splatmap's column order; a shift swaps every biome's texture.
    /// </summary>
    public class SplatLayerTableTest
    {
        private static readonly string[] NoDebugLayers = Array.Empty<string>();

        [Test]
        public void PinsBeachToIndexZeroThenRockThenBiomeLayersInOrder()
        {
            var table = SplatLayerTable.Build(
                "addr/beach", "addr/rock",
                new[] { "addr/grass", "addr/sand" },
                new[] { CreateTextureConfig(), CreateTextureConfig() },
                CreateSurroundConfigs("addr/rock", "addr/rock"), CreateTreeSurroundSpecies(), NoDebugLayers);

            Assert.That(table.OrderedLayerAddresses,
                Is.EqualTo(new[] { "addr/beach", "addr/rock", "addr/grass", "addr/sand" }));
            Assert.That(table.LayerIndexByAddress["addr/beach"], Is.EqualTo(0));
            Assert.That(table.LayerIndexByAddress["addr/rock"], Is.EqualTo(1));
            Assert.That(table.LayerIndexByAddress["addr/grass"], Is.EqualTo(2));
            Assert.That(table.LayerIndexByAddress["addr/sand"], Is.EqualTo(3));
        }

        [Test]
        public void RegistersTextureEntryLayersAfterTheirBiomeMainLayer()
        {
            var table = SplatLayerTable.Build(
                "addr/beach", "addr/rock",
                new[] { "addr/grass" },
                new[] { CreateTextureConfig("addr/cliff", "addr/moss") },
                CreateSurroundConfigs("addr/rock"), CreateTreeSurroundSpecies(), NoDebugLayers);

            Assert.That(table.OrderedLayerAddresses,
                Is.EqualTo(new[] { "addr/beach", "addr/rock", "addr/grass", "addr/cliff", "addr/moss" }));
        }

        [Test]
        public void DeduplicatesAnAddressSharedBySeveralBiomes()
        {
            // 同じレイヤーを2度登録すると列が増えsplatWeightsの重み合計が割れる
            // Registering the same layer twice would add a column and split the splatWeights total
            var table = SplatLayerTable.Build(
                "addr/beach", "addr/rock",
                new[] { "addr/grass", "addr/grass" },
                new[] { CreateTextureConfig("addr/rock"), CreateTextureConfig() },
                CreateSurroundConfigs("addr/rock", "addr/rock"), CreateTreeSurroundSpecies(), NoDebugLayers);

            Assert.That(table.OrderedLayerAddresses,
                Is.EqualTo(new[] { "addr/beach", "addr/rock", "addr/grass" }));
            Assert.That(table.LayerIndexByAddress["addr/rock"], Is.EqualTo(1));
        }

        [Test]
        public void ThrowsWhenAnyLayerAddressIsEmpty()
        {
            // 空アドレスを0番へ倒すと地形全面がビーチテクスチャになり、整備漏れに気づけない
            // Falling an empty address back to index 0 would paint the whole terrain with sand, hiding the data gap
            Assert.Throws<InvalidOperationException>(() => SplatLayerTable.Build(
                string.Empty, "addr/rock", new[] { "addr/grass" }, new[] { CreateTextureConfig() },
                CreateSurroundConfigs("addr/rock"), CreateTreeSurroundSpecies(), NoDebugLayers));

            Assert.Throws<InvalidOperationException>(() => SplatLayerTable.Build(
                "addr/beach", "addr/rock", new[] { string.Empty }, new[] { CreateTextureConfig() },
                CreateSurroundConfigs("addr/rock"), CreateTreeSurroundSpecies(), NoDebugLayers));

            Assert.Throws<InvalidOperationException>(() => SplatLayerTable.Build(
                "addr/beach", "addr/rock", new[] { "addr/grass" }, new[] { CreateTextureConfig(string.Empty) },
                CreateSurroundConfigs("addr/rock"), CreateTreeSurroundSpecies(), NoDebugLayers));
        }

        [Test]
        public void RegistersASurroundLayerAddressAfterEveryBiomeLayer()
        {
            // 未登録のまま参照すると裸地テクスチャが列を持たず、岩の周りだけ描けない
            // Leaving it unregistered would give the bare-ground texture no column, so only rocks lose their surroundings
            var table = SplatLayerTable.Build(
                "addr/beach", "addr/rock",
                new[] { "addr/grass" },
                new[] { CreateTextureConfig("addr/cliff") },
                CreateSurroundConfigs("addr/mud"), CreateTreeSurroundSpecies(), NoDebugLayers);

            Assert.That(table.OrderedLayerAddresses,
                Is.EqualTo(new[] { "addr/beach", "addr/rock", "addr/grass", "addr/cliff", "addr/mud" }));
            Assert.That(table.LayerIndexByAddress["addr/mud"], Is.EqualTo(4));
        }

        [Test]
        public void RegistersEveryTreeRootLayerAfterTheRockSurroundLayer()
        {
            // 木の根元レイヤーが列を持たないと TreeSurroundTexturePainter の索引引きが落ちる。重複は岩側と同じく畳む
            // Without a column the tree painter's index lookup throws, and a duplicate folds onto the rock layer's column as elsewhere
            var table = SplatLayerTable.Build(
                "addr/beach", "addr/rock",
                new[] { "addr/grass" },
                new[] { CreateTextureConfig() },
                CreateSurroundConfigs("addr/mud"), CreateTreeSurroundSpecies("addr/dirt", "addr/mud"), NoDebugLayers);

            Assert.That(table.OrderedLayerAddresses,
                Is.EqualTo(new[] { "addr/beach", "addr/rock", "addr/grass", "addr/mud", "addr/dirt" }));
            Assert.That(table.LayerIndexByAddress["addr/dirt"], Is.EqualTo(4));
        }

        [Test]
        public void RejectsAnEmptySurroundLayerInsteadOfSkippingIt()
        {
            // surroundLayerはマスタの必須キー。空を黙って読み飛ばすと、アドレス整備漏れが岩の裸地消失として無言で出る
            // The surroundLayer is a required master key: silently skipping an empty one turns a data gap into bare ground that quietly vanishes
            Assert.Throws<InvalidOperationException>(() => SplatLayerTable.Build(
                "addr/beach", "addr/rock",
                new[] { "addr/grass" },
                new[] { CreateTextureConfig() },
                CreateSurroundConfigs(string.Empty), CreateTreeSurroundSpecies(), NoDebugLayers));
        }

        [Test]
        public void AssignsEachBiomeItsOwnSliceOfTheFlatEntryArray()
        {
            var table = SplatLayerTable.Build(
                "addr/beach", "addr/rock",
                new[] { "addr/grass", "addr/sand" },
                new[] { CreateTextureConfig("addr/cliff", "addr/moss"), CreateTextureConfig("addr/dune") },
                CreateSurroundConfigs("addr/rock", "addr/rock"), CreateTreeSurroundSpecies(), NoDebugLayers);

            var biomeParams = new NativeArray<BiomeParams>(2, Allocator.Temp);
            var textureEntries = TextureEntryParamsBuilder.Build(
                123, new[] { CreateTextureConfig("addr/cliff", "addr/moss"), CreateTextureConfig("addr/dune") },
                table.LayerIndexByAddress, biomeParams, Allocator.Temp);

            // スライスがずれると別バイオームのフィルタが適用され、境界一帯のテクスチャが崩れる
            // A shifted slice applies another biome's filters and wrecks the textures along every boundary
            Assert.That(biomeParams[0].textureEntryBase, Is.EqualTo(0));
            Assert.That(biomeParams[0].textureEntryCount, Is.EqualTo(2));
            Assert.That(biomeParams[1].textureEntryBase, Is.EqualTo(2));
            Assert.That(biomeParams[1].textureEntryCount, Is.EqualTo(1));

            Assert.That(textureEntries[0].layerIndex, Is.EqualTo(table.LayerIndexByAddress["addr/cliff"]));
            Assert.That(textureEntries[1].layerIndex, Is.EqualTo(table.LayerIndexByAddress["addr/moss"]));
            Assert.That(textureEntries[2].layerIndex, Is.EqualTo(table.LayerIndexByAddress["addr/dune"]));

            // ノイズ索引は全バイオーム通し
            // Noise indices span all biomes
            Assert.That(textureEntries[0].noiseOffsetIndex, Is.EqualTo(0));
            Assert.That(textureEntries[2].noiseOffsetIndex, Is.EqualTo(2));

            biomeParams.Dispose();
            textureEntries.Dispose();
        }

        // 木の根元の列は樹種設定からしか作れない。アドレスの配列を直に渡せる口が無いので、列と塗りの導出は本番と同じ1本に揃う
        // The tree root columns come only from species settings; with no way to hand in a bare address array, columns and painting share production's single derivation
        private static TreeSurroundSpeciesTable CreateTreeSurroundSpecies(params string[] layerAddresses)
        {
            var prototypes = new TreePrototypeEntry[layerAddresses.Length];
            for (var i = 0; i < layerAddresses.Length; i++)
                prototypes[i] = SurroundTestFixtures.CreateTreePrototype(
                    new[] { $"00000000-0000-0000-0000-00000000000{i}" }, layerAddresses[i], 1f, 1f);

            return SurroundTestFixtures.CreateTreeSurroundSpecies(prototypes);
        }

        private static SurroundTextureConfig[] CreateSurroundConfigs(params string[] surroundLayerAddresses)
        {
            var configs = new SurroundTextureConfig[surroundLayerAddresses.Length];
            for (var i = 0; i < surroundLayerAddresses.Length; i++)
                configs[i] = new SurroundTextureConfig { surroundLayerAddressablePath = surroundLayerAddresses[i] };

            return configs;
        }

        private static BiomeTextureConfig CreateTextureConfig(params string[] layerAddresses)
        {
            var entries = new TextureEntry[layerAddresses.Length];
            for (var i = 0; i < layerAddresses.Length; i++)
                entries[i] = new TextureEntry { layerAddressablePath = layerAddresses[i], weight = 1f };

            return new BiomeTextureConfig { entries = entries };
        }
    }
}
