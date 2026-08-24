using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Game.MapGeneration.Export;
using Game.MapGeneration.Transfer;
using Game.Paths;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Protocol.PacketResponse.MapData;
using Tests.Module;

namespace Tests.UnitTest.Game.MapGeneration
{
    // チャンクの唯一の契約は「全て解凍して連結すると元のterrainバイナリ列に戻る」こと。並び順と境界を実バイトで検証する
    // The only chunk contract is that decompressing and concatenating all of them reproduces the terrain binaries
    public class TerrainChunkReaderTest
    {
        private const int SyntheticFileByteSize = 100 * 1024;

        // ForUnitTestModのgeneration.jsonが定めるgridSizeX/Z。値は生成jsonの実値であり推測ではない
        // gridSizeX/Z as declared in ForUnitTestMod's generation.json; a real value, not a guess
        private const int GridSideForUnitTestMod = 5;

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
            var worldDataDirectory = CreateSyntheticFourTileWorld(SyntheticFileByteSize);
            var expectedStreamBytes = TerrainTransferTestScope.ReadFilesInOrder(ExpectedStreamFilePathsOfFourTiles(worldDataDirectory));

            var chunkTotal = TerrainTransferMetaReader.Read(worldDataDirectory).TerrainChunkTotal;
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
            var worldDataDirectory = CreateSyntheticFourTileWorld(SyntheticFileByteSize);
            var expectedStreamBytes = TerrainTransferTestScope.ReadFilesInOrder(ExpectedStreamFilePathsOfFourTiles(worldDataDirectory));

            using var sha256 = SHA256.Create();
            var expectedHash = BitConverter.ToString(sha256.ComputeHash(expectedStreamBytes)).Replace("-", string.Empty).ToLowerInvariant();

            var terrainMeta = TerrainTransferMetaReader.Read(worldDataDirectory);
            Assert.AreEqual(expectedHash, TerrainStreamHasher.Compute(worldDataDirectory, terrainMeta));
        }

        [Test]
        public void 生成済みワールドの全チャンクを連結すると実terrainファイルと一致する()
        {
            var worldDataDirectory = _testScope.ProvisionGeneratedWorld(12345);
            var terrainMeta = TerrainTransferMetaReader.Read(worldDataDirectory);

            // ForUnitTestModのgeneration.jsonはgridSizeX/Z=5固定なのでタイル数25を直書きできる
            // ForUnitTestMod's generation.json pins gridSizeX/Z=5, so the tile count 25 can be hardcoded here
            Assert.AreEqual(GridSideForUnitTestMod * GridSideForUnitTestMod, terrainMeta.TerrainTileCount);

            var expectedStreamBytes = TerrainTransferTestScope.ReadFilesInOrder(
                ExpectedStreamFilePathsOfGeneratedWorld(worldDataDirectory, GridSideForUnitTestMod));
            var restoredStreamBytes = Enumerable.Range(0, terrainMeta.TerrainChunkTotal)
                .SelectMany(chunkIndex => TerrainTransferTestScope.DecompressChunk(TerrainChunkReader.Read(worldDataDirectory, chunkIndex))).ToArray();

            Assert.AreEqual(expectedStreamBytes, restoredStreamBytes);
        }

        [Test]
        public void 範囲外のChunkIndexは空応答ではなく例外になる()
        {
            var worldDataDirectory = CreateSyntheticFourTileWorld(SyntheticFileByteSize);
            var chunkTotal = TerrainTransferMetaReader.Read(worldDataDirectory).TerrainChunkTotal;

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
            var terrainMeta = TerrainTransferMetaReader.Read(worldDataDirectory);
            Assert.AreEqual(WorldMapMode.Generated, terrainMeta.MapMode);
            Assert.AreEqual(0, terrainMeta.TerrainChunkTotal);

            var hashException = Assert.Throws<InvalidOperationException>(() => TerrainStreamHasher.Compute(worldDataDirectory, terrainMeta));
            StringAssert.Contains("truncated", hashException.Message);
            var readException = Assert.Throws<InvalidOperationException>(() => TerrainChunkReader.Read(worldDataDirectory, 0));
            StringAssert.Contains("truncated", readException.Message);
        }

        // 期待する並び順をテスト側に直書きする。実装の列挙メソッドを使うと並び順の検証が循環する
        // Spell the expected order out here; reusing the production enumerator would make the check circular
        private static string[] ExpectedStreamFilePathsOfFourTiles(WorldDataDirectory worldDataDirectory)
        {
            return new[]
            {
                worldDataDirectory.TerrainHeightFilePath(0, 0),
                worldDataDirectory.TerrainHeightFilePath(1, 0),
                worldDataDirectory.TerrainHeightFilePath(0, 1),
                worldDataDirectory.TerrainHeightFilePath(1, 1),
            };
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

        // 実生成を通さずタイル数と内容を制御した合成ワールドを作る。ファイルごとに異なる値で埋めて順序違反を検出可能にする
        // Build a synthetic world with controlled tile count and content; distinct fill values expose any ordering slip
        private WorldDataDirectory CreateSyntheticFourTileWorld(int fileByteSize)
        {
            var worldDataDirectory = _testScope.CreateEmptyWorldDataDirectory();
            Directory.CreateDirectory(worldDataDirectory.TerrainDirectory);

            var streamFilePaths = ExpectedStreamFilePathsOfFourTiles(worldDataDirectory);
            for (var fileIndex = 0; fileIndex < streamFilePaths.Length; fileIndex++)
            {
                var fileBytes = new byte[fileByteSize];
                for (var byteIndex = 0; byteIndex < fileBytes.Length; byteIndex++) fileBytes[byteIndex] = (byte)(fileIndex + 1);
                File.WriteAllBytes(streamFilePaths[fileIndex], fileBytes);
            }

            var worldMeta = new WorldMetaJson
            {
                Seed = 1,
                GeneratorVersion = WorldGeneratorVersion.Current,
                Algorithm = "test",
                MapMode = WorldMapMode.Generated,
                CreatedAt = DateTime.UtcNow.ToString("O"),
                TerrainResolution = 256,
                TerrainTileCount = 4,

                // generatedのworld.jsonは原点を必ず持つ契約。合成ワールドも原点0の実値として明示する
                // A generated world.json always carries origins by contract, so the synthetic world states them explicitly as a real 0
                TerrainNoiseOriginX = 0f,
                TerrainNoiseOriginZ = 0f,
                TerrainSceneOriginX = 0f,
                TerrainSceneOriginZ = 0f,

                // 指紋は必須契約だがチャンク読み出しの対象外
                // The fingerprint is a required contract but out of scope for this chunk-reading test
                GenerationMasterFingerprint = "synthetic-fingerprint",

                // 台帳の指紋も同じく必須契約。チャンク読み出しは見た目キャッシュを引かないので値は問わない
                // The ledger digest is a required contract too; chunk reading never touches the visual cache, so its value is immaterial here
                PlacementLedgerDigest = "synthetic-ledger-digest",
            };
            File.WriteAllText(worldDataDirectory.WorldMetaFilePath, JsonConvert.SerializeObject(worldMeta, Formatting.Indented));
            return worldDataDirectory;
        }
    }
}
