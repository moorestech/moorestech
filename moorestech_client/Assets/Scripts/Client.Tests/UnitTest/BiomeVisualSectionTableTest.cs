using Game.MapGeneration.Pipeline.Visual.Source;
using Core.Master;
using Game.MapGeneration.Pipeline.Biomes;
using NUnit.Framework;
using Server.Boot;
using Tests.Module.TestMod;

namespace Client.Tests.UnitTest
{
    /// <summary>
    ///     BiomeTypeごとの見た目セクション引き当てを検証する。8分岐の写し間違いは例外にならず「別バイオームの
    ///     テクスチャと草が生える」形でしか現れないため、フィクスチャ側に固有値を仕込んで取り違えを直接見る
    ///     Verifies the per-BiomeType visual section lookup. A miswired arm among the eight never throws and shows up
    ///     only as another biome's textures and grass, so the fixture carries per-biome values to catch swaps directly
    /// </summary>
    public class BiomeVisualSectionTableTest
    {
        [SetUp]
        public void SetUp()
        {
            // DIコンテナ生成でMasterHolderをForUnitTest modからロードする
            // Load MasterHolder from ForUnitTest mod via DI container generation
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
        }

        [Test]
        public void EveryBiomeGetsItsOwnLayerAddressTextureConfigAndDetailConfig()
        {
            // フィクスチャは3種の値すべてをBiomeTypeの列挙値から導けるよう仕込んである。
            // 1本でも隣のバイオームを読んでいれば3つの一致のどれかが必ず崩れる
            // The fixture derives all three values from the BiomeType enum value, so reading a neighbouring
            // biome in any arm necessarily breaks one of the three matches
            var biomeTypes = new[]
            {
                BiomeType.Grassland, BiomeType.Forest, BiomeType.Savanna, BiomeType.Desert,
                BiomeType.Mesa, BiomeType.Alpine, BiomeType.Jungle, BiomeType.Woods,
            };

            var sections = BiomeVisualSectionTable.Resolve(MasterHolder.GenerationMaster.SelectedGeneration, biomeTypes);

            for (var index = 0; index < biomeTypes.Length; index++)
            {
                var biomeType = biomeTypes[index];
                Assert.That(sections.MainLayerAddresses[index], Is.EqualTo($"test/terrain-layer/{biomeType}"), $"{biomeType} main layer");
                Assert.That(sections.TextureConfigs[index].entries[0].weight, Is.EqualTo((float)(int)biomeType), $"{biomeType} texture config");
                Assert.That(sections.DetailConfigs[index].borderMargin, Is.EqualTo((float)(int)biomeType), $"{biomeType} detail config");
            }
        }

        [Test]
        public void FollowsTheGivenBiomeOrderRatherThanADeclarationOrder()
        {
            // 実行時の並びはClassificationStage.GetEnabledBiomeTypesが決める。表側が独自順に並べ替えると
            // splatmapの列とdetailの添字が同時にずれる
            // The runtime order comes from ClassificationStage.GetEnabledBiomeTypes; reordering it here would
            // desynchronize the splatmap columns and the detail indices at once
            var biomeTypes = new[] { BiomeType.Woods, BiomeType.Grassland, BiomeType.Mesa };

            var sections = BiomeVisualSectionTable.Resolve(MasterHolder.GenerationMaster.SelectedGeneration, biomeTypes);

            Assert.That(sections.MainLayerAddresses[0], Is.EqualTo("test/terrain-layer/Woods"));
            Assert.That(sections.MainLayerAddresses[1], Is.EqualTo("test/terrain-layer/Grassland"));
            Assert.That(sections.MainLayerAddresses[2], Is.EqualTo("test/terrain-layer/Mesa"));
        }

        [Test]
        public void ThrowsForStructuralBiomesThatOwnNoVisualSection()
        {
            // Ocean/Beachは有効バイオーム列に現れない。既定化すると陸のテクスチャで海が塗られる
            // Ocean and Beach never appear in the enabled list; defaulting them would paint the sea with land textures
            Assert.Throws<System.InvalidOperationException>(() => BiomeVisualSectionTable.Resolve(
                MasterHolder.GenerationMaster.SelectedGeneration, new[] { BiomeType.Ocean }));
        }
    }
}
