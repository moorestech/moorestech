using System;
using System.Collections.Generic;
using System.IO;
using Game.MapGeneration.Cache;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration.Visual.Cache
{
    /// <summary>
    ///     TerrainFileWriter出力からの復元を検証
    ///     Verifies restoration from TerrainFileWriter's output
    /// </summary>
    public class HeightFileLoaderTest
    {
        // r16は0-1をushortへ量子化するため、往復誤差は半ステップ(約7.6e-6)まで許容する
        // r16 quantizes 0-1 into a ushort, so a round trip may drift by half a step (about 7.6e-6)
        private const float QuantizationTolerance = 1e-5f;

        private string _tempWorldRoot;

        [SetUp]
        public void SetUp()
        {
            _tempWorldRoot = Path.Combine(Path.GetTempPath(), "HeightFileLoaderTest_" + Guid.NewGuid());
        }

        [TearDown]
        public void TearDown()
        {
            // テスト用一時ディレクトリの後始末。外部境界(ファイルIO)のため実体有無だけ見る
            // Clean up the temp test directory; file IO is an external boundary so only existence is checked
            if (Directory.Exists(_tempWorldRoot))
                Directory.Delete(_tempWorldRoot, true);
        }

        [Test]
        public void RestoresWriterHeightsIncludingTheirXzOrientation()
        {
            // x係数とz係数を変えた非対称な高さを書き、転置・行列取り違えを検出できるようにする
            // Write asymmetric heights with different x and z coefficients so a transpose or index swap is detectable
            const int resolution = 5;
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(_tempWorldRoot);
            var writtenHeights = new float[resolution * resolution];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                writtenHeights[z * resolution + x] = (x * 7 + z * 3) / 100f;

            TerrainFileWriter.Write(worldDataDirectory, CreateOutput(resolution, writtenHeights));

            var loadedHeights = HeightFileLoader.LoadHeights(worldDataDirectory, 0, 0, resolution);

            Assert.That(loadedHeights.GetLength(0), Is.EqualTo(resolution));
            Assert.That(loadedHeights.GetLength(1), Is.EqualTo(resolution));
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                Assert.That(loadedHeights[z, x], Is.EqualTo(writtenHeights[z * resolution + x]).Within(QuantizationTolerance), $"z={z} x={x}");
        }

        [Test]
        public void DecodesHeightBytesAsLittleEndianUshort()
        {
            // writerを介さず生バイトから読み、下位バイト先行という符号化そのものを固定する
            // Read raw bytes without the writer so the little-endian-first encoding itself is pinned down
            const int resolution = 2;
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(_tempWorldRoot);
            var heightBytes = new byte[] { 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x80, 0x34, 0x12 };
            WriteRawTerrainFile(worldDataDirectory.TerrainHeightFilePath(0, 0), heightBytes);

            var loadedHeights = HeightFileLoader.LoadHeights(worldDataDirectory, 0, 0, resolution);

            Assert.That(loadedHeights[0, 0], Is.EqualTo(0f).Within(QuantizationTolerance));
            Assert.That(loadedHeights[0, 1], Is.EqualTo(1f).Within(QuantizationTolerance));
            Assert.That(loadedHeights[1, 0], Is.EqualTo(32768f / 65535f).Within(QuantizationTolerance));
            // 0x1234をビッグエンディアンで読むと0x3412になり、この検証で必ず落ちる
            // Reading 0x1234 as big-endian would yield 0x3412 and always fail this assertion
            Assert.That(loadedHeights[1, 1], Is.EqualTo(0x1234 / 65535f).Within(QuantizationTolerance));
        }

        [Test]
        public void ReadsTheFileBelongingToTheRequestedTile()
        {
            // タイル座標ごとに別ファイルを置き、要求したタイルのファイルだけを読むことを確かめる
            // Place a different file per tile coordinate to confirm only the requested tile's file is read
            const int resolution = 2;
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(_tempWorldRoot);
            WriteRawTerrainFile(worldDataDirectory.TerrainHeightFilePath(0, 0), new byte[8]);
            var otherTileBytes = new byte[] { 0x00, 0x00, 0xFF, 0xFF, 0x00, 0x00, 0xFF, 0xFF };
            WriteRawTerrainFile(worldDataDirectory.TerrainHeightFilePath(1, 2), otherTileBytes);

            var loadedHeights = HeightFileLoader.LoadHeights(worldDataDirectory, 1, 2, resolution);

            Assert.That(loadedHeights[0, 0], Is.EqualTo(0f).Within(QuantizationTolerance));
            Assert.That(loadedHeights[0, 1], Is.EqualTo(1f).Within(QuantizationTolerance));
            Assert.That(loadedHeights[1, 0], Is.EqualTo(0f).Within(QuantizationTolerance));
            Assert.That(loadedHeights[1, 1], Is.EqualTo(1f).Within(QuantizationTolerance));
        }

        [Test]
        public void ThrowsWhenHeightFileLengthDoesNotMatchResolution()
        {
            // 長さ不一致を黙って読むと以降の全ピクセルがずれる。切り詰めや解像度取り違えは明示失敗にする
            // Silently reading a mismatched length shifts every later pixel, so truncation or a wrong resolution must fail loudly
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(_tempWorldRoot);
            WriteRawTerrainFile(worldDataDirectory.TerrainHeightFilePath(0, 0), new byte[2 * 2 * 2 - 2]);

            Assert.Throws<InvalidOperationException>(() => HeightFileLoader.LoadHeights(worldDataDirectory, 0, 0, 2));
        }

        private static void WriteRawTerrainFile(string filePath, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllBytes(filePath, bytes);
        }

        private static MapGenerationOutput CreateOutput(int resolution, float[] heights)
        {
            var output = new MapGenerationOutput
            {
                Resolution = resolution,
                SpawnPoint = Vector3.zero,
                MapObjects = new List<PlacedMapObject>(),
                ItemVeins = new List<PlacedVein>(),
            };
            output.Tiles.Add(new TerrainTileOutput { TileX = 0, TileZ = 0, Heights = heights });
            return output;
        }
    }
}
