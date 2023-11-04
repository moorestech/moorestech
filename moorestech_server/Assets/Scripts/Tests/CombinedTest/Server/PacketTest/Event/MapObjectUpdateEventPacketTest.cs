
using System.Collections.Generic;
using System.Linq;
using Game.MapObject.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Test.Module.TestMod;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class MapObjectUpdateEventPacketTest
    {
        private const int PlayerId = 1;

        [Test]
        public void MapObjectDestroyToEventTest()
        {
            var (packetResponse, serviceProvider) = new PacketResponseCreatorDiContainerGenerators().Create(TestModDirectory.ForUnitTestModDirectory);
            var mapObjectDatastore = serviceProvider.GetService<IMapObjectDatastore>();

            var response = packetResponse.GetPacketResponse(EventRequestData());
            //イベントがないことを確認する
            Assert.AreEqual(0, response.Count);


            //MapObjectを一つ破壊する
            var mapObject = mapObjectDatastore.MapObjects[0];
            mapObject.Destroy();


            //map objectが破壊されたことを確かめる
            response = packetResponse.GetPacketResponse(EventRequestData());
            Assert.AreEqual(1, response.Count);
            var data = MessagePackSerializer.Deserialize<MapObjectUpdateEventMessagePack>(response[0].ToArray());
            Assert.AreEqual(MapObjectUpdateEventMessagePack.OnDestroyEventType, data.EventType);
            Assert.AreEqual(mapObject.InstanceId, data.InstanceId);
        }


        private List<byte> EventRequestData()
        {
            return MessagePackSerializer.Serialize(new EventProtocolMessagePack(PlayerId)).ToList();
            ;
        }
    }
}