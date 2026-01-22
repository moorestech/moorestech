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
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using System;
using Mooresmaster.Model.TrainModule;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RequestBlockInventoryTest
    {
        private const int InputSlotNum = 2;
        private const int OutPutSlotNum = 3;
        
        //通常の機械のテスト
        [Test]
        public void MachineInventoryRequest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var itemStackFactory = ServerContext.ItemStackFactory;
            
            
            ServerContext.WorldBlockDatastore.TryAddBlock(ForUnitTestModBlockId.MachineId, new Vector3Int(5, 10), BlockDirection.North, Array.Empty<BlockCreateParam>(), out var machineBlock);
            var machineComponent = machineBlock.GetComponent<VanillaMachineBlockInventoryComponent>();
            machineComponent.SetItem(0, itemStackFactory.Create(new ItemId(1), 2));
            machineComponent.SetItem(2, itemStackFactory.Create(new ItemId(4), 5));
            
            //レスポンスの取得
            var data = MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(packet.GetPacketResponse(RequestBlock(new Vector3Int(5, 10)))[0].ToArray());
            
            Assert.AreEqual(InputSlotNum + OutPutSlotNum, data.Items.Length); // slot num
            
            
            Assert.AreEqual(1, data.Items[0].Id.AsPrimitive()); // item id
            Assert.AreEqual(2, data.Items[0].Count); // item count
            
            Assert.AreEqual(0, data.Items[1].Id.AsPrimitive());
            Assert.AreEqual(0, data.Items[1].Count);
            
            Assert.AreEqual(4, data.Items[2].Id.AsPrimitive());
            Assert.AreEqual(5, data.Items[2].Count);
        }
        
        private List<byte> RequestBlock(Vector3Int pos)
        {
            var identifier = InventoryIdentifierMessagePack.CreateBlockMessage(pos);
            return MessagePackSerializer.Serialize(new InventoryRequestProtocol.RequestInventoryRequestProtocolMessagePack(identifier)).ToList();
        }

        [Test]
        public void TrainInventoryRequest()
        {
            // テスト環境を構築
            // Build test environment
            var environment = TrainTestHelper.CreateEnvironment();
            var railA = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North);
            var railB = TrainTestHelper.PlaceRail(environment, new Vector3Int(100, 0, 0), BlockDirection.North);
            railA.ConnectRailComponent(railB, true, true, 10);
            railB.ConnectRailComponent(railA, true, true, 10);

            // 列車とインベントリを準備
            // Prepare train and its inventory
            var frontNode = railB.FrontNode;
            var backNode = railA.FrontNode;
            var distance = Mathf.Max(1, frontNode.GetDistanceToNode(backNode));
            var railPosition = new RailPosition(new List<IRailNode> { frontNode, backNode }, distance, 0);
            var trainCar = TrainTestCarFactory.CreateTrainCar(0, 1000, 3, distance, true);
            var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { trainCar }, environment.GetTrainUpdateService(), environment.GetTrainRailPositionManager(), environment.GetTrainDiagramManager());

            // 列車をTrainUpdateServiceに登録
            // Register the train to TrainUpdateService
            environment.GetTrainUpdateService().RegisterTrain(trainUnit);

            var itemFactory = ServerContext.ItemStackFactory;
            trainCar.SetItem(0, itemFactory.Create(new ItemId(1), 7));
            trainCar.SetItem(1, itemFactory.Create(new ItemId(2), 3));

            var responseBytes = environment.PacketResponseCreator.GetPacketResponse(RequestTrain(trainCar.CarId))[0];
            var data = MessagePackSerializer.Deserialize<InventoryRequestProtocol.ResponseInventoryRequestProtocolMessagePack>(responseBytes.ToArray());

            Assert.AreEqual(InventoryType.Train, data.InventoryType); // inventory type
            Assert.AreEqual(3, data.Items.Length); // slot count
            Assert.AreEqual(1, data.Items[0].Id.AsPrimitive());
            Assert.AreEqual(7, data.Items[0].Count);
            Assert.AreEqual(2, data.Items[1].Id.AsPrimitive());
            Assert.AreEqual(3, data.Items[1].Count);
        }

        private List<byte> RequestTrain(Guid trainId)
        {
            var identifier = InventoryIdentifierMessagePack.CreateTrainMessage(trainId);
            return MessagePackSerializer.Serialize(new InventoryRequestProtocol.RequestInventoryRequestProtocolMessagePack(identifier)).ToList();
        }
    }
}
