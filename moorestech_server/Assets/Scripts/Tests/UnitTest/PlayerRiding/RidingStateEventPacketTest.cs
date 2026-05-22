using System.Linq;
using Game.PlayerConnection;
using Game.PlayerRiding.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Event.EventReceive;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Util;

namespace Tests.UnitTest.PlayerRiding
{
    public class RidingStateEventPacketTest
    {
        [Test]
        public void RidingStateChanged_BroadcastsRideAndDismountEvents()
        {
            // EventProtocol 購読済みプレイヤーに乗車・降車イベントを broadcast する。
            // Broadcasts ride and dismount events to players already registered through EventProtocol.
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            RegisterPlayer(environment, 1);
            PollEvents(environment, 1);
            var id = new TrainCarRidableIdentifier(car.TrainCarInstanceId.AsPrimitive());

            datastore.TryRide(1, id, out _);
            datastore.TryDismount(1);

            var events = PollEvents(environment, 1);
            var ridingEvents = events.Events.Where(e => e.Tag == RidingStateEventPacket.EventTag).ToList();
            Assert.AreEqual(2, ridingEvents.Count);
            var ride = MessagePackSerializer.Deserialize<RidingStateEventMessagePack>(ridingEvents[0].Payload);
            var dismount = MessagePackSerializer.Deserialize<RidingStateEventMessagePack>(ridingEvents[1].Payload);
            Assert.AreEqual(1, ride.PlayerId);
            Assert.AreEqual(RidingStateEventPacket.RideStateType, ride.StateType);
            Assert.AreEqual(0, ride.SeatIndex);
            Assert.IsNotNull(ride.Target);
            Assert.AreEqual(1, dismount.PlayerId);
            Assert.AreEqual(RidingStateEventPacket.DismountStateType, dismount.StateType);
            Assert.AreEqual(-1, dismount.SeatIndex);
            Assert.IsTrue(dismount.IsDismount);
        }

        private static EventProtocol.ResponseEventProtocolMessagePack PollEvents(TrainTestEnvironment environment, int playerId)
        {
            var request = new EventProtocol.EventProtocolMessagePack(playerId);
            var responseBytes = environment.PacketResponseCreator.GetPacketResponse(
                MessagePackSerializer.Serialize(request),
                new PacketResponseContext())[0];
            return MessagePackSerializer.Deserialize<EventProtocol.ResponseEventProtocolMessagePack>(responseBytes);
        }

        private static void RegisterPlayer(TrainTestEnvironment environment, int playerId)
        {
            var connectionChecker = environment.ServiceProvider.GetService<IPlayerConnectionChecker>();
            ((PlayerConnectionRegistry)connectionChecker).Register(playerId);
        }
    }
}
