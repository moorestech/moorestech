using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
using Game.UnlockState;
using Game.World.Interface.DataStore;
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
    public class RailConnectWithPlacePierProtocolTest : RailConnectWithPlacePierProtocolTestBase
    {

        [Test]
        public void 橋脚設置と接続でレールが複数素材の距離比例で消費される()
        {
            UnlockRailConnectTool();
            SetInventory(reinforce: ReinforcePlenty, plate: PlatePlenty);

            var response = Send(ForUnitTestModBlockId.TestTrainRail);

            // 成功応答と橋脚の設置を検証する
            // Verify the success response and the placed pier
            Assert.IsTrue(response.Success, "設置は成功するべき / Placement should succeed");
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(PierPosition), "橋脚が設置されるべき / Pier should be placed");
            Assert.IsTrue(_environment.GetRailGraphDatastore().TryGetRailNode(response.ToNodeId, out var toNode), "toNodeが存在するべき / toNode should exist");
            TrainTestHelper.Node2NodeCheckAndAssert(_fromNode, toNode, "fromNode", "toNode");

            // units×各素材count＋橋脚コスト(鉄板x2)が消費されることを検証する
            // Verify units×each material count plus the pier cost (plate x2) are consumed
            var units = UnitsFor(toNode);
            Assert.Greater(units, 1, "距離比例で2単位以上消費されるべき / Two or more units should be consumed by distance");
            Assert.AreEqual(ReinforcePlenty - units * ReinforcePerUnit, CountItem(_reinforceItemId), "補強棒材がunits×12消費されるべき / Reinforce should be consumed units×12");
            Assert.AreEqual(PlatePlenty - units * PlatePerUnit - PierPlateCost, CountItem(_plateItemId), "鉄板がunits×5＋橋脚コスト消費されるべき / Plate should be consumed units×5 plus the pier cost");
        }
    }
}
