using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Core.Master;
using Game.MapGeneration.Pipeline.Runtime;
using Game.Paths;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Server.Protocol.PacketResponse.MapData;
using Tests.Module;
using Tests.Module.TestMod;
using UnityEngine;
using UnityEngine.TestTools;
using static Server.Protocol.PacketResponse.GetMapDataProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    // ワイヤ経由の地形復元を検証する
    // Verify terrain restoration over the wire
    public class GetMapDataTerrainChunkTest
    {
        private TerrainTransferTestScope _testScope;

        [SetUp]
        public void SetUp()
        {
            _testScope = new TerrainTransferTestScope(nameof(GetMapDataTerrainChunkTest));
        }

        [TearDown]
        public void TearDown()
        {
            _testScope.End();
        }

        [Test]
        public void 全TerrainChunkを解凍して連結すると実terrainファイルと一致しTerrainHashも整合する()
        {
            var worldDataDirectory = _testScope.ProvisionGeneratedWorld(12345);
            var packetResponseCreator = CreatePacketResponseCreator(worldDataDirectory);

            var layoutResponse = RequestLayout(packetResponseCreator);

            // タイル数の正はマスタ1箇所。テスト側に写しを置かずロード済みマスタから読む
            // The tile count has a single source in the master, so it is read from the loaded master instead of a copy held here
            var gridSide = LoadedMasterGridSide();
            Assert.AreEqual(gridSide * gridSide, layoutResponse.TerrainMeta.TerrainTileCount);
            Assert.Greater(layoutResponse.TerrainMeta.TerrainChunkTotal, 0);

            // ChunkIndexは応答にも載る。要求と応答がずれていないことを1件ずつ確かめる
            // ChunkIndex is echoed in the response, so check request/response alignment chunk by chunk
            var restoredStreamBytes = new List<byte>();
            for (var chunkIndex = 0; chunkIndex < layoutResponse.TerrainMeta.TerrainChunkTotal; chunkIndex++)
            {
                var chunkResponse = RequestTerrainChunk(packetResponseCreator, chunkIndex);
                Assert.AreEqual(chunkIndex, chunkResponse.ChunkIndex);
                restoredStreamBytes.AddRange(TerrainTransferTestScope.DecompressChunk(chunkResponse.Payload));
            }

            var expectedStreamBytes = TerrainTransferTestScope.ReadFilesInOrder(
                ExpectedStreamFilePathsOfGeneratedWorld(worldDataDirectory, gridSide));
            Assert.AreEqual(expectedStreamBytes, restoredStreamBytes.ToArray());

            // TerrainHashは同じ論理ストリームのSHA256。テスト側で実ファイルから独立に再計算して突き合わせる
            // TerrainHash is the SHA256 of the same logical stream, recomputed here independently from the real files
            Assert.AreEqual(ComputeSha256Hex(expectedStreamBytes), layoutResponse.TerrainMeta.TerrainHash);
        }

        // 実生成は1タイルで往復するためChunkIndexの検証が1周で終わる。合成マルチチャンクワールドでN>=2を通す
        // Real generation round-trips one tile, so the ChunkIndex check closes after a single pass; the synthetic multi-chunk world drives N >= 2
        [Test]
        public void 複数チャンクのワールドはChunkIndexごとに対応するチャンクを返す()
        {
            var worldDataDirectory = SyntheticMultiTileWorldFactory.Create(
                _testScope, SyntheticMultiTileWorldFactory.MultiChunkFileByteSize);
            var packetResponseCreator = CreatePacketResponseCreator(worldDataDirectory);

            var layoutResponse = RequestLayout(packetResponseCreator);
            Assert.AreEqual(SyntheticMultiTileWorldFactory.TileCount, layoutResponse.TerrainMeta.TerrainTileCount);

            // 1周で終わるループはChunkIndexの往復をトートロジーにするため、複数チャンクであること自体を主張する
            // A single-pass loop makes the ChunkIndex round trip a tautology, so having more than one chunk is asserted outright
            Assert.Greater(layoutResponse.TerrainMeta.TerrainChunkTotal, 1);

            var restoredStreamBytes = new List<byte>();
            for (var chunkIndex = 0; chunkIndex < layoutResponse.TerrainMeta.TerrainChunkTotal; chunkIndex++)
            {
                var chunkResponse = RequestTerrainChunk(packetResponseCreator, chunkIndex);
                Assert.AreEqual(chunkIndex, chunkResponse.ChunkIndex);
                restoredStreamBytes.AddRange(TerrainTransferTestScope.DecompressChunk(chunkResponse.Payload));
            }

            var expectedStreamBytes = TerrainTransferTestScope.ReadFilesInOrder(
                SyntheticMultiTileWorldFactory.ExpectedStreamFilePaths(worldDataDirectory));
            Assert.AreEqual(expectedStreamBytes, restoredStreamBytes.ToArray());
            Assert.AreEqual(ComputeSha256Hex(expectedStreamBytes), layoutResponse.TerrainMeta.TerrainHash);
        }

        [Test]
        public void 地形を持たないtemplateワールドはTerrainHashが空文字になる()
        {
            var worldDataDirectory = _testScope.ProvisionTemplateWorld(42);

            var layoutResponse = RequestLayout(CreatePacketResponseCreator(worldDataDirectory));

            Assert.AreEqual(0, layoutResponse.TerrainMeta.TerrainChunkTotal);
            Assert.AreEqual(string.Empty, layoutResponse.TerrainMeta.TerrainHash);
        }

        [Test]
        public void 範囲外のChunkIndexは空のチャンクを返さずエラーになる()
        {
            var worldDataDirectory = _testScope.ProvisionGeneratedWorld(12345);
            var packetResponseCreator = CreatePacketResponseCreator(worldDataDirectory);
            var chunkTotal = RequestLayout(packetResponseCreator).TerrainMeta.TerrainChunkTotal;

            // 範囲外要求はプロトコルが例外にし、パケット層はエラーログを出して応答を返さない
            // An out-of-range request throws in the protocol; the packet layer logs the error and returns no response
            LogAssert.Expect(LogType.Error, new Regex($"[\\s\\S]*ChunkIndex {chunkTotal} is out of range[\\s\\S]*"));

            Assert.AreEqual(0, SendTerrainChunkRequest(packetResponseCreator, chunkTotal));
        }

        [Test]
        public void templateワールドへのTerrainChunk要求は空のチャンクを返さずエラーになる()
        {
            var worldDataDirectory = _testScope.ProvisionTemplateWorld(42);
            var packetResponseCreator = CreatePacketResponseCreator(worldDataDirectory);

            // 地形なしワールドへの要求も範囲外と同じくプロトコルが例外にする。応答0件はクライアントには届かないことを意味する
            // A terrain-less world throws in the protocol just like an out-of-range index; zero responses means the client gets nothing
            LogAssert.Expect(LogType.Error, new Regex("[\\s\\S]*owns no terrain chunk to read[\\s\\S]*"));

            Assert.AreEqual(0, SendTerrainChunkRequest(packetResponseCreator, 0));
        }

        // 生成マスタは正方格子なのでgridSizeXだけを返す。Zとの一致はWorldProvisionerTestが持つ
        // The generation master uses a square grid, so gridSizeX alone is returned; WorldProvisionerTest owns the match with Z
        private static int LoadedMasterGridSide()
        {
            return GenerationRuntimeConfigFactory.Build(MasterHolder.GenerationMaster.SelectedGeneration).gridSizeX;
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

        private static int SendTerrainChunkRequest(PacketResponseCreator packetResponseCreator, int chunkIndex)
        {
            var request = RequestMapDataMessagePack.CreateTerrainChunkRequest(chunkIndex);
            return packetResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null)).Count;
        }

        private static ResponseMapDataMessagePack RequestLayout(PacketResponseCreator packetResponseCreator)
        {
            var request = RequestMapDataMessagePack.CreateLayoutRequest();
            var responseBytes = packetResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null))[0];
            return MessagePackSerializer.Deserialize<ResponseMapDataMessagePack>(responseBytes);
        }

        private static ResponseMapDataTerrainChunkMessagePack RequestTerrainChunk(PacketResponseCreator packetResponseCreator, int chunkIndex)
        {
            var request = RequestMapDataMessagePack.CreateTerrainChunkRequest(chunkIndex);
            var responseBytes = packetResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null))[0];
            return MessagePackSerializer.Deserialize<ResponseMapDataTerrainChunkMessagePack>(responseBytes);
        }

        private static PacketResponseCreator CreatePacketResponseCreator(WorldDataDirectory worldDataDirectory)
        {
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = worldDataDirectory,
            };
            var (packetResponseCreator, _) = new MoorestechServerDIContainerGenerator().Create(options);
            return packetResponseCreator;
        }

        private static string ComputeSha256Hex(byte[] streamBytes)
        {
            using var sha256 = SHA256.Create();
            return BitConverter.ToString(sha256.ComputeHash(streamBytes)).Replace("-", string.Empty).ToLowerInvariant();
        }
    }
}
