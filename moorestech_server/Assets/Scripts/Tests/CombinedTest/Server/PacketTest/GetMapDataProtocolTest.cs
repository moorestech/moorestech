using Game.MapGeneration.Provisioning;
using MessagePack;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.GetMapDataProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    // ワールドディレクトリを持たない構成でva:mapData Layoutが返すmap.json由来の内容を検証する
    // Verifies the map.json-derived content va:mapData Layout returns for a configuration owning no world directory
    public class GetMapDataProtocolTest
    {
        [Test]
        public void GetMapDataLayoutTest()
        {
            var (packet, _) = new MoorestechServerDIContainerGenerator()
                .Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            // va:mapData Layoutをリクエストしてレスポンスを取得
            // Request va:mapData Layout and obtain the response
            var request = RequestMapDataMessagePack.CreateLayoutRequest();
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

            // world.jsonが無い構成にはseedという概念自体が無いため0を載せる（地形なしの合図はTerrainResolution=0が担う）
            // A config without world.json has no seed at all, so 0 is carried (TerrainResolution=0 remains the terrain-less signal)
            Assert.AreEqual(0, response.WorldSeed);
        }
    }
}
