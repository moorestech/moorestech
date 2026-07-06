using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.Train.RailGraph;
using Game.Train.RailPositions;
using Game.Train.Unit;
using Game.Train.Unit.Containers;
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol.PacketResponse;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.PlaceTrainCarOnRailProtocol;
using Server.Protocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class RemoveTrainCarProtocolTest
    {
        private const int PlayerId = 1;

        [Test]
        public void RemoveTrainCar_RefundsCarBlockAndContents_ToPlayerInventory()
        {
            // テスト環境を構築し、列車を1両配置する
            // Build the environment and place a single train car.
            var (environment, trainCar) = SetupAndPlaceTrain();

            // 配置直後は建設素材が消費されている
            // The construction materials are consumed right after placement.
            var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            Assert.AreEqual(0, TotalCount(inventory.MainOpenableInventory, ForUnitTestItemId.ItemId3), "配置で素材が消費されるべき / Materials should be consumed on placement");
            Assert.AreEqual(0, TotalCount(inventory.MainOpenableInventory, ForUnitTestItemId.ItemId4), "配置で素材が消費されるべき / Materials should be consumed on placement");

            // 車両コンテナにアイテムを積み込む(アイテムコンテナを持つ場合のみ)
            // Load an item into the car container (only when the car has an item container).
            var loadedItemId = ForUnitTestItemId.ItemId1;
            var hasItemContainer = trainCar.Container is ItemTrainCarContainer;
            if (hasItemContainer)
            {
                var itemContainer = (ItemTrainCarContainer)trainCar.Container;
                itemContainer.SetItem(0, ServerContext.ItemStackFactory.Create(loadedItemId, 3));
            }

            // 削除プロトコルを実行する
            // Execute the removal protocol.
            var packet = MessagePackSerializer.Serialize(
                new RemoveTrainCarProtocol.RemoveTrainCarRequestMessagePack(trainCar.TrainCarInstanceId.AsPrimitive(), PlayerId));
            environment.PacketResponseCreator.GetPacketResponse(packet, new PacketResponseContext());

            // 列車が削除され、建設コスト全額(Test3x3 + Test4x2)がインベントリへ返却されている
            // The train is removed and the full construction cost (Test3x3 + Test4x2) is refunded.
            Assert.AreEqual(0, environment.GetITrainLookupDatastore().GetRegisteredTrains().Count, "削除後は列車が存在しないべき / No train should remain after removal");
            Assert.AreEqual(3, TotalCount(inventory.MainOpenableInventory, ForUnitTestItemId.ItemId3), "Test3が全額返却されるべき / Test3 should be fully refunded");
            Assert.AreEqual(2, TotalCount(inventory.MainOpenableInventory, ForUnitTestItemId.ItemId4), "Test4が全額返却されるべき / Test4 should be fully refunded");

            // 積載アイテムも返却されている
            // Loaded items are refunded as well.
            if (hasItemContainer)
            {
                Assert.AreEqual(3, TotalCount(inventory.MainOpenableInventory, loadedItemId), "車両内のアイテムが返却されるべき / Items inside the car should be refunded");
            }
        }

        [Test]
        public void RemoveTrainCar_PlayerInventoryFull_AbortsRemoval()
        {
            // 列車を1両配置する
            // Place a single train car.
            var (environment, trainCar) = SetupAndPlaceTrain();

            // プレイヤーのメインインベントリを満杯にする
            // Fill the player's main inventory completely.
            var mainInventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            var fillItemMaxStack = MasterHolder.ItemMaster.GetItemMaster(ForUnitTestItemId.ItemId2).MaxStack;
            for (var i = 0; i < mainInventory.GetSlotSize(); i++)
                mainInventory.SetItem(i, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId2, fillItemMaxStack));

            // 削除プロトコルを実行する
            // Execute the removal protocol.
            var packet = MessagePackSerializer.Serialize(
                new RemoveTrainCarProtocol.RemoveTrainCarRequestMessagePack(trainCar.TrainCarInstanceId.AsPrimitive(), PlayerId));
            environment.PacketResponseCreator.GetPacketResponse(packet, new PacketResponseContext());

            // 満杯時は削除が中止され、列車はそのまま残る
            // On a full inventory the removal is aborted and the train remains.
            Assert.AreEqual(1, environment.GetITrainLookupDatastore().GetRegisteredTrains().Count, "満杯時は削除が中止され列車が残るべき / Train should remain when inventory is full");
        }

        private static int TotalCount(IOpenableInventory inventory, ItemId itemId)
        {
            var total = 0;
            for (var i = 0; i < inventory.GetSlotSize(); i++)
            {
                var item = inventory.GetItem(i);
                if (item.Id == itemId) total += item.Count;
            }
            return total;
        }

        private (TrainTestEnvironment Environment, TrainCar TrainCar) SetupAndPlaceTrain()
        {
            // レールとインベントリを準備する
            // Prepare rails and inventory.
            var environment = TrainTestHelper.CreateEnvironment();

            var rail1Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North, out _);
            var rail2Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(1000, 0, 0), BlockDirection.North, out _);
            rail1Component.FrontNode.ConnectNode(rail2Component.FrontNode);
            rail2Component.BackNode.ConnectNode(rail1Component.BackNode);

            // 建設素材(Test3x3 + Test4x2)をインベントリへ投入する
            // Put the construction materials (Test3x3 + Test4x2) into the inventory.
            var inventory = environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId);
            inventory.MainOpenableInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId3, 3));
            inventory.MainOpenableInventory.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId4, 2));

            // レール位置スナップショットを生成する
            // Create the rail position snapshot.
            MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(ForUnitTestItemId.TrainCarItem, out var trainCarMasterElement);
            var trainLength = TrainLengthConverter.ToRailUnits(trainCarMasterElement.Length);
            var railNodes = new List<IRailNode> { rail1Component.BackNode, rail2Component.BackNode };
            var railPosition = new RailPosition(railNodes, trainLength, 0);
            var railPositionSnapshot = new RailPositionSnapshotMessagePack(railPosition.CreateSaveSnapshot());

            // 配置プロトコルで列車を生成する
            // Create the train through the placement protocol.
            var placePacket = MessagePackSerializer.Serialize(new PlaceTrainOnRailRequestMessagePack(railPositionSnapshot, trainCarMasterElement.TrainCarGuid, PlayerId));
            environment.PacketResponseCreator.GetPacketResponse(placePacket, new PacketResponseContext());

            var trainCar = environment.GetITrainLookupDatastore().GetRegisteredTrains().Last().Cars[0];
            return (environment, trainCar);
        }
    }
}
