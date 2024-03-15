using System.Collections.Generic;
using System.Linq;
using Game.MapObject.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class MapObjectDestructionInformationTest
    {
        [Test]
        public void GetMapObjectTest()
        {
            var (packet, serviceProvider) =
                new MoorestechServerDiContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory);
            var mapObjectDatastore = serviceProvider.GetService<IMapObjectDatastore>();


            //一個だけマップオブジェクトを破壊
            mapObjectDatastore.Get(mapObjectDatastore.MapObjects[0].InstanceId).Destroy();


            var responseArray = packet.GetPacketResponse(MapObjectDestructionInformationProtocol())[0];
            var response = MessagePackSerializer.Deserialize<ResponseMapObjectInfosMessagePack>(responseArray.ToArray());

            foreach (var mapObject in mapObjectDatastore.MapObjects)
            {
                var responseObject =
                    response.MapObjects.Find(m => m.InstanceId == mapObject.InstanceId);
                Assert.AreEqual(mapObject.IsDestroyed, responseObject.IsDestroyed);
            }
        }

        // Packet
        private List<byte> MapObjectDestructionInformationProtocol()
        {
            return MessagePackSerializer.Serialize(new RequestMapObjectInfosMessagePack()).ToList();
        }
    }
}