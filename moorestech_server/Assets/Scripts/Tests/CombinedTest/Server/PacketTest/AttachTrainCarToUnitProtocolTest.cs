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
    public class AttachTrainCarToUnitProtocolTest : AttachTrainCarToUnitProtocolTestBase
    {

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
    }
}
