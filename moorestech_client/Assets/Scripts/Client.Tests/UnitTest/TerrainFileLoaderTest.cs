using System;
using System.Collections.Generic;
using System.IO;
using Client.Game.InGame.Environment.Terrain;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;

namespace Client.Tests.UnitTest
{
    /// <summary>
    ///     TerrainFileLoader が P1 TerrainFileWriter の出力を元の高さ・バイオームへ復元することを検証する。
    ///     Verifies TerrainFileLoader restores the original heights and biomes from P1 TerrainFileWriter's output.
    /// </summary>
    public class TerrainFileLoaderTest
    {
        // r16は0-1をushortへ量子化するため、往復誤差は半ステップ(約7.6e-6)まで許容する
        // r16 quantizes 0-1 into a ushort, so a round trip may drift by half a step (about 7.6e-6)
        private const float QuantizationTolerance = 1e-5f;

        private string _tempWorldRoot;

        [SetUp]
        public void SetUp()
        {
            _tempWorldRoot = Path.Combine(Path.GetTempPath(), "TerrainFileLoaderTest_" + Guid.NewGuid());
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

            TerrainFileWriter.Write(worldDataDirectory, CreateOutput(resolution, writtenHeights, new byte[resolution * resolution]));

            var loadedHeights = TerrainFileLoader.LoadHeights(worldDataDirectory, 0, 0, resolution);

            Assert.That(loadedHeights.GetLength(0), Is.EqualTo(resolution));
            Assert.That(loadedHeights.GetLength(1), Is.EqualTo(resolution));
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                Assert.That(loadedHeights[z, x], Is.EqualTo(writtenHeights[z * resolution + x]).Within(QuantizationTolerance), $"z={z} x={x}");
        }

        [Test]
        public void RestoresWriterBiomeIndicesIncludingTheirXzOrientation()
        {
            // バイオームは全ピクセルで異なる値にし、転置しても偶然一致しないようにする
            // Give every pixel a distinct biome so a transposed read cannot coincidentally match
            const int resolution = 5;
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(_tempWorldRoot);
            var writtenBiomeIndices = new byte[resolution * resolution];
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                writtenBiomeIndices[z * resolution + x] = (byte)(z * resolution + x + 1);

            TerrainFileWriter.Write(worldDataDirectory, CreateOutput(resolution, new float[resolution * resolution], writtenBiomeIndices));

            var loadedBiomeIndices = TerrainFileLoader.LoadBiomeIndices(worldDataDirectory, 0, 0, resolution);

            Assert.That(loadedBiomeIndices.GetLength(0), Is.EqualTo(resolution));
            Assert.That(loadedBiomeIndices.GetLength(1), Is.EqualTo(resolution));
            for (var z = 0; z < resolution; z++)
            for (var x = 0; x < resolution; x++)
                Assert.That(loadedBiomeIndices[z, x], Is.EqualTo(writtenBiomeIndices[z * resolution + x]), $"z={z} x={x}");
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

            var loadedHeights = TerrainFileLoader.LoadHeights(worldDataDirectory, 0, 0, resolution);

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
            WriteRawTerrainFile(worldDataDirectory.TerrainBiomeFilePath(0, 0), new byte[] { 1, 1, 1, 1 });
            WriteRawTerrainFile(worldDataDirectory.TerrainBiomeFilePath(1, 2), new byte[] { 9, 8, 7, 6 });

            var loadedBiomeIndices = TerrainFileLoader.LoadBiomeIndices(worldDataDirectory, 1, 2, resolution);

            Assert.That(loadedBiomeIndices[0, 0], Is.EqualTo(9));
            Assert.That(loadedBiomeIndices[0, 1], Is.EqualTo(8));
            Assert.That(loadedBiomeIndices[1, 0], Is.EqualTo(7));
            Assert.That(loadedBiomeIndices[1, 1], Is.EqualTo(6));
        }

        [Test]
        public void ThrowsWhenHeightFileLengthDoesNotMatchResolution()
        {
            // 長さ不一致を黙って読むと以降の全ピクセルがずれる。切り詰めや解像度取り違えは明示失敗にする
            // Silently reading a mismatched length shifts every later pixel, so truncation or a wrong resolution must fail loudly
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(_tempWorldRoot);
            WriteRawTerrainFile(worldDataDirectory.TerrainHeightFilePath(0, 0), new byte[2 * 2 * 2 - 2]);

            Assert.Throws<InvalidOperationException>(() => TerrainFileLoader.LoadHeights(worldDataDirectory, 0, 0, 2));
        }

        [Test]
        public void ThrowsWhenBiomeFileLengthDoesNotMatchResolution()
        {
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(_tempWorldRoot);
            WriteRawTerrainFile(worldDataDirectory.TerrainBiomeFilePath(0, 0), new byte[2 * 2 + 1]);

            Assert.Throws<InvalidOperationException>(() => TerrainFileLoader.LoadBiomeIndices(worldDataDirectory, 0, 0, 2));
        }

        private static void WriteRawTerrainFile(string filePath, byte[] bytes)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath));
            File.WriteAllBytes(filePath, bytes);
        }

        private static MapGenerationOutput CreateOutput(int resolution, float[] heights, byte[] biomeIndices)
        {
            var output = new MapGenerationOutput
            {
                Resolution = resolution,
                SpawnPoint = Vector3.zero,
                MapObjects = new List<PlacedMapObject>(),
                ItemVeins = new List<PlacedVein>(),
            };
            output.Tiles.Add(new TerrainTileOutput { TileX = 0, TileZ = 0, Heights = heights, BiomeIndices = biomeIndices });
            return output;
        }
    }
}
