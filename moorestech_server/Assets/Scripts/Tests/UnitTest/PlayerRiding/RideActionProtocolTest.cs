using Game.PlayerConnection;
using Game.PlayerRiding.Interface;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Util;

namespace Tests.UnitTest.PlayerRiding
{
    public class RideActionProtocolTest
    {
        [Test]
        public void RideAction_Ride_ReturnsSuccessAndSeatIndex()
        {
            // 乗車要求は成功結果と割り当て seatIndex を返す。
            // A ride request returns success and the assigned seat index.
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            RegisterPlayer(environment, 1);
            var target = RidableIdentifierMessagePack.CreateTrainCarMessage(car.TrainCarInstanceId.AsPrimitive());
            var request = new RideActionProtocol.RequestRideActionMessagePack(1, RideActionType.Ride, target);

            var response = SendRideAction(environment, request, CreateBoundContext(1));

            Assert.AreEqual(RideActionResult.Success, response.Result);
            Assert.AreEqual(0, response.SeatIndex);
        }

        [Test]
        public void RideAction_Dismount_WhenNotRiding_ReturnsNotRiding()
        {
            // 未乗車の降車要求は NotRiding を返す。
            // A dismount request while not riding returns NotRiding.
            var environment = TrainTestHelper.CreateEnvironment();
            RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            RegisterPlayer(environment, 1);
            var request = new RideActionProtocol.RequestRideActionMessagePack(1, RideActionType.Dismount, null);

            var response = SendRideAction(environment, request, CreateBoundContext(1));

            Assert.AreEqual(RideActionResult.NotRiding, response.Result);
            Assert.AreEqual(-1, response.SeatIndex);
        }

        private static RideActionProtocol.ResponseRideActionMessagePack SendRideAction(
            TrainTestEnvironment environment,
            RideActionProtocol.RequestRideActionMessagePack request,
            PacketResponseContext context)
        {
            var responseBytes = environment.PacketResponseCreator.GetPacketResponse(
                MessagePackSerializer.Serialize(request),
                context)[0];
            return MessagePackSerializer.Deserialize<RideActionProtocol.ResponseRideActionMessagePack>(responseBytes);
        }

        private static PacketResponseContext CreateBoundContext(int playerId)
        {
            var context = new PacketResponseContext();
            context.TryBindPlayerId(playerId);
            return context;
        }

        private static void RegisterPlayer(TrainTestEnvironment environment, int playerId)
        {
            var connectionChecker = environment.ServiceProvider.GetService<IPlayerConnectionChecker>();
            ((PlayerConnectionRegistry)connectionChecker).Register(playerId);
        }
    }
}
