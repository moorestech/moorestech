using System.Linq;
using Game.PlayerConnection;
using Game.PlayerRiding.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Util;

namespace Tests.UnitTest.PlayerRiding
{
    public class RidingStateEventPacketTest
    {
        [Test]
        public void RidingStateChanged_BroadcastsRideAndDismountEvents()
        {
            // sink登録プレイヤーへ乗車・降車をbroadcast
            // Broadcasts ride and dismount to registered sinks.
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            RegisterPlayer(environment, 1);
            var sink = EventTestUtil.RegisterCaptureSink(environment.ServiceProvider, 1);
            sink.TakeAll();
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());

            datastore.TryRide(1, id, out _);
            datastore.TryDismount(1);

            var events = sink.TakeAll();
            var ridingEvents = events.Where(e => e.Tag == RidingStateEventPacket.EventTag).ToList();
            Assert.AreEqual(2, ridingEvents.Count);
            var ride = MessagePackSerializer.Deserialize<RidingStateEventMessagePack>(ridingEvents[0].Payload);
            var dismount = MessagePackSerializer.Deserialize<RidingStateEventMessagePack>(ridingEvents[1].Payload);
            Assert.AreEqual(1, ride.PlayerId);
            Assert.AreEqual(RidingStateEventType.Ride, ride.StateType);
            Assert.AreEqual(0, ride.SeatIndex);
            Assert.IsNotNull(ride.Target);
            Assert.AreEqual(1, dismount.PlayerId);
            Assert.AreEqual(RidingStateEventType.Dismount, dismount.StateType);
            Assert.AreEqual(-1, dismount.SeatIndex);
        }

        private static void RegisterPlayer(TrainTestEnvironment environment, int playerId)
        {
            var connectionChecker = environment.ServiceProvider.GetService<IPlayerConnectionChecker>();
            ((PlayerConnectionRegistry)connectionChecker).Register(playerId);
        }
    }
}
