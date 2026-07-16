using System;
using System.Linq;
using Core.Master;
using Game.Block.Blocks.TrainRail;
using Game.Block.Interface;
using Game.Context;
using Game.PlayerInventory.Interface;
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
    public class RailConnectWithPlacePierProtocolTest
    {
        private const int PlayerId = 1;
        private static readonly Vector3Int FromRailPosition = Vector3Int.zero;
        private static readonly Vector3Int PierPosition = new(10, 0, 0);

        // レール種はTestTrainRailアイテム(railLengthPerItem=1)を使用する
        // Rail type uses the TestTrainRail item (railLengthPerItem = 1)
        private static readonly Guid RailTypeGuid = Guid.Parse("1b72541e-0896-43bb-91fa-0b3fef137dcf");

        private TrainTestEnvironment _environment;
        private global::Game.Train.RailGraph.RailNode _fromNode;
        private global::Core.Inventory.IOpenableInventory _inventory;
        private ItemId _materialItemId;
        private ItemId _railItemId;

        [SetUp]
        public void SetUp()
        {
            // 起点レールを直接設置しfromNodeとインベントリを準備する
            // Place the from rail directly and prepare fromNode and the inventory
            _environment = TrainTestHelper.CreateEnvironment();
            var fromRailComponent = TrainTestHelper.PlaceRail(_environment, FromRailPosition, BlockDirection.North);
            _fromNode = fromRailComponent.FrontNode;
            _inventory = _environment.ServiceProvider.GetService<IPlayerInventoryDataStore>().GetInventoryData(PlayerId).MainOpenableInventory;
            _materialItemId = ForUnitTestItemId.ItemId3;
            _railItemId = MasterHolder.ItemMaster.GetItemId(RailTypeGuid);
        }

        [Test]
        public void 橋脚設置と接続で橋脚コストとレールが距離比例で消費される()
        {
            SetInventory(materialCount: 2, railCount: 100);

            var response = Send(ForUnitTestModBlockId.TestTrainRail);

            // 成功応答と橋脚の設置を検証する
            // Verify the success response and the placed pier
            Assert.IsTrue(response.Success, "設置は成功するべき / Placement should succeed");
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(PierPosition), "橋脚が設置されるべき / Pier should be placed");

            // toNodeへの接続を検証する
            // Verify the connection to toNode
            Assert.IsTrue(_environment.GetRailGraphDatastore().TryGetRailNode(response.ToNodeId, out var toNode), "toNodeが存在するべき / toNode should exist");
            Assert.AreEqual(toNode.Guid, response.ToNodeGuid, "応答のtoNode Guidが一致するべき / Response toNode guid should match");
            TrainTestHelper.Node2NodeCheckAndAssert(_fromNode, toNode, "fromNode", "toNode");

            // 橋脚コスト(Test3 x2)と距離比例のレール消費を検証する
            // Verify the pier cost (Test3 x2) and the distance-based rail consumption
            var expectedRailCount = Mathf.CeilToInt(RailConnectionEditProtocol.GetRailLength(_fromNode, toNode));
            Assert.Greater(expectedRailCount, 1, "距離比例で2個以上消費されるべき / Two or more rails should be consumed by distance");
            Assert.AreEqual(0, CountItem(_materialItemId), "橋脚素材が全消費されるべき / Pier materials should be fully consumed");
            Assert.AreEqual(100 - expectedRailCount, CountItem(_railItemId), "レールが距離比例で消費されるべき / Rails should be consumed by distance");
        }

        [Test]
        public void 橋脚素材不足なら設置されず失敗応答を返す()
        {
            // 必要数2に対して素材を1個のみ所持する
            // Hold only 1 material while 2 are required
            SetInventory(materialCount: 1, railCount: 100);

            var response = Send(ForUnitTestModBlockId.TestTrainRail);

            AssertFailedWithoutStateChange(response, expectedMaterialCount: 1, expectedRailCount: 100);
        }

        [Test]
        public void レール素材不足なら設置されず失敗応答を返す()
        {
            // 距離約10に対しレールを3個のみ所持する
            // Hold only 3 rails against a distance of about 10
            SetInventory(materialCount: 2, railCount: 3);

            var response = Send(ForUnitTestModBlockId.TestTrainRail);

            // 旧実装では橋脚が残留していた。新実装ではロールバックされ状態不変
            // The old implementation left the pier; the new one rolls back leaving no state change
            AssertFailedWithoutStateChange(response, expectedMaterialCount: 2, expectedRailCount: 3);
        }

        [Test]
        public void 未解放橋脚は設置されず失敗応答を返す()
        {
            SetInventory(materialCount: 2, railCount: 100);

            var response = Send(ForUnitTestModBlockId.LockedTrainRail);

            AssertFailedWithoutStateChange(response, expectedMaterialCount: 2, expectedRailCount: 100);
        }

        [Test]
        public void 橋脚コストとレールが同一アイテムなら合算不足で失敗しロールバックされる()
        {
            // 橋脚コスト(レールx5)と敷設分(約10)は個別には足りるが合算15には足りない12個を所持する
            // Hold 12 rails: enough for pier cost (5) and laying (about 10) separately but not for the combined 15
            _inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_railItemId, 12));

            var response = Send(ForUnitTestModBlockId.RailCostTrainRail);

            AssertFailedWithoutStateChange(response, expectedMaterialCount: 0, expectedRailCount: 12);
        }

        [Test]
        public void 橋脚コストとレールが同一アイテムでも合算充足なら成功し両方消費される()
        {
            _inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_railItemId, 30));

            var response = Send(ForUnitTestModBlockId.RailCostTrainRail);

            // 橋脚コスト(レールx5)＋距離比例のレール敷設分が合算で消費されることを検証する
            // Verify the pier cost (5 rails) plus the distance-based laying cost are consumed together
            Assert.IsTrue(response.Success, "設置は成功するべき / Placement should succeed");
            Assert.IsTrue(ServerContext.WorldBlockDatastore.Exists(PierPosition), "橋脚が設置されるべき / Pier should be placed");
            Assert.IsTrue(_environment.GetRailGraphDatastore().TryGetRailNode(response.ToNodeId, out var toNode), "toNodeが存在するべき / toNode should exist");
            var expectedRailCount = Mathf.CeilToInt(RailConnectionEditProtocol.GetRailLength(_fromNode, toNode));
            Assert.AreEqual(30 - 5 - expectedRailCount, CountItem(_railItemId), "橋脚コストと敷設レールが合算消費されるべき / Pier cost and laying rails should both be consumed");
        }

        private void SetInventory(int materialCount, int railCount)
        {
            if (0 < materialCount) _inventory.SetItem(0, ServerContext.ItemStackFactory.Create(_materialItemId, materialCount));
            if (0 < railCount) _inventory.SetItem(1, ServerContext.ItemStackFactory.Create(_railItemId, railCount));
        }

        private RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse Send(BlockId pierBlockId)
        {
            // クライアント同様にレール向きの生成パラメータを付与する
            // Attach the rail direction create param just like the client does
            var stateDetail = new RailBridgePierComponentStateDetail(Vector3.forward);
            var createParams = new[] { new BlockCreateParam(RailBridgePierComponentStateDetail.StateDetailKey, MessagePackSerializer.Serialize(stateDetail)) };
            var placeInfo = new PlaceInfo
            {
                Position = PierPosition,
                Direction = BlockDirection.North,
                VerticalDirection = BlockVerticalDirection.Horizontal,
                CreateParams = createParams,
            };
            var request = RailConnectWithPlacePierProtocol.RailConnectWithPlacePierRequest.Create(PlayerId, _fromNode.NodeId, _fromNode.Guid, pierBlockId, placeInfo, RailTypeGuid);
            var responseBytes = _environment.PacketResponseCreator.GetPacketResponseForTest(MessagePackSerializer.Serialize(request), new PacketResponseContext()).First();
            return MessagePackSerializer.Deserialize<RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse>(responseBytes.ToArray());
        }

        private void AssertFailedWithoutStateChange(RailConnectWithPlacePierProtocol.RailConnectWithPlacePierResponse response, int expectedMaterialCount, int expectedRailCount)
        {
            // 失敗時に橋脚もアイテム消費も残らないことを検証する
            // Verify failures leave neither a pier nor any item consumption
            Assert.IsFalse(response.Success, "失敗応答を返すべき / Should return a failure response");
            Assert.IsFalse(ServerContext.WorldBlockDatastore.Exists(PierPosition), "橋脚は設置されないべき / Pier should not be placed");
            Assert.AreEqual(expectedMaterialCount, CountItem(_materialItemId), "橋脚素材は消費されないべき / Pier materials should not be consumed");
            Assert.AreEqual(expectedRailCount, CountItem(_railItemId), "レールは消費されないべき / Rails should not be consumed");
        }

        private int CountItem(ItemId itemId)
        {
            return _inventory.InventoryItems.Where(stack => stack.Id == itemId).Sum(stack => stack.Count);
        }
    }
}
