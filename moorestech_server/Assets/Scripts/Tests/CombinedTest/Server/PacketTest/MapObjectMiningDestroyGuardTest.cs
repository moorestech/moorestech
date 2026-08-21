using System;
using System.Linq;
using Game.Context;
using Game.Map;
using Game.Map.Interface.MapObject;
using Game.PlayerInventory.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Event;
using Server.Event.EventReceive;
using Server.Protocol;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Module.TestMod;
using Server.Protocol.PacketResponse;

namespace Tests.CombinedTest.Server.PacketTest
{
    /// <summary>
    ///     破壊済みmapObjectへの再打撃がdestroyイベントを再ブロードキャストしないことを検証する
    ///     Verifies that re-hitting a destroyed map object never re-broadcasts the destroy event
    /// </summary>
    public class MapObjectMiningDestroyGuardTest
    {
        private const int PlayerId = 0;

        // テストマスタのPickUp型mapObject。素手の1打で破壊される
        // The PickUp-type map object in the test master; one bare-handed hit destroys it
        private static readonly Guid PickUpMapObjectGuid = Guid.Parse("8c0e1339-be75-4690-99cd-58b5385a17cd");

        [Test]
        public void 破壊済みへのプロトコル再送では破壊イベントが再ブロードキャストされない()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var mapObject = GetMapObject();

            // 1打目で破壊し、同じinstanceIdへ2打目を送っても状態は変わらない
            // The first hit destroys it and a second hit on the same instanceId changes nothing
            SendAttack(packet, mapObject.InstanceId);
            SendAttack(packet, mapObject.InstanceId);
            Assert.IsTrue(mapObject.IsDestroyed);

            // 破壊イベントは初回の1件だけで全クライアントへの再送は起きない
            // Only the first destroy event exists; no resend goes out to all clients
            Assert.AreEqual(1, CountDestroyEvents(sink));
        }

        [Test]
        public void 破壊済みへの採掘サービス呼び出しは弾かれる()
        {
            var (_, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var sink = EventTestUtil.RegisterCaptureSink(serviceProvider, PlayerId);
            var miningService = serviceProvider.GetService<MapObjectMiningService>();
            var playerInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            var equippedItem = playerInventory.EquipmentInventory.GetSelectedItem();
            var mapObject = GetMapObject();

            // PickUpなので素手の1打で破壊される
            // Being PickUp, a single bare-handed hit destroys it
            Assert.AreEqual(MiningAttackResult.Success,
                miningService.TryAttack(PlayerId, mapObject, equippedItem, playerInventory.MainOpenableInventory, out _));

            // プロトコル層を経由せず直接叩いてもサービス側のガードが弾く
            // Even called directly without the protocol layer, the service guard rejects it
            Assert.AreEqual(MiningAttackResult.AlreadyDestroyed,
                miningService.TryAttack(PlayerId, mapObject, equippedItem, playerInventory.MainOpenableInventory, out _));

            // 破壊イベントは初回の1件だけ
            // Only the first destroy event exists
            Assert.AreEqual(1, CountDestroyEvents(sink));
        }

        private IMapObject GetMapObject()
        {
            return ServerContext.MapObjectDatastore.MapObjects.First(mapObject => mapObject.MapObjectGuid == PickUpMapObjectGuid);
        }

        private void SendAttack(PacketResponseCreator packet, int instanceId)
        {
            var messagePack = MiningProtocol.MiningProtocolMessagePack.CreateMapObjectRequest(PlayerId, instanceId);
            packet.GetPacketResponse(MessagePackSerializer.Serialize(messagePack), new PacketResponseContext(null));
        }

        private int CountDestroyEvents(CapturedEventSink sink)
        {
            return sink.TakeAll().
                Where(capturedEvent => capturedEvent.Tag == MapObjectUpdateEventPacket.EventTag).
                Select(capturedEvent => MessagePackSerializer.Deserialize<MapObjectUpdateEventMessagePack>(capturedEvent.Payload)).
                Count(eventData => eventData.EventType == MapObjectUpdateEventMessagePack.DestroyEventType);
        }
    }
}
