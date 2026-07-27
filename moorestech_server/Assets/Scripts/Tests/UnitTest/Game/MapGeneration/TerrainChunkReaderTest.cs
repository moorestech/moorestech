using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using Game.MapGeneration.Export;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.MapData;
using Tests.Module.TestMod;

namespace Tests.UnitTest.Game.MapGeneration
{
    // チャンクの唯一の契約は「全て解凍して連結すると元のterrainバイナリ列に戻る」こと。並び順と境界を実バイトで検証する
    // The only chunk contract is that decompressing and concatenating all of them reproduces the terrain binaries
    public class TerrainChunkReaderTest
    {
        private const int SyntheticFileByteSize = 100 * 1024;
        private readonly List<WorldDataDirectory> _createdWorldDataDirectories = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var worldDataDirectory in _createdWorldDataDirectories)
            {
                if (Directory.Exists(worldDataDirectory.Root)) Directory.Delete(worldDataDirectory.Root, true);
                if (Directory.Exists(worldDataDirectory.ProvisioningTempDirectory)) Directory.Delete(worldDataDirectory.ProvisioningTempDirectory, true);
            }
            _createdWorldDataDirectories.Clear();
        }

        [Test]
        public void 論理ストリームはタイル順にheightとbiomeを交互に並べチャンク境界はファイル途中でも切れる()
        {
            // 4タイル×各100KBなのでチャンク境界(256KB)はファイルの途中に落ちる。並び順を取り違えれば内容が食い違う
            // With 4 tiles of 100KB each the 256KB boundary falls mid-file, so a wrong order changes the bytes
            var worldDataDirectory = CreateSyntheticFourTileWorld();
            var expectedStreamBytes = ReadFilesInOrder(ExpectedStreamFilePathsOfFourTiles(worldDataDirectory));

            var chunkTotal = TerrainTransferMetaReader.Read(worldDataDirectory).TerrainChunkTotal;
            Assert.AreEqual(4, chunkTotal);

            var decompressedChunks = Enumerable.Range(0, chunkTotal)
                .Select(chunkIndex => Decompress(TerrainChunkReader.Read(worldDataDirectory, chunkIndex))).ToList();

            // 最終チャンク以外は必ず満杯。ここがずれるとクライアントの書き戻しオフセットが全て狂う
            // Every chunk but the last must be full; a slip here shifts every client-side write offset
            for (var chunkIndex = 0; chunkIndex < chunkTotal - 1; chunkIndex++)
                Assert.AreEqual(TerrainTransferMeta.ChunkByteSize, decompressedChunks[chunkIndex].Length);
            Assert.AreEqual(expectedStreamBytes.Length - (chunkTotal - 1) * TerrainTransferMeta.ChunkByteSize, decompressedChunks[chunkTotal - 1].Length);

            Assert.AreEqual(expectedStreamBytes, decompressedChunks.SelectMany(chunk => chunk).ToArray());
        }

        [Test]
        public void ComputeStreamHashは論理ストリーム全体のSHA256と一致する()
        {
            var worldDataDirectory = CreateSyntheticFourTileWorld();
            var expectedStreamBytes = ReadFilesInOrder(ExpectedStreamFilePathsOfFourTiles(worldDataDirectory));

            using var sha256 = SHA256.Create();
            var expectedHash = BitConverter.ToString(sha256.ComputeHash(expectedStreamBytes)).Replace("-", string.Empty).ToLowerInvariant();

            Assert.AreEqual(expectedHash, TerrainChunkReader.ComputeStreamHash(worldDataDirectory));
        }

        [Test]
        public void 生成済みワールドの全チャンクを連結すると実terrainファイルと一致する()
        {
            var worldDataDirectory = ProvisionGeneratedWorld();
            var terrainMeta = TerrainTransferMetaReader.Read(worldDataDirectory);
            Assert.AreEqual(1, terrainMeta.TerrainTileCount);

            var expectedStreamBytes = ReadFilesInOrder(new[]
            {
                worldDataDirectory.TerrainHeightFilePath(0, 0),
                worldDataDirectory.TerrainBiomeFilePath(0, 0),
            });
            var restoredStreamBytes = Enumerable.Range(0, terrainMeta.TerrainChunkTotal)
                .SelectMany(chunkIndex => Decompress(TerrainChunkReader.Read(worldDataDirectory, chunkIndex))).ToArray();

            Assert.AreEqual(expectedStreamBytes, restoredStreamBytes);
        }

        [Test]
        public void 範囲外のChunkIndexは空応答ではなく例外になる()
        {
            var worldDataDirectory = CreateSyntheticFourTileWorld();
            var chunkTotal = TerrainTransferMetaReader.Read(worldDataDirectory).TerrainChunkTotal;

            Assert.Throws<ArgumentOutOfRangeException>(() => TerrainChunkReader.Read(worldDataDirectory, chunkTotal));
            Assert.Throws<ArgumentOutOfRangeException>(() => TerrainChunkReader.Read(worldDataDirectory, -1));
        }

        [Test]
        public void 地形を持たないtemplateワールドはチャンク要求が例外でハッシュは空文字になる()
        {
            var worldDataDirectory = CreateWorldDataDirectory();
            WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
                worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, WorldProvisioner.TemplateMapMode, 42));

            Assert.Throws<InvalidOperationException>(() => TerrainChunkReader.Read(worldDataDirectory, 0));
            Assert.AreEqual(string.Empty, TerrainChunkReader.ComputeStreamHash(worldDataDirectory));
        }

        // 期待する並び順をテスト側に直書きする。実装の列挙メソッドを使うと並び順の検証が循環する
        // Spell the expected order out here; reusing the production enumerator would make the check circular
        private static string[] ExpectedStreamFilePathsOfFourTiles(WorldDataDirectory worldDataDirectory)
        {
            return new[]
            {
                worldDataDirectory.TerrainHeightFilePath(0, 0), worldDataDirectory.TerrainBiomeFilePath(0, 0),
                worldDataDirectory.TerrainHeightFilePath(1, 0), worldDataDirectory.TerrainBiomeFilePath(1, 0),
                worldDataDirectory.TerrainHeightFilePath(0, 1), worldDataDirectory.TerrainBiomeFilePath(0, 1),
                worldDataDirectory.TerrainHeightFilePath(1, 1), worldDataDirectory.TerrainBiomeFilePath(1, 1),
            };
        }

        private static byte[] ReadFilesInOrder(IReadOnlyList<string> filePaths)
        {
            return filePaths.SelectMany(File.ReadAllBytes).ToArray();
        }

        private static byte[] Decompress(byte[] compressedBytes)
        {
            using var decompressedStream = new MemoryStream();
            using (var gzipStream = new GZipStream(new MemoryStream(compressedBytes), CompressionMode.Decompress))
                gzipStream.CopyTo(decompressedStream);
            return decompressedStream.ToArray();
        }

        // 実生成を通さずタイル数と内容を制御した合成ワールドを作る。ファイルごとに異なる値で埋めて順序違反を検出可能にする
        // Build a synthetic world with controlled tile count and content; distinct fill values expose any ordering slip
        private WorldDataDirectory CreateSyntheticFourTileWorld()
        {
            var worldDataDirectory = CreateWorldDataDirectory();
            Directory.CreateDirectory(worldDataDirectory.TerrainDirectory);

            var streamFilePaths = ExpectedStreamFilePathsOfFourTiles(worldDataDirectory);
            for (var fileIndex = 0; fileIndex < streamFilePaths.Length; fileIndex++)
            {
                var fileBytes = new byte[SyntheticFileByteSize];
                for (var byteIndex = 0; byteIndex < fileBytes.Length; byteIndex++) fileBytes[byteIndex] = (byte)(fileIndex + 1);
                File.WriteAllBytes(streamFilePaths[fileIndex], fileBytes);
            }

            var worldMeta = new WorldMetaJson
            {
                Seed = 1,
                GeneratorVersion = "1.0.0",
                Algorithm = "test",
                MapMode = WorldProvisioner.GeneratedMapMode,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                TerrainResolution = 256,
                TerrainTileCount = 4,
            };
            File.WriteAllText(worldDataDirectory.WorldMetaFilePath, JsonConvert.SerializeObject(worldMeta, Formatting.Indented));
            return worldDataDirectory;
        }

        private WorldDataDirectory ProvisionGeneratedWorld()
        {
            // generatedの生成はMasterHolderを要求するのでDI構築でマスタをロードする
            // Generated mode requires MasterHolder, so load masters via a DI build first
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldDataDirectory = CreateWorldDataDirectory();
            WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
                worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, WorldProvisioner.GeneratedMapMode, 12345));
            return worldDataDirectory;
        }

        private WorldDataDirectory CreateWorldDataDirectory()
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), "TerrainChunkReaderTest_" + Guid.NewGuid());
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
            _createdWorldDataDirectories.Add(worldDataDirectory);
            return worldDataDirectory;
        }
    }
}
