using System.Collections.Generic;
using System.Linq;
using Game.Block.Interface;
using Game.Fluid;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using MessagePack;
using NUnit.Framework;
using Server.Event.EventReceive.UnifiedInventoryEvent;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.CombinedTest.Server.PacketTest.Event;
using Tests.Util;
using UnityEngine;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class TrainContainerRefreshEventTest : RequestBlockInventoryTestBase
    {
        [TestCase("Item", "None")]
        [TestCase("None", "Item")]
        [TestCase("Item", "Fluid")]
        [TestCase("Fluid", "Item")]
        [TestCase("Fluid", "None")]
        [TestCase("None", "Fluid")]
        public void ContainerReplacementRefreshesSubscribersAndReturnsFinalContents(string before, string after)
        {
            var env = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(env, Vector3Int.zero, BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(env, new Vector3Int(100, 0, 0), BlockDirection.North);
            railA.FrontNode.ConnectNode(railB.FrontNode, 10);
            railB.BackNode.ConnectNode(railA.BackNode, 10);
            railB.FrontNode.ConnectNode(railA.FrontNode, 10);
            railA.BackNode.ConnectNode(railB.BackNode, 10);
            var length = Mathf.Max(1, railB.FrontNode.GetDistanceToNode(railA.FrontNode));
            var position = new RailPosition(new List<IRailNode> { railB.FrontNode, railA.FrontNode }, length, 0);
            var car = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 3, length, true).trainCar;
            car.SetContainer(Create(before));
            var train = new TrainUnit(position, new List<TrainCar> { car }, env.GetTrainRailPositionManager(), env.GetTrainDiagramManager());
            env.GetITrainUnitMutationDatastore().RegisterTrain(train);

            // 在庫を開いた人と開いていない人の通知を捕捉する
            // Capture events for a viewer and a non-viewer
            var viewer = EventTestUtil.RegisterCaptureSink(env.ServiceProvider, 1);
            var other = EventTestUtil.RegisterCaptureSink(env.ServiceProvider, 2);
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(car.TrainCarInstanceId.AsPrimitive());
            var subscribe = new SubscribeInventoryProtocol.SubscribeInventoryRequestMessagePack(identifier, true);
            env.PacketResponseCreator.GetPacketResponse(MessagePackSerializer.Serialize(subscribe), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1));
            viewer.TakeAll();
            other.TakeAll();
            car.SetContainer(Create(after));
            var refresh = viewer.TakeAll().Where(e => e.Tag == UnifiedInventoryEventPacket.EventTag)
                .Select(e => MessagePackSerializer.Deserialize<UnifiedInventoryEventMessagePack>(e.Payload)).ToList();
            Assert.AreEqual(1, refresh.Count);
            Assert.AreEqual(InventoryEventType.Refresh, refresh[0].EventType);
            Assert.AreEqual(identifier.TrainCarInstanceId, refresh[0].Identifier.TrainCarInstanceId);
            Assert.IsFalse(other.TakeAll().Any(e => e.Tag == UnifiedInventoryEventPacket.EventTag));

            // 再取得では交換後の容器の形を返す
            // The refetch must return the final container shape
            var response = env.PacketResponseCreator.GetPacketResponse(RequestTrain(car.TrainCarInstanceId), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1))[0];
            var data = MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(response);
            Assert.AreEqual(after == "None" ? InventoryRequestResult.ContainerNotFound : InventoryRequestResult.Success, data.Result);
            Assert.AreEqual(after == "Item" ? 3 : 0, data.Items.Length);
            Assert.AreEqual(after == "Fluid" ? 1 : 0, data.Fluids.Length);

            #region Internal

            ITrainCarContainer Create(string kind)
            {
                return kind switch
                {
                    "Item" => ItemTrainCarContainer.CreateWithEmptySlots(3),
                    "Fluid" => new FluidTrainCarContainer(new FluidContainer(1000)),
                    _ => null,
                };
            }

            #endregion
        }
    }
}
