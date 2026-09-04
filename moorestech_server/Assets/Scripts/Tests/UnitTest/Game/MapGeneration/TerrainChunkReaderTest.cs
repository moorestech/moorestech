using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using Game.MapGeneration.Export;
using Game.MapGeneration.Transfer;
using Game.Paths;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using Tests.Module;

namespace Tests.UnitTest.Game.MapGeneration
{
    // チャンクの唯一の契約は「全て解凍して連結すると元のterrainバイナリ列に戻る」こと。並び順と境界を実バイトで検証する
    // The only chunk contract is that decompressing and concatenating all of them reproduces the terrain binaries
    // shard割当はクラスと一緒に移動・改名される
    // The shard assignment travels with the class through moves and renames
    [Category("CiShardServerMap3")]
    public class TerrainChunkReaderTest
    {
        private TerrainTransferTestScope _testScope;

        [SetUp]
        public void SetUp()
        {
            _testScope = new TerrainTransferTestScope(nameof(TerrainChunkReaderTest));
        }

        [TearDown]
        public void TearDown()
        {
            _testScope.End();
        }

        [Test]
        public void 論理ストリームはタイル順にheightを並べチャンク境界はファイル途中でも切れる()
        {
            // 4タイル×各100KBの400KBなのでチャンク境界(256KB)はファイルの途中に落ちる。並び順を取り違えれば内容が食い違う
            // With 4 tiles of 100KB each (400KB total) the 256KB boundary falls mid-file, so a wrong order changes the bytes
            var worldDataDirectory = CreateSyntheticFourTileWorld(SyntheticMultiTileWorldFactory.MultiChunkFileByteSize);
            var expectedStreamBytes = TerrainTransferTestScope.ReadFilesInOrder(
                SyntheticMultiTileWorldFactory.ExpectedStreamFilePaths(worldDataDirectory));

            var chunkTotal = ReadGenerated(worldDataDirectory).TerrainChunkTotal;
            Assert.AreEqual(2, chunkTotal);

            var decompressedChunks = Enumerable.Range(0, chunkTotal)
                .Select(chunkIndex => TerrainTransferTestScope.DecompressChunk(TerrainChunkReader.Read(worldDataDirectory, chunkIndex))).ToList();

            // 最終チャンク以外は必ず満杯。ここがずれるとクライアントの書き戻しオフセットが全て狂う
            // Every chunk but the last must be full; a slip here shifts every client-side write offset
            for (var chunkIndex = 0; chunkIndex < chunkTotal - 1; chunkIndex++)
                Assert.AreEqual(TerrainTransferMeta.ChunkByteSize, decompressedChunks[chunkIndex].Length);
            Assert.AreEqual(expectedStreamBytes.Length - (chunkTotal - 1) * TerrainTransferMeta.ChunkByteSize, decompressedChunks[chunkTotal - 1].Length);

            Assert.AreEqual(expectedStreamBytes, decompressedChunks.SelectMany(chunk => chunk).ToArray());
        }

        [Test]
        public void TerrainStreamHasherは論理ストリーム全体のSHA256と一致する()
        {
            var worldDataDirectory = CreateSyntheticFourTileWorld(SyntheticMultiTileWorldFactory.MultiChunkFileByteSize);
            var expectedStreamBytes = TerrainTransferTestScope.ReadFilesInOrder(
                SyntheticMultiTileWorldFactory.ExpectedStreamFilePaths(worldDataDirectory));

            using var sha256 = SHA256.Create();
            var expectedHash = BitConverter.ToString(sha256.ComputeHash(expectedStreamBytes)).Replace("-", string.Empty).ToLowerInvariant();

            var terrainMeta = TerrainTransferMetaReader.Read(worldDataDirectory);
            Assert.AreEqual(expectedHash, TerrainStreamHasher.Compute(worldDataDirectory, terrainMeta));
        }

        [Test]
        public void 生成済みワールドの全チャンクを連結すると実terrainファイルと一致する()
        {
            // 合成ワールドは並びを自前で書くので実生成のtileX_tileZ取り違えを検出できない。ここだけ実生成を多タイルで通す
            // A synthetic world spells its own order out and cannot catch a real tileX/tileZ mix-up, so this case generates multiple tiles for real
            var worldDataDirectory = _testScope.ProvisionLowResolutionMultiTileGeneratedWorld(12345);
            var terrainMeta = ReadGenerated(worldDataDirectory);

            const int gridSide = TerrainTransferTestScope.LowResolutionMultiTileGridSide;
            Assert.AreEqual(gridSide * gridSide, terrainMeta.TerrainTileCount);

            var expectedStreamBytes = TerrainTransferTestScope.ReadFilesInOrder(
                ExpectedStreamFilePathsOfGeneratedWorld(worldDataDirectory, gridSide));
            var restoredStreamBytes = Enumerable.Range(0, terrainMeta.TerrainChunkTotal)
                .SelectMany(chunkIndex => TerrainTransferTestScope.DecompressChunk(TerrainChunkReader.Read(worldDataDirectory, chunkIndex))).ToArray();

            Assert.AreEqual(expectedStreamBytes, restoredStreamBytes);
        }

        [Test]
        public void 範囲外のChunkIndexは空応答ではなく例外になる()
        {
            var worldDataDirectory = CreateSyntheticFourTileWorld(SyntheticMultiTileWorldFactory.MultiChunkFileByteSize);
            var chunkTotal = ReadGenerated(worldDataDirectory).TerrainChunkTotal;

            Assert.Throws<ArgumentOutOfRangeException>(() => TerrainChunkReader.Read(worldDataDirectory, chunkTotal));
            Assert.Throws<ArgumentOutOfRangeException>(() => TerrainChunkReader.Read(worldDataDirectory, -1));
        }

        [Test]
        public void 地形を持たないtemplateワールドはチャンク要求が例外でハッシュは空文字になる()
        {
            var worldDataDirectory = _testScope.ProvisionTemplateWorld(42);
            var terrainMeta = TerrainTransferMetaReader.Read(worldDataDirectory);

            Assert.Throws<InvalidOperationException>(() => TerrainChunkReader.Read(worldDataDirectory, 0));
            Assert.AreEqual(string.Empty, TerrainStreamHasher.Compute(worldDataDirectory, terrainMeta));
        }

        [Test]
        public void generatedワールドのterrainが0バイトなら地形なし扱いにせず例外になる()
        {
            // 生成失敗や切り詰めで実ファイルが空になった状態。templateと同一視すると壊れたワールドを正常として配ってしまう
            // Terrain emptied by a failed generation or truncation; equating it with template would ship a broken world as healthy
            var worldDataDirectory = CreateSyntheticFourTileWorld(0);
            var terrainMeta = ReadGenerated(worldDataDirectory);
            Assert.AreEqual(WorldMapMode.Generated, terrainMeta.MapMode);
            Assert.AreEqual(0, terrainMeta.TerrainChunkTotal);

            var hashException = Assert.Throws<InvalidOperationException>(() => TerrainStreamHasher.Compute(worldDataDirectory, terrainMeta));
            StringAssert.Contains("truncated", hashException.Message);
            var readException = Assert.Throws<InvalidOperationException>(() => TerrainChunkReader.Read(worldDataDirectory, 0));
            StringAssert.Contains("truncated", readException.Message);
        }

        // 期待する並び順をテスト側に直書きする。実装の列挙メソッドを使うと並び順の検証が循環する
        // Spell the expected order out here; reusing the production enumerator would make the check circular
        private static List<string> ExpectedStreamFilePathsOfGeneratedWorld(WorldDataDirectory worldDataDirectory, int gridSide)
        {
            var streamFilePaths = new List<string>(gridSide * gridSide);
            for (var tileZ = 0; tileZ < gridSide; tileZ++)
            for (var tileX = 0; tileX < gridSide; tileX++)
                streamFilePaths.Add(worldDataDirectory.TerrainHeightFilePath(tileX, tileZ));
            return streamFilePaths;
        }

        private WorldDataDirectory CreateSyntheticFourTileWorld(int fileByteSize)
        {
            return SyntheticMultiTileWorldFactory.Create(_testScope, fileByteSize);
        }

        // 地形の寸法はgeneratedのメタにしか無い。generatedワールドを読んだ結果がその型であること自体が検証対象でもある
        // Terrain dimensions live on the generated meta alone, and a generated world reading back as that type is itself part of what is verified
        private static GeneratedTerrainTransferMeta ReadGenerated(WorldDataDirectory worldDataDirectory)
        {
            return (GeneratedTerrainTransferMeta)TerrainTransferMetaReader.Read(worldDataDirectory);
        }
    }
}
