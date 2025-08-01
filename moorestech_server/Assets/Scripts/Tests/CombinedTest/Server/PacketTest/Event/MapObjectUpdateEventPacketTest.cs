using Game.Context;
using Game.Map.Interface.MapObject;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using static Server.Protocol.PacketResponse.EventProtocol;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class MapObjectUpdateEventPacketTest
    {
        private const int PlayerId = 1;
        
        [Test]
        public void MapObjectDestroyToEventTest()
        {
            var (packetResponse, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(TestModDirectory.ForUnitTestModDirectory, true);
            
            var response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            var eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            //イベントがないことを確認する
            Assert.AreEqual(0, eventMessagePack.Events.Count);
            
            //MapObjectを一つ破壊する
            var mapObject = ServerContext.MapObjectDatastore.MapObjects[0];
            mapObject.Destroy();
            
            //map objectが破壊されたことを確かめる
            response = packetResponse.GetPacketResponse(EventTestUtil.EventRequestData(PlayerId));
            eventMessagePack = MessagePackSerializer.Deserialize<ResponseEventProtocolMessagePack>(response[0].ToArray());
            Assert.AreEqual(1, eventMessagePack.Events.Count);
            
            var data = MessagePackSerializer.Deserialize<MapObjectUpdateEventMessagePack>(eventMessagePack.Events[0].Payload);
            Assert.AreEqual(MapObjectUpdateEventMessagePack.DestroyEventType, data.EventType);
            Assert.AreEqual(mapObject.InstanceId, data.InstanceId);
        }
    }
}