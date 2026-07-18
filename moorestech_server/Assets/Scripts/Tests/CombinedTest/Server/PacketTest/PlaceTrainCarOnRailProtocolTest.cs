using System;
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
using MessagePack;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Server.Protocol;
using Server.Util.MessagePack;
using Tests.Module.TestMod;
using Tests.Util;
using UnityEngine;
using static Server.Protocol.PacketResponse.PlaceTrainCarOnRailProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class PlaceTrainCarOnRailProtocolTest
    {
        private const int PlayerId = 1;

        [Test]
        public void PlaceTrainOnRail_ValidRailAndItem_CreatesTrainUnit()
        {
            // テスト環境を構築し、素材をインベントリへ投入する
            // Build the environment and put construction materials into the inventory
            var (environment, railPosition, trainCarGuid) = SetupEnvironment();
            var mainInventory = GetMainInventory(environment);
            mainInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId3, 3));
            mainInventory.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId4, 2));

            Assert.AreEqual(0, environment.GetITrainLookupDatastore().GetRegisteredTrains().Count, "初期状態では列車が存在しないべき / No trains should exist initially");

            // プロトコルで列車を配置する
            // Place a train through the protocol
            var response = ExecutePlace(environment, railPosition, trainCarGuid);

            // 列車生成と成功応答を検証する
            // Validate train creation and a success response
            Assert.IsTrue(response.Success, "設置は成功するべき / Placement should succeed");
            Assert.AreEqual(1, environment.GetITrainLookupDatastore().GetRegisteredTrains().Count, "列車が1つ生成されるべき / One train should be created");
            var createdTrain = environment.GetITrainLookupDatastore().GetRegisteredTrains().Last();
            Assert.Greater(createdTrain.Cars.Count, 0, "列車は1両以上の車両を持つべき / Train should have at least one car");
        }

        [Test]
        public void 車両設置でrequiredItemsがインベントリ横断で消費される()
        {
            // 素材を複数スロットへ分割投入する(Test3 x3 + Test4 x2)
            // Split the materials across multiple slots (Test3 x3 + Test4 x2)
            var (environment, railPosition, trainCarGuid) = SetupEnvironment();
            var mainInventory = GetMainInventory(environment);
            mainInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId3, 2));
            mainInventory.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId3, 1));
            mainInventory.SetItem(2, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId4, 2));

            var response = ExecutePlace(environment, railPosition, trainCarGuid);

            // 設置成功後、両素材の合計所持数が0になる
            // After a successful placement both materials are fully consumed
            Assert.IsTrue(response.Success, "設置は成功するべき / Placement should succeed");
            Assert.AreEqual(0, TotalCount(mainInventory, ForUnitTestItemId.ItemId3), "Test3が全消費されるべき / Test3 should be fully consumed");
            Assert.AreEqual(0, TotalCount(mainInventory, ForUnitTestItemId.ItemId4), "Test4が全消費されるべき / Test4 should be fully consumed");
        }

        [Test]
        public void 素材不足なら設置されずInsufficientItemsを返す()
        {
            // Test3を2個のみ所持する(必要数は3)
            // Hold only 2 of Test3 while 3 are required
            var (environment, railPosition, trainCarGuid) = SetupEnvironment();
            var mainInventory = GetMainInventory(environment);
            mainInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId3, 2));
            mainInventory.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId4, 2));

            var response = ExecutePlace(environment, railPosition, trainCarGuid);

            // 失敗応答・列車未生成・素材非消費を検証する
            // Validate failure response, no train, and untouched materials
            Assert.IsFalse(response.Success, "素材不足では失敗するべき / Placement should fail when materials are insufficient");
            Assert.AreEqual(PlaceTrainCarFailureType.InsufficientItems, response.FailureType, "InsufficientItemsを返すべき / Should return InsufficientItems");
            Assert.AreEqual(0, environment.GetITrainLookupDatastore().GetRegisteredTrains().Count, "列車は生成されないべき / No train should be created");
            Assert.AreEqual(2, TotalCount(mainInventory, ForUnitTestItemId.ItemId3), "素材は消費されないべき / Materials should not be consumed");
            Assert.AreEqual(2, TotalCount(mainInventory, ForUnitTestItemId.ItemId4), "素材は消費されないべき / Materials should not be consumed");
        }

        [Test]
        public void 未解放車両はNotUnlockedで拒否される()
        {
            // 2両目(initialUnlocked無し)のGuidで送信する
            // Send with the 2nd car guid that has no initialUnlocked flag
            var (environment, railPosition, _) = SetupEnvironment();
            var lockedTrainCarGuid = MasterHolder.TrainUnitMaster.Train.TrainCars[1].TrainCarGuid;

            var response = ExecutePlace(environment, railPosition, lockedTrainCarGuid);

            // 未解放拒否と列車未生成を検証する
            // Validate the not-unlocked rejection and that no train is created
            Assert.IsFalse(response.Success, "未解放車両は拒否されるべき / Locked car should be rejected");
            Assert.AreEqual(PlaceTrainCarFailureType.NotUnlocked, response.FailureType, "NotUnlockedを返すべき / Should return NotUnlocked");
            Assert.AreEqual(0, environment.GetITrainLookupDatastore().GetRegisteredTrains().Count, "列車は生成されないべき / No train should be created");
        }

        [Test]
        public void 存在しない車両GuidはItemNotFoundで拒否される()
        {
            // マスタに存在しないGuidで送信する
            // Send with a guid that does not exist in the master
            var (environment, railPosition, _) = SetupEnvironment();

            var response = ExecutePlace(environment, railPosition, Guid.NewGuid());

            // マスタ未検出拒否を検証する
            // Validate the master-not-found rejection
            Assert.IsFalse(response.Success, "存在しない車両は拒否されるべき / Unknown car should be rejected");
            Assert.AreEqual(PlaceTrainCarFailureType.ItemNotFound, response.FailureType, "ItemNotFoundを返すべき / Should return ItemNotFound");
            Assert.AreEqual(0, environment.GetITrainLookupDatastore().GetRegisteredTrains().Count, "列車は生成されないべき / No train should be created");
        }

        #region TestUtil

        private static (TrainTestEnvironment Environment, RailPositionSnapshotMessagePack RailPosition, Guid TrainCarGuid) SetupEnvironment()
        {
            // レールを準備し接続する
            // Prepare and connect rails
            var environment = TrainTestHelper.CreateEnvironment();
            var rail1Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North, out _);
            var rail2Component = TrainTestHelper.PlaceRail(environment, new Vector3Int(1000, 0, 0), BlockDirection.North, out _);
            rail1Component.FrontNode.ConnectNode(rail2Component.FrontNode);
            rail2Component.BackNode.ConnectNode(rail1Component.BackNode);

            // 1両目マスタからレール位置スナップショットを生成する
            // Build the rail position snapshot from the 1st car master
            var trainCarMasterElement = MasterHolder.TrainUnitMaster.Train.TrainCars[0];
            var trainLength = TrainLengthConverter.ToRailUnits(trainCarMasterElement.Length);
            var railNodes = new List<IRailNode> { rail1Component.BackNode, rail2Component.BackNode };
            var railPosition = new RailPosition(railNodes, trainLength, 0);
            var railPositionSnapshot = new RailPositionSnapshotMessagePack(railPosition.CreateSaveSnapshot());

            return (environment, railPositionSnapshot, trainCarMasterElement.TrainCarGuid);
        }

        private static IOpenableInventory GetMainInventory(TrainTestEnvironment environment)
        {
            return environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
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

        private static PlaceTrainOnRailResponseMessagePack ExecutePlace(TrainTestEnvironment environment, RailPositionSnapshotMessagePack railPosition, Guid trainCarGuid)
        {
            var packet = MessagePackSerializer.Serialize(new PlaceTrainOnRailRequestMessagePack(railPosition, trainCarGuid, PlayerId));
            var responses = environment.PacketResponseCreator.GetPacketResponse(packet, new PacketResponseContext(null));
            return MessagePackSerializer.Deserialize<PlaceTrainOnRailResponseMessagePack>(responses[0]);
        }

        #endregion
    }
}
