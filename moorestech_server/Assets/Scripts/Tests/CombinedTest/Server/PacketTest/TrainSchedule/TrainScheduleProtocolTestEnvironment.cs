using System.Collections.Generic;
using Game.Block.Interface;
using Game.Train.Event;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol;
using Server.Protocol.PacketResponse;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    internal sealed class TrainScheduleProtocolTestEnvironment
    {
        internal TrainTestEnvironment Environment { get; }
        internal TrainUnit Train { get; }
        internal ITrainUnitSnapshotNotifyEvent Notifications { get; }

        internal TrainScheduleProtocolTestEnvironment()
        {
            Environment = TrainTestHelper.CreateEnvironment();
            Notifications = Environment.ServiceProvider.GetRequiredService<ITrainUnitSnapshotNotifyEvent>();

            // 到達済みの目的地を持つ列車を登録する
            // Register a train whose initial destination is its approaching node
            var start = TrainTestHelper.PlaceRail(Environment, Vector3Int.zero, BlockDirection.North).FrontNode;
            var end = TrainTestHelper.PlaceRail(Environment, new Vector3Int(0, 0, 100), BlockDirection.North).FrontNode;
            start.ConnectNode(end, 10000);
            var (car, _) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 1, 1, true, TrainTestCarFactory.StableAutoRunTestWeight);
            var position = new RailPosition(new List<IRailNode> { end, start }, TrainLengthConverter.ToRailUnits(1), 0);
            Train = new TrainUnit(position, new List<TrainCar> { car }, Environment.GetTrainRailPositionManager(), Environment.GetTrainDiagramManager());
            Environment.GetITrainUnitMutationDatastore().RegisterTrain(Train);
            Train.trainDiagram.ReplaceEntries(new[] { end });
            Train.TurnOnAutoRun();
            Assert.IsTrue(Train.IsAutoRun);
        }

        internal IBlock PlaceStation(Vector3Int position)
        {
            return TrainTestHelper.PlaceBlock(Environment, ForUnitTestModBlockId.TestTrainStation, position, BlockDirection.North);
        }

        internal TrainScheduleEditProtocol.TrainScheduleEditResponse Send(TrainScheduleEditProtocol.TrainScheduleEditRequest request)
        {
            // 実際のパケット登録とMessagePack往復を通す
            // Exercise actual packet registration and MessagePack round trips
            var payload = MessagePackSerializer.Serialize(request);
            var responses = Environment.PacketResponseCreator.GetPacketResponse(payload, new PacketResponseContext(null));
            Assert.AreEqual(1, responses.Count);
            return MessagePackSerializer.Deserialize<TrainScheduleEditProtocol.TrainScheduleEditResponse>(responses[0]);
        }
    }
}
