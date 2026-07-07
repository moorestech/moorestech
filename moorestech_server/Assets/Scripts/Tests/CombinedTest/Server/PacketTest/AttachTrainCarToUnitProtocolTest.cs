using System;
using System.Collections.Generic;
using System.Linq;
using Core.Inventory;
using Core.Master;
using Game.Block.Blocks.TrainRail;
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
using static Server.Protocol.PacketResponse.AttachTrainCarToUnitProtocol;
using static Server.Protocol.PacketResponse.PlaceTrainCarOnRailProtocol;

namespace Tests.CombinedTest.Server.PacketTest
{
    public class AttachTrainCarToUnitProtocolTest
    {
        private const int PlayerId = 1;

        [Test]
        public void 連結成功でrequiredItemsが消費される()
        {
            // 連結先編成を準備し、連結用の素材を投入する
            // Prepare the target train and put attach materials into the inventory
            var setup = SetupTargetTrain();
            var mainInventory = GetMainInventory(setup.Environment);
            mainInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId3, 3));
            mainInventory.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId4, 2));

            var response = ExecuteAttach(setup, BuildAttachSnapshot(setup), setup.TrainCarGuid);

            // 連結成功・2両化・素材全消費を検証する
            // Validate success, a 2-car train, and fully consumed materials
            Assert.IsTrue(response.Success, "連結は成功するべき / Attach should succeed");
            var train = setup.Environment.GetITrainLookupDatastore().GetRegisteredTrains().Last();
            Assert.AreEqual(2, train.Cars.Count, "連結後は2両になるべき / Train should have 2 cars after attach");
            Assert.AreEqual(0, TotalCount(mainInventory, ForUnitTestItemId.ItemId3), "Test3が全消費されるべき / Test3 should be fully consumed");
            Assert.AreEqual(0, TotalCount(mainInventory, ForUnitTestItemId.ItemId4), "Test4が全消費されるべき / Test4 should be fully consumed");
        }

        [Test]
        public void 素材不足なら連結されずInsufficientItemsを返す()
        {
            // 連結先編成を準備し、Test3を2個のみ所持する(必要数は3)
            // Prepare the target train and hold only 2 of Test3 while 3 are required
            var setup = SetupTargetTrain();
            var mainInventory = GetMainInventory(setup.Environment);
            mainInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId3, 2));
            mainInventory.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId4, 2));

            var response = ExecuteAttach(setup, BuildAttachSnapshot(setup), setup.TrainCarGuid);

            // 素材非消費も検証する
            // Also validate untouched materials
            AssertRejected(setup, response, AttachTrainCarFailureType.InsufficientItems);
            Assert.AreEqual(2, TotalCount(mainInventory, ForUnitTestItemId.ItemId3), "素材は消費されないべき / Materials should not be consumed");
            Assert.AreEqual(2, TotalCount(mainInventory, ForUnitTestItemId.ItemId4), "素材は消費されないべき / Materials should not be consumed");
        }

        [Test]
        public void 未解放車両は連結されずNotUnlockedを返す()
        {
            // 2両目(initialUnlocked無し)のGuidで連結を要求する
            // Request attach with the 2nd car guid that lacks initialUnlocked
            var setup = SetupTargetTrain();
            var lockedTrainCarGuid = MasterHolder.TrainUnitMaster.Train.TrainCars[1].TrainCarGuid;

            var response = ExecuteAttach(setup, BuildAttachSnapshot(setup), lockedTrainCarGuid);

            AssertRejected(setup, response, AttachTrainCarFailureType.NotUnlocked);
        }

        [Test]
        public void 存在しない車両Guidは連結されずItemNotFoundを返す()
        {
            // マスタに存在しないGuidで連結を要求する
            // Request attach with a guid absent from the master
            var setup = SetupTargetTrain();

            var response = ExecuteAttach(setup, BuildAttachSnapshot(setup), Guid.NewGuid());

            AssertRejected(setup, response, AttachTrainCarFailureType.ItemNotFound);
        }

        #region Internal

        private readonly struct AttachTestSetup
        {
            public AttachTestSetup(TrainTestEnvironment environment, TrainUnitInstanceId targetTrainUnitInstanceId, RailComponent rail1, RailComponent rail2, Guid trainCarGuid, int trainLength)
            {
                Environment = environment;
                TargetTrainUnitInstanceId = targetTrainUnitInstanceId;
                Rail1 = rail1;
                Rail2 = rail2;
                TrainCarGuid = trainCarGuid;
                TrainLength = trainLength;
            }

            public TrainTestEnvironment Environment { get; }
            public TrainUnitInstanceId TargetTrainUnitInstanceId { get; }
            public RailComponent Rail1 { get; }
            public RailComponent Rail2 { get; }
            public Guid TrainCarGuid { get; }
            public int TrainLength { get; }
        }

        private static AttachTestSetup SetupTargetTrain()
        {
            // レールを準備し接続する
            // Prepare and connect rails
            var environment = TrainTestHelper.CreateEnvironment();
            var rail1 = TrainTestHelper.PlaceRail(environment, new Vector3Int(0, 0, 0), BlockDirection.North, out _);
            var rail2 = TrainTestHelper.PlaceRail(environment, new Vector3Int(1000, 0, 0), BlockDirection.North, out _);
            rail1.FrontNode.ConnectNode(rail2.FrontNode);
            rail2.BackNode.ConnectNode(rail1.BackNode);

            // 1両目マスタを解決する
            // Resolve the 1st car master
            MasterHolder.TrainUnitMaster.TryGetTrainCarMaster(ForUnitTestItemId.TrainCarItem, out var trainCarMasterElement);
            var trainLength = TrainLengthConverter.ToRailUnits(trainCarMasterElement.Length);

            // 連結先の単機編成を設置プロトコルで生成する
            // Create the single-car target train through the placement protocol
            var mainInventory = GetMainInventory(environment);
            mainInventory.SetItem(0, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId3, 3));
            mainInventory.SetItem(1, ServerContext.ItemStackFactory.Create(ForUnitTestItemId.ItemId4, 2));

            var targetNodes = new List<IRailNode> { rail1.BackNode, rail2.BackNode };
            var targetRailPosition = new RailPosition(targetNodes, trainLength, 0);
            var targetSnapshot = new RailPositionSnapshotMessagePack(targetRailPosition.CreateSaveSnapshot());
            var placePacket = MessagePackSerializer.Serialize(new PlaceTrainOnRailRequestMessagePack(targetSnapshot, trainCarMasterElement.TrainCarGuid, PlayerId));
            environment.PacketResponseCreator.GetPacketResponse(placePacket, new PacketResponseContext());

            var targetTrain = environment.GetITrainLookupDatastore().GetRegisteredTrains().Last();
            return new AttachTestSetup(environment, targetTrain.TrainUnitInstanceId, rail1, rail2, trainCarMasterElement.TrainCarGuid, trainLength);
        }

        private static RailPositionSnapshotMessagePack BuildAttachSnapshot(AttachTestSetup setup)
        {
            // 連結先の後尾に接する位置(1両分後方)へ新規車両を置く
            // Place the new car right behind the target's rear (one car length back)
            var attachNodes = new List<IRailNode> { setup.Rail1.BackNode, setup.Rail2.BackNode };
            var attachRailPosition = new RailPosition(attachNodes, setup.TrainLength, setup.TrainLength);
            return new RailPositionSnapshotMessagePack(attachRailPosition.CreateSaveSnapshot());
        }

        private static AttachTrainCarToUnitResponseMessagePack ExecuteAttach(AttachTestSetup setup, RailPositionSnapshotMessagePack railPosition, Guid trainCarGuid)
        {
            // 後尾連結(前向き)で送信する
            // Send a rear-attach request with a forward-facing car
            var packet = MessagePackSerializer.Serialize(new AttachTrainCarToUnitRequestMessagePack(
                setup.TargetTrainUnitInstanceId,
                railPosition,
                trainCarGuid,
                PlayerId,
                true,
                false));
            var responses = setup.Environment.PacketResponseCreator.GetPacketResponse(packet, new PacketResponseContext());
            return MessagePackSerializer.Deserialize<AttachTrainCarToUnitResponseMessagePack>(responses[0]);
        }

        private static void AssertRejected(AttachTestSetup setup, AttachTrainCarToUnitResponseMessagePack response, AttachTrainCarFailureType expectedFailureType)
        {
            // 失敗応答と編成不変を検証する
            // Validate the failure response and the unchanged train
            Assert.IsFalse(response.Success, "連結は拒否されるべき / Attach should be rejected");
            Assert.AreEqual(expectedFailureType, response.FailureType, $"{expectedFailureType}を返すべき / Should return {expectedFailureType}");
            var train = setup.Environment.GetITrainLookupDatastore().GetRegisteredTrains().Last();
            Assert.AreEqual(1, train.Cars.Count, "編成は1両のままであるべき / Train should stay at 1 car");
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

        #endregion
    }
}
