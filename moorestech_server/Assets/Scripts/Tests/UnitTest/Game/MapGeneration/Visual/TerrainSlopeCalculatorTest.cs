using Game.MapGeneration.Pipeline.Visual;
using Game.MapGeneration.Pipeline.Config;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Visual
{
    /// <summary>
    ///     傾斜計算の[z,x]↔flat往復を検証する。転置してもジョブは同じ画素数を処理して落ちないため、
    ///     Detailが地形と鏡像の位置に生えるという見た目でしか現れない
    ///     Verifies the [z,x] to flat round trip. A transposition still feeds the job the same pixel count and never
    ///     throws, surfacing only as details growing mirrored against the terrain
    /// </summary>
    public class TerrainSlopeCalculatorTest
    {
        private const int Resolution = 5;

        // 幅と長さを変えると、転置した実装はcellXとcellZを取り違えて別の角度を出す
        // Differing width and length make a transposed implementation swap cellX and cellZ and produce a different angle
        private const float TerrainWidth = 100f;
        private const float TerrainLength = 400f;
        private const float TerrainHeight = 50f;

        private const float HeightStepPerRow = 0.1f;

        [Test]
        public void ComputesTheSlopeOfARampRunningAlongZ()
        {
            // z方向にだけ傾いた地形。dhdz = 段差 × terrainHeight / cellZ で解析解が出る
            // A ramp tilted only along z, so dhdz = step x terrainHeight / cellZ gives a closed-form answer
            var heights = new float[Resolution, Resolution];
            for (var z = 0; z < Resolution; z++)
            for (var x = 0; x < Resolution; x++)
                heights[z, x] = z * HeightStepPerRow;

            var slopes = TerrainSlopeCalculator.Compute(heights, CreateConfig());

            var cellZ = TerrainLength / (Resolution - 1);
            var expected = Mathf.Atan(HeightStepPerRow * TerrainHeight / cellZ) * Mathf.Rad2Deg;

            // 最終行は上隣が無く自身の高さで代用されるので傾斜0になる
            // The last row has no upper neighbour and falls back to its own height, so its slope is 0
            for (var z = 0; z < Resolution - 1; z++)
            for (var x = 0; x < Resolution; x++)
                Assert.That(slopes[z, x], Is.EqualTo(expected).Within(1e-3f), $"z={z} x={x}");

            for (var x = 0; x < Resolution; x++)
                Assert.That(slopes[Resolution - 1, x], Is.EqualTo(0f).Within(1e-3f), $"最終行 x={x}");

            // 転置していれば cellX=25 で計算され、この値とは一致しない
            // A transposed implementation would use cellX=25 and never match this value
            var transposedExpected = Mathf.Atan(HeightStepPerRow * TerrainHeight / (TerrainWidth / (Resolution - 1))) * Mathf.Rad2Deg;
            Assert.That(expected, Is.Not.EqualTo(transposedExpected).Within(1f), "テスト自体が転置を区別できること");
        }

        [Test]
        public void PlacesTheSteepCellAtTheSameZxAsTheHeightStep()
        {
            // 1画素だけ突き出す。傾斜が立つのはその画素と、そこを上/右に見る2画素だけ
            // Raise a single pixel; only that pixel and the two seeing it as their up/right neighbour become steep
            var heights = new float[Resolution, Resolution];
            heights[3, 1] = 1f;

            var slopes = TerrainSlopeCalculator.Compute(heights, CreateConfig());

            Assert.That(slopes[3, 1], Is.GreaterThan(0f), "突き出した画素自身");
            Assert.That(slopes[2, 1], Is.GreaterThan(0f), "z-側から上隣として見る画素");
            Assert.That(slopes[3, 0], Is.GreaterThan(0f), "x-側から右隣として見る画素");

            // 転置していればここが立つ
            // These would be the steep cells if the axes were transposed
            Assert.That(slopes[1, 3], Is.EqualTo(0f).Within(1e-4f), "転置先の画素は平坦");
            Assert.That(slopes[0, 3], Is.EqualTo(0f).Within(1e-4f));
        }

        private static TerrainGenerationConfig CreateConfig()
        {
            return new TerrainGenerationConfig
            {
                overrideResolution = Resolution,
                detailResolution = 1024,
                terrainWidth = TerrainWidth,
                terrainLength = TerrainLength,
                terrainHeight = TerrainHeight,
            };
        }
    }
}
