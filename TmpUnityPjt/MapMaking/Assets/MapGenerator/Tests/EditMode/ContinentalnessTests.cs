using NUnit.Framework;
using MapGenerator.Pipeline;
using MapGenerator.Pipeline.Biomes;
using MapGenerator.Pipeline.Config;
using UnityEngine;

namespace MapGenerator.Tests.EditMode
{
    /// <summary>
    /// ClassificationJobのContinentalness+ErosionベースlandMask生成を検証する。
    /// </summary>
    [TestFixture]
    public class ContinentalnessTests
    {
        // landMaskが0-1の範囲に収まること
        [Test]
        public void LandMask_ValuesInZeroOneRange()
        {
            var config = TestConfigFactory.Create();
            config.resolutionPreset = TerrainResolutionPreset._256;
            var result = TerrainGenerator.Generate(config);
            Assert.IsNotNull(result.Heights);

            // heightsが0-1範囲（landMaskは内部だが、heights経由で間接検証）
            foreach (var h in result.Heights)
                Assert.That(h, Is.InRange(0f, 1f));
        }

        // 陸地と海洋が両方存在すること（全部陸or全部海にならない）
        [Test]
        public void Generate_HasBothLandAndOcean()
        {
            var config = TestConfigFactory.Create();
            config.resolutionPreset = TerrainResolutionPreset._256;
            var result = TerrainGenerator.Generate(config);

            int zeroCount = 0;
            int nonZeroCount = 0;
            foreach (var h in result.Heights)
            {
                if (h < 0.001f) zeroCount++;
                else nonZeroCount++;
            }

            // 陸地が最低10%、海洋が最低10%存在する
            float total = result.Heights.Length;
            Assert.That(zeroCount / total, Is.GreaterThan(0.1f),
                "海洋が少なすぎる（10%未満）");
            Assert.That(nonZeroCount / total, Is.GreaterThan(0.1f),
                "陸地が少なすぎる（10%未満）");
        }

        // seed再現性: 同じseedで同じ結果
        [Test]
        public void Generate_IsDeterministic()
        {
            var config = TestConfigFactory.Create();
            config.resolutionPreset = TerrainResolutionPreset._256;
            config.seed = 123;
            var r1 = TerrainGenerator.Generate(config);
            var r2 = TerrainGenerator.Generate(config);

            for (int i = 0; i < r1.Heights.Length; i++)
                Assert.AreEqual(r1.Heights[i], r2.Heights[i], 1e-6f,
                    $"Pixel {i} differs");
        }
    }
}
