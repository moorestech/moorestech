using System.Collections.Generic;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.Chest;
using Game.Block.Interface;
using Game.Block.Interface.Extension;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Boot;
using Server.Protocol.PacketResponse.Util.InventoryMoveUtil;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.InventoryItemMoveProtocol;
using System;
using ItemTrainCarContainer = global::Game.Train.Unit.Containers.ItemTrainCarContainer;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class InventoryItemMoveProtocolTest
    {
        private const int PlayerId = 0;

        [Test]
        public void MainInventoryMoveTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var mainInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).MainOpenableInventory;
            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).GrabInventory;
            var itemStackFactory = ServerContext.ItemStackFactory;

            //インベントリの設定
            mainInventory.SetItem(0, new ItemId(1), 10);

            //インベントリを持っているアイテムに移す
            packet.GetPacketResponse(GetPacket(7,
                InventoryIdentifierMessagePack.CreateMainMessage(PlayerId), 0,
                InventoryIdentifierMessagePack.CreateGrabMessage(PlayerId), 0), new PacketResponseContext(null));

            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 3), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 7), grabInventory.GetItem(0));


            //持っているアイテムをインベントリに移す
            packet.GetPacketResponse(GetPacket(5,
                InventoryIdentifierMessagePack.CreateGrabMessage(PlayerId), 0,
                InventoryIdentifierMessagePack.CreateMainMessage(PlayerId), 0), new PacketResponseContext(null));


            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 8), mainInventory.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 2), grabInventory.GetItem(0));
        }


        [Test]
        public void MainInventoryMoveUsesIdentifierPlayerIdTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));
            var playerInventoryDataStore = serviceProvider.GetService<IPlayerInventoryDataStore>();
            var itemStackFactory = ServerContext.ItemStackFactory;
            const int otherPlayerId = 1;

            // 識別子に入ったPlayerIdのインベントリだけを操作対象にする
            // Only the inventory for the player id embedded in the identifier is targeted.
            var player0Main = playerInventoryDataStore.GetInventoryData(PlayerId).MainOpenableInventory;
            var player1Main = playerInventoryDataStore.GetInventoryData(otherPlayerId).MainOpenableInventory;
            var player1Grab = playerInventoryDataStore.GetInventoryData(otherPlayerId).GrabInventory;
            player0Main.SetItem(0, new ItemId(2), 10);
            player1Main.SetItem(0, new ItemId(1), 10);

            // PlayerId=1のMainからGrabへ移動する
            // Move from player 1 main inventory to player 1 grab inventory.
            packet.GetPacketResponse(GetPacket(7,
                InventoryIdentifierMessagePack.CreateMainMessage(otherPlayerId), 0,
                InventoryIdentifierMessagePack.CreateGrabMessage(otherPlayerId), 0), new PacketResponseContext(null));

            Assert.AreEqual(itemStackFactory.Create(new ItemId(2), 10), player0Main.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 3), player1Main.GetItem(0));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 7), player1Grab.GetItem(0));
        }


        [Test]
        public void BlockInventoryTest()
        {
            var (packet, serviceProvider) = new MoorestechServerDIContainerGenerator().Create(new MoorestechServerDIContainerOptions(TestModDirectory.ForUnitTestModDirectory));

            var grabInventory = serviceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(0).GrabInventory;
            var worldDataStore = ServerContext.WorldBlockDatastore;
            var itemStackFactory = ServerContext.ItemStackFactory;

            var chestPosition = new Vector3Int(5, 10);

            worldDataStore.TryAddBlock(ForUnitTestModBlockId.ChestId, chestPosition, BlockDirection.North, Array.Empty<BlockCreateParam>(), out var chest);
            var chestComponent = chest.GetComponent<VanillaChestComponent>();

            //ブロックインベントリの設定
            chestComponent.SetItem(1, new ItemId(1), 10);

            //インベントリを持っているアイテムに移す
            packet.GetPacketResponse(GetPacket(7,
                InventoryIdentifierMessagePack.CreateBlockMessage(new Vector3Int(5, 10)), 1,
                InventoryIdentifierMessagePack.CreateGrabMessage(PlayerId), 0), new PacketResponseContext(null));

            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 3), chestComponent.GetItem(1));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 7), grabInventory.GetItem(0));


            //持っているアイテムをインベントリに移す
            packet.GetPacketResponse(GetPacket(5,
                InventoryIdentifierMessagePack.CreateGrabMessage(PlayerId), 0,
                InventoryIdentifierMessagePack.CreateBlockMessage(new Vector3Int(5, 10)), 1), new PacketResponseContext(null));

            //移っているかチェック
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 8), chestComponent.GetItem(1));
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 2), grabInventory.GetItem(0));
        }

        [Test]
        public void TrainInventoryMoveTest()
        {
            // 列車インベントリを持つテスト環境を構築
            // Build a test environment with a train inventory
            var environment = TrainTestHelper.CreateEnvironment();
            var (trainCar, itemContainer) = CreateRegisteredTrainWithItemContainer(environment);
            var grabInventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).GrabInventory;
            var itemStackFactory = ServerContext.ItemStackFactory;
            itemContainer.SetItem(1, itemStackFactory.Create(new ItemId(1), 10));

            // 列車から手持ちへ移動
            // Move items from train to grab inventory
            environment.PacketResponseCreator.GetPacketResponse(GetPacket(7,
                InventoryIdentifierMessagePack.CreateTrainMessage(trainCar.TrainCarInstanceId.AsPrimitive()), 1,
                InventoryIdentifierMessagePack.CreateGrabMessage(PlayerId), 0), new PacketResponseContext(null));

            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 3), itemContainer.InventoryItems[1]);
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 7), grabInventory.GetItem(0));

            // 手持ちから列車へ戻す
            // Move items from grab inventory back to train
            environment.PacketResponseCreator.GetPacketResponse(GetPacket(5,
                InventoryIdentifierMessagePack.CreateGrabMessage(PlayerId), 0,
                InventoryIdentifierMessagePack.CreateTrainMessage(trainCar.TrainCarInstanceId.AsPrimitive()), 1), new PacketResponseContext(null));

            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 8), itemContainer.InventoryItems[1]);
            Assert.AreEqual(itemStackFactory.Create(new ItemId(1), 2), grabInventory.GetItem(0));

            #region Internal

            (TrainCar trainCar, ItemTrainCarContainer itemContainer) CreateRegisteredTrainWithItemContainer(TrainTestEnvironment testEnvironment)
            {
                var railA = TrainTestHelper.PlaceRail(testEnvironment, new Vector3Int(0, 0, 0), BlockDirection.North);
                var railB = TrainTestHelper.PlaceRail(testEnvironment, new Vector3Int(100, 0, 0), BlockDirection.North);

                // レールを双方向に接続して列車位置を作成する
                // Connect rails bidirectionally and create the train position
                railA.FrontNode.ConnectNode(railB.FrontNode, 10);
                railB.BackNode.ConnectNode(railA.BackNode, 10);
                railB.FrontNode.ConnectNode(railA.FrontNode, 10);
                railA.BackNode.ConnectNode(railB.BackNode, 10);

                var frontNode = railB.FrontNode;
                var backNode = railA.FrontNode;
                var distance = Mathf.Max(1, frontNode.GetDistanceToNode(backNode));
                var railPosition = new RailPosition(new List<IRailNode> { frontNode, backNode }, distance, 0);
                var result = TrainTestCarFactory.CreateTrainCarWithItemContainer(0, 400000, 3, distance, true);
                var trainUnit = new TrainUnit(railPosition, new List<TrainCar> { result.trainCar }, testEnvironment.GetTrainRailPositionManager(), testEnvironment.GetTrainDiagramManager());
                testEnvironment.GetITrainUnitMutationDatastore().RegisterTrain(trainUnit);
                return result;
            }

            #endregion
        }


        private byte[] GetPacket(int count, InventoryIdentifierMessagePack from, int fromSlot, InventoryIdentifierMessagePack to, int toSlot,
            ItemMoveType itemMoveType = ItemMoveType.SwapSlot)
        {
            return MessagePackSerializer.Serialize(
                new InventoryItemMoveProtocolMessagePack(count, itemMoveType, from, fromSlot, to, toSlot));
        }
    }
}
