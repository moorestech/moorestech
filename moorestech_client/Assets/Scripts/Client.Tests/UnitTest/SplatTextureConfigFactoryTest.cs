using Game.MapGeneration.Pipeline.Visual.Splat;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using GenTexture = Mooresmaster.Model.BiomeTextureConfigModule;

namespace Client.Tests.UnitTest
{
    /// <summary>
    ///     マスタのbiomeTextureConfigの全フィールドが実行時型へ到達することを検証する。
    ///     写し漏れは例外にならず、テクスチャ合成が静かに変わるだけなのでここで固定する。
    ///     Verifies every biomeTextureConfig field reaches the runtime type. A dropped field throws nothing and
    ///     only changes the texture composition silently, so it is pinned here.
    /// </summary>
    public class SplatTextureConfigFactoryTest
    {
        [Test]
        public void CarriesEverySchemaFieldOfATextureEntry()
        {
            // 全フィールドに異なる値を与え、隣のフィールドと取り違えても検知できるようにする
            // Every field gets a distinct value so a swap with its neighbour is still detectable
            var generated = new GenTexture.BiomeTextureConfig(new[]
            {
                new GenTexture.EntriesElement(
                    0, "addr/cliff", 0.5f,
                    true, 1.1f, 1.2f, 1.3f,
                    false, 2.1f, 2.2f, 2.3f,
                    true, 3.1f, 3.2f, 3.3f,
                    "Worley", 4.1f, 4.2f),
            });

            var entry = SplatTextureConfigFactory.Build(generated).entries[0];

            Assert.That(entry.layerAddressablePath, Is.EqualTo("addr/cliff"));
            Assert.That(entry.weight, Is.EqualTo(0.5f));

            // 3枠のuseXxxFilterをtrue/false/trueに撒き分け、隣接フィルタとの取り違えを検知する
            // The three useXxxFilter bools alternate true/false/true so a swap with a neighbouring filter is detectable
            Assert.That(entry.useSlopeFilter, Is.True);
            Assert.That(entry.slopeMin, Is.EqualTo(1.1f));
            Assert.That(entry.slopeMax, Is.EqualTo(1.2f));
            Assert.That(entry.slopeSmoothness, Is.EqualTo(1.3f));

            Assert.That(entry.useHeightFilter, Is.False);
            Assert.That(entry.heightMin, Is.EqualTo(2.1f));
            Assert.That(entry.heightMax, Is.EqualTo(2.2f));
            Assert.That(entry.heightSmoothness, Is.EqualTo(2.3f));

            Assert.That(entry.useCurvatureFilter, Is.True);
            Assert.That(entry.curvatureMin, Is.EqualTo(3.1f));
            Assert.That(entry.curvatureMax, Is.EqualTo(3.2f));
            Assert.That(entry.curvatureSmoothness, Is.EqualTo(3.3f));

            Assert.That(entry.noiseType, Is.EqualTo(MapNoiseType.Worley));
            Assert.That(entry.noiseFrequency, Is.EqualTo(4.1f));
            Assert.That(entry.noiseAmplitude, Is.EqualTo(4.2f));
        }

        [Test]
        public void KeepsEntryOrderSoLayerIndicesStayAligned()
        {
            // 並びはSplatLayerTableの登録順＝splatmapの列順を決めるため、入れ替わると全バイオームのテクスチャがずれる
            // The order drives SplatLayerTable's registration and thus the splatmap column order, so a swap shifts every biome's texture
            var generated = new GenTexture.BiomeTextureConfig(new[]
            {
                new GenTexture.EntriesElement(0, "addr/first", 1f, false, 0f, 0f, 0f, false, 0f, 0f, 0f, false, 0f, 0f, 0f, "None", 0f, 0f),
                new GenTexture.EntriesElement(0, "addr/second", 1f, false, 0f, 0f, 0f, false, 0f, 0f, 0f, false, 0f, 0f, 0f, "None", 0f, 0f),
            });

            var config = SplatTextureConfigFactory.Build(generated);

            Assert.That(config.entries[0].layerAddressablePath, Is.EqualTo("addr/first"));
            Assert.That(config.entries[1].layerAddressablePath, Is.EqualTo("addr/second"));
        }
    }
}
