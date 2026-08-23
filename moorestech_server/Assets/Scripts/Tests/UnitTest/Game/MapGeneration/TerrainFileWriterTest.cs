using System;
using System.Collections.Generic;
using System.IO;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using UnityEngine;

namespace Tests.UnitTest.Game.MapGeneration
{
    // TerrainFileWriter が terrain バイナリと cache README を規定サイズ・内容で書き出すことを検証する。
    // Verify TerrainFileWriter writes terrain binaries and the cache README at the expected size/content.
    public class TerrainFileWriterTest
    {
        private string _tempWorldRoot;

        [SetUp]
        public void SetUp()
        {
            _tempWorldRoot = Path.Combine(Path.GetTempPath(), "TerrainFileWriterTest_" + Guid.NewGuid());
        }

        [TearDown]
        public void TearDown()
        {
            // テスト用一時ディレクトリの後始末。外部境界(ファイルIO)のため例外を許容する
            // Clean up the temp test directory; file IO is an external boundary
            if (Directory.Exists(_tempWorldRoot))
                Directory.Delete(_tempWorldRoot, true);
        }

        [Test]
        public void WritesTerrainBinariesAndCacheReadme()
        {
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(_tempWorldRoot);
            const int resolution = 4;

            TerrainFileWriter.Write(worldDataDirectory, CreateFlatOutput(resolution));

            var heightFilePath = worldDataDirectory.TerrainHeightFilePath(0, 0);

            Assert.That(File.Exists(heightFilePath), Is.True);
            Assert.That(new FileInfo(heightFilePath).Length, Is.EqualTo(resolution * resolution * 2));

            Assert.That(File.Exists(worldDataDirectory.CacheReadmeFilePath), Is.True);
            var readmeText = File.ReadAllText(worldDataDirectory.CacheReadmeFilePath);
            Assert.That(readmeText, Is.EqualTo("このディレクトリは削除可能です。削除しても次回起動時に自動で再構築されます。"));
        }

        [Test]
        public void EncodesKnownHeightsToExpectedUshortValues()
        {
            // 既知の高さ値(0/0.5/1.0)がr16へ正しく符号化されることを検証する(丸め・クランプの回帰防止)。
            // Verify known height values (0/0.5/1.0) encode correctly to r16 (guards rounding/clamp regressions).
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(_tempWorldRoot);
            const int resolution = 2;
            var output = new MapGenerationOutput
            {
                Resolution = resolution,
                SpawnPoint = Vector3.zero,
                MapObjects = new List<PlacedMapObject>(),
                ItemVeins = new List<PlacedVein>(),
            };
            output.Tiles.Add(new TerrainTileOutput
            {
                TileX = 0,
                TileZ = 0,
                Heights = new[] { 0f, 0.5f, 1.0f, 1.0000003f },
            });

            TerrainFileWriter.Write(worldDataDirectory, output);

            var bytes = File.ReadAllBytes(worldDataDirectory.TerrainHeightFilePath(0, 0));

            Assert.That(DecodeUshortLittleEndian(bytes, 0), Is.EqualTo(0));
            Assert.That(DecodeUshortLittleEndian(bytes, 1), Is.EqualTo(32768));
            Assert.That(DecodeUshortLittleEndian(bytes, 2), Is.EqualTo(65535));
            // わずかに1.0を超える値もクランプにより65535に丸まりラップしないことを確認
            // A value slightly above 1.0 clamps to 65535 rather than wrapping
            Assert.That(DecodeUshortLittleEndian(bytes, 3), Is.EqualTo(65535));
        }

        [Test]
        public void 書き出したファイル長は転送メタが想定するセグメント長と一致する()
        {
            // EnumerateStreamSegmentsのres²×bppは実ファイル長の第2定義。乖離すると受信側の書き戻し境界が全て狂う
            // EnumerateStreamSegments' res^2 x bpp is a second definition of the real file length; a drift shifts every restore boundary
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(_tempWorldRoot);
            const int resolution = 4;

            TerrainFileWriter.Write(worldDataDirectory, CreateFlatOutput(resolution));

            foreach (var segment in TerrainTransferMeta.EnumerateStreamSegments(worldDataDirectory, 1, resolution))
                Assert.That(new FileInfo(segment.FilePath).Length, Is.EqualTo(segment.ByteLength), segment.FilePath);
        }

        private static ushort DecodeUshortLittleEndian(byte[] bytes, int index)
        {
            var offset = index * 2;
            return (ushort)(bytes[offset] | (bytes[offset + 1] << 8));
        }

        private static MapGenerationOutput CreateFlatOutput(int resolution)
        {
            var output = new MapGenerationOutput
            {
                Resolution = resolution,
                SpawnPoint = Vector3.zero,
                MapObjects = new List<PlacedMapObject>(),
                ItemVeins = new List<PlacedVein>(),
            };
            output.Tiles.Add(new TerrainTileOutput
            {
                TileX = 0,
                TileZ = 0,
                Heights = new float[resolution * resolution],
            });
            return output;
        }
    }
}
