using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.MapGeneration.Export;
using Game.MapGeneration.Provisioning;
using Game.MapGeneration.Transfer;
using Game.Paths;
using MessagePack;
using Newtonsoft.Json;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.GetMapDataProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    // 実生成ワールドの地形メタを検証する
    // Verify metadata from a provisioned world
    public class GetMapDataTerrainMetaTest
    {
        private readonly List<WorldDataDirectory> _createdWorldDataDirectories = new();

        [TearDown]
        public void TearDown()
        {
            // 削除対象のパスはWorldDataDirectoryから取る。テスト側でパス規則を再導出しない
            // Take the paths to delete from WorldDataDirectory; never re-derive the path rules here
            foreach (var worldDataDirectory in _createdWorldDataDirectories)
            {
                if (Directory.Exists(worldDataDirectory.Root)) Directory.Delete(worldDataDirectory.Root, true);
                if (Directory.Exists(worldDataDirectory.ProvisioningTempDirectory)) Directory.Delete(worldDataDirectory.ProvisioningTempDirectory, true);
            }
            _createdWorldDataDirectories.Clear();
        }

        [Test]
        public void Generatedワールドのterrainメタがworld_jsonとterrainファイル実体に整合する()
        {
            // generatedのプロビジョニングはMasterHolderを要求するため、先にDI構築でマスタをロードする
            // Provisioning in generated mode requires MasterHolder, so load masters via a DI build first
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldDataDirectory = ProvisionWorld(WorldMapMode.Generated, 12345);
            var response = RequestMapDataLayout(worldDataDirectory);

            var worldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(worldDataDirectory.WorldMetaFilePath));
            Assert.AreEqual(WorldMapMode.Generated, response.TerrainMeta.MapMode);
            Assert.AreEqual(worldMeta.TerrainResolution, response.TerrainMeta.TerrainResolution);
            Assert.AreEqual(worldMeta.TerrainTileCount, response.TerrainMeta.TerrainTileCount);
            Assert.Greater(response.TerrainMeta.TerrainResolution, 0);

            // チャンク総数をterrain実ファイルの総バイト数から独立に算出して突き合わせる
            // Recompute the chunk total independently from the real terrain file sizes
            var terrainTotalBytes = Directory.GetFiles(worldDataDirectory.TerrainDirectory).Sum(filePath => new FileInfo(filePath).Length);
            var expectedChunkTotal = (int)((terrainTotalBytes + TerrainTransferMeta.ChunkByteSize - 1) / TerrainTransferMeta.ChunkByteSize);
            Assert.Greater(expectedChunkTotal, 0);
            Assert.AreEqual(expectedChunkTotal, response.TerrainMeta.TerrainChunkTotal);

            Assert.IsTrue(response.TerrainMeta.WorldId.Length == 16 && response.TerrainMeta.WorldId.All(Uri.IsHexDigit), $"WorldId is not a 16-digit hex: '{response.TerrainMeta.WorldId}'");

            // クライアントは分類段の再実行にこのseedを使う。値がずれると別ワールドの海岸線を転送地形に貼ることになる
            // Clients re-run the classification stage with this seed; a wrong value paints another world's coastline onto the transferred terrain
            Assert.AreEqual(12345, worldMeta.Seed, "前提: 指定したseedがworld.jsonに記録されている");
            Assert.AreEqual(worldMeta.Seed, response.TerrainMeta.WorldSeed);

            // クライアントはこのノイズ窓原点で分類段を再実行し、このシーン原点に地形を置く。落ちると別の場所の海岸線と草を貼る
            // Clients re-run the classification stage on this noise origin and place the terrain at this scene origin; dropping them paints another place's coastline and grass
            Assert.AreEqual(worldMeta.TerrainNoiseOriginX, response.TerrainMeta.NoiseOrigin.X);
            Assert.AreEqual(worldMeta.TerrainNoiseOriginZ, response.TerrainMeta.NoiseOrigin.Y);
            Assert.AreEqual(worldMeta.TerrainSceneOriginX, response.TerrainMeta.SceneOrigin.X);
            Assert.AreEqual(worldMeta.TerrainSceneOriginZ, response.TerrainMeta.SceneOrigin.Y);
        }

        // スポーン探索が効いたワールドではノイズ窓原点が0から離れる。テストmodは探索無効なのでworld.jsonへ直に非ゼロを書いて経路を試す
        // A world with an effective spawn search has a noise origin far from 0; the test mod disables the search, so non-zero values are written into world.json to exercise the path
        [Test]
        public void world_jsonのノイズ窓原点とシーン原点がLayout応答まで運ばれる()
        {
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldDataDirectory = ProvisionWorld(WorldMapMode.Generated, 12345);

            var worldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(worldDataDirectory.WorldMetaFilePath));
            worldMeta.TerrainNoiseOriginX = 1617.5f;
            worldMeta.TerrainNoiseOriginZ = -1308.25f;
            worldMeta.TerrainSceneOriginX = 617.5f;
            worldMeta.TerrainSceneOriginZ = -308.25f;
            File.WriteAllText(worldDataDirectory.WorldMetaFilePath, JsonConvert.SerializeObject(worldMeta, Formatting.Indented));

            var response = RequestMapDataLayout(worldDataDirectory);

            Assert.AreEqual(1617.5f, response.TerrainMeta.NoiseOrigin.X);
            Assert.AreEqual(-1308.25f, response.TerrainMeta.NoiseOrigin.Y);
            Assert.AreEqual(617.5f, response.TerrainMeta.SceneOrigin.X);
            Assert.AreEqual(-308.25f, response.TerrainMeta.SceneOrigin.Y);
        }

        [Test]
        public void Templateワールドはterrainメタが0でWorldIdはワールドごとに異なる()
        {
            var firstWorld = ProvisionWorld(WorldMapMode.Template, 42);
            var firstResponse = RequestMapDataLayout(firstWorld);

            // templateは地形を持たないので3項目とも0、WorldIdだけは埋まる
            // Template owns no terrain, so all three values are 0 while WorldId is still filled
            Assert.AreEqual(WorldMapMode.Template, firstResponse.TerrainMeta.MapMode);
            Assert.AreEqual(0, firstResponse.TerrainMeta.TerrainResolution);
            Assert.AreEqual(0, firstResponse.TerrainMeta.TerrainTileCount);
            Assert.AreEqual(0, firstResponse.TerrainMeta.TerrainChunkTotal);
            Assert.IsTrue(firstResponse.TerrainMeta.WorldId.Length == 16 && firstResponse.TerrainMeta.WorldId.All(Uri.IsHexDigit), $"WorldId is not a 16-digit hex: '{firstResponse.TerrainMeta.WorldId}'");

            // 別ワールドは別のWorldIdになる（クライアント側のワールド識別に使うため）
            // A different world yields a different WorldId, since clients identify worlds by it
            var secondWorld = ProvisionWorld(WorldMapMode.Template, 43);
            var secondResponse = RequestMapDataLayout(secondWorld);
            Assert.AreNotEqual(firstResponse.TerrainMeta.WorldId, secondResponse.TerrainMeta.WorldId);

            // templateもworld.jsonにseedを持つので実値をそのまま載せる。地形を構築しないので分類段では使われない
            // A template world still records a seed in world.json, so the real value is carried; it builds no terrain, so the classification stage never consumes it
            Assert.AreEqual(42, firstResponse.TerrainMeta.WorldSeed);
            Assert.AreEqual(43, secondResponse.TerrainMeta.WorldSeed);
        }

        private WorldDataDirectory ProvisionWorld(string mapMode, int seed)
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), "GetMapDataTerrainMetaTest_" + Guid.NewGuid());
            var worldDataDirectory = WorldDataDirectory.FromWorldRoot(worldRoot);
            _createdWorldDataDirectories.Add(worldDataDirectory);

            WorldProvisioner.EnsureWorld(new WorldProvisionSettings(
                worldDataDirectory, TestModDirectory.ForUnitTestModDirectory, mapMode, seed));
            return worldDataDirectory;
        }

        private static ResponseMapDataMessagePack RequestMapDataLayout(WorldDataDirectory worldDataDirectory)
        {
            var options = new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory)
            {
                worldDataDirectory = worldDataDirectory,
            };
            var (packet, _) = new MoorestechServerDIContainerGenerator().Create(options);

            var request = RequestMapDataMessagePack.CreateLayoutRequest();
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null))[0];
            return MessagePackSerializer.Deserialize<ResponseMapDataMessagePack>(responseBytes);
        }
    }
}
