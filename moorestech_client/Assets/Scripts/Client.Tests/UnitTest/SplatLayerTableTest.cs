using System;
using Client.Game.InGame.Environment.Terrain.Visual.Splat;
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
        [Test]
        public void PinsBeachToIndexZeroThenRockThenBiomeLayersInOrder()
        {
            var table = SplatLayerTable.Build(
                "addr/beach", "addr/rock",
                new[] { "addr/grass", "addr/sand" },
                new[] { CreateTextureConfig(), CreateTextureConfig() });

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
                new[] { CreateTextureConfig("addr/cliff", "addr/moss") });

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
                new[] { CreateTextureConfig("addr/rock"), CreateTextureConfig() });

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
                string.Empty, "addr/rock", new[] { "addr/grass" }, new[] { CreateTextureConfig() }));

            Assert.Throws<InvalidOperationException>(() => SplatLayerTable.Build(
                "addr/beach", "addr/rock", new[] { string.Empty }, new[] { CreateTextureConfig() }));

            Assert.Throws<InvalidOperationException>(() => SplatLayerTable.Build(
                "addr/beach", "addr/rock", new[] { "addr/grass" }, new[] { CreateTextureConfig(string.Empty) }));
        }

        [Test]
        public void AssignsEachBiomeItsOwnSliceOfTheFlatEntryArray()
        {
            var table = SplatLayerTable.Build(
                "addr/beach", "addr/rock",
                new[] { "addr/grass", "addr/sand" },
                new[] { CreateTextureConfig("addr/cliff", "addr/moss"), CreateTextureConfig("addr/dune") });

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

            // ノイズオフセット索引は全バイオーム通しの連番
            // The noise offset index is a single running counter across all biomes
            Assert.That(textureEntries[0].noiseOffsetIndex, Is.EqualTo(0));
            Assert.That(textureEntries[2].noiseOffsetIndex, Is.EqualTo(2));

            biomeParams.Dispose();
            textureEntries.Dispose();
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
