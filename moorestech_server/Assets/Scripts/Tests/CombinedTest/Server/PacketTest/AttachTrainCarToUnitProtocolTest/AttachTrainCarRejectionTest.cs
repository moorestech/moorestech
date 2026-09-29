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
    public class AttachTrainCarRejectionTest : AttachTrainCarToUnitProtocolTestBase
    {

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
    }
}
