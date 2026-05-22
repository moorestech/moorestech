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
            var request = new RideActionProtocol.RequestRideActionMessagePack(1, (byte)RideActionType.Ride, target);

            var response = SendRideAction(environment, request, CreateBoundContext(1));

            Assert.AreEqual((byte)RideActionResult.Success, response.Result);
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
            var request = new RideActionProtocol.RequestRideActionMessagePack(1, (byte)RideActionType.Dismount, null);

            var response = SendRideAction(environment, request, CreateBoundContext(1));

            Assert.AreEqual((byte)RideActionResult.NotRiding, response.Result);
            Assert.AreEqual(-1, response.SeatIndex);
        }

        [Test]
        public void RideAction_Ride_WithInvalidTarget_ReturnsRidableNotFound()
        {
            // 不正な外部 Target は例外にせず RidableNotFound として返す。
            // Invalid external Target data returns RidableNotFound instead of throwing.
            var environment = TrainTestHelper.CreateEnvironment();
            RegisterPlayer(environment, 1);
            var target = new RidableIdentifierMessagePack
            {
                RidableType = RidableType.TrainCar.AsPrimitive(),
                TrainCarInstanceId = "invalid",
            };
            var request = new RideActionProtocol.RequestRideActionMessagePack(1, (byte)RideActionType.Ride, target);

            var response = SendRideAction(environment, request, CreateBoundContext(1));

            Assert.AreEqual((byte)RideActionResult.RidableNotFound, response.Result);
            Assert.AreEqual(-1, response.SeatIndex);
        }

        [Test]
        public void RideAction_Ride_WithDifferentContextPlayer_DoesNotMutateState()
        {
            // 接続に紐付いた playerId と異なる要求は状態を変更しない。
            // Requests for a playerId different from the bound connection do not mutate state.
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            RegisterPlayer(environment, 1);
            RegisterPlayer(environment, 2);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            var target = RidableIdentifierMessagePack.CreateTrainCarMessage(car.TrainCarInstanceId.AsPrimitive());
            var request = new RideActionProtocol.RequestRideActionMessagePack(2, (byte)RideActionType.Ride, target);

            var response = SendRideAction(environment, request, CreateBoundContext(1));

            Assert.AreEqual((byte)RideActionResult.InvalidPlayer, response.Result);
            Assert.AreEqual(-1, response.SeatIndex);
            Assert.IsFalse(datastore.TryGetRidingState(2, out _));
        }

        [Test]
        public void RideAction_Ride_WithoutHandshakeContext_DoesNotMutateState()
        {
            // handshake 前の要求は接続所有者が不明なので拒否する。
            // Requests before handshake are rejected because the connection owner is unknown.
            var environment = TrainTestHelper.CreateEnvironment();
            var car = RidingTestHelper.RegisterSeatedCarOnNewTrain(environment, 0);
            RegisterPlayer(environment, 1);
            var datastore = environment.ServiceProvider.GetService<IPlayerRidingDatastore>();
            var target = RidableIdentifierMessagePack.CreateTrainCarMessage(car.TrainCarInstanceId.AsPrimitive());
            var request = new RideActionProtocol.RequestRideActionMessagePack(1, (byte)RideActionType.Ride, target);

            var response = SendRideAction(environment, request, new PacketResponseContext());

            Assert.AreEqual((byte)RideActionResult.InvalidPlayer, response.Result);
            Assert.AreEqual(-1, response.SeatIndex);
            Assert.IsFalse(datastore.TryGetRidingState(1, out _));
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
            context.BindPlayerId(playerId);
            return context;
        }

        private static void RegisterPlayer(TrainTestEnvironment environment, int playerId)
        {
            var connectionChecker = environment.ServiceProvider.GetService<IPlayerConnectionChecker>();
            ((PlayerConnectionRegistry)connectionChecker).Register(playerId);
        }
    }
}
