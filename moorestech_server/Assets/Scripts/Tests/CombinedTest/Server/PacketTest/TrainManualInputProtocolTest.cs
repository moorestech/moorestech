using System.Collections.Generic;
using Core.Master;
using Core.Update;
using Game.Block.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using MessagePack;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class TrainManualInputProtocolTest
    {
        [Test]
        public void SelectCommandAndManualInputPacket_MoveTrainForward()
        {
            const int playerId = 7;

            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(1, 0, 0), BlockDirection.North);
            var railC = TrainTestHelper.PlaceRail(environment, new Vector3Int(2, 0, 0), BlockDirection.North);

            railC.FrontNode.ConnectNode(railB.FrontNode, 40);
            railB.BackNode.ConnectNode(railC.BackNode, 40);
            railB.FrontNode.ConnectNode(railA.FrontNode, 40);
            railA.BackNode.ConnectNode(railB.BackNode, 40);

            var (trainCar, _) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 1, 20, true);
            var initialRailPosition = new RailPosition(new List<IRailNode> { railB.FrontNode, railC.FrontNode }, trainCar.Length, 10);
            var trainUnit = new TrainUnit(
                initialRailPosition,
                new List<TrainCar> { trainCar },
                environment.GetTrainRailPositionManager(),
                environment.GetTrainDiagramManager());
            environment.GetITrainUnitMutationDatastore().RegisterTrain(trainUnit);

            var selectPayload = MessagePackSerializer.Serialize(
                new SendCommandProtocol.SendCommandProtocolMessagePack(
                    $"{SendCommandProtocol.TrainManualSelectCarCommand} {playerId} {trainCar.TrainCarInstanceId.AsPrimitive()}"));
            environment.PacketResponseCreator.GetPacketResponse(selectPayload);

            var inputPayload = MessagePackSerializer.Serialize(
                new TrainManualInputProtocol.TrainManualInputRequestMessagePack(
                    playerId,
                    trainCar.TrainCarInstanceId.AsPrimitive(),
                    (int)TrainManualInputFlags.Forward));

            for (var i = 0; i < 20; i++)
            {
                environment.PacketResponseCreator.GetPacketResponse(inputPayload);
                GameUpdater.Update();
            }

            Assert.AreEqual(MasterHolder.TrainUnitMaster.MasconLevelMaximum, trainUnit.masconLevel);
            Assert.Greater(trainUnit.CurrentSpeed, 0d);
            Assert.Less(trainUnit.RailPosition.GetDistanceToNextNode(), 10);
        }
    }
}
