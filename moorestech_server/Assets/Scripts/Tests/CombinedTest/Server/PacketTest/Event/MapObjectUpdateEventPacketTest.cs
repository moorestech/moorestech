using Game.Context;
using Game.Map.Interface.MapObject;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event.EventReceive;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest.Event
{
    public class MapObjectUpdateEventPacketTest
    {
        private const int PlayerId = 1;
        
        [Test]
        public void MapObjectDestroyToEventTest()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            
            //イベントがないことを確認する
            Assert.AreEqual(0, sink.TakeAll().Count);
            
            //MapObjectを一つ破壊する
            var mapObject = ServerContext.MapObjectDatastore.MapObjects[0];
            mapObject.Destroy();
            
            //map objectが破壊されたことを確かめる
            //Verify the map object destruction event was delivered
            var events = sink.TakeAll();
            Assert.AreEqual(1, events.Count);
            
            var data = MessagePackSerializer.Deserialize<MapObjectUpdateEventMessagePack>(events[0].Payload);
            Assert.AreEqual(MapObjectUpdateEventMessagePack.DestroyEventType, data.EventType);
            Assert.AreEqual(mapObject.InstanceId, data.InstanceId);
        }
    }
}