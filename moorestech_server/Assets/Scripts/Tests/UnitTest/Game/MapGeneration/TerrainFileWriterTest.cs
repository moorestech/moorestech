using System;
using System.Collections.Generic;
using System.IO;
using Game.MapGeneration.Export;
using Game.MapGeneration.Pipeline;
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
            var output = new MapGenerationOutput
            {
                Heights = new float[resolution * resolution],
                BiomeIndices = new byte[resolution * resolution],
                Resolution = resolution,
                SpawnPoint = Vector3.zero,
                MapObjects = new List<PlacedMapObject>(),
                ItemVeins = new List<PlacedVein>(),
            };

            TerrainFileWriter.Write(worldDataDirectory, output);

            var heightFilePath = Path.Combine(worldDataDirectory.TerrainDirectory, "height_0_0.r16");
            var biomeFilePath = Path.Combine(worldDataDirectory.TerrainDirectory, "biome_0_0.bin");

            Assert.That(File.Exists(heightFilePath), Is.True);
            Assert.That(File.Exists(biomeFilePath), Is.True);
            Assert.That(new FileInfo(heightFilePath).Length, Is.EqualTo(resolution * resolution * 2));
            Assert.That(new FileInfo(biomeFilePath).Length, Is.EqualTo(resolution * resolution));

            Assert.That(File.Exists(worldDataDirectory.CacheReadmeFilePath), Is.True);
            var readmeText = File.ReadAllText(worldDataDirectory.CacheReadmeFilePath);
            Assert.That(readmeText, Is.EqualTo("このディレクトリは削除可能です。削除しても次回起動時に自動で再構築されます。"));
        }
    }
}
