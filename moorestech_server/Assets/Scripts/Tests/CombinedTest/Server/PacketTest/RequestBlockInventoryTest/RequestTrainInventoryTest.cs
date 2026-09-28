using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Machine.Inventory;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using MessagePack;
using Mooresmaster.Model.BlocksModule;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using System;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RequestTrainInventoryTest : RequestBlockInventoryTestBase
    {

        [Test]
        public void TrainInventoryRequest()
        {
            // テスト環境を構築
            // Build test environment
            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(100, 0, 0), BlockDirection.North);

            railA.FrontNode.ConnectNode(railB.FrontNode, 10);
            railB.BackNode.ConnectNode(railA.BackNode, 10);
            railB.FrontNode.ConnectNode(railA.FrontNode, 10);
            railA.BackNode.ConnectNode(railB.BackNode, 10);

            // 列車とインベントリを準備
            // Prepare train and its inventory
            var frontNode = railB.FrontNode;
            var backNode = railA.FrontNode;
            var distance = Mathf.Max(1, frontNode.GetDistanceToNode(backNode));
            var railPosition = new RailPosition(new List<IRailNode> { frontNode, backNode }, distance, 0);
            var (trainCar, itemContainer) = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 3, distance, true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());

            // 列車をTrainUpdateServiceに登録
            // Register the train to TrainUpdateService
            environment.GetITrainUnitMutationDatastore().RegisterTrain(trainUnit);

            // インベントリにアイテムをセット
            // Set items in the inventory
            var itemFactory = ServerContext.ItemStackFactory;
            itemContainer.SetItem(0, itemFactory.Create(new ItemId(1), 7));
            itemContainer.SetItem(1, itemFactory.Create(new ItemId(2), 3));

            var responseBytes = environment.PacketResponseCreator.GetPacketResponse(RequestTrain(trainCar.TrainCarInstanceId), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1))[0];
            var data = MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(responseBytes);

            Assert.AreEqual(InventoryType.Train, data.InventoryType); // inventory type
            Assert.AreEqual(InventoryRequestResult.Success, data.Result); // request result
            Assert.AreEqual(3, data.Items.Length); // slot count
            Assert.AreEqual(1, data.Items[0].Id.AsPrimitive());
            Assert.AreEqual(7, data.Items[0].Count);
            Assert.AreEqual(2, data.Items[1].Id.AsPrimitive());
            Assert.AreEqual(3, data.Items[1].Count);
        }

        [Test]
        public void TrainInventoryRequestWithoutContainer()
        {
            // コンテナを持たない列車を登録
            // Register a train without an item container
            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(100, 0, 0), BlockDirection.North);

            railA.FrontNode.ConnectNode(railB.FrontNode, 10);
            railB.BackNode.ConnectNode(railA.BackNode, 10);
            railB.FrontNode.ConnectNode(railA.FrontNode, 10);
            railA.BackNode.ConnectNode(railB.BackNode, 10);

            var frontNode = railB.FrontNode;
            var backNode = railA.FrontNode;
            var distance = Mathf.Max(1, frontNode.GetDistanceToNode(backNode));
            var railPosition = new RailPosition(new List<IRailNode> { frontNode, backNode }, distance, 0);
            var trainCar = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 3, distance, true).trainCar;
            trainCar.SetContainer(null);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());
            environment.GetITrainUnitMutationDatastore().RegisterTrain(trainUnit);

            // コンテナなしが空インベントリとは別の結果として返ることを確認
            // Verify that missing container is returned separately from an empty inventory
            var responseBytes = environment.PacketResponseCreator.GetPacketResponse(RequestTrain(trainCar.TrainCarInstanceId), Tests.Util.PlayerIdentity.BoundPacketContext.Bind(1))[0];
            var data = MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(responseBytes);

            Assert.AreEqual(InventoryType.Train, data.InventoryType);
            Assert.AreEqual(InventoryRequestResult.ContainerNotFound, data.Result);
            Assert.AreEqual(0, data.Items.Length);
        }
    }
}
