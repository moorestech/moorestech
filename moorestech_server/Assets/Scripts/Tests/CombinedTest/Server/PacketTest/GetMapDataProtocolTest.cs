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
    public class GetMapDataProtocolTest
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
        public void GetMapDataLayoutTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // va:mapData Layoutをリクエストしてレスポンスを取得
            // Request va:mapData Layout and obtain the response
            var request = new RequestMapDataMessagePack(MapDataMode.Layout);
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null))[0];
            var response = MessagePackSerializer.Deserialize<ResponseMapDataMessagePack>(responseBytes);

            // Spawnがmap.jsonのdefaultSpawnPointと一致することを検証
            // Verify Spawn matches map.json's defaultSpawnPoint
            Assert.AreEqual(186.0f, response.Spawn.X);
            Assert.AreEqual(15.7f, response.Spawn.Y);
            Assert.AreEqual(-37.401f, response.Spawn.Z);

            // mapObjects全5件がmap.jsonの内容と一致することを検証
            // Verify all 5 mapObjects match map.json's content
            Assert.AreEqual(5, response.MapObjects.Count);

            var object0 = response.MapObjects[0];
            Assert.AreEqual(0, object0.InstanceId);
            Assert.AreEqual("8c0e1339-be75-4690-99cd-58b5385a17cd", object0.MapObjectGuid);
            Assert.AreEqual(0.0f, object0.X);
            Assert.AreEqual(0.0f, object0.Y);
            Assert.AreEqual(0.0f, object0.Z);

            var object3 = response.MapObjects[3];
            Assert.AreEqual(3, object3.InstanceId);
            Assert.AreEqual("00000000-0000-1111-0000-000000000001", object3.MapObjectGuid);
            Assert.AreEqual(1.0f, object3.X);
            Assert.AreEqual(0.0f, object3.Y);
            Assert.AreEqual(1.0f, object3.Z);

            var object4 = response.MapObjects[4];
            Assert.AreEqual(4, object4.InstanceId);
            Assert.AreEqual("00000000-0000-1111-0000-000000000001", object4.MapObjectGuid);
            Assert.AreEqual(5.5f, object4.X);
            Assert.AreEqual(0.5f, object4.Y);
            Assert.AreEqual(1.5f, object4.Z);

            // mapVeins全3件のAABBがmap.jsonの内容と一致することを検証
            // Verify all 3 mapVeins' AABBs match map.json's content
            Assert.AreEqual(3, response.MapVeins.Count);

            var vein0 = response.MapVeins[0];
            Assert.AreEqual("11111111-0000-0000-0000-000000000001", vein0.VeinGuid);
            Assert.AreEqual(0, vein0.MinX);
            Assert.AreEqual(5, vein0.MinY);
            Assert.AreEqual(0, vein0.MinZ);
            Assert.AreEqual(0, vein0.MaxX);
            Assert.AreEqual(5, vein0.MaxY);
            Assert.AreEqual(0, vein0.MaxZ);

            var vein1 = response.MapVeins[1];
            Assert.AreEqual("11111111-0000-0000-0000-000000000002", vein1.VeinGuid);
            Assert.AreEqual(0, vein1.MinX);
            Assert.AreEqual(0, vein1.MinY);
            Assert.AreEqual(0, vein1.MinZ);
            Assert.AreEqual(10, vein1.MaxX);
            Assert.AreEqual(0, vein1.MaxY);
            Assert.AreEqual(0, vein1.MaxZ);

            var vein2 = response.MapVeins[2];
            Assert.AreEqual("11111111-0000-0000-0000-000000000003", vein2.VeinGuid);
            Assert.AreEqual(20, vein2.MinX);
            Assert.AreEqual(0, vein2.MinY);
            Assert.AreEqual(0, vein2.MinZ);
            Assert.AreEqual(20, vein2.MaxX);
            Assert.AreEqual(0, vein2.MaxY);
            Assert.AreEqual(0, vein2.MaxZ);

            // ワールドディレクトリを持たない構成では地形を持たずWorldIdも定まらない
            // A config without a world directory owns no terrain and has no world identity
            Assert.AreEqual(WorldProvisioner.TemplateMapMode, response.MapMode);
            Assert.AreEqual(string.Empty, response.WorldId);
            Assert.AreEqual(0, response.TerrainResolution);
            Assert.AreEqual(0, response.TerrainTileCount);
            Assert.AreEqual(0, response.TerrainChunkTotal);
        }

        [Test]
        public void Generatedワールドのterrainメタがworld_jsonとterrainファイル実体に整合する()
        {
            // generatedのプロビジョニングはMasterHolderを要求するため、先にDI構築でマスタをロードする
            // Provisioning in generated mode requires MasterHolder, so load masters via a DI build first
            new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var worldDataDirectory = ProvisionWorld(WorldProvisioner.GeneratedMapMode, 12345);
            var response = RequestMapDataLayout(worldDataDirectory);

            // MapMode・解像度・タイル数がworld.jsonの記録と一致する
            // MapMode, resolution and tile count match what world.json recorded
            var worldMeta = JsonConvert.DeserializeObject<WorldMetaJson>(File.ReadAllText(worldDataDirectory.WorldMetaFilePath));
            Assert.AreEqual(WorldProvisioner.GeneratedMapMode, response.MapMode);
            Assert.AreEqual(worldMeta.TerrainResolution, response.TerrainResolution);
            Assert.AreEqual(worldMeta.TerrainTileCount, response.TerrainTileCount);
            Assert.Greater(response.TerrainResolution, 0);

            // チャンク総数をterrain実ファイルの総バイト数から独立に算出して突き合わせる
            // Recompute the chunk total independently from the real terrain file sizes
            var terrainTotalBytes = Directory.GetFiles(worldDataDirectory.TerrainDirectory).Sum(filePath => new FileInfo(filePath).Length);
            var expectedChunkTotal = (int)((terrainTotalBytes + TerrainTransferMeta.ChunkByteSize - 1) / TerrainTransferMeta.ChunkByteSize);
            Assert.Greater(expectedChunkTotal, 0);
            Assert.AreEqual(expectedChunkTotal, response.TerrainChunkTotal);

            Assert.IsTrue(response.WorldId.Length == 16 && response.WorldId.All(Uri.IsHexDigit), $"WorldId is not a 16-digit hex: '{response.WorldId}'");
        }

        [Test]
        public void Templateワールドはterrainメタが0でWorldIdはワールドごとに異なる()
        {
            var firstWorld = ProvisionWorld(WorldProvisioner.TemplateMapMode, 42);
            var firstResponse = RequestMapDataLayout(firstWorld);

            // templateは地形を持たないので3項目とも0、WorldIdだけは埋まる
            // Template owns no terrain, so all three values are 0 while WorldId is still filled
            Assert.AreEqual(WorldProvisioner.TemplateMapMode, firstResponse.MapMode);
            Assert.AreEqual(0, firstResponse.TerrainResolution);
            Assert.AreEqual(0, firstResponse.TerrainTileCount);
            Assert.AreEqual(0, firstResponse.TerrainChunkTotal);
            Assert.IsTrue(firstResponse.WorldId.Length == 16 && firstResponse.WorldId.All(Uri.IsHexDigit), $"WorldId is not a 16-digit hex: '{firstResponse.WorldId}'");

            // 別ワールドは別のWorldIdになる（クライアント側のワールド識別に使うため）
            // A different world yields a different WorldId, since clients identify worlds by it
            var secondWorld = ProvisionWorld(WorldProvisioner.TemplateMapMode, 43);
            var secondResponse = RequestMapDataLayout(secondWorld);
            Assert.AreNotEqual(firstResponse.WorldId, secondResponse.WorldId);
        }

        private WorldDataDirectory ProvisionWorld(string mapMode, int seed)
        {
            var worldRoot = Path.Combine(Path.GetTempPath(), "GetMapDataProtocolTest_" + Guid.NewGuid());
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

            var request = new RequestMapDataMessagePack(MapDataMode.Layout);
            var responseBytes = packet.GetPacketResponse(MessagePackSerializer.Serialize(request), new PacketResponseContext(null))[0];
            return MessagePackSerializer.Deserialize<ResponseMapDataMessagePack>(responseBytes);
        }
    }
}
